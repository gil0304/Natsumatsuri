using Matsuri.Data;
using Matsuri.Festival;
using UnityEngine;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場者から見た「その施設に行きたい度」の入力 (§34)。
    /// VisitorAgent を経由せず値だけで評価できるので、EditMode テストで直接検証できる。
    /// </summary>
    public struct AmenityDesire
    {
        /// <summary>体力 0-100。低いほど休みたい。</summary>
        public float Energy;

        /// <summary>遊びたさ 0-100。高いほど踊りたい（＝まだ満たされていない）。</summary>
        public float Fun;

        /// <summary>満足度 0-100。低いほど「何かで持ち直したい」。</summary>
        public float Satisfaction;

        /// <summary>我慢強さ。混んでいる施設をどれだけ許せるか。</summary>
        public float Patience;

        /// <summary>花火への興味 0-100。祭りそのものの好きさとして使う。</summary>
        public float FireworksInterest;

        public AmenityDesire(float energy, float fun, float satisfaction, float patience, float fireworksInterest)
        {
            Energy = energy;
            Fun = fun;
            Satisfaction = satisfaction;
            Patience = patience;
            FireworksInterest = fireworksInterest;
        }

        /// <summary>実際の来場者から作る。</summary>
        public static AmenityDesire From(VisitorAgent v)
        {
            if (v == null) return new AmenityDesire(100f, 0f, 100f, 50f, 0f);
            return new AmenityDesire(v.Energy, v.Fun, v.Satisfaction, v.Patience, v.FireworksInterest);
        }
    }

    /// <summary>施設側の評価入力 (§34)。</summary>
    public struct AmenityCandidate
    {
        public FacilityEffect Effect;

        /// <summary>来場者からの平面距離 (m)。</summary>
        public float Distance;

        /// <summary>空いている立ち位置の数。0 なら選べない。</summary>
        public int FreeSlots;

        /// <summary>収容人数。0 以下なら「混雑を気にしない施設」として扱う。</summary>
        public int Capacity;

        public AmenityCandidate(FacilityEffect effect, float distance, int freeSlots, int capacity)
        {
            Effect = effect;
            Distance = distance;
            FreeSlots = freeSlots;
            Capacity = capacity;
        }

        /// <summary>実際の施設から作る。</summary>
        public static AmenityCandidate From(Facility f, Vector3 from)
        {
            if (f == null) return new AmenityCandidate(FacilityEffect.Cleanliness, 0f, 0, 0);

            Vector3 d = f.transform.position - from;
            d.y = 0f;
            return new AmenityCandidate(f.Effect, d.magnitude, f.FreeSlots, f.Capacity);
        }
    }

    /// <summary>
    /// 仕様書 §34「満足度を上げる施設」を、§29 の目的地決定と**同じ尺度**で採点する。
    ///
    ///   Score = Need    * WeightAmenityNeed     （施設ごとの「いま欲しい度」0-100）
    ///         + Appeal                          （施設そのものの華やかさ 0-100）
    ///         - Distance * WeightDistance       （DestinationScorer と同じ式）
    ///         - Crowd    * WeightQueue          （混んでいるほど行きたくない。我慢強さで割る）
    ///         + noise                           （全員が同じ施設に殺到しないためのゆらぎ）
    ///
    /// 距離ペナルティの係数は <see cref="DestinationScorer.DistancePenaltyScale"/> と共有する。
    /// これにより VisitorBrain は「次は屋台か、施設か」を1つのスコア表で比べられる。
    ///
    /// LLM は一切使わない (§25)。Unity のシーンに依存しないので EditMode テストで直接叩ける。
    /// </summary>
    public static class AmenityScorer
    {
        /// <summary>混雑ペナルティのスケール。満員直前でこの点数ぶん下がる。</summary>
        public const float CrowdPenaltyScale = 25f;

        /// <summary>我慢強さの基準値。DestinationScorer と揃える。</summary>
        public const float PatienceReference = DestinationScorer.PatienceReference;

        /// <summary>滞在できる施設か。ゴミ箱・案内板・門は「行き先」にならない。</summary>
        public static bool IsAmenityEffect(FacilityEffect e)
        {
            switch (e)
            {
                case FacilityEffect.Rest:
                case FacilityEffect.Dance:
                case FacilityEffect.Worship:
                case FacilityEffect.Purify:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 施設そのものの華やかさ 0-100。
        /// やぐらは遠くからでも目に入るので高く、ベンチは「用があるときだけ」なので低い (§34)。
        /// </summary>
        public static float BaseAppeal(FacilityEffect e)
        {
            switch (e)
            {
                // 屋台のスコアには Preference(最大100) と Popularity が常に乗るため、
                // 屋台は何もしなくても 80〜120 点の下駄を履いている。
                // 施設が「行き先の候補」として土俵に上がるには、同じ桁の魅力が要る。
                // ここを低くしすぎると、盆踊り場を建てても誰も踊らない。
                case FacilityEffect.Dance:   return 78f;   // 太鼓と提灯。祭りの中心
                case FacilityEffect.Worship: return 66f;   // 参道があれば足が向く
                case FacilityEffect.Purify:  return 26f;   // ついでに寄る程度
                default:                     return 46f;   // 休憩所・ベンチ
            }
        }

        /// <summary>「祭り好き」がその施設に上乗せする点数の係数。花火への興味を代用にする。</summary>
        /// <summary>
        /// 体力切れの切迫感。1 なら線形とほぼ同じ、大きいほど「限界なら必ず休む」に寄る。
        /// </summary>
        public const float ExhaustionUrgency = 1.9f;

        public static float FestivalLoveScale(FacilityEffect e)
        {
            switch (e)
            {
                case FacilityEffect.Dance:   return 0.15f;
                case FacilityEffect.Worship: return 0.10f;
                case FacilityEffect.Purify:  return 0.05f;
                default:                     return 0f;
            }
        }

        /// <summary>
        /// いまその施設をどれだけ欲しているか 0-100。
        /// 休憩所は疲れているほど、盆踊り場は遊びたいほど、神社は満足度が低いほど上がる。
        /// </summary>
        public static float Need(in AmenityDesire d, FacilityEffect e)
        {
            switch (e)
            {
                // 疲れているほど休みたい。
                case FacilityEffect.Rest:
                {
                    // 体力の不足ぶん。ただし線形だと、盆踊り場の華やかさに負けて
                    // 倒れかけの客が踊りに行ってしまう。
                    // 残り体力が少ないほど急激に効くカーブにして、
                    // 「もう歩けない」人は必ず休憩所を選ぶようにする (§34 疲労)。
                    float lack = Mathf.Clamp(100f - d.Energy, 0f, 100f);
                    float urgency = (lack * lack / 100f) * ExhaustionUrgency;
                    return Mathf.Max(lack, urgency);
                }

                // 遊びたさが満たされていないほど踊りたい。
                case FacilityEffect.Dance:
                    return Mathf.Clamp(d.Fun, 0f, 100f);

                // 満足度が低いほど「お参りして持ち直したい」。
                case FacilityEffect.Worship:
                    return Mathf.Clamp(100f - d.Satisfaction, 0f, 100f);

                // 手水舎はついでの立ち寄り。欲求としては半分ぶんしか効かない。
                case FacilityEffect.Purify:
                    return Mathf.Clamp((100f - d.Satisfaction) * 0.5f, 0f, 100f);

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// コントラクトのシグネチャ。
        /// 空きが無い施設・滞在できない設備は float.NegativeInfinity を返し、必ず選ばれないようにする。
        /// </summary>
        public static float Score(in AmenityDesire desire, in AmenityCandidate cand, BalanceConfig b, float noise)
        {
            if (b == null) return float.NegativeInfinity;

            // --- 選択肢から外れる条件 ---
            if (!IsAmenityEffect(cand.Effect)) return float.NegativeInfinity;   // 滞在できない設備
            if (cand.FreeSlots <= 0) return float.NegativeInfinity;             // 空きが無い

            // --- 欲求 (0-100) ---
            float needTerm = Need(in desire, cand.Effect) * b.WeightAmenityNeed;

            // --- 施設そのものの魅力 ---
            float love = Mathf.Clamp(desire.FireworksInterest, 0f, 100f) * FestivalLoveScale(cand.Effect);
            float appealTerm = BaseAppeal(cand.Effect) + love;

            // --- 距離。屋台とまったく同じ式で計算する ---
            float unit = Mathf.Max(0.01f, b.DistanceUnit);
            float distance = Mathf.Max(0f, cand.Distance);
            float distanceTerm = (distance / unit) * DestinationScorer.DistancePenaltyScale * b.WeightDistance;

            // --- 混雑。我慢強い人ほど気にしない (§34) ---
            float patienceFactor = Mathf.Max(0.25f, Mathf.Clamp(desire.Patience, 1f, 200f) / PatienceReference);
            float occupancy = 0f;
            if (cand.Capacity > 0)
                occupancy = Mathf.Clamp01(1f - cand.FreeSlots / (float)cand.Capacity);
            float crowdTerm = (occupancy / patienceFactor) * CrowdPenaltyScale * b.WeightQueue;

            return needTerm + appealTerm - distanceTerm - crowdTerm + noise;
        }

        /// <summary>
        /// 実体版。VisitorBrain から呼ぶ。
        /// 施設が滞在の効き目を持っていない（StayMinutes が 0）ときも選ばれない。
        /// </summary>
        public static float Score(VisitorAgent v, Facility f, BalanceConfig b, float noise)
        {
            if (v == null || f == null || b == null) return float.NegativeInfinity;
            if (!f.IsPlaceToStay) return float.NegativeInfinity;

            var desire = AmenityDesire.From(v);
            var cand = AmenityCandidate.From(f, v.Position);
            return Score(in desire, in cand, b, noise);
        }
    }
}
