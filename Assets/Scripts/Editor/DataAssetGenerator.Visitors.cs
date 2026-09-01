using System.Collections.Generic;
using UnityEngine;
using Matsuri.Data;
using Matsuri.Script;

namespace Matsuri.EditorTools
{
    /// <summary>
    /// NPCタイプ 5種 (§27)。子ども / 高校生 / カップル / 家族 / 大人。
    /// §27「性格の差」をそのままパラメータに落とす。
    /// 服・肌・髪の色は §79「同じNPCを大量に複製したように見せない」ため
    /// 各タイプに 5〜8色ずつ持たせる。浴衣らしい色（紺・藍・白地に赤・薄桃・浅葱）を軸にした。
    /// </summary>
    public static partial class DataAssetGenerator
    {
        // 肌の色。どのタイプにも同じ幅を持たせる。
        private static readonly string[] SkinTones =
        {
            "#F7DCC6", "#F0CDAF", "#E2B692", "#CC9A73", "#AE7E5C", "#8C6247"
        };

        // 髪の色はタイプごとに少しずつ変える（子どもは明るめ、大人は白髪混じり）。

        private static List<VisitorArchetype> BuildArchetypes()
        {
            return new List<VisitorArchetype>
            {
                BuildChild(),
                BuildHighSchooler(),
                BuildCouple(),
                BuildFamily(),
                BuildAdult(),
            };
        }

        private static VisitorArchetype NewArchetype(string id)
            => LoadOrCreate<VisitorArchetype>($"{VisitorFolder}/Visitor_{id}.asset");

        // ── 子ども ────────────────────────────────────────────────────────
        // 所持金が少なく、遊びを最優先。待てない。背が低くてちょこまか走る。
        private static VisitorArchetype BuildChild()
        {
            var a = NewArchetype("child");
            a.Id = "child";
            a.DisplayName = "子ども";
            a.SpawnWeight = 1.3f;

            a.Money = new FloatRange(300f, 1200f);
            a.Hunger = new FloatRange(25f, 60f);
            a.Fun = new FloatRange(70f, 100f);         // 遊び優先
            a.Energy = new FloatRange(55f, 95f);
            a.WalkingSpeed = new FloatRange(1.30f, 1.95f);
            a.Patience = new FloatRange(10f, 32f);     // すぐ飽きる
            a.FireworksInterest = 82f;

            a.PreferenceFood = Prefs(
                (MatsuriIds.Wataame, 88f), (MatsuriIds.RingoAme, 84f), (MatsuriIds.Kakigori, 80f),
                (MatsuriIds.Takoyaki, 55f), (MatsuriIds.Frankfurt, 50f), (MatsuriIds.Yakisoba, 40f));
            a.PreferenceGame = Prefs(
                (MatsuriIds.Kingyosukui, 92f), (MatsuriIds.SuperBall, 88f), (MatsuriIds.YoyoTsuri, 86f),
                (MatsuriIds.Shateki, 78f), (MatsuriIds.Katanuki, 70f));
            a.DefaultFoodPreference = 48f;
            a.DefaultGamePreference = 82f;

            a.TargetVisitCount = new Vector2Int(2, 4);
            a.PriceSensitivity = new FloatRange(1.6f, 2.4f);   // 高いと即あきらめる
            a.BodyHeight = new FloatRange(1.02f, 1.32f);       // 体格小

            // 甚平・浴衣。子どもは原色が多い。
            a.OutfitColors = Palette(
                "#D93B2E", "#F0B93A", "#4A9FD4", "#5FAF62", "#F7F2E6", "#EC93AC", "#2F4A82");
            a.SkinColors = Palette(SkinTones);
            a.HairColors = Palette("#17140F", "#241B12", "#3A2718", "#4E3620", "#2B2119");
            Touch(a);
            return a;
        }

