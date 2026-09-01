using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Matsuri.Core;
using UnityEngine;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §46 / §77 BATTLE MODE。
    /// 「複数人が同じ条件で祭りを作り、最後に売上を比較する」ための入口。
    ///
    /// 公平性が肝なので、セッションは <see cref="BattleSession.Seed"/> を持つ。
    /// セッション中は <see cref="CurrentSeed"/> が値を持ち、
    /// VisitorManager 側がこれを読んで来場者の乱数の種にする。
    /// 種が同じなら、来場のタイミングも好みも全員まったく同じ列になる。
    ///
    /// セッションは JSON でローカルに保存でき、あとから読み直して結果を並べられる。
    /// </summary>
    public static class BattleMode
    {
        /// <summary>セッションを置くフォルダ名。セーブ (§54) やランキング (§37) とは分ける。</summary>
        public const string DirectoryName = "MatsuriBattles";

        /// <summary>セッションファイルの拡張子。</summary>
        public const string Extension = ".json";

        /// <summary>1人ぶんに残すソースコードの長さ。JSON肥大化を防ぐ。</summary>
        public const int MaxSourceLength = 40000;

        static BattleSession _current;
        static string _directoryOverride;
        static string _cachedDirectory;

        /// <summary>いま進行中のセッション。無ければ null。</summary>
        public static BattleSession Current => _current;

        /// <summary>
        /// セッション中だけ値を持つ乱数種。
        /// VisitorManager はこれが null でなければ、自前の乱数の種として使う。
        /// </summary>
        public static int? CurrentSeed => _current != null ? _current.Seed : (int?)null;

        /// <summary>セッションが始まった・終わった・投稿が増えたときに通知する（UIの再描画用）。</summary>
        public static event Action SessionChanged;

        /// <summary>セッションの保存先フォルダ（絶対パス）。</summary>
        public static string SessionDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_directoryOverride)) return _directoryOverride;
                if (string.IsNullOrEmpty(_cachedDirectory))
                    _cachedDirectory = Path.Combine(Application.persistentDataPath, DirectoryName);
                return _cachedDirectory;
            }
        }

        /// <summary>保存先を差し替える（テスト用）。null を渡すと既定に戻る。</summary>
        public static void UseDirectory(string absolutePath)
        {
            _directoryOverride = string.IsNullOrEmpty(absolutePath) ? null : absolutePath;
        }

        // ── セッション ────────────────────────────────────────

        /// <summary>
        /// 新しい勝負を始める。challenge が null なら §47 の既定のお題を使う。
        /// すでにセッションがあれば破棄して作り直す（保存はしない）。
        /// </summary>
        public static BattleSession StartSession(ChallengeDefinition challenge, int seed)
        {
            var used = challenge ?? ChallengePresets.Default ?? ChallengePresets.FreePlay();

            _current = new BattleSession(NewSessionId(), used, seed);
            MatsuriLog.Always($"BATTLE MODE を開始しました: {_current.Describe()}");
            SessionChanged?.Invoke();
            return _current;
        }

        /// <summary>乱数種を自動で決めて勝負を始める。</summary>
        public static BattleSession StartSession(ChallengeDefinition challenge)
            => StartSession(challenge, NewSeed());

        /// <summary>読み込んだセッションを進行中にする。</summary>
        public static BattleSession ResumeSession(BattleSession session)
        {
            if (session == null)
            {
                MatsuriLog.Warn("再開するセッションがありません。");
                return null;
            }

            session.EnsureValid();
            _current = session;
            MatsuriLog.Always($"BATTLE MODE を再開しました: {session.Describe()}");
            SessionChanged?.Invoke();
            return _current;
        }

        /// <summary>勝負を終える。記録は消えるので、残したいなら先に <see cref="SaveSession()"/> を呼ぶ。</summary>
        public static void EndSession()
        {
            if (_current == null) return;

            MatsuriLog.Always($"BATTLE MODE を終了しました: {_current.Id}（参加 {_current.EntryCount}人）");
            _current = null;
            SessionChanged?.Invoke();
        }

        /// <summary>同じ条件のまま、投稿だけを消してやり直す (§37「同じ条件でもう一度」)。</summary>
        public static void RestartSameConditions()
        {
            if (_current == null)
            {
                MatsuriLog.Warn("やり直す勝負がありません。");
                return;
            }

            _current.Entries.Clear();
            _current.Id = NewSessionId();
            _current.CreatedAt = DateTime.Now.ToString(FestivalResult.DateFormat, CultureInfo.InvariantCulture);
            MatsuriLog.Always($"同じ条件でやり直します: {_current.Describe()}");
            SessionChanged?.Invoke();
        }

        /// <summary>乱数種を新しく作る。</summary>
        public static int NewSeed() => UnityEngine.Random.Range(1, int.MaxValue);

        /// <summary>
        /// 来場者などに使う乱数を作る。
        /// BATTLE MODE のセッション中はセッションの種を、そうでなければ渡された種を使う。
        /// VisitorManager がこれを使えば、同じ勝負では全員同じ来場の列になる (§46 の公平性)。
        /// </summary>
        public static System.Random CreateRandom(int fallbackSeed)
            => new System.Random(CurrentSeed ?? fallbackSeed);

        // ── 投稿 ──────────────────────────────────────────────

        /// <summary>
        /// 参加者の結果を登録する。
        /// 同じ名前がすでにあれば、売上の良い方だけを残す。
        /// セッションが無いときは何もしない（例外は投げない）。
        /// </summary>
        public static void Submit(string playerName, string sourceCode, FestivalResult result)
        {
            if (_current == null)
            {
                MatsuriLog.Warn("BATTLE MODE のセッションが無いため、結果を登録できませんでした。");
                return;
            }
            if (result == null)
            {
                MatsuriLog.Warn("BATTLE MODE に登録する結果がありません。");
                return;
            }

            string name = string.IsNullOrWhiteSpace(playerName) ? "プレイヤー" : playerName.Trim();

            var copy = result.Clone();
            copy.StampCreatedDate();
            if (string.IsNullOrEmpty(sourceCode)) sourceCode = copy.SourceCode;
            sourceCode = Truncate(sourceCode);
            copy.SourceCode = sourceCode;

            var entry = new BattleEntry(name, sourceCode, copy);

            for (int i = 0; i < _current.Entries.Count; i++)
            {
                if (!string.Equals(_current.Entries[i].PlayerName, name, StringComparison.Ordinal)) continue;

                if (entry.Revenue > _current.Entries[i].Revenue)
                {
                    _current.Entries[i] = entry;
                    MatsuriLog.Always($"BATTLE の記録を更新: {entry}");
                    SessionChanged?.Invoke();
                }
                else
                {
                    MatsuriLog.Info($"前回より売上が低いため記録は更新しませんでした: {name}");
                }
                return;
            }

            _current.Entries.Add(entry);
            MatsuriLog.Always($"BATTLE に登録: {entry}");
            SessionChanged?.Invoke();
        }

        /// <summary>売上の降順に並べた参加者一覧 (§35)。同点ならスコア、それも同じなら投稿の早い方が上。</summary>
        public static IReadOnlyList<BattleEntry> GetRanking()
        {
            var list = new List<BattleEntry>();
            if (_current == null || _current.Entries == null) return list;

            for (int i = 0; i < _current.Entries.Count; i++)
                if (_current.Entries[i] != null) list.Add(_current.Entries[i]);

            list.Sort(Compare);
            return list;
        }

        /// <summary>その参加者の順位（1始まり。いなければ 0）。</summary>
        public static int GetRank(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return 0;

            var ranking = GetRanking();
            for (int i = 0; i < ranking.Count; i++)
                if (string.Equals(ranking[i].PlayerName, playerName, StringComparison.Ordinal))
                    return i + 1;

            return 0;
        }

        /// <summary>売上降順。同額ならスコア降順、さらに同じなら投稿が早い方・名前順。</summary>
        internal static int Compare(BattleEntry a, BattleEntry b)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;

            int byRevenue = b.Revenue.CompareTo(a.Revenue);
            if (byRevenue != 0) return byRevenue;

            int byScore = b.TotalScore.CompareTo(a.TotalScore);
            if (byScore != 0) return byScore;

            int byTime = string.CompareOrdinal(a.SubmittedAt, b.SubmittedAt);
            if (byTime != 0) return byTime;

            return string.CompareOrdinal(a.PlayerName, b.PlayerName);
        }

        // ── 保存・読み込み ────────────────────────────────────

        /// <summary>進行中のセッションを JSON で保存する。</summary>
        public static bool SaveSession()
        {
            if (_current == null)
            {
                MatsuriLog.Warn("保存する勝負がありません。");
                return false;
            }
            return SaveSession(_current);
        }

        /// <summary>指定したセッションを JSON で保存する。</summary>
        public static bool SaveSession(BattleSession session)
        {
            if (session == null) return false;

            session.EnsureValid();
            if (!EnsureDirectory()) return false;

            string path = PathOf(session.Id);
            string tempPath = path + ".tmp";

            try
            {
                string json = JsonUtility.ToJson(session, true);
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));

                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);

                MatsuriLog.Always($"勝負を保存しました: {path}");
                return true;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"勝負の保存に失敗しました: {path}\n{e.Message}");
                TryDelete(tempPath);
                return false;
            }
        }

        /// <summary>保存したセッションを読む。無い・壊れているときは null（例外は投げない）。</summary>
        public static BattleSession LoadSession(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                MatsuriLog.Warn("読み込む勝負のIDが空です。");
                return null;
            }

            string path = PathOf(id);
            try
            {
                if (!File.Exists(path))
                {
                    MatsuriLog.Warn($"勝負が見つかりませんでした: {id}");
                    return null;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return null;

                var session = JsonUtility.FromJson<BattleSession>(json);
                if (session == null)
                {
                    MatsuriLog.Warn($"勝負のファイルを解釈できませんでした: {id}");
                    return null;
                }

                session.EnsureValid();
                session.Id = id;
                return session;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"勝負のファイルが壊れているため読み飛ばしました: {id}\n{e.Message}");
                return null;
            }
        }

        /// <summary>保存されているセッションIDの一覧（新しい順）。</summary>
        public static IReadOnlyList<string> ListSessions()
        {
            var ids = new List<string>();
            try
            {
                if (!Directory.Exists(SessionDirectory)) return ids;

                string[] files = Directory.GetFiles(SessionDirectory, "*" + Extension);
                for (int i = 0; i < files.Length; i++)
                    ids.Add(Path.GetFileNameWithoutExtension(files[i]));

                ids.Sort(StringComparer.Ordinal);
                ids.Reverse();   // IDの先頭が日時なので、逆順にすると新しい順になる
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"勝負の一覧を取得できませんでした: {SessionDirectory}\n{e.Message}");
            }
            return ids;
        }

        /// <summary>保存されたセッションを消す。</summary>
        public static bool DeleteSession(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            bool ok = TryDelete(PathOf(id));
            if (ok) MatsuriLog.Always($"勝負を削除しました: {id}");
            return ok;
        }

        // ── 内部 ──────────────────────────────────────────────

        static string PathOf(string id) => Path.Combine(SessionDirectory, SanitizeId(id) + Extension);

        static string NewSessionId()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string tail = UnityEngine.Random.Range(0, 1000).ToString("000", CultureInfo.InvariantCulture);
            return $"battle_{stamp}_{tail}";
        }

        /// <summary>ファイル名に使えない文字を落とす。</summary>
        internal static string SanitizeId(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "battle";

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                bool bad = c == ' ' || c == '.' || char.IsControl(c);
                for (int k = 0; !bad && k < invalid.Length; k++)
                    if (c == invalid[k]) bad = true;

                sb.Append(bad ? '_' : c);
            }

            string cleaned = sb.ToString().Trim('_');
            if (cleaned.Length == 0) return "battle";
            return cleaned.Length > 60 ? cleaned.Substring(0, 60) : cleaned;
        }

        static string Truncate(string source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            return source.Length > MaxSourceLength ? source.Substring(0, MaxSourceLength) : source;
        }

        static bool EnsureDirectory()
        {
            try
            {
                if (!Directory.Exists(SessionDirectory)) Directory.CreateDirectory(SessionDirectory);
                return true;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"勝負の保存フォルダを作成できませんでした: {SessionDirectory}\n{e.Message}");
                return false;
            }
        }

        static bool TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"ファイルを削除できませんでした: {path}\n{e.Message}");
                return false;
            }
        }
    }
}
