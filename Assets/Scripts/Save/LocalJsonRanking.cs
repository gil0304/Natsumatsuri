using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Matsuri.Core;
using UnityEngine;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §37。ローカルJSONで持つランキング。
    /// 並び順は §35 に従い「売上の降順」。同額ならスコア、それも同じなら登録が早い方が上。
    /// ファイルが壊れていた場合は空のランキングとして扱い、次回の登録で作り直す。
    /// </summary>
    public sealed class LocalJsonRanking : IRankingBackend
    {
        /// <summary>ランキングを置くフォルダ名。セーブ (§54) とは分けて衝突を避ける。</summary>
        public const string DirectoryName = "MatsuriRanking";

        /// <summary>ランキングファイル名。</summary>
        public const string FileName = "ranking.json";

        /// <summary>オンライン送信待ちを貯めておくファイル名 (§37 のオフライン耐性)。</summary>
        public const string PendingFileName = "ranking_pending.json";

        /// <summary>送信待ちに貯める最大件数。これを超えたら古い方から捨てる。</summary>
        public const int MaxPendingEntries = 100;

        /// <summary>保持する最大件数。これを超えたら下位から捨てる。</summary>
        public const int MaxEntries = 200;

        /// <summary>ランキングに残すソースコードの長さ。JSON肥大化を防ぐ。</summary>
        public const int MaxSourceLength = 20000;

        [Serializable]
        sealed class RankingFile
        {
            public int Version = 1;
            public List<FestivalResult> Entries = new List<FestivalResult>();
        }

        readonly string _directory;
        readonly string _path;
        readonly string _pendingPath;

        RankingFile _file;
        bool _loaded;

        RankingFile _pending;
        bool _pendingLoaded;

        public LocalJsonRanking()
            : this(Path.Combine(Application.persistentDataPath, DirectoryName))
        {
        }

        /// <summary>保存先を指定して作る（テスト用）。</summary>
        public LocalJsonRanking(string directory)
        {
            _directory = string.IsNullOrEmpty(directory)
                ? Path.Combine(Application.persistentDataPath, DirectoryName)
                : directory;
            _path = Path.Combine(_directory, FileName);
            _pendingPath = Path.Combine(_directory, PendingFileName);
        }

        public string DisplayName => "ローカルランキング";

        /// <summary>ローカル保存はオンラインではない。</summary>
        public bool IsOnline => false;

        /// <summary>ランキングJSONの絶対パス。</summary>
        public string FilePath => _path;

        /// <summary>送信待ちJSONの絶対パス。</summary>
        public string PendingFilePath => _pendingPath;

        /// <summary>登録件数。</summary>
        public int Count
        {
            get
            {
                EnsureLoaded();
                return _file.Entries.Count;
            }
        }

        public void Submit(FestivalResult result)
        {
            if (result == null)
            {
                MatsuriLog.Warn("ランキングに登録する結果が空です。");
                return;
            }

            EnsureLoaded();

            var entry = result.Clone();
            entry.StampCreatedDate();
            if (entry.SourceCode != null && entry.SourceCode.Length > MaxSourceLength)
                entry.SourceCode = entry.SourceCode.Substring(0, MaxSourceLength);

            // 同じ結果を二重登録しない。
            for (int i = 0; i < _file.Entries.Count; i++)
            {
                if (entry.IsSameEntry(_file.Entries[i]))
                {
                    MatsuriLog.Info("同じ結果がすでにランキングにあるため登録しませんでした。");
                    return;
                }
            }

            _file.Entries.Add(entry);
            SortEntries();

            if (_file.Entries.Count > MaxEntries)
                _file.Entries.RemoveRange(MaxEntries, _file.Entries.Count - MaxEntries);

            WriteFile();
            MatsuriLog.Always($"ランキングに登録しました: {entry.ToRankingLine()}（{GetRank(entry)}位）");
        }

        public List<FestivalResult> GetTop(int count)
        {
            EnsureLoaded();

            var list = new List<FestivalResult>();
            if (count <= 0) return list;

            int take = Mathf.Min(count, _file.Entries.Count);
            for (int i = 0; i < take; i++)
                list.Add(_file.Entries[i]);

            return list;
        }

        public int GetRank(FestivalResult result)
        {
            if (result == null) return 0;
            EnsureLoaded();

            int better = 0;
            for (int i = 0; i < _file.Entries.Count; i++)
            {
                var other = _file.Entries[i];
                if (other == null) continue;
                if (result.IsSameEntry(other)) continue;       // 自分自身は数えない
                if (Compare(other, result) < 0) better++;      // other の方が上位
            }
            return better + 1;
        }

        /// <summary>ランキングを空にする（デバッグ・テスト用）。</summary>
        public void Clear()
        {
            EnsureLoaded();
            _file.Entries.Clear();
            WriteFile();
            MatsuriLog.Always("ランキングを消去しました。");
        }

        /// <summary>ファイルから読み直す。</summary>
        public void Reload()
        {
            _loaded = false;
            _pendingLoaded = false;
            EnsureLoaded();
        }

        void EnsureLoaded()
        {
            if (_loaded && _file != null) return;
            _loaded = true;
            _file = ReadFile(_path);
            SortEntries();
        }

        RankingFile ReadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return new RankingFile();

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return new RankingFile();

                var loaded = JsonUtility.FromJson<RankingFile>(json);
                if (loaded == null) return new RankingFile();

                loaded.Entries ??= new List<FestivalResult>();
                for (int i = loaded.Entries.Count - 1; i >= 0; i--)
                    if (loaded.Entries[i] == null) loaded.Entries.RemoveAt(i);

                return loaded;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"ランキングファイルが壊れているため空から始めます: {path}\n{e.Message}");
                return new RankingFile();
            }
        }

        void WriteFile() => WriteFile(_path, _file);

        void WriteFile(string path, RankingFile file)
        {
            string tempPath = path + ".tmp";
            try
            {
                if (!Directory.Exists(_directory)) Directory.CreateDirectory(_directory);

                string json = JsonUtility.ToJson(file, true);
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));

                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"ランキングを保存できませんでした: {path}\n{e.Message}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch (Exception inner) { MatsuriLog.Error($"一時ファイルを消せませんでした: {inner.Message}"); }
            }
        }

        // ── オンライン送信キュー (§37) ────────────────────────
        // オンラインに送れなかった結果はここに貯めておき、
        // 次につながったときに RemoteRanking がまとめて送る。

        /// <summary>送信待ちの件数。</summary>
        public int PendingCount
        {
            get
            {
                EnsurePendingLoaded();
                return _pending.Entries.Count;
            }
        }

        /// <summary>送信待ちに1件積む。同じ結果が入っていれば積まない。</summary>
        public void EnqueuePending(FestivalResult result)
        {
            if (result == null) return;

            EnsurePendingLoaded();

            var entry = result.Clone();
            entry.StampCreatedDate();
            if (entry.SourceCode != null && entry.SourceCode.Length > MaxSourceLength)
                entry.SourceCode = entry.SourceCode.Substring(0, MaxSourceLength);

            for (int i = 0; i < _pending.Entries.Count; i++)
                if (entry.IsSameEntry(_pending.Entries[i])) return;

            _pending.Entries.Add(entry);

            if (_pending.Entries.Count > MaxPendingEntries)
                _pending.Entries.RemoveRange(0, _pending.Entries.Count - MaxPendingEntries);

            WriteFile(_pendingPath, _pending);
        }

        /// <summary>送信待ちの一覧（積んだ順のコピー）。</summary>
        public List<FestivalResult> GetPending()
        {
            EnsurePendingLoaded();
            return new List<FestivalResult>(_pending.Entries);
        }

        /// <summary>送信できた1件を待ち行列から外す。</summary>
        public void RemovePending(FestivalResult result)
        {
            if (result == null) return;

            EnsurePendingLoaded();
            for (int i = _pending.Entries.Count - 1; i >= 0; i--)
            {
                if (!result.IsSameEntry(_pending.Entries[i])) continue;
                _pending.Entries.RemoveAt(i);
                WriteFile(_pendingPath, _pending);
                return;
            }
        }

        /// <summary>送信待ちを空にする。</summary>
        public void ClearPending()
        {
            EnsurePendingLoaded();
            if (_pending.Entries.Count == 0) return;

            _pending.Entries.Clear();
            WriteFile(_pendingPath, _pending);
        }

        void EnsurePendingLoaded()
        {
            if (_pendingLoaded && _pending != null) return;
            _pendingLoaded = true;
            _pending = ReadFile(_pendingPath);
        }

        void SortEntries()
        {
            _file.Entries.Sort(Compare);
        }

        /// <summary>売上降順。同額ならスコア降順、さらに同じなら登録が早い方を上にする。</summary>
        static int Compare(FestivalResult a, FestivalResult b)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;

            int byRevenue = b.Revenue.CompareTo(a.Revenue);
            if (byRevenue != 0) return byRevenue;

            int byScore = b.TotalScore.CompareTo(a.TotalScore);
            if (byScore != 0) return byScore;

            return string.CompareOrdinal(a.CreatedDate, b.CreatedDate);
        }
    }
}
