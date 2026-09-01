using System;
using System.Collections.Generic;
using System.Text;

namespace Matsuri.Save
{
    /// <summary>
    /// 建てた物の内訳1行ぶん (§54「作った祭りの内容」)。
    /// 「たこ焼き × 3」のように種類ごとに数をまとめる。
    /// </summary>
    [Serializable]
    public sealed class SavedObjectEntry
    {
        /// <summary>正規ID (MatsuriIds)。</summary>
        public string ObjectId = "";

        /// <summary>表示名（日本語）。</summary>
        public string DisplayName = "";

        /// <summary>種別。"屋台" / "装飾" / "設備" / "イベント"。</summary>
        public string KindName = "";

        /// <summary>同じIDを何個建てたか。</summary>
        public int Count;

        public SavedObjectEntry() { }

        public SavedObjectEntry(string objectId, string displayName, string kindName, int count)
        {
            ObjectId = objectId ?? "";
            DisplayName = displayName ?? "";
            KindName = kindName ?? "";
            Count = count;
        }

        public override string ToString() =>
            Count > 1 ? $"{DisplayName} × {Count}" : DisplayName;
    }

    /// <summary>
    /// 仕様書 §54。保存される祭り1件。
    /// JsonUtility でシリアライズするため、フィールドは public かつ
    /// プリミティブ / string / [Serializable] クラス / List に限る。
    /// </summary>
    [Serializable]
    public sealed class SavedFestival
    {
        /// <summary>祭りの名前。</summary>
        public string FestivalName = "夏祭り";

        /// <summary>祭りを作った Matsuri Script のソース (§54)。</summary>
        public string SourceCode = "";

        /// <summary>保存日時。"yyyy-MM-dd HH:mm:ss"。</summary>
        public string CreatedDate = "";

        /// <summary>総合スコア (§35)。一覧の並べ替えに使うので結果とは別にも持つ。</summary>
        public long Score;

        /// <summary>売上。一覧表示で結果を開かずに見せるため。</summary>
        public long Revenue;

        /// <summary>建てた物の総数。</summary>
        public int CreatedObjectCount;

        /// <summary>建てた物の内訳。</summary>
        public List<SavedObjectEntry> CreatedObjects = new List<SavedObjectEntry>();

        /// <summary>この祭りの成績 (§36)。まだ開催していなければ売上0のまま。</summary>
        public FestivalResult Result = new FestivalResult();

        /// <summary>
        /// 実際に書き出されたファイル名（拡張子込み）。
        /// JSON には含めない。SaveSystem が読み書きのときに設定する。
        /// </summary>
        [NonSerialized] public string FileName;

        public SavedFestival() { }

        public SavedFestival(string festivalName, string sourceCode)
        {
            if (!string.IsNullOrEmpty(festivalName)) FestivalName = festivalName;
            SourceCode = sourceCode ?? "";
            StampCreatedDate();
        }

        /// <summary>結果からセーブデータを組み立てる。</summary>
        public static SavedFestival FromResult(FestivalResult result)
        {
            var saved = new SavedFestival();
            if (result == null) return saved;

            saved.FestivalName = result.FestivalName;
            saved.SourceCode = result.SourceCode;
            saved.CreatedDate = string.IsNullOrEmpty(result.CreatedDate)
                ? DateTime.Now.ToString(FestivalResult.DateFormat, System.Globalization.CultureInfo.InvariantCulture)
                : result.CreatedDate;
            saved.Score = result.TotalScore;
            saved.Revenue = result.Revenue;
            saved.Result = result.Clone();
            return saved;
        }

        public void StampCreatedDate(bool overwrite = false)
        {
            if (!overwrite && !string.IsNullOrEmpty(CreatedDate)) return;
            CreatedDate = DateTime.Now.ToString(FestivalResult.DateFormat,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>建てた物を1個ぶん記録する。同じIDなら数を増やす。</summary>
        public void AddCreatedObject(string objectId, string displayName, string kindName)
        {
            if (string.IsNullOrEmpty(objectId)) return;
            CreatedObjects ??= new List<SavedObjectEntry>();

            for (int i = 0; i < CreatedObjects.Count; i++)
            {
                if (CreatedObjects[i] != null && CreatedObjects[i].ObjectId == objectId)
                {
                    CreatedObjects[i].Count++;
                    CreatedObjectCount++;
                    return;
                }
            }

            CreatedObjects.Add(new SavedObjectEntry(objectId,
                string.IsNullOrEmpty(displayName) ? objectId : displayName, kindName, 1));
            CreatedObjectCount++;
        }

        /// <summary>「たこ焼き × 3 / 提灯 × 12」のような1行サマリ。</summary>
        public string DescribeContents()
        {
            if (CreatedObjects == null || CreatedObjects.Count == 0) return "（何も建てていない）";

            var sb = new StringBuilder();
            for (int i = 0; i < CreatedObjects.Count; i++)
            {
                if (CreatedObjects[i] == null) continue;
                if (sb.Length > 0) sb.Append(" / ");
                sb.Append(CreatedObjects[i].ToString());
            }
            return sb.Length == 0 ? "（何も建てていない）" : sb.ToString();
        }

        /// <summary>セーブ一覧 (§54) に出す1行。</summary>
        public string ToListLine()
        {
            var name = string.IsNullOrEmpty(FestivalName) ? "名もなき祭り" : FestivalName;
            return $"{name}　{CreatedDate}　売上 {Revenue:N0}円　スコア {Score:N0}";
        }

        /// <summary>読み込み直後の欠損を埋める。壊れかけのJSONでも落とさないため。</summary>
        public void EnsureValid()
        {
            if (string.IsNullOrEmpty(FestivalName)) FestivalName = "名もなき祭り";
            SourceCode ??= "";
            CreatedObjects ??= new List<SavedObjectEntry>();
            Result ??= new FestivalResult();
            StampCreatedDate();
            if (CreatedObjectCount <= 0)
            {
                int sum = 0;
                for (int i = 0; i < CreatedObjects.Count; i++)
                    if (CreatedObjects[i] != null) sum += CreatedObjects[i].Count;
                CreatedObjectCount = sum;
            }
        }

        public DateTime ParseCreatedDate()
        {
            return DateTime.TryParseExact(CreatedDate, FestivalResult.DateFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed)
                ? parsed
                : DateTime.MinValue;
        }

        public override string ToString() => ToListLine();
    }
}
