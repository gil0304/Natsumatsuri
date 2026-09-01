using System.Collections.Generic;
using Matsuri.Data;
using Matsuri.Script;

namespace Matsuri.EditorTools
{
    /// <summary>
    /// 設備 6種 (§20) / 装飾 7種 (§21) / イベント 3種 (§22)。
    /// </summary>
    public static partial class DataAssetGenerator
    {
        // ═════════════════════════════════════════════════════════════════
        // 設備 (§20)
        // ═════════════════════════════════════════════════════════════════

        private static List<FacilityData> BuildFacilities()
        {
            var list = new List<FacilityData>
            {
                Facility(MatsuriIds.Bench, "ベンチ",
                    new[] { "べんち", "ベンチ", "椅子", "いす", "腰掛け", "bench", "seat" },
                    FacilityVisualKind.Bench, 10000, FacilityEffect.Rest,
                    radius: 3.5f, strength: 25f, capacity: 3),

                Facility(MatsuriIds.TrashCan, "ゴミ箱",
                    new[] { "ごみ箱", "ごみばこ", "ゴミ", "くずかご", "ダストボックス", "trash", "trashcan" },
                    FacilityVisualKind.TrashCan, 3000, FacilityEffect.Cleanliness,
                    radius: 10f, strength: 18f, capacity: 0),

                Facility(MatsuriIds.Toilet, "トイレ",
                    new[] { "といれ", "お手洗い", "手洗い", "便所", "化粧室", "toilet", "restroom", "wc" },
                    FacilityVisualKind.Toilet, 50000, FacilityEffect.Relief,
                    radius: 22f, strength: 30f, capacity: 4),

                Facility(MatsuriIds.Entrance, "入り口",
                    new[] { "入口", "いりぐち", "入場口", "正門", "ゲート", "entrance", "gate", "in" },
                    FacilityVisualKind.Gate, 0, FacilityEffect.Entrance,
                    radius: 6f, strength: 0f, capacity: 0),

                Facility(MatsuriIds.Exit, "出口",
                    new[] { "でぐち", "退場口", "裏門", "exit", "out" },
                    FacilityVisualKind.Gate, 0, FacilityEffect.Exit,
                    radius: 6f, strength: 0f, capacity: 0),

                Facility(MatsuriIds.SignBoard, "案内板",
                    new[] { "あんないばん", "案内", "案内看板", "看板", "地図", "マップ", "signboard", "sign", "map" },
                    FacilityVisualKind.SignBoard, 15000, FacilityEffect.Guidance,
                    radius: 18f, strength: 20f, capacity: 0),
            };

            // 「居場所」になる施設 (§34)。盆踊り場・休憩所・神社・手水舎。
            list.AddRange(BuildAmenities());
            return list;
        }

        private static FacilityData Facility(string id, string displayName, string[] aliases,
            FacilityVisualKind visual, long buildCost, FacilityEffect effect,
            float radius, float strength, int capacity)
        {
            var d = LoadOrCreate<FacilityData>($"{FacilityFolder}/Facility_{id}.asset");
            d.Id = id;
            d.DisplayName = displayName;
            d.Aliases = aliases;
            d.Prefab = null;
            d.Visual = visual;
            d.BuildCost = buildCost;
            d.Effect = effect;
            d.EffectRadius = radius;
            d.EffectStrength = strength;
            d.Capacity = capacity;
            Touch(d);
            return d;
        }

        // ═════════════════════════════════════════════════════════════════
        // 装飾 (§21)
        // ═════════════════════════════════════════════════════════════════