        // ── 高校生 ────────────────────────────────────────────────────────
        // 食べ物と遊びを両方こなす。所持金は中くらい。友達と長居する。
        private static VisitorArchetype BuildHighSchooler()
        {
            var a = NewArchetype("highschool");
            a.Id = "highschool";
            a.DisplayName = "高校生";
            a.SpawnWeight = 1.5f;

            a.Money = new FloatRange(1500f, 4000f);
            a.Hunger = new FloatRange(45f, 90f);
            a.Fun = new FloatRange(55f, 95f);
            a.Energy = new FloatRange(65f, 100f);
            a.WalkingSpeed = new FloatRange(1.15f, 1.65f);
            a.Patience = new FloatRange(30f, 60f);
            a.FireworksInterest = 76f;

            a.PreferenceFood = Prefs(
                (MatsuriIds.Takoyaki, 84f), (MatsuriIds.Kakigori, 80f), (MatsuriIds.Yakisoba, 76f),
                (MatsuriIds.Frankfurt, 68f), (MatsuriIds.Wataame, 60f), (MatsuriIds.RingoAme, 58f));
            a.PreferenceGame = Prefs(
                (MatsuriIds.Shateki, 82f), (MatsuriIds.Kingyosukui, 70f), (MatsuriIds.YoyoTsuri, 60f),
                (MatsuriIds.SuperBall, 58f), (MatsuriIds.Katanuki, 56f));
            a.DefaultFoodPreference = 68f;
            a.DefaultGamePreference = 66f;

            a.TargetVisitCount = new Vector2Int(3, 6);
            a.PriceSensitivity = new FloatRange(1.0f, 1.7f);
            a.BodyHeight = new FloatRange(1.52f, 1.76f);

            // 浴衣。紺・藍・白地に赤・浅葱。
            a.OutfitColors = Palette(
                "#1E2A52", "#2C4374", "#F2EDE1", "#E9A8B4", "#4FA8AE", "#6E8F55", "#B33A46");
            a.SkinColors = Palette(SkinTones);
            a.HairColors = Palette("#17140F", "#241B12", "#3A2718", "#513A22", "#6B4C2C");
            Touch(a);
            return a;
        }

        // ── カップル ──────────────────────────────────────────────────────
        // 花火目当て。所持金が多く、値段をあまり気にしない。ゆっくり歩いて長く居る。
        private static VisitorArchetype BuildCouple()
        {
            var a = NewArchetype("couple");
            a.Id = "couple";
            a.DisplayName = "カップル";
            a.SpawnWeight = 1.2f;

            a.Money = new FloatRange(3000f, 9000f);
            a.Hunger = new FloatRange(35f, 75f);
            a.Fun = new FloatRange(45f, 80f);
            a.Energy = new FloatRange(60f, 100f);
            a.WalkingSpeed = new FloatRange(0.90f, 1.25f);     // ゆっくり
            a.Patience = new FloatRange(45f, 82f);
            a.FireworksInterest = 90f;                          // 花火興味高 (§27)

            a.PreferenceFood = Prefs(
                (MatsuriIds.RingoAme, 82f), (MatsuriIds.Takoyaki, 78f), (MatsuriIds.Kakigori, 76f),
                (MatsuriIds.Wataame, 74f), (MatsuriIds.Yakisoba, 66f), (MatsuriIds.Frankfurt, 60f));
            a.PreferenceGame = Prefs(
                (MatsuriIds.Kingyosukui, 72f), (MatsuriIds.Shateki, 64f), (MatsuriIds.YoyoTsuri, 55f),
                (MatsuriIds.SuperBall, 50f), (MatsuriIds.Katanuki, 44f));
            a.DefaultFoodPreference = 74f;                      // 食べ物寄り
            a.DefaultGamePreference = 52f;

            a.TargetVisitCount = new Vector2Int(3, 6);
            a.PriceSensitivity = new FloatRange(0.55f, 1.05f);  // 値段は気にしない
            a.BodyHeight = new FloatRange(1.54f, 1.80f);

            a.OutfitColors = Palette(
                "#1B2547", "#31456F", "#F5F0E4", "#EBA3AE", "#4AA6B8", "#8E7ABF", "#C03A48", "#5C7F4E");
            a.SkinColors = Palette(SkinTones);
            a.HairColors = Palette("#17140F", "#241B12", "#3A2718", "#4C351F", "#7A5734");
            Touch(a);
            return a;
        }

