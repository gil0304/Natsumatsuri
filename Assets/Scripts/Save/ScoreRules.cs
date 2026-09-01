using System;
using Matsuri.Data;
using UnityEngine;

namespace Matsuri.Save
{
    /// <summary>
    /// スコアの内訳 (§36 の結果画面で項目ごとに見せるため)。
    /// </summary>
    public readonly struct ScoreBreakdown
    {
        public readonly long RevenueScore;
        public readonly long VisitorScore;
        public readonly long SatisfactionScore;
        public readonly long PeakScore;
        public readonly long VarietyScore;

        public ScoreBreakdown(long revenue, long visitor, long satisfaction, long peak, long variety)
        {
            RevenueScore = revenue;
            VisitorScore = visitor;
            SatisfactionScore = satisfaction;
            PeakScore = peak;
            VarietyScore = variety;
        }

        public long Total => RevenueScore + VisitorScore + SatisfactionScore + PeakScore + VarietyScore;

        /// <summary>売上がスコア全体に占める割合（0〜1）。§35「メインランキングは売上」の確認用。</summary>
        public float RevenueShare
        {
            get
            {
                long total = Total;
                return total <= 0 ? 0f : Mathf.Clamp01(RevenueScore / (float)total);
            }
        }

        public string ToDisplayText()
        {
            return
                $"売上　　　: {RevenueScore:N0}\n" +
                $"来場者　　: {VisitorScore:N0}\n" +
                $"満足度　　: {SatisfactionScore:N0}\n" +
                $"最大同時　: {PeakScore:N0}\n" +
                $"屋台の種類: {VarietyScore:N0}\n" +
                $"合計　　　: {Total:N0}";
        }
    }

    /// <summary>
    /// 仕様書 §35。総合スコアの計算。
    /// Unity のオブジェクトに触らない純関数なので EditMode テストで検証できる (§67)。
    ///
    /// §35「メインランキングは売上」なので、売上の比重を他項目より圧倒的に大きくする。
    /// 具体的には売上スコアに <see cref="RevenueDominance"/> を掛け、
    /// さらに「売上以外の合計は売上スコアを超えない」上限を掛ける。
    /// 係数そのものは BalanceConfig 側に置く (§31 ハードコードしない)。
    /// </summary>
    public static class ScoreRules
    {
        /// <summary>売上スコアに掛ける倍率。売上をランキングの主役に固定するための重み。</summary>
        public const float RevenueDominance = 3f;

        /// <summary>売上以外のボーナス合計が、売上スコアに対して占めてよい上限の割合。</summary>
        public const float BonusCapRatio = 0.5f;

        // BalanceConfig が渡されなかったときの保険値 (§31 の既定値と揃える)。
        const float FallbackRevenueWeight      = 1.0f;
        const float FallbackVisitorWeight      = 120f;
        const float FallbackSatisfactionWeight = 4000f;
        const float FallbackPeakWeight         = 200f;
        const float FallbackVarietyWeight      = 15000f;

        /// <summary>§35 の総合スコア。</summary>
        public static long CalculateTotal(FestivalResult result, BalanceConfig balance)
        {
            if (result == null) return 0;
            return Breakdown(result, balance).Total;
        }

        /// <summary>項目ごとの内訳を出す。CalculateTotal はこの合計。</summary>
        public static ScoreBreakdown Breakdown(FestivalResult result, BalanceConfig balance)
        {
            if (result == null) return default;

            float wRevenue      = balance != null ? balance.ScoreRevenueWeight      : FallbackRevenueWeight;
            float wVisitor      = balance != null ? balance.ScoreVisitorWeight      : FallbackVisitorWeight;
            float wSatisfaction = balance != null ? balance.ScoreSatisfactionWeight : FallbackSatisfactionWeight;
            float wPeak         = balance != null ? balance.ScorePeakWeight         : FallbackPeakWeight;
            float wVariety      = balance != null ? balance.ScoreVarietyWeight      : FallbackVarietyWeight;

            // 赤字（マイナス売上）は 0 として扱う。スコアが負になっても意味が無いため。
            double revenue = Math.Max(0L, result.Revenue);

            double revenueScore = revenue * wRevenue * RevenueDominance;

            double visitorScore      = Math.Max(0, result.VisitorCount) * wVisitor;
            double satisfactionScore = Mathf.Clamp(result.AverageSatisfaction, 0f, 100f) / 100f * wSatisfaction;
            double peakScore         = Math.Max(0, result.PeakConcurrent) * wPeak;
            double varietyScore      = Math.Max(0, result.StallKindsUsed) * wVariety;

            // 売上以外の合計が売上を食わないよう頭を押さえる (§35)。
            double bonusSum = visitorScore + satisfactionScore + peakScore + varietyScore;
            double bonusCap = revenueScore * BonusCapRatio;
            if (bonusSum > bonusCap && bonusSum > 0.0)
            {
                double scale = bonusCap / bonusSum;
                visitorScore      *= scale;
                satisfactionScore *= scale;
                peakScore         *= scale;
                varietyScore      *= scale;
            }

            return new ScoreBreakdown(
                Round(revenueScore),
                Round(visitorScore),
                Round(satisfactionScore),
                Round(peakScore),
                Round(varietyScore));
        }

        /// <summary>
        /// スコアを結果に書き戻す。結果画面 (§36) とランキング送信 (§37) の直前に呼ぶ。
        /// </summary>
        public static long ApplyTo(FestivalResult result, BalanceConfig balance)
        {
            if (result == null) return 0;
            result.TotalScore = CalculateTotal(result, balance);
            return result.TotalScore;
        }

        /// <summary>
        /// §36 の評価文。売上を主軸に、遊んだ人へ一言返す。
        /// </summary>
        public static string Evaluate(FestivalResult result)
        {
            if (result == null) return "結果がありません。";

            long revenue = result.Revenue;
            float satisfaction = result.AverageSatisfaction;

            string revenueComment;
            if (revenue >= 5000000)      revenueComment = "伝説の祭りだ。町中の話題をさらった。";
            else if (revenue >= 2000000) revenueComment = "大成功。屋台の並びが完全に噛み合っていた。";
            else if (revenue >= 800000)  revenueComment = "上々の売上。人の流れを掴めている。";
            else if (revenue >= 200000)  revenueComment = "まずまず。屋台を増やせばもっと伸びる。";
            else if (revenue > 0)        revenueComment = "静かな祭りだった。人を呼ぶ工夫を足そう。";
            else                         revenueComment = "売上ゼロ。まずは屋台を1軒建てて開催してみよう。";

            string satisfactionComment;
            if (satisfaction >= 80)      satisfactionComment = "来場者はみんな満足して帰っていった。";
            else if (satisfaction >= 60) satisfactionComment = "おおむね好評。行列を短くできればもっと良い。";
            else if (satisfaction >= 40) satisfactionComment = "待ち時間が長かったようだ。屋台を増やすかベンチを置こう。";
            else                         satisfactionComment = "不満が多い。混雑と価格を見直そう。";

            return revenueComment + "\n" + satisfactionComment;
        }

        static long Round(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            double clamped = Math.Min(value, long.MaxValue / 4.0);
            return (long)Math.Round(Math.Max(0.0, clamped), MidpointRounding.AwayFromZero);
        }
    }
}