        private static List<DecorationData> BuildDecorations()
        {
            var list = new List<DecorationData>();

            // 提灯：祭りの主役の光源 (§59)。赤い紙に暖色の光。
            var lantern = Decoration(MatsuriIds.Lantern, "提灯",
                new[] { "ちょうちん", "ちょーちん", "チョウチン", "提燈", "赤提灯", "lantern" },
                DecorationVisualKind.Lantern, mainColor: "#E23B2E",
                buildCost: 5000, effect: DecorationEffect.Ambience,
                radius: 8f, ambience: 3f, sways: true);
            lantern.EmitsLight = true;
            lantern.LightColor = Hex("#FFB05C");
            lantern.LightIntensity = 420f;
            lantern.LightRange = 9f;
            Touch(lantern);
            list.Add(lantern);

            // のぼり：風に揺れる布 (§63)。光らない。
            var nobori = Decoration(MatsuriIds.Nobori, "のぼり",
                new[] { "幟", "ノボリ", "のぼり旗", "旗", "はた", "nobori", "flag" },
                DecorationVisualKind.Nobori, mainColor: "#F2EFE4",
                buildCost: 4000, effect: DecorationEffect.Ambience,
                radius: 6f, ambience: 2f, sways: true);
            NoLight(nobori);
            list.Add(nobori);

            // 神社：会場のランドマーク。遠くからでも目印になる。
            var shrine = Decoration(MatsuriIds.Shrine, "神社",
                new[] { "じんじゃ", "ジンジャ", "お宮", "御宮", "社", "本殿", "shrine" },
                DecorationVisualKind.Shrine, mainColor: "#C0392B",
                buildCost: 200000, effect: DecorationEffect.Landmark,
                radius: 40f, ambience: 8f, sways: false);
            NoLight(shrine);
            list.Add(shrine);

            var torii = Decoration(MatsuriIds.Torii, "鳥居",
                new[] { "とりい", "トリイ", "鳥井", "torii" },
                DecorationVisualKind.Torii, mainColor: "#D63B23",
                buildCost: 120000, effect: DecorationEffect.Landmark,
                radius: 35f, ambience: 6f, sways: false);
            NoLight(torii);
            list.Add(torii);

            var tree = Decoration(MatsuriIds.Tree, "木",
                new[] { "樹", "木々", "立木", "街路樹", "tree" },
                DecorationVisualKind.Tree, mainColor: "#386B31",
                buildCost: 8000, effect: DecorationEffect.Ambience,
                radius: 7f, ambience: 2f, sways: true);
            NoLight(tree);
            list.Add(tree);

            // 屋台用ライト：作業灯。白っぽく強い光で足元を照らす。
            var stallLight = Decoration(MatsuriIds.StallLight, "屋台用ライト",
                new[] { "屋台ライト", "ライト", "らいと", "照明", "電灯", "作業灯", "light", "stalllight" },
                DecorationVisualKind.StallLight, mainColor: "#D8D5CC",
                buildCost: 6000, effect: DecorationEffect.Lighting,
                radius: 12f, ambience: 1f, sways: false);
            stallLight.EmitsLight = true;
            stallLight.LightColor = Hex("#FFD08A");
            stallLight.LightIntensity = 950f;
            stallLight.LightRange = 15f;
            Touch(stallLight);
            list.Add(stallLight);

            var festivalSign = Decoration(MatsuriIds.FestivalSign, "夏祭り看板",
                new[] { "なつまつり看板", "夏祭り", "祭り看板", "大看板", "アーチ", "festivalsign" },
                DecorationVisualKind.FestivalSign, mainColor: "#F5C24A",
                buildCost: 20000, effect: DecorationEffect.Landmark,
                radius: 25f, ambience: 5f, sways: false);
            NoLight(festivalSign);
            list.Add(festivalSign);

            return list;
        }

        private static DecorationData Decoration(string id, string displayName, string[] aliases,
            DecorationVisualKind visual, string mainColor, long buildCost,
            DecorationEffect effect, float radius, float ambience, bool sways)
        {
            var d = LoadOrCreate<DecorationData>($"{DecorationFolder}/Decoration_{id}.asset");
            d.Id = id;
            d.DisplayName = displayName;
            d.Aliases = aliases;
            d.Prefab = null;
            d.Visual = visual;
            d.MainColor = Hex(mainColor);
            d.SwaysInWind = sways;
            d.BuildCost = buildCost;
            d.Effect = effect;
            d.EffectRadius = radius;
            d.AmbienceValue = ambience;
            Touch(d);
            return d;
        }

        private static void NoLight(DecorationData d)
        {
            d.EmitsLight = false;
            d.LightColor = Hex("#FFD08A");
            d.LightIntensity = 0f;
            d.LightRange = 0f;
            Touch(d);
        }

        // ═════════════════════════════════════════════════════════════════
        // イベント (§22)
        // ═════════════════════════════════════════════════════════════════

        private static List<FestivalEventData> BuildEvents()
        {
            var list = new List<FestivalEventData>
            {
                // 花火：会場全体に効く。§31 の 300,000円。
                Event(MatsuriIds.Fireworks, "花火",
                    new[] { "はなび", "ハナビ", "打ち上げ花火", "花火大会", "fireworks", "firework" },
                    FestivalEventKind.Fireworks, cost: 300000, duration: 25f,
                    burst: 40f, radius: 200f, stayExtend: 1.5f, attract: 1.8f),

                // 盆踊り：やぐらの周りだけ。長く続く。
                Event(MatsuriIds.BonOdori, "盆踊り",
                    new[] { "ぼんおどり", "ボンオドリ", "盆踊", "踊り", "やぐら", "櫓", "bonodori" },
                    FestivalEventKind.BonOdori, cost: 150000, duration: 90f,
                    burst: 25f, radius: 45f, stayExtend: 1.35f, attract: 1.4f),

                // 太鼓演奏：音で人を集める。
                Event(MatsuriIds.Taiko, "太鼓演奏",
                    new[] { "たいこ", "太鼓", "タイコ", "和太鼓", "たいこえんそう", "taiko" },
                    FestivalEventKind.Taiko, cost: 100000, duration: 60f,
                    burst: 20f, radius: 40f, stayExtend: 1.2f, attract: 1.3f),
            };
            return list;
        }

        private static FestivalEventData Event(string id, string displayName, string[] aliases,
            FestivalEventKind kind, long cost, float duration,
            float burst, float radius, float stayExtend, float attract)
        {
            var d = LoadOrCreate<FestivalEventData>($"{EventFolder}/Event_{id}.asset");
            d.Id = id;
            d.DisplayName = displayName;
            d.Aliases = aliases;
            d.Cost = cost;
            d.Duration = duration;
            d.Kind = kind;
            d.SatisfactionBurst = burst;
            d.EffectRadius = radius;
            d.StayExtendMultiplier = stayExtend;
            d.VisitorAttractMultiplier = attract;
            Touch(d);
            return d;
        }
    }
}
