using System;
using System.Globalization;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §35 / §36。1回の祭りの成績。
    /// JsonUtility でそのまま保存できるよう、フィールドは
    /// プリミティブと string だけで構成する（コレクションや Nullable を持たない）。
    /// </summary>
    [Serializable]
    public sealed class FestivalResult
    {
        /// <summary>祭りの名前（Matsuri Script の「祭り」宣言から）。</summary>
        public string FestivalName = "夏祭り";

        /// <summary>総売上（円）。§35 のメインランキングはこれで決まる。</summary>
        public long Revenue;

        /// <summary>この祭りに来た総来場者数。</summary>
        public int VisitorCount;

        /// <summary>平均満足度（0〜100）。</summary>
        public float AverageSatisfaction;

        /// <summary>同時来場者数の最大値。会場のにぎわいの指標。</summary>
        public int PeakConcurrent;

        /// <summary>建てた屋台の種類数（同じ屋台を何軒建てても1と数える）。</summary>
        public int StallKindsUsed;

        /// <summary>一番売れた屋台の表示名。</summary>
        public string TopStallName = "";

        /// <summary>一番売れた屋台の売上。</summary>
        public long TopStallRevenue;

        /// <summary>総合スコア。ScoreRules.CalculateTotal の結果を入れる。</summary>
        public long TotalScore;

        /// <summary>作成日時。"yyyy-MM-dd HH:mm:ss" 形式。</summary>
        public string CreatedDate = "";

        /// <summary>この結果を出した Matsuri Script のソース（リプレイ・共有用 §54）。</summary>
        public string SourceCode = "";

        /// <summary>モード名。"FREE" / "CHALLENGE: 食べ物祭り" / "BATTLE" など。</summary>
        public string ModeName = "FREE";

        /// <summary>保存用の日時フォーマット。全システムで揃える。</summary>
        public const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        public FestivalResult() { }

        public FestivalResult(string festivalName)
        {
            if (!string.IsNullOrEmpty(festivalName)) FestivalName = festivalName;
            StampCreatedDate();
        }

        /// <summary>CreatedDate に「今」を入れる。すでに入っていれば何もしない。</summary>
        public void StampCreatedDate(bool overwrite = false)
        {
            if (!overwrite && !string.IsNullOrEmpty(CreatedDate)) return;
            CreatedDate = DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>CreatedDate を DateTime に戻す。壊れていれば DateTime.MinValue。</summary>
        public DateTime ParseCreatedDate()
        {
            if (DateTime.TryParseExact(CreatedDate, DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                return parsed;

            return DateTime.TryParse(CreatedDate, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var loose) ? loose : DateTime.MinValue;
        }

        /// <summary>ランキング行 (§37) に出す1行テキスト。</summary>
        public string ToRankingLine()
        {
            var name = string.IsNullOrEmpty(FestivalName) ? "名もなき祭り" : FestivalName;
            return $"{name}　売上 {Revenue:N0}円　来場 {VisitorCount:N0}人　満足度 {AverageSatisfaction:0.0}";
        }

        /// <summary>結果画面 (§36) に出す複数行テキスト。</summary>
        public string ToSummaryText()
        {
            var top = string.IsNullOrEmpty(TopStallName) ? "なし" : $"{TopStallName}（{TopStallRevenue:N0}円）";
            return
                $"祭りの名前　　: {FestivalName}\n" +
                $"総売上　　　　: {Revenue:N0} 円\n" +
                $"来場者数　　　: {VisitorCount:N0} 人\n" +
                $"平均満足度　　: {AverageSatisfaction:0.0} / 100\n" +
                $"最大同時来場　: {PeakConcurrent:N0} 人\n" +
                $"屋台の種類　　: {StallKindsUsed} 種類\n" +
                $"一番人気　　　: {top}\n" +
                $"総合スコア　　: {TotalScore:N0}";
        }

        /// <summary>値を丸ごとコピーした新しいインスタンスを返す（ランキングへ渡すときに使う）。</summary>
        public FestivalResult Clone()
        {
            return new FestivalResult
            {
                FestivalName = FestivalName,
                Revenue = Revenue,
                VisitorCount = VisitorCount,
                AverageSatisfaction = AverageSatisfaction,
                PeakConcurrent = PeakConcurrent,
                StallKindsUsed = StallKindsUsed,
                TopStallName = TopStallName,
                TopStallRevenue = TopStallRevenue,
                TotalScore = TotalScore,
                CreatedDate = CreatedDate,
                SourceCode = SourceCode,
                ModeName = ModeName
            };
        }

        /// <summary>同じ祭りの結果とみなせるか（重複投稿の判定に使う）。</summary>
        public bool IsSameEntry(FestivalResult other)
        {
            if (other == null) return false;
            return Revenue == other.Revenue
                && CreatedDate == other.CreatedDate
                && string.Equals(FestivalName, other.FestivalName, StringComparison.Ordinal);
        }

        public override string ToString() => ToRankingLine();
    }
}
