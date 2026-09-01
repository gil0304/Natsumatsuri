using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Matsuri.Core;
using Matsuri.Data;

namespace Matsuri.EditorTools
{
    /// <summary>
    /// 仕様書 §19〜§22 / §27 / §31 のデータを、すべてコードから ScriptableObject アセットとして生成する。
    /// 「Inspector で手作業」を前提にしないので、リポジトリを clone しただけの状態から
    /// メニュー1回で祭りのデータが揃う。
    ///
    /// 再実行しても壊れない：既存アセットがあれば読み込んで上書き更新する。
    /// バッチからは -executeMethod Matsuri.EditorTools.DataAssetGenerator.GenerateAll で呼べる。
    /// </summary>
    public static partial class DataAssetGenerator
    {
        public const string RootFolder = "Assets/ScriptableObjects";
        public const string StallFolder = RootFolder + "/Stalls";
        public const string FacilityFolder = RootFolder + "/Facilities";
        public const string DecorationFolder = RootFolder + "/Decorations";
        public const string EventFolder = RootFolder;
        public const string VisitorFolder = RootFolder + "/Visitors";
        public const string BalanceFolder = RootFolder + "/Balance";

        /// <summary>
        /// カタログは Resources 配下に置く。
        /// 実行時に Resources.Load で読むため、ここを外すと
        /// 「生成したのに反映されない」二重管理になる。
        /// </summary>
        public const string CatalogPath = RootFolder + "/Resources/MatsuriCatalog.asset";
        public const string BalancePath = BalanceFolder + "/BalanceConfig.asset";

        private static int _createdCount;
        private static int _updatedCount;

        [MenuItem("Matsuri/1. Generate Data Assets", false, 1)]
        public static void GenerateAll()
        {
            _createdCount = 0;
            _updatedCount = 0;

            EnsureFolder(RootFolder);
            EnsureFolder(StallFolder);
            EnsureFolder(FacilityFolder);
            EnsureFolder(DecorationFolder);
            EnsureFolder(VisitorFolder);
            EnsureFolder(BalanceFolder);

            try
            {
                AssetDatabase.StartAssetEditing();

                var balance = BuildBalanceConfig();
                var stalls = BuildAllStalls();
                var facilities = BuildFacilities();
                var decorations = BuildDecorations();
                var events = BuildEvents();
                var archetypes = BuildArchetypes();

                var catalog = LoadOrCreate<MatsuriCatalog>(CatalogPath);
                catalog.Balance = balance;
                catalog.Stalls = stalls.ToArray();
                catalog.Facilities = facilities.ToArray();
                catalog.Decorations = decorations.ToArray();
                catalog.Events = events.ToArray();
                catalog.Archetypes = archetypes.ToArray();
                catalog.GroundMinX = -60f;
                catalog.GroundMaxX = 60f;
                catalog.GroundMinZ = -60f;
                catalog.GroundMaxZ = 60f;
                catalog.RebuildIndex();
                Touch(catalog);

                MatsuriLog.Build(
                    $"データアセット生成 完了: 屋台 {stalls.Count}種 / 設備 {facilities.Count}種 / " +
                    $"装飾 {decorations.Count}種 / イベント {events.Count}種 / NPC {archetypes.Count}種" +
                    $"（新規 {_createdCount}件・更新 {_updatedCount}件）");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MatsuriLog.Build($"カタログ: {CatalogPath}");
        }

        // ─────────────────────────────────────────────────────────────────
        // アセット入出力
        // ─────────────────────────────────────────────────────────────────

        /// <summary>"Assets/A/B/C" のような階層を、無い階層だけ順に作る。</summary>
        public static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            string current = parts[0];                      // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>既にあれば読み込み、無ければ作る。どちらでも中身は呼び出し側が上書きする。</summary>
        public static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                _updatedCount++;
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            _createdCount++;
            return asset;
        }

        /// <summary>変更を保存対象に載せる。</summary>
        public static void Touch(Object asset)
        {
            if (asset != null) EditorUtility.SetDirty(asset);
        }

        /// <summary>#RRGGBB から Color を作る（色指定を読みやすくするための小道具）。</summary>
        public static Color Hex(string rrggbb)
        {
            if (ColorUtility.TryParseHtmlString(rrggbb.StartsWith("#") ? rrggbb : "#" + rrggbb, out var c))
                return c;
            return Color.magenta;   // 指定ミスがすぐ目に付くように
        }

        /// <summary>好みテーブルを短く書くための小道具。</summary>
        public static PreferenceEntry[] Prefs(params (string id, float value)[] entries)
        {
            var result = new PreferenceEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                result[i] = new PreferenceEntry { StallId = entries[i].id, Value = entries[i].value };
            return result;
        }

