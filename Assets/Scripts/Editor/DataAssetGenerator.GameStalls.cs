using Matsuri.Data;
using Matsuri.Script;

namespace Matsuri.EditorTools
{
    /// <summary>
    /// 遊びの屋台 5種 (§19)。食べ物より接客時間が長く、行列が伸びやすい。
    /// 色は屋台ごとに大きく振ってある（かき氷=水色 / 金魚すくい=青緑 / 射的=紺 …）(§79)。
    /// </summary>
    public static partial class DataAssetGenerator
    {
        // ── 金魚すくい ────────────────────────────────────────────────────
        private static StallData BuildKingyosukui()
        {
            var r = NewRecipe(MatsuriIds.Kingyosukui);
            r.Width = 3.6f; r.Depth = 2.6f; r.Height = 2.40f; r.CounterHeight = 0.70f;  // 水槽は低い
            r.Roof = StallRoofKind.Awning;
            r.RoofColor = Hex("#1E837E"); r.RoofStripeColor = Hex("#EFF7F2"); r.StripedRoof = true;
            r.NorenColor = Hex("#146F6C"); r.NorenText = "金魚"; r.NorenSlits = 4;
            r.SignBoardColor = Hex("#E8F1E6"); r.SignTextColor = Hex("#D93B26");
            r.WoodColor = Hex("#664A2E"); r.CounterColor = Hex("#4C8385");
            r.BulbCount = 6; r.BulbColor = Hex("#FFD8A0");
            r.LanternCount = 3; r.LanternColor = Hex("#EF5C3C"); r.LightIntensity = 1000f;
            r.Prop = StallPropKind.FishTank; r.ProductColor = Hex("#F2682A");            // 金魚の朱
            r.QueuePointCount = 12; r.QueueSpacing = 0.80f;
            Touch(r);

            var d = NewStall(MatsuriIds.Kingyosukui);
            d.Id = MatsuriIds.Kingyosukui; d.DisplayName = "金魚すくい";
            d.Aliases = new[] { "きんぎょすくい", "キンギョスクイ", "金魚掬い", "金魚", "きんぎょ", "kingyo", "kingyosukui" };
            d.Category = StallCategory.Game;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 150000; d.DefaultPrice = 300; d.MinPrice = 100; d.MaxPrice = 800;
            d.ServiceTime = 12f; d.Capacity = 6; d.MaxQueueLength = 20;
            d.BasePopularity = 76f; d.SatisfactionValue = 28f;
            d.HungerRelief = 2f; d.FunRelief = 40f; d.EnergyCost = 6f;
            d.Ambience = StallAmbienceKind.Water; d.HasSteam = false;
            Touch(d);
            return d;
        }

        // ── 射的 ──────────────────────────────────────────────────────────
        private static StallData BuildShateki()
        {
            var r = NewRecipe(MatsuriIds.Shateki);
            r.Width = 3.8f; r.Depth = 2.8f; r.Height = 2.62f; r.CounterHeight = 1.05f;
            r.Roof = StallRoofKind.Gable;
            r.RoofColor = Hex("#1E2A52"); r.RoofStripeColor = Hex("#E4DCBC"); r.StripedRoof = true;
            r.NorenColor = Hex("#182347"); r.NorenText = "射的"; r.NorenSlits = 4;
            r.SignBoardColor = Hex("#F0DC9C"); r.SignTextColor = Hex("#1E2A52");
            r.WoodColor = Hex("#4E3722"); r.CounterColor = Hex("#463523");
            r.BulbCount = 9; r.BulbColor = Hex("#FFE0AA");                                // 景品棚を煌々と照らす
            r.LanternCount = 2; r.LanternColor = Hex("#F5C24A"); r.LightIntensity = 1150f;
            r.Prop = StallPropKind.ShootingRack; r.ProductColor = Hex("#E7B93E");
            r.QueuePointCount = 10; r.QueueSpacing = 0.78f;
            Touch(r);

            var d = NewStall(MatsuriIds.Shateki);
            d.Id = MatsuriIds.Shateki; d.DisplayName = "射的";
            d.Aliases = new[] { "しゃてき", "シャテキ", "射的屋", "鉄砲", "てっぽう", "コルク銃", "shateki" };
            d.Category = StallCategory.Game;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 180000; d.DefaultPrice = 400; d.MinPrice = 150; d.MaxPrice = 1200;
            d.ServiceTime = 14f; d.Capacity = 5; d.MaxQueueLength = 18;
            d.BasePopularity = 74f; d.SatisfactionValue = 30f;
            d.HungerRelief = 1f; d.FunRelief = 44f; d.EnergyCost = 7f;
            d.Ambience = StallAmbienceKind.Pop; d.HasSteam = false;
            Touch(d);
            return d;
        }

