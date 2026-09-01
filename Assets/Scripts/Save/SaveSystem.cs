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
    /// 仕様書 §54。作った祭りをローカルのJSONとして保存・読み込みする。
    /// 保存先は Application.persistentDataPath/MatsuriSaves/ 。
    /// 壊れたファイルは読み飛ばし、例外はログに出す（一覧全体を巻き添えにしない）。
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>セーブフォルダ名。</summary>
        public const string DirectoryName = "MatsuriSaves";

        /// <summary>セーブファイルの拡張子。</summary>
        public const string Extension = ".json";

        /// <summary>1つの祭りに許すソースコードの長さ。異常に巨大なJSONを弾く。</summary>
        public const int MaxSourceLength = 200000;

        static string _cachedDirectory;

        /// <summary>セーブフォルダの絶対パス。</summary>
        public static string SaveDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_cachedDirectory))
                    _cachedDirectory = Path.Combine(Application.persistentDataPath, DirectoryName);
                return _cachedDirectory;
            }
        }

        /// <summary>フォルダが無ければ作る。作れなければ false。</summary>
        public static bool EnsureDirectory()
        {
            try
            {
                if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
                return true;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"セーブフォルダを作成できませんでした: {SaveDirectory}\n{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 祭りを保存する。すでに FileName を持っていれば同じファイルを上書きする。
        /// 成功したら true。
        /// </summary>
        public static bool Save(SavedFestival festival)
        {
            if (festival == null)
            {
                MatsuriLog.Warn("保存するデータが空です。");
                return false;
            }
            if (!EnsureDirectory()) return false;

            festival.EnsureValid();
            if (festival.SourceCode != null && festival.SourceCode.Length > MaxSourceLength)
                festival.SourceCode = festival.SourceCode.Substring(0, MaxSourceLength);

            string fileName = string.IsNullOrEmpty(festival.FileName)
                ? BuildUniqueFileName(festival)
                : festival.FileName;

            string path = Path.Combine(SaveDirectory, fileName);
            string tempPath = path + ".tmp";

            try
            {
                string json = JsonUtility.ToJson(festival, true);
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));

                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);

                festival.FileName = fileName;
                MatsuriLog.Always($"祭りを保存しました: {path}");
                return true;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"祭りの保存に失敗しました: {path}\n{e.Message}");
                TryDeleteFile(tempPath);
                return false;
            }
        }

        /// <summary>
        /// 保存されている祭りをすべて読む。新しい順。
        /// 壊れたファイルはログに出して読み飛ばす。
        /// </summary>
        public static List<SavedFestival> LoadAll()
        {
            var results = new List<SavedFestival>();

            string[] files;
            try
            {
                if (!Directory.Exists(SaveDirectory)) return results;
                files = Directory.GetFiles(SaveDirectory, "*" + Extension);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"セーブフォルダを読めませんでした: {SaveDirectory}\n{e.Message}");
                return results;
            }

            Array.Sort(files, StringComparer.Ordinal);

            for (int i = 0; i < files.Length; i++)
            {
                var loaded = LoadFile(files[i]);
                if (loaded != null) results.Add(loaded);
            }

            results.Sort(CompareByDateDescending);
            return results;
        }

        /// <summary>ファイル1つを読む。壊れていれば null を返す（例外は投げない）。</summary>
        public static SavedFestival LoadFile(string path)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json) || json.IndexOf("\"FestivalName\"", StringComparison.Ordinal) < 0)
                {
                    MatsuriLog.Warn($"セーブファイルの形式が違うため読み飛ばしました: {Path.GetFileName(path)}");
                    return null;
                }

                var festival = JsonUtility.FromJson<SavedFestival>(json);
                if (festival == null)
                {
                    MatsuriLog.Warn($"セーブファイルを解釈できないため読み飛ばしました: {Path.GetFileName(path)}");
                    return null;
                }

                festival.EnsureValid();
                festival.FileName = Path.GetFileName(path);
                return festival;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"セーブファイルが壊れているため読み飛ばしました: {Path.GetFileName(path)}\n{e.Message}");
                return null;
            }
        }

        /// <summary>保存済みの祭りを消す。</summary>
        public static bool Delete(SavedFestival festival)
        {
            if (festival == null) return false;

            string fileName = festival.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                // FileName が無い場合は名前と日時から探す。
                var all = LoadAll();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].FestivalName == festival.FestivalName && all[i].CreatedDate == festival.CreatedDate)
                    {
                        fileName = all[i].FileName;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(fileName))
            {
                MatsuriLog.Warn($"削除対象のセーブが見つかりませんでした: {festival.FestivalName}");
                return false;
            }

            string path = Path.Combine(SaveDirectory, fileName);
            if (!TryDeleteFile(path)) return false;

            festival.FileName = null;
            MatsuriLog.Always($"祭りを削除しました: {fileName}");
            return true;
        }

        /// <summary>保存件数。</summary>
        public static int Count()
        {
            try
            {
                return Directory.Exists(SaveDirectory)
                    ? Directory.GetFiles(SaveDirectory, "*" + Extension).Length
                    : 0;
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"セーブ件数を数えられませんでした: {e.Message}");
                return 0;
            }
        }

        /// <summary>ファイル名に使えない文字を落とす。空になったら既定名を返す。</summary>
        public static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "matsuri";

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
            if (cleaned.Length == 0) return "matsuri";
            return cleaned.Length > 40 ? cleaned.Substring(0, 40) : cleaned;
        }

        static string BuildUniqueFileName(SavedFestival festival)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string baseName = $"{SanitizeFileName(festival.FestivalName)}_{stamp}";
            string candidate = baseName + Extension;

            int suffix = 1;
            while (File.Exists(Path.Combine(SaveDirectory, candidate)))
            {
                candidate = $"{baseName}_{suffix}{Extension}";
                suffix++;
                if (suffix > 999) break;
            }
            return candidate;
        }

        static bool TryDeleteFile(string path)
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

        static int CompareByDateDescending(SavedFestival a, SavedFestival b)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;
            return b.ParseCreatedDate().CompareTo(a.ParseCreatedDate());
        }
    }
}
