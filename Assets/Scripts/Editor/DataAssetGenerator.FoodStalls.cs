using System.Collections.Generic;
using Matsuri.Data;
using Matsuri.Script;

namespace Matsuri.EditorTools
{
    /// <summary>
    /// 食べ物の屋台 6種 (§19)。数値は §31 の表に準拠。
    /// 見た目は §79「全部同じ材質・同じ形」を禁じているので、
    /// 屋台ごとに色・寸法・提灯数・電球数・屋根の形を変えている。
    /// </summary>
    public static partial class DataAssetGenerator
    {
        private static StallData NewStall(string id)
            => LoadOrCreate<StallData>($"{StallFolder}/Stall_{id}.asset");

        private static StallVisualRecipe NewRecipe(string id)
            => LoadOrCreate<StallVisualRecipe>($"{StallFolder}/StallVisual_{id}.asset");

        private static List<StallData> BuildAllStalls()
        {
            return new List<StallData>
            {
                // 食べ物 (§19)
                BuildTakoyaki(),
                BuildYakisoba(),
                BuildKakigori(),
                BuildRingoAme(),
                BuildWataame(),
                BuildFrankfurt(),
                // 遊び (§19)
                BuildKingyosukui(),
                BuildShateki(),
                BuildYoyoTsuri(),
                BuildSuperBall(),
                BuildKatanuki(),
            };
        }

        // ── たこ焼き ──────────────────────────────────────────────────────
        private static StallData BuildTakoyaki()
        {
            var r = NewRecipe(MatsuriIds.Takoyaki);
            r.Width = 3.4f; r.Depth = 2.3f; r.Height = 2.50f; r.CounterHeight = 1.00f;
            r.Roof = StallRoofKind.Gable;
            r.RoofColor = Hex("#B92A22"); r.RoofStripeColor = Hex("#F4EFE4"); r.StripedRoof = true;
            r.NorenColor = Hex("#A81C18"); r.NorenText = "たこ焼"; r.NorenSlits = 4;
            r.SignBoardColor = Hex("#EBDCB4"); r.SignTextColor = Hex("#1A1613");
            r.WoodColor = Hex("#6B4A2E"); r.CounterColor = Hex("#8C6640");
            r.BulbCount = 6; r.BulbColor = Hex("#FFD08A");
            r.LanternCount = 2; r.LanternColor = Hex("#E23B2E"); r.LightIntensity = 950f;
            r.Prop = StallPropKind.TakoyakiPlate; r.ProductColor = Hex("#C97833");
            r.QueuePointCount = 10; r.QueueSpacing = 0.75f;
            Touch(r);

            var d = NewStall(MatsuriIds.Takoyaki);
            d.Id = MatsuriIds.Takoyaki; d.DisplayName = "たこ焼き";
            d.Aliases = new[] { "たこやき", "タコ焼き", "たこ焼", "たこ焼き屋", "たこ", "tako", "takoyaki", "蛸焼き" };
            d.Category = StallCategory.Food;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 100000; d.DefaultPrice = 500; d.MinPrice = 200; d.MaxPrice = 1200;
            d.ServiceTime = 8f; d.Capacity = 6; d.MaxQueueLength = 24;
            d.BasePopularity = 70f; d.SatisfactionValue = 22f;
            d.HungerRelief = 32f; d.FunRelief = 6f; d.EnergyCost = 4f;
            d.Ambience = StallAmbienceKind.Sizzle; d.HasSteam = true;   // 湯気あり
            Touch(d);
            return d;
        }

        // ── 焼きそば ──────────────────────────────────────────────────────
        private static StallData BuildYakisoba()
        {
            var r = NewRecipe(MatsuriIds.Yakisoba);
            r.Width = 3.8f; r.Depth = 2.4f; r.Height = 2.55f; r.CounterHeight = 1.02f;
            r.Roof = StallRoofKind.Gable;
            r.RoofColor = Hex("#C75A24"); r.RoofStripeColor = Hex("#F2E7D2"); r.StripedRoof = true;
            r.NorenColor = Hex("#243154"); r.NorenText = "焼そば"; r.NorenSlits = 5;
            r.SignBoardColor = Hex("#E7D6A8"); r.SignTextColor = Hex("#20180F");
            r.WoodColor = Hex("#5E4127"); r.CounterColor = Hex("#7E5B38");
            r.BulbCount = 7; r.BulbColor = Hex("#FFD08A");
            r.LanternCount = 2; r.LanternColor = Hex("#F0A63C"); r.LightIntensity = 1020f;
            r.Prop = StallPropKind.Teppan; r.ProductColor = Hex("#9E5B24");
            r.QueuePointCount = 10; r.QueueSpacing = 0.78f;
            Touch(r);

            var d = NewStall(MatsuriIds.Yakisoba);
            d.Id = MatsuriIds.Yakisoba; d.DisplayName = "焼きそば";
            d.Aliases = new[] { "やきそば", "ヤキソバ", "焼そば", "焼き蕎麦", "yakisoba", "そば" };
            d.Category = StallCategory.Food;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 120000; d.DefaultPrice = 600; d.MinPrice = 250; d.MaxPrice = 1500;
            d.ServiceTime = 9f; d.Capacity = 6; d.MaxQueueLength = 24;
            d.BasePopularity = 66f; d.SatisfactionValue = 24f;
            d.HungerRelief = 40f; d.FunRelief = 5f; d.EnergyCost = 4f;
            d.Ambience = StallAmbienceKind.Sizzle; d.HasSteam = true;
            Touch(d);
            return d;
        }