        // ── 家族 ──────────────────────────────────────────────────────────
        // 所持金が多く、複数の屋台を順に回る。子連れなので歩くのが遅く、我慢強い。
        private static VisitorArchetype BuildFamily()
        {
            var a = NewArchetype("family");
            a.Id = "family";
            a.DisplayName = "家族";
            a.SpawnWeight = 1.0f;

            a.Money = new FloatRange(5000f, 14000f);
            a.Hunger = new FloatRange(50f, 95f);
            a.Fun = new FloatRange(50f, 85f);
            a.Energy = new FloatRange(50f, 85f);
            a.WalkingSpeed = new FloatRange(0.78f, 1.12f);      // 歩くのが遅い
            a.Patience = new FloatRange(55f, 92f);              // 我慢強い
            a.FireworksInterest = 72f;

            a.PreferenceFood = Prefs(
                (MatsuriIds.Takoyaki, 86f), (MatsuriIds.Yakisoba, 84f), (MatsuriIds.Frankfurt, 76f),
                (MatsuriIds.Kakigori, 72f), (MatsuriIds.RingoAme, 70f), (MatsuriIds.Wataame, 68f));
            a.PreferenceGame = Prefs(
                (MatsuriIds.Kingyosukui, 86f), (MatsuriIds.SuperBall, 78f), (MatsuriIds.YoyoTsuri, 74f),
                (MatsuriIds.Katanuki, 66f), (MatsuriIds.Shateki, 62f));
            a.DefaultFoodPreference = 70f;
            a.DefaultGamePreference = 68f;

            a.TargetVisitCount = new Vector2Int(5, 9);          // たくさん回る
            a.PriceSensitivity = new FloatRange(0.9f, 1.5f);
            a.BodyHeight = new FloatRange(1.30f, 1.78f);        // 大人と子どもが混ざる

            a.OutfitColors = Palette(
                "#22304F", "#3B5170", "#EFE9DA", "#D9A0A8", "#579AA0", "#7C8C5A", "#8E5A3C", "#A83B3B");
            a.SkinColors = Palette(SkinTones);
            a.HairColors = Palette("#17140F", "#241B12", "#3A2718", "#4C3621", "#6E6A66");
            Touch(a);
            return a;
        }

        // ── 大人 ──────────────────────────────────────────────────────────
        // 食事中心。遊びにはあまり興味がない。所持金が多く、行列にも耐える。
        private static VisitorArchetype BuildAdult()
        {
            var a = NewArchetype("adult");
            a.Id = "adult";
            a.DisplayName = "大人";
            a.SpawnWeight = 1.6f;

            a.Money = new FloatRange(3000f, 10000f);
            a.Hunger = new FloatRange(55f, 100f);
            a.Fun = new FloatRange(15f, 50f);                   // 遊びは控えめ
            a.Energy = new FloatRange(55f, 95f);
            a.WalkingSpeed = new FloatRange(1.05f, 1.45f);
            a.Patience = new FloatRange(50f, 88f);
            a.FireworksInterest = 58f;

            a.PreferenceFood = Prefs(
                (MatsuriIds.Yakisoba, 88f), (MatsuriIds.Takoyaki, 84f), (MatsuriIds.Frankfurt, 78f),
                (MatsuriIds.Kakigori, 62f), (MatsuriIds.RingoAme, 45f), (MatsuriIds.Wataame, 40f));
            a.PreferenceGame = Prefs(
                (MatsuriIds.Shateki, 56f), (MatsuriIds.Kingyosukui, 42f), (MatsuriIds.Katanuki, 35f),
                (MatsuriIds.YoyoTsuri, 30f), (MatsuriIds.SuperBall, 28f));
            a.DefaultFoodPreference = 76f;
            a.DefaultGamePreference = 32f;

            a.TargetVisitCount = new Vector2Int(2, 5);
            a.PriceSensitivity = new FloatRange(0.7f, 1.2f);
            a.BodyHeight = new FloatRange(1.56f, 1.84f);

            a.OutfitColors = Palette(
                "#18213F", "#2A3557", "#3E4A56", "#E8E2D2", "#7A6A52", "#4C6B4A", "#6B3B3B");
            a.SkinColors = Palette(SkinTones);
            a.HairColors = Palette("#17140F", "#241B12", "#3A2718", "#5A5450", "#8A8580");
            Touch(a);
            return a;
        }
    }
}
