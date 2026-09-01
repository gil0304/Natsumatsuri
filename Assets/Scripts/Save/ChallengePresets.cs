using System;
using System.Collections.Generic;
using Matsuri.Script;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §47。CHALLENGE MODE の既定のお題。
    /// ここに足すだけで選択画面に増える。
    /// </summary>
    public static class ChallengePresets
    {
        // ── ID (§47) ──────────────────────────────────────────
        public const string StandardId      = "standard";
        public const string FoodFestivalId  = "food_festival";
        public const string SmallFestivalId = "small_festival";
        public const string FireworkNightId = "firework_night";
        public const string LowBudgetId     = "low_budget";

        static ChallengeDefinition[] _all;

        /// <summary>すべてのお題。</summary>
        public static IReadOnlyList<ChallengeDefinition> All => _all ??= Build();

        /// <summary>IDで引く。無ければ null。</summary>
        public static ChallengeDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var all = All;
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return all[i];

            return null;
        }

        /// <summary>先頭のお題。選択画面の初期選択に使う。</summary>
        public static ChallengeDefinition Default => All.Count > 0 ? All[0] : null;

        /// <summary>
        /// FREE MODE 用の「制限なし」定義。
        /// 予算は BalanceConfig 側が決めるため、ここでは目安の値を入れておく。
        /// </summary>
        public static ChallengeDefinition FreePlay()
        {
            return new ChallengeDefinition(
                    "free",
                    "FREE MODE",
                    "制限なし。好きなだけ祭りを大きくする。",
                    100000000L)
                .WithBounds(GroundBounds.Default.MinX, GroundBounds.Default.MaxX,
                            GroundBounds.Default.MinZ, GroundBounds.Default.MaxZ);
        }

        static ChallengeDefinition[] Build()
        {
            // 標準のお題。仕様書 §31 の「予算 1,000,000円」と
            // §46 CHALLENGE MODE の例（予算100万円・制限時間5分）がこれにあたる。
            // 屋台の制限も敷地の制限も無く、これが起動時の既定になる。
            var standard = new ChallengeDefinition(
                    StandardId,
                    "夏祭り",
                    "予算 1,000,000円。17:00から22:00まで、いちばん売れる祭りを作る。",
                    1000000L)
                .WithBounds(GroundBounds.Default.MinX, GroundBounds.Default.MaxX,
                            GroundBounds.Default.MinZ, GroundBounds.Default.MaxZ);

            var foodFestival = new ChallengeDefinition(
                    FoodFestivalId,
                    "FOOD FESTIVAL",
                    "食べ物の屋台だけで祭りを作る。遊びの屋台は建てられない。",
                    1000000L)
                .WithAllowedStalls(
                    MatsuriIds.Takoyaki,
                    MatsuriIds.Yakisoba,
                    MatsuriIds.Kakigori,
                    MatsuriIds.RingoAme,
                    MatsuriIds.Wataame,
                    MatsuriIds.Frankfurt)
                .WithRequiredStalls(MatsuriIds.Takoyaki);

            var smallFestival = new ChallengeDefinition(
                    SmallFestivalId,
                    "SMALL FESTIVAL",
                    "狭い土地でやりくりする。会場は 40m 四方しかない。",
                    1000000L)
                .WithBounds(-20f, 20f, -20f, 20f);

            var fireworkNight = new ChallengeDefinition(
                    FireworkNightId,
                    "FIREWORK NIGHT",
                    "花火を必ず上げる夜祭り。予算は多めだが花火代がかかる。",
                    1500000L)
                .WithFireworks(true);

            var lowBudget = new ChallengeDefinition(
                    LowBudgetId,
                    "LOW BUDGET",
                    "予算50万円。少ない元手でどこまで売り上げられるか。",
                    500000L);

            return new[] {
                standard, foodFestival, smallFestival, fireworkNight, lowBudget };
        }
    }
}
