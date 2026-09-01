using UnityEngine;

namespace Matsuri.Data
{
    /// <summary>
    /// 仕様書 §31「数値はゲームバランス調整可能なデータとして管理する。ハードコードしない。」
    /// ゲームバランスに関わる係数はすべてここに集約する。
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Matsuri/Balance Config", order = 10)]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("経営 (§31)")]
        [Tooltip("初期予算。")]
        public long InitialBudget = 1000000;

        [Tooltip("FREE MODE の予算。負なら無制限扱い。")]
        public long FreeModeBudget = -1;

        [Header("時間 (§7)")]
        [Tooltip("現実の1秒がゲーム内の何分に相当するか。\n" +
                 "2.5 なら 17:00〜22:00 の5時間が実時間2分で終わる。\n" +
                 "接客時間・来場ペース・歩行速度はこの値から自動で換算されるので、\n" +
                 "ここを変えるだけで祭りの長さを変えられる。")]
        public float MinutesPerRealSecond = 2.5f;

        [Tooltip("開始時刻（分）。17:00 = 1020。")]
        public int StartMinutes = 17 * 60;

        [Tooltip("終了時刻（分）。22:00 = 1320。")]
        public int EndMinutes = 22 * 60;

        [Header("来場者 (§8 / §56)")]
        [Tooltip("同時に存在できるNPCの上限。目標は300人、最終目標1000人 (§56)。")]
        public int MaxConcurrentVisitors = 300;

        [Tooltip("その祭りに来る総来場者数の上限。")]
        public int MaxTotalVisitors = 4000;