        public static Color[] Palette(params string[] hexes)
        {
            var result = new Color[hexes.Length];
            for (int i = 0; i < hexes.Length; i++) result[i] = Hex(hexes[i]);
            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        // BalanceConfig (§31)
        // ─────────────────────────────────────────────────────────────────

        private static BalanceConfig BuildBalanceConfig()
        {
            var b = LoadOrCreate<BalanceConfig>(BalancePath);

            b.InitialBudget = 1000000;
            b.FreeModeBudget = -1;

            // 17:00〜22:00 の5時間を実時間2分で走らせる (§7)。
            // 接客時間・来場ペース・歩行速度はこの値から自動で換算されるので、
            // 祭りの長さを変えたいときはここだけ触ればよい。
            b.MinutesPerRealSecond = 2.5f;      // 300分 ÷ 2.5 = 120秒
            b.StartMinutes = 17 * 60;
            b.EndMinutes = 22 * 60;

            b.MaxConcurrentVisitors = 300;
            b.MaxTotalVisitors = 4000;
            b.MaxActiveNavAgents = 220;
            b.VisitorSpeedMultiplier = 1.35f;
            b.ArrivalCurve = BuildArrivalCurve();

            b.AttractionToArrivalScale = 0.06f;
            b.EmptyFestivalArrivalMultiplier = 0.15f;

            b.WeightPreference = 1.0f;
            b.WeightNeed = 1.2f;
            b.WeightDistance = 0.55f;
            b.WeightQueue = 2.4f;
            b.WeightPrice = 1.1f;
            b.WeightPopularity = 0.5f;
            b.DistanceUnit = 10f;
            b.DecisionNoise = 12f;

            b.ReferencePrice = 500;
            b.PriceHalfPoint = 2.2f;

            b.PopularityPerQueuer = 1.5f;
            b.PopularityDecay = 2f;
            b.MaxPopularity = 100f;
            b.PopularityPerSale = 0.8f;

            b.SatisfactionPerWaitSecond = 0.6f;
            b.SatisfactionOnGiveUp = 12f;
            b.SatisfactionWhenNothingToDo = 0.4f;
            b.SatisfactionPerCrowding = 0.05f;
            b.CrowdingThreshold = 12;
            b.SatisfactionOnExpensive = 6f;

            b.ScoreRevenueWeight = 1.0f;
            b.ScoreVisitorWeight = 120f;
            b.ScoreSatisfactionWeight = 4000f;
            b.ScorePeakWeight = 200f;
            b.ScoreVarietyWeight = 15000f;

            b.BuildRiseDuration = 0.85f;
            b.BuildStagger = 0.12f;

            b.VisitorSimplifyDistance = 45f;
            b.VisitorThinkBuckets = 12;

            Touch(b);
            return b;
        }

        /// <summary>
        /// §8 の時間帯変化。X = 祭りの進行度 0〜1（17:00→22:00）、Y = 1秒あたりの来場人数。
        /// 17:00 まばら → 18:30 夕方の人出 → 20:00 ピーク → 21:00 花火時間に少し戻り → 22:00 打ち止め。
        /// </summary>
        private static AnimationCurve BuildArrivalCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0.00f, 1.2f),   // 17:00 まだ明るい。準備中の空気
                new Keyframe(0.10f, 2.4f),   // 17:30
                new Keyframe(0.20f, 4.2f),   // 18:00 仕事帰り・夕飯どき
                new Keyframe(0.30f, 5.6f),   // 18:30
                new Keyframe(0.40f, 7.2f),   // 19:00 日が落ちて提灯が主役に
                new Keyframe(0.50f, 8.6f),   // 19:30
                new Keyframe(0.60f, 9.6f),   // 20:00 ピーク (§8)
                new Keyframe(0.70f, 7.4f),   // 20:30 ピークアウト
                new Keyframe(0.80f, 4.6f),   // 21:00 花火目当ての駆け込み
                new Keyframe(0.90f, 1.6f),   // 21:30
                new Keyframe(1.00f, 0.0f));  // 22:00 終了

            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);

            return curve;
        }
        // 各カテゴリの生成 (BuildAllStalls / BuildFacilities / BuildDecorations /
        // BuildEvents / BuildArchetypes) は同じ partial クラスの別ファイルにある (§66 1ファイル1責務)。
        //   DataAssetGenerator.FoodStalls.cs
        //   DataAssetGenerator.GameStalls.cs
        //   DataAssetGenerator.Props.cs      … 設備・装飾・イベント
        //   DataAssetGenerator.Visitors.cs   … NPCタイプ
    }
}