        // ── かき氷 ────────────────────────────────────────────────────────
        private static StallData BuildKakigori()
        {
            var r = NewRecipe(MatsuriIds.Kakigori);
            r.Width = 3.0f; r.Depth = 2.1f; r.Height = 2.40f; r.CounterHeight = 0.96f;
            r.Roof = StallRoofKind.Awning;
            r.RoofColor = Hex("#3FA9D6"); r.RoofStripeColor = Hex("#F5FBFF"); r.StripedRoof = true;
            r.NorenColor = Hex("#5CBDE0"); r.NorenText = "氷"; r.NorenSlits = 3;
            r.SignBoardColor = Hex("#EAF6FC"); r.SignTextColor = Hex("#12507F");
            r.WoodColor = Hex("#8A6C4E"); r.CounterColor = Hex("#B3C7CE");
            r.BulbCount = 5; r.BulbColor = Hex("#DCF0FF");                 // 涼しげな白光
            r.LanternCount = 2; r.LanternColor = Hex("#6FC7E8"); r.LightIntensity = 820f;
            r.Prop = StallPropKind.IceShaver; r.ProductColor = Hex("#8CDCF0");
            r.QueuePointCount = 9; r.QueueSpacing = 0.72f;
            Touch(r);

            var d = NewStall(MatsuriIds.Kakigori);
            d.Id = MatsuriIds.Kakigori; d.DisplayName = "かき氷";
            d.Aliases = new[] { "かきごおり", "カキ氷", "カキゴオリ", "掻き氷", "氷", "kakigori", "こおり" };
            d.Category = StallCategory.Food;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 90000; d.DefaultPrice = 400; d.MinPrice = 150; d.MaxPrice = 1000;
            d.ServiceTime = 5f; d.Capacity = 8; d.MaxQueueLength = 26;
            d.BasePopularity = 72f; d.SatisfactionValue = 20f;
            d.HungerRelief = 18f; d.FunRelief = 10f; d.EnergyCost = 2f;
            d.Ambience = StallAmbienceKind.Shaving; d.HasSteam = false;    // 湯気ではなく氷の粉
            Touch(d);
            return d;
        }

        // ── りんご飴 ──────────────────────────────────────────────────────
        private static StallData BuildRingoAme()
        {
            var r = NewRecipe(MatsuriIds.RingoAme);
            r.Width = 2.6f; r.Depth = 1.9f; r.Height = 2.30f; r.CounterHeight = 0.98f;
            r.Roof = StallRoofKind.Shed;
            r.RoofColor = Hex("#D82C2E"); r.RoofStripeColor = Hex("#FFF3E0"); r.StripedRoof = false;
            r.NorenColor = Hex("#E03436"); r.NorenText = "りんご飴"; r.NorenSlits = 3;
            r.SignBoardColor = Hex("#F5EBCC"); r.SignTextColor = Hex("#961A1C");
            r.WoodColor = Hex("#754F30"); r.CounterColor = Hex("#96703F");
            r.BulbCount = 8; r.BulbColor = Hex("#FFDCA0");                 // 飴を照らしてテラテラ光らせる
            r.LanternCount = 1; r.LanternColor = Hex("#F04A44"); r.LightIntensity = 880f;
            r.Prop = StallPropKind.CandyAppleRack; r.ProductColor = Hex("#E81F26");
            r.QueuePointCount = 7; r.QueueSpacing = 0.70f;
            Touch(r);

            var d = NewStall(MatsuriIds.RingoAme);
            d.Id = MatsuriIds.RingoAme; d.DisplayName = "りんご飴";
            d.Aliases = new[] { "りんごあめ", "リンゴ飴", "リンゴアメ", "林檎飴", "りんご", "ringoame", "ringo", "applecandy" };
            d.Category = StallCategory.Food;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 70000; d.DefaultPrice = 300; d.MinPrice = 100; d.MaxPrice = 800;
            d.ServiceTime = 3f; d.Capacity = 10; d.MaxQueueLength = 20;
            d.BasePopularity = 60f; d.SatisfactionValue = 18f;
            d.HungerRelief = 14f; d.FunRelief = 8f; d.EnergyCost = 2f;
            d.Ambience = StallAmbienceKind.None; d.HasSteam = false;
            Touch(d);
            return d;
        }

