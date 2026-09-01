using System;
using Matsuri.Data;
using UnityEngine;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場者の状態機械 (§28 / §34)。
    ///
    /// Entering → Browsing →┬→ MovingToStall → Queueing → BeingServed → Enjoying →┐
    ///                      └→ MovingToAmenity → (Resting | Dancing | Praying) ────┤
    ///                                                                            │
    ///            Browsing ←──────────────────────────────────────────────────────┘
    ///                      → (WatchingFireworks) → Leaving → Gone
    ///
    /// 屋台で買うだけでなく、盆踊り場・休憩所・神社に「居る」ことでも満足度が上がる (§34)。
    /// </summary>
    public enum VisitorStateKind
    {
        /// <summary>入場中。入り口から会場に入ってくる。</summary>
        Entering,
        /// <summary>ぶらぶら歩きながら次に行く屋台を探している。</summary>
        Browsing,
        /// <summary>目的の屋台へ向かって歩いている。</summary>
        MovingToStall,
        /// <summary>盆踊り場・休憩所・神社などの施設へ向かって歩いている (§34)。</summary>
        MovingToAmenity,
        /// <summary>行列に並んでいる (§30)。</summary>
        Queueing,
        /// <summary>接客を受けている。</summary>
        BeingServed,
        /// <summary>買った物を食べている・遊んでいる。</summary>
        Enjoying,
        /// <summary>ベンチ・休憩所で休憩している (§20 設備の効果)。</summary>
        Resting,
        /// <summary>盆踊り場で踊っている (§22 / §34)。</summary>
        Dancing,
        /// <summary>神社・手水舎で参拝している (§34)。</summary>
        Praying,
        /// <summary>花火を見上げている (§22)。</summary>
        WatchingFireworks,
        /// <summary>出口へ向かって帰っている。</summary>
        Leaving,
        /// <summary>退場済み。プールに返せる状態。</summary>
        Gone
    }

    /// <summary>帰った理由 (§28 の帰宅条件)。結果画面やデバッグ表示に使う。</summary>
    public enum VisitorLeaveReason
    {
        None,
        /// <summary>目標軒数を回り終えて満足した。</summary>
        Satisfied,
        /// <summary>体力切れ。</summary>
        Tired,
        /// <summary>所持金が足りなくなった。</summary>
        OutOfMoney,
        /// <summary>つまらなかった（満足度が低すぎる）。</summary>
        Unsatisfied,
        /// <summary>22:00 閉場。</summary>
        ClosingTime,
        /// <summary>祭りの終了処理で強制的に帰された。</summary>
        ForcedHome
    }

    /// <summary>
    /// 来場者1人ぶんの数値状態 (§26)。
    /// MonoBehaviour ではなく構造体に閉じておくことで、
    /// 意思決定ロジック (DestinationScorer / VisitorBrain) を Unity 非依存でテストできる。
    /// </summary>
    [Serializable]
    public struct VisitorState
    {
        [Header("状態機械")]
        public VisitorStateKind Kind;
        public VisitorLeaveReason LeaveReason;

        [Header("パラメータ (§26)")]
        // 所持金。これを超える買い物はしない。
        public float Money;
        // 空腹度 0-100。高いほど食べ物を求める。
        public float Hunger;
        // 遊びたさ 0-100。
        public float Fun;
        // 体力 0-100。0 になると帰る。
        public float Energy;
        // 我慢強さ。行列で何秒待てるかの目安 (§34)。
        public float Patience;
        // 満足度 0-100 (§34)。祭り全体のスコアに直結する (§35)。
        public float Satisfaction;

        [Header("個体差")]
        public float WalkingSpeed;
        public float PriceSensitivity;
        public float FireworksInterest;
        public float BodyHeight;
        // 何軒回ったら帰るか。
        public int TargetVisitCount;
        // 実際に買った回数。
        public int VisitCount;
        public uint Seed;

        [Header("タイマー（すべて実時間の秒）")]
        public float StateTime;
        public float LifeTime;
        public float QueueWaitTime;
        public float ServeTimer;
        public float EnjoyTimer;
        public float RestTimer;
        // 施設に滞在している時間。休憩・踊り・参拝で共通に使う (§34)。
        public float AmenityTimer;
        public float FireworksTimer;
        // 花火を見上げている残り時間。状態を変えずに首だけ上げる。
        public float LookUpTimer;

        /// <summary>空腹の切実さ 0-1。</summary>
        public float FoodNeed01 => Mathf.Clamp01(Hunger * 0.01f);

        /// <summary>遊びたさ 0-1。</summary>
        public float GameNeed01 => Mathf.Clamp01(Fun * 0.01f);

        /// <summary>まだ会場に居る（プールに返してはいけない）か。</summary>
        public bool IsAlive => Kind != VisitorStateKind.Gone;

        /// <summary>足を止めている状態か。歩行アニメの切り替えに使う (§79)。</summary>
        public bool IsStationary =>
            Kind == VisitorStateKind.BeingServed ||
            Kind == VisitorStateKind.Enjoying ||
            Kind == VisitorStateKind.Resting ||
            Kind == VisitorStateKind.Dancing ||
            Kind == VisitorStateKind.Praying ||
            Kind == VisitorStateKind.WatchingFireworks ||
            Kind == VisitorStateKind.Gone;

        /// <summary>
        /// アーキタイプのレンジから個体の値を引く (§27「ランダム性も追加する」)。
        /// 歩速・体格・気分・目的の軒数がここでばらける。
        /// </summary>
        public void Roll(VisitorArchetype a, ref MRandom rng)
        {
            ResetTimers();
            Kind = VisitorStateKind.Entering;
            LeaveReason = VisitorLeaveReason.None;
            VisitCount = 0;

            if (a == null)
            {
                // アーキタイプが無い異常系でも「それらしい人」にしておく。
                Money = 2000f; Hunger = 55f; Fun = 60f; Energy = 80f; Patience = 45f;
                WalkingSpeed = 1.3f; PriceSensitivity = 1f; FireworksInterest = 60f;
                BodyHeight = 1.65f; TargetVisitCount = 3; Satisfaction = 55f;
                return;
            }

            Money            = a.Money.Sample(ref rng);
            Hunger           = Mathf.Clamp(a.Hunger.Sample(ref rng), 0f, 100f);
            Fun              = Mathf.Clamp(a.Fun.Sample(ref rng), 0f, 100f);
            Energy           = Mathf.Clamp(a.Energy.Sample(ref rng), 1f, 100f);
            Patience         = Mathf.Max(5f, a.Patience.Sample(ref rng));
            WalkingSpeed     = Mathf.Max(0.4f, a.WalkingSpeed.Sample(ref rng));
            PriceSensitivity = Mathf.Max(0.05f, a.PriceSensitivity.Sample(ref rng));
            BodyHeight       = Mathf.Max(0.6f, a.BodyHeight.Sample(ref rng));

            // 花火への興味も個体でばらす。同じアーキタイプでも全員が同じ反応をしないように。
            FireworksInterest = Mathf.Clamp(a.FireworksInterest + rng.NextFloat(-18f, 18f), 0f, 100f);

            int lo = Mathf.Min(a.TargetVisitCount.x, a.TargetVisitCount.y);
            int hi = Mathf.Max(a.TargetVisitCount.x, a.TargetVisitCount.y);
            TargetVisitCount = Mathf.Max(1, rng.NextInt(lo, hi + 1));

            // 「今日の機嫌」。全員が同じ満足度から始まると祭りが単調に見える。
            Satisfaction = Mathf.Clamp(55f + rng.NextFloat(-10f, 14f), 0f, 100f);
        }

        public void ResetTimers()
        {
            StateTime = 0f;
            LifeTime = 0f;
            QueueWaitTime = 0f;
            ServeTimer = 0f;
            EnjoyTimer = 0f;
            RestTimer = 0f;
            AmenityTimer = 0f;
            FireworksTimer = 0f;
            LookUpTimer = 0f;
        }

        /// <summary>プールに返すときに完全初期化する (§57 プール返却時に完全リセット)。</summary>
        public void Clear()
        {
            this = default;
            Kind = VisitorStateKind.Gone;
        }

        /// <summary>すべての 0-100 パラメータを範囲内に収める。</summary>
        public void ClampAll()
        {
            Hunger       = Mathf.Clamp(Hunger, 0f, 100f);
            Fun          = Mathf.Clamp(Fun, 0f, 100f);
            Energy       = Mathf.Clamp(Energy, 0f, 100f);
            Satisfaction = Mathf.Clamp(Satisfaction, 0f, 100f);
            if (Money < 0f) Money = 0f;
        }
    }

    /// <summary>状態の日本語表記。デバッグ表示や §38 の来場者視点カメラの吹き出しに使う。</summary>
    public static class VisitorStateLabel
    {
        public static string ToJapanese(VisitorStateKind kind)
        {
            switch (kind)
            {
                case VisitorStateKind.Entering:         return "入場中";
                case VisitorStateKind.Browsing:         return "屋台を探している";
                case VisitorStateKind.MovingToStall:    return "屋台へ向かっている";
                case VisitorStateKind.MovingToAmenity:  return "休みどころへ向かっている";
                case VisitorStateKind.Queueing:         return "並んでいる";
                case VisitorStateKind.BeingServed:      return "買っている";
                case VisitorStateKind.Enjoying:         return "楽しんでいる";
                case VisitorStateKind.Resting:          return "休憩中";
                case VisitorStateKind.Dancing:          return "踊っている";
                case VisitorStateKind.Praying:          return "お参りしている";
                case VisitorStateKind.WatchingFireworks:return "花火を見ている";
                case VisitorStateKind.Leaving:          return "帰り道";
                default:                                return "退場済み";
            }
        }

        public static string ToJapanese(VisitorLeaveReason reason)
        {
            switch (reason)
            {
                case VisitorLeaveReason.Satisfied:   return "満足して帰った";
                case VisitorLeaveReason.Tired:       return "疲れて帰った";
                case VisitorLeaveReason.OutOfMoney:  return "お金が無くなって帰った";
                case VisitorLeaveReason.Unsatisfied: return "つまらなくて帰った";
                case VisitorLeaveReason.ClosingTime: return "閉場時刻で帰った";
                case VisitorLeaveReason.ForcedHome:  return "祭りが終わって帰った";
                default:                             return "まだ帰っていない";
            }
        }
    }
}