        [Tooltip("時刻ごとの来場ペース。X=祭りの進行度0〜1、Y=**ゲーム内1分あたり**の来場人数。\n" +
                 "実時間に換算するときは MinutesPerRealSecond を掛ける。\n" +
                 "こうしておくと祭りの長さを変えても総来場者数が変わらない。§8のピーク20:00に山を作る。")]
        public AnimationCurve ArrivalCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.5f),   // 17:00 まだ少ない
            new Keyframe(0.20f, 4.0f),   // 18:00 増え始める
            new Keyframe(0.40f, 7.0f),   // 19:00 夜
            new Keyframe(0.60f, 9.0f),   // 20:00 ピーク
            new Keyframe(0.80f, 5.0f),   // 21:00 花火
            new Keyframe(1.00f, 0.0f));  // 22:00 終了

        [Tooltip("祭りの魅力（屋台の数・種類・装飾）が来場者数に効く強さ。")]
        public float AttractionToArrivalScale = 0.06f;

        [Tooltip("屋台が1軒も無いときの来場倍率。")]
        public float EmptyFestivalArrivalMultiplier = 0.15f;

        [Header("NPCの目的地決定 (§29)")]
        [Tooltip("Score = Preference*w1 + Need*w2 - Distance*w3 - Queue*w4 - Price*w5 + Popularity*w6")]
        public float WeightPreference = 1.0f;
        public float WeightNeed        = 1.2f;
        public float WeightDistance    = 0.55f;
        public float WeightQueue       = 2.4f;
        public float WeightPrice       = 1.1f;
        public float WeightPopularity  = 0.5f;

        [Tooltip("この距離(m)を1単位として距離ペナルティを計算する。")]
        public float DistanceUnit = 10f;

        [Tooltip("スコアに乗せるランダムのゆらぎ幅。全員が同じ屋台に行かないようにする。")]
        public float DecisionNoise = 12f;

        [Header("価格 (§32)")]
        [Tooltip("この価格を基準に「高い／安い」を判定する。")]
        public int ReferencePrice = 500;

        [Tooltip("価格が基準の何倍で買う気が半減するか。")]
        public float PriceHalfPoint = 2.2f;

        [Header("人気度 (§33)")]
        [Tooltip("行列が1人増えるごとに人気度がどれだけ上がるか（にぎわい効果）。")]
        public float PopularityPerQueuer = 1.5f;

        [Tooltip("人気度が戻る速さ（毎秒）。")]
        public float PopularityDecay = 2f;

        [Tooltip("人気度の上限。")]
        public float MaxPopularity = 100f;

        [Tooltip("1回売れるごとに上がる人気度。")]
        public float PopularityPerSale = 0.8f;

        [Header("満足度 (§34)")]
        [Tooltip("行列で1秒待つごとに減る満足度。")]
        public float SatisfactionPerWaitSecond = 0.6f;

        [Tooltip("我慢の限界を超えたときに一気に減る満足度。")]
        public float SatisfactionOnGiveUp = 12f;

        [Tooltip("欲しい屋台が見つからないときに毎秒減る満足度。")]
        public float SatisfactionWhenNothingToDo = 0.4f;

        [Tooltip("周囲の混雑（半径5m以内の人数）1人あたり毎秒減る満足度。")]
        public float SatisfactionPerCrowding = 0.05f;

        [Tooltip("混雑と見なす人数のしきい値。")]
        public int CrowdingThreshold = 12;

        [Tooltip("価格が高すぎたときに減る満足度。")]
        public float SatisfactionOnExpensive = 6f;

        [Header("スコア (§35)")]
        public float ScoreRevenueWeight       = 1.0f;
        public float ScoreVisitorWeight       = 120f;
        public float ScoreSatisfactionWeight  = 4000f;
        public float ScorePeakWeight          = 200f;
        public float ScoreVarietyWeight       = 15000f;

        [Header("建設演出 (§39)")]
        [Tooltip("1つのオブジェクトがせり上がる時間（秒）。")]
        public float BuildRiseDuration = 0.85f;

        [Tooltip("複数オブジェクトを順番に建てるときの間隔（秒）。")]
        public float BuildStagger = 0.12f;

        [Header("演出しきい値")]
        [Header("移動")]
        [Tooltip("NPCの歩行速度にかける倍率。\n" +
                 "実際に使われる速さは、これに MinutesPerRealSecond を掛けた値。\n" +
                 "祭りが圧縮されている (§7) ぶんだけ歩調も速くしないと、\n" +
                 "会場を横切るだけで祭りが終わってしまう。")]
        public float VisitorSpeedMultiplier = 1.35f;

        /// <summary>圧縮された祭り時間に合わせた、実際の歩行速度の倍率。</summary>
        public float EffectiveVisitorSpeedScale =>
            Mathf.Max(0.1f, VisitorSpeedMultiplier) * Mathf.Max(0.1f, MinutesPerRealSecond);

        /// <summary>
        /// 屋台の接客時間 (§30) はゲーム内の「分」で書かれている。
        /// たこ焼き8分・かき氷5分。それを実時間の秒に直す。
        /// </summary>
        public float ToRealSeconds(float gameMinutes)
            => Mathf.Max(0.05f, gameMinutes / Mathf.Max(0.0001f, MinutesPerRealSecond));

        [Tooltip("この距離を超えたNPCは簡易更新にする (§57)。")]
        public float VisitorSimplifyDistance = 45f;

        [Tooltip("NPCの思考を何フレームに分散するか (§57)。")]
        public int VisitorThinkBuckets = 12;

        [Tooltip("同時に NavMeshAgent を持てるNPCの数 (§57)。\n" +
                 "カメラに近い順にこの数だけが経路探索を行い、残りは簡易移動になる。\n" +
                 "1000人を出すときはここが効く。")]
        public int MaxActiveNavAgents = 220;

        [Header("施設 (§34)")]
        [Tooltip("盆踊り場・休憩所・神社などの滞在で得られる満足度の全体倍率。")]
        public float AmenitySatisfactionScale = 1f;

        [Tooltip("施設を選ぶスコアの重み。屋台のスコアと同じ尺度で比べる。")]
        public float WeightAmenityNeed = 1.3f;

        /// <summary>祭りの長さ（ゲーム内分）。</summary>
        public int SpanMinutes => Mathf.Max(1, EndMinutes - StartMinutes);

        /// <summary>祭り全体の実時間（秒）。既定なら 300秒 = 5分 (§7)。</summary>
        public float RealSecondsTotal => SpanMinutes / Mathf.Max(0.0001f, MinutesPerRealSecond);
    }
}