        // ── わたあめ ──────────────────────────────────────────────────────
        private static StallData BuildWataame()
        {
            var r = NewRecipe(MatsuriIds.Wataame);
            r.Width = 2.8f; r.Depth = 2.0f; r.Height = 2.35f; r.CounterHeight = 0.94f;
            r.Roof = StallRoofKind.Awning;
            r.RoofColor = Hex("#F4B4C8"); r.RoofStripeColor = Hex("#FDF6F8"); r.StripedRoof = true;
            r.NorenColor = Hex("#EE93B4"); r.NorenText = "わたあめ"; r.NorenSlits = 4;
            r.SignBoardColor = Hex("#FCEFF3"); r.SignTextColor = Hex("#A83261");
            r.WoodColor = Hex("#7F5C3C"); r.CounterColor = Hex("#C6A9A2");
            r.BulbCount = 5; r.BulbColor = Hex("#FFE6CC");
            r.LanternCount = 1; r.LanternColor = Hex("#F79CB6"); r.LightIntensity = 800f;
            r.Prop = StallPropKind.CottonCandyMachine; r.ProductColor = Hex("#FBD3E4");
            r.QueuePointCount = 8; r.QueueSpacing = 0.72f;
            Touch(r);

            var d = NewStall(MatsuriIds.Wataame);
            d.Id = MatsuriIds.Wataame; d.DisplayName = "わたあめ";
            d.Aliases = new[] { "わたがし", "綿あめ", "綿飴", "綿菓子", "ワタアメ", "ワタガシ", "wataame", "watagashi", "cottoncandy" };
            d.Category = StallCategory.Food;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 80000; d.DefaultPrice = 300; d.MinPrice = 100; d.MaxPrice = 800;
            d.ServiceTime = 6f; d.Capacity = 5; d.MaxQueueLength = 20;
            d.BasePopularity = 64f; d.SatisfactionValue = 19f;
            d.HungerRelief = 12f; d.FunRelief = 14f; d.EnergyCost = 2f;
            d.Ambience = StallAmbienceKind.Whirr; d.HasSteam = false;
            Touch(d);
            return d;
        }

        // ── フランクフルト ────────────────────────────────────────────────
        private static StallData BuildFrankfurt()
        {
            var r = NewRecipe(MatsuriIds.Frankfurt);
            r.Width = 2.9f; r.Depth = 2.0f; r.Height = 2.42f; r.CounterHeight = 1.04f;
            r.Roof = StallRoofKind.Shed;
            r.RoofColor = Hex("#8C3A20"); r.RoofStripeColor = Hex("#E8C95C"); r.StripedRoof = true;
            r.NorenColor = Hex("#9E4522"); r.NorenText = "フランク"; r.NorenSlits = 3;
            r.SignBoardColor = Hex("#EFE0AE"); r.SignTextColor = Hex("#4C240F");
            r.WoodColor = Hex("#57381F"); r.CounterColor = Hex("#7A5330");
            r.BulbCount = 6; r.BulbColor = Hex("#FFCE86");
            r.LanternCount = 1; r.LanternColor = Hex("#F08A34"); r.LightIntensity = 900f;
            r.Prop = StallPropKind.Grill; r.ProductColor = Hex("#AE5330");
            r.QueuePointCount = 8; r.QueueSpacing = 0.74f;
            Touch(r);

            var d = NewStall(MatsuriIds.Frankfurt);
            d.Id = MatsuriIds.Frankfurt; d.DisplayName = "フランクフルト";
            d.Aliases = new[] { "フランク", "ふらんくふると", "ふらんく", "frank", "frankfurt", "ソーセージ", "sausage" };
            d.Category = StallCategory.Food;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 95000; d.DefaultPrice = 400; d.MinPrice = 150; d.MaxPrice = 1000;
            d.ServiceTime = 5f; d.Capacity = 8; d.MaxQueueLength = 22;
            d.BasePopularity = 58f; d.SatisfactionValue = 20f;
            d.HungerRelief = 34f; d.FunRelief = 4f; d.EnergyCost = 3f;
            d.Ambience = StallAmbienceKind.Sizzle; d.HasSteam = true;
            Touch(d);
            return d;
        }
    }
}