        // ── ヨーヨー釣り ──────────────────────────────────────────────────
        private static StallData BuildYoyoTsuri()
        {
            var r = NewRecipe(MatsuriIds.YoyoTsuri);
            r.Width = 3.2f; r.Depth = 2.4f; r.Height = 2.35f; r.CounterHeight = 0.66f;
            r.Roof = StallRoofKind.Awning;
            r.RoofColor = Hex("#3FB169"); r.RoofStripeColor = Hex("#FBF3A6"); r.StripedRoof = true;
            r.NorenColor = Hex("#2F94AE"); r.NorenText = "ヨーヨー"; r.NorenSlits = 5;
            r.SignBoardColor = Hex("#F4F2DE"); r.SignTextColor = Hex("#1E6473");
            r.WoodColor = Hex("#70502F"); r.CounterColor = Hex("#4E9199");
            r.BulbCount = 6; r.BulbColor = Hex("#FFDDA8");
            r.LanternCount = 2; r.LanternColor = Hex("#F7B33F"); r.LightIntensity = 900f;
            r.Prop = StallPropKind.YoyoTub; r.ProductColor = Hex("#F5804A");
            r.QueuePointCount = 10; r.QueueSpacing = 0.75f;
            Touch(r);

            var d = NewStall(MatsuriIds.YoyoTsuri);
            d.Id = MatsuriIds.YoyoTsuri; d.DisplayName = "ヨーヨー釣り";
            d.Aliases = new[] { "よーよーつり", "ヨーヨーつり", "ヨーヨー", "よーよー", "水風船", "yoyo", "yoyotsuri" };
            d.Category = StallCategory.Game;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 110000; d.DefaultPrice = 300; d.MinPrice = 100; d.MaxPrice = 700;
            d.ServiceTime = 8f; d.Capacity = 6; d.MaxQueueLength = 18;
            d.BasePopularity = 62f; d.SatisfactionValue = 24f;
            d.HungerRelief = 1f; d.FunRelief = 34f; d.EnergyCost = 5f;
            d.Ambience = StallAmbienceKind.Water; d.HasSteam = false;
            Touch(d);
            return d;
        }

        // ── スーパーボールすくい ──────────────────────────────────────────
        private static StallData BuildSuperBall()
        {
            var r = NewRecipe(MatsuriIds.SuperBall);
            r.Width = 3.2f; r.Depth = 2.4f; r.Height = 2.30f; r.CounterHeight = 0.66f;
            r.Roof = StallRoofKind.Awning;
            r.RoofColor = Hex("#7548A8"); r.RoofStripeColor = Hex("#F6EFFA"); r.StripedRoof = true;
            r.NorenColor = Hex("#663C99"); r.NorenText = "ボール"; r.NorenSlits = 4;
            r.SignBoardColor = Hex("#F2ECFA"); r.SignTextColor = Hex("#572E85");
            r.WoodColor = Hex("#6B4C33"); r.CounterColor = Hex("#806A99");
            r.BulbCount = 6; r.BulbColor = Hex("#FFEBC8");
            r.LanternCount = 2; r.LanternColor = Hex("#B75CDB"); r.LightIntensity = 880f;
            r.Prop = StallPropKind.BallTub; r.ProductColor = Hex("#4CB4F0");
            r.QueuePointCount = 10; r.QueueSpacing = 0.75f;
            Touch(r);

            var d = NewStall(MatsuriIds.SuperBall);
            d.Id = MatsuriIds.SuperBall; d.DisplayName = "スーパーボールすくい";
            d.Aliases = new[] { "すーぱーぼーるすくい", "スーパーボール", "すーぱーぼーる", "ボールすくい", "ボール掬い", "superball", "supaboru" };
            d.Category = StallCategory.Game;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 100000; d.DefaultPrice = 300; d.MinPrice = 100; d.MaxPrice = 700;
            d.ServiceTime = 9f; d.Capacity = 6; d.MaxQueueLength = 18;
            d.BasePopularity = 58f; d.SatisfactionValue = 23f;
            d.HungerRelief = 1f; d.FunRelief = 32f; d.EnergyCost = 5f;
            d.Ambience = StallAmbienceKind.Water; d.HasSteam = false;
            Touch(d);
            return d;
        }

        // ── 型抜き ────────────────────────────────────────────────────────
        private static StallData BuildKatanuki()
        {
            var r = NewRecipe(MatsuriIds.Katanuki);
            r.Width = 2.8f; r.Depth = 2.2f; r.Height = 2.25f; r.CounterHeight = 0.62f;  // 座ってやる低い台
            r.Roof = StallRoofKind.Shed;
            r.RoofColor = Hex("#94764A"); r.RoofStripeColor = Hex("#DFD5B4"); r.StripedRoof = false;
            r.NorenColor = Hex("#DCCFA0"); r.NorenText = "型抜き"; r.NorenSlits = 3;
            r.SignBoardColor = Hex("#E4DCBE"); r.SignTextColor = Hex("#3D2E1A");
            r.WoodColor = Hex("#7B5734"); r.CounterColor = Hex("#9A7645");
            r.BulbCount = 4; r.BulbColor = Hex("#FFD696");                                // 素朴に薄暗い
            r.LanternCount = 1; r.LanternColor = Hex("#E5943C"); r.LightIntensity = 700f;
            r.Prop = StallPropKind.KatanukiDesk; r.ProductColor = Hex("#EFE0B4");
            r.QueuePointCount = 8; r.QueueSpacing = 0.70f;
            Touch(r);

            var d = NewStall(MatsuriIds.Katanuki);
            d.Id = MatsuriIds.Katanuki; d.DisplayName = "型抜き";
            d.Aliases = new[] { "かたぬき", "カタヌキ", "型抜", "かた抜き", "katanuki" };
            d.Category = StallCategory.Game;
            d.Prefab = null; d.VisualRecipe = r;
            d.BuildCost = 60000; d.DefaultPrice = 200; d.MinPrice = 50; d.MaxPrice = 600;
            d.ServiceTime = 15f; d.Capacity = 8; d.MaxQueueLength = 16;
            d.BasePopularity = 48f; d.SatisfactionValue = 26f;
            d.HungerRelief = 1f; d.FunRelief = 30f; d.EnergyCost = 4f;
            d.Ambience = StallAmbienceKind.None; d.HasSteam = false;
            Touch(d);
            return d;
        }
    }
}
