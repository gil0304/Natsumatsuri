using System.Collections.Generic;
using Matsuri.Data;
using Matsuri.Festival;
using Matsuri.Stalls;
using UnityEngine;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場者の「考える部分」 (§28 / §29 / §34)。
    ///
    /// **LLM は使わない (§25)**。すべて BalanceConfig の数値から決まる純粋なゲームロジック。
    /// VisitorAgent（見た目と移動）から意思決定を切り離してあるので、
    /// ここだけを EditMode テストで検証できる。
    /// </summary>
    public static class VisitorBrain
    {
        /// <summary>これを下回ったら「つまらない」と判断して帰る。</summary>
        public const float GiveUpSatisfaction = 12f;

        /// <summary>帰宅判定を始めるまでの最低滞在秒数。来た瞬間に帰らせない。</summary>
        public const float MinStaySeconds = 20f;

        /// <summary>この体力を下回るとベンチを探す。</summary>
        public const float RestEnergyThreshold = 26f;

        /// <summary>閉場の何分前から帰り始めるか。</summary>
        public const float LeaveBeforeCloseMinutes = 6f;

        /// <summary>目的地を乗り換えるのに必要なスコア差。ふらふら迷わせないためのヒステリシス。</summary>
        public const float SwitchTargetMargin = 18f;

        /// <summary>短い待ち時間とみなす秒数。ここを下回ると満足度にボーナス (§34)。</summary>
        public const float ShortWaitSeconds = 6f;

        /// <summary>施設 (§34) を探す半径。これより遠い盆踊り場や神社は目に入らない。</summary>
        public const float AmenitySearchRadius = 55f;

        /// <summary>
        /// 屋台をやめて施設へ向かうのに必要なスコア差。
        /// 屋台と施設は同じ尺度 (AmenityScorer) で採点しているので、
        /// ここは「わざわざ寄り道するか」のためだけの余裕分。
        /// </summary>
        public const float AmenityPreferenceMargin = 6f;

        /// <summary>施設に着いたとみなす距離 (m)。立ち位置の目の前。</summary>
        public const float AmenityArriveDistance = 1.7f;   // 立ち位置は混み合うので少し緩める

        // ------------------------------------------------------------------
        // 目的地選択
        // ------------------------------------------------------------------

        /// <summary>
        /// §29 のスコアが最大の屋台を選ぶ。
        /// 候補がひとつも無ければ null（＝「行きたい屋台が無い」＝満足度が下がる状況）。
        /// </summary>
        public static Stall ChooseStall(VisitorAgent v, IReadOnlyList<Stall> stalls, BalanceConfig b,
                                        ref MRandom rng, Stall avoid, out float bestScore)
        {
            bestScore = float.NegativeInfinity;
            Stall best = null;
            if (v == null || stalls == null || b == null) return null;

            float noiseRange = Mathf.Max(0f, b.DecisionNoise);

            for (int i = 0; i < stalls.Count; i++)
            {
                Stall s = stalls[i];
                if (s == null) continue;
                if (s == avoid) continue;                 // 直前に諦めた／買ったばかりの屋台は外す

                float noise = noiseRange > 0f ? rng.NextFloat(-noiseRange, noiseRange) : 0f;
                float score = DestinationScorer.Score(v, s, b, noise);
                if (float.IsNegativeInfinity(score)) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>いま所持金で買える屋台がひとつでもあるか。帰宅理由の判定に使う。</summary>
        public static bool AnyAffordable(VisitorAgent v, IReadOnlyList<Stall> stalls)
        {
            if (v == null || stalls == null) return false;
            for (int i = 0; i < stalls.Count; i++)
            {
                Stall s = stalls[i];
                if (s == null || !s.IsOpen) continue;
                if (s.Price <= v.Money) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 施設の選択 (§34「満足度を上げる施設」)
        // ------------------------------------------------------------------

        /// <summary>
        /// 盆踊り場・休憩所・神社・手水舎のうち、いま一番行きたい所を選ぶ。
        /// 空きが無い施設・遠すぎる施設は候補から外れる。無ければ null。
        ///
        /// 返すスコアは <see cref="ChooseStall"/> と同じ尺度なので、そのまま比べてよい。
        /// </summary>
        public static Facility ChooseAmenity(VisitorAgent v, BalanceConfig b,
                                             ref MRandom rng, Facility avoid, out float bestScore)
        {
            bestScore = float.NegativeInfinity;
            Facility best = null;
            if (v == null || b == null) return null;

            var all = AmenityRegistry.All;
            if (all.Count == 0) return null;

            float noiseRange = Mathf.Max(0f, b.DecisionNoise);
            float maxSq = AmenitySearchRadius * AmenitySearchRadius;
            Vector3 from = v.Position;

            for (int i = 0; i < all.Count; i++)
            {
                Facility f = all[i];
                if (f == null) continue;
                if (f == avoid) continue;                 // 直前まで居た施設には戻らない
                if (!f.IsPlaceToStay) continue;           // ゴミ箱・案内板・門は行き先にならない
                if (!f.HasFreeSlot) continue;

                Vector3 d = f.transform.position - from;
                d.y = 0f;
                if (d.sqrMagnitude > maxSq) continue;

                float noise = noiseRange > 0f ? rng.NextFloat(-noiseRange, noiseRange) : 0f;
                float score = AmenityScorer.Score(v, f, b, noise);
                if (float.IsNegativeInfinity(score)) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = f;
                }
            }
            return best;
        }

        /// <summary>
        /// 施設に滞在すべき残り時間（実時間の秒）。
        /// Facility.StayMinutes は**ゲーム内の分**なので、BalanceConfig で実秒に換算する (§7)。
        /// </summary>
        public static float AmenityStaySeconds(Facility f, BalanceConfig b)
        {
            if (f == null) return 0f;
            float minutes = Mathf.Max(0f, f.StayMinutes);
            if (minutes <= 0f) return 0f;
            return b != null ? b.ToRealSeconds(minutes) : minutes;
        }

        // ------------------------------------------------------------------
        // 帰宅判定 (§28)
        // ------------------------------------------------------------------

        /// <summary>
        /// 帰る理由があれば返す。無ければ None。
        /// 条件: 体力切れ / 目標軒数達成 / 満足度が低すぎる / 22:00 / 所持金不足。
        /// </summary>
        public static VisitorLeaveReason EvaluateGoHome(VisitorAgent v, BalanceConfig b,
                                                        float minutesOfDay, bool hasCandidate, bool anyAffordable)
        {
            if (v == null) return VisitorLeaveReason.None;

            // 22:00（閉場）が近い。これだけは滞在時間に関係なく効く (§8)。
            float endMinutes = b != null ? b.EndMinutes : 22f * 60f;
            if (minutesOfDay >= endMinutes - LeaveBeforeCloseMinutes)
                return VisitorLeaveReason.ClosingTime;

            if (v.Energy <= 0.5f) return VisitorLeaveReason.Tired;

            if (v.LifeTime < MinStaySeconds) return VisitorLeaveReason.None;

            if (v.VisitCount >= v.TargetVisitCount) return VisitorLeaveReason.Satisfied;
            if (v.Satisfaction <= GiveUpSatisfaction) return VisitorLeaveReason.Unsatisfied;

            // 買える物が何も無い。お金が尽きたのか、そもそも屋台が無いのかを区別する。
            if (!anyAffordable && v.LifeTime > MinStaySeconds * 2f)
                return VisitorLeaveReason.OutOfMoney;
            if (!hasCandidate && v.LifeTime > MinStaySeconds * 3f)
                return VisitorLeaveReason.Unsatisfied;

            return VisitorLeaveReason.None;
        }

        // ------------------------------------------------------------------
        // 行列 (§30 / §34)
        // ------------------------------------------------------------------

        /// <summary>
        /// この人がこの屋台の行列で我慢できる秒数。
        /// 我慢強さ (Patience) が基本で、その屋台が好きなほど長く待てる。
        /// </summary>
        public static float QueuePatienceSeconds(VisitorAgent v, Stall s)
        {
            if (v == null) return 20f;
            float seconds = Mathf.Max(6f, v.Patience);

            var data = s != null ? s.Data : null;
            if (data != null && v.Archetype != null)
            {
                float pref = v.Archetype.GetPreference(data.Id, data.Category);   // 0-100
                seconds *= Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01(pref * 0.01f));
            }
            // 空腹／遊びたさが強いほど粘る。
            float urge = data != null && data.Category == StallCategory.Food
                ? v.Hunger * 0.01f : v.Fun * 0.01f;
            seconds *= Mathf.Lerp(0.85f, 1.35f, Mathf.Clamp01(urge));

            return seconds;
        }

        // ------------------------------------------------------------------
        // 満足度 (§34)
        // ------------------------------------------------------------------

        /// <summary>
        /// 購入が成立したときの満足度の増減 (§34)。
        /// 上がる: 好きな屋台 / 短い待ち / 安い。
        /// 下がる: 高すぎる。
        /// </summary>
        public static float ServeSatisfaction(VisitorAgent v, Stall s, int price, BalanceConfig b, float waitedSeconds)
        {
            var data = s != null ? s.Data : null;
            if (data == null) return 0f;

            float value = data.SatisfactionValue;

            // 好きな屋台ほど嬉しい。
            if (v != null && v.Archetype != null)
            {
                float pref = v.Archetype.GetPreference(data.Id, data.Category);   // 0-100
                value += (pref - 50f) * 0.16f;                                    // ±8 程度
            }

            // 短い待ちは気持ちがいい。
            if (waitedSeconds <= ShortWaitSeconds) value += 5f;

            if (b != null)
            {
                float reference = Mathf.Max(1f, b.ReferencePrice);
                float ratio = price / reference;
                float sensitivity = v != null ? Mathf.Max(0.05f, v.PriceSensitivity) : 1f;

                if (ratio >= b.PriceHalfPoint * 0.8f)
                    value -= b.SatisfactionOnExpensive * sensitivity;   // 高すぎた
                else if (ratio <= 0.7f)
                    value += 4f;                                        // 安かった
            }

            return value;
        }

        /// <summary>
        /// 会場の環境から毎秒受ける満足度の増減 (§34)。
        /// 下がる: 混雑 / 疲労。上がる: 装飾の雰囲気 / 花火開催中。
        /// </summary>
        public static float AmbientSatisfactionPerSecond(VisitorAgent v, BalanceConfig b,
                                                         int crowding, float ambience, bool fireworksActive)
        {
            if (v == null || b == null) return 0f;
            float delta = 0f;

            // 混雑（§34「混雑しすぎ」）
            int threshold = Mathf.Max(1, b.CrowdingThreshold);
            if (crowding > threshold)
                delta -= (crowding - threshold) * b.SatisfactionPerCrowding;

            // 装飾の雰囲気（§34「装飾で満足度が上がる」）
            if (ambience > 0f) delta += ambience * 0.02f;

            // 疲労
            if (v.Energy < 20f) delta -= 0.12f;

            // 花火が上がっている間はそれだけで機嫌がよい。
            if (fireworksActive) delta += 0.05f * (v.FireworksInterest * 0.01f);

            return delta;
        }

        /// <summary>「行きたい屋台が無い」ときに毎秒下がる満足度 (§34)。</summary>
        public static float NothingToDoPerSecond(BalanceConfig b)
            => b != null ? b.SatisfactionWhenNothingToDo : 0.4f;

        // ------------------------------------------------------------------
        // ぶらぶら歩き
        // ------------------------------------------------------------------

        /// <summary>
        /// 目的地が無いときの散歩先。会場の中心付近をうろつかせる。
        /// 直立静止させないための最低限の仕掛け (§79)。
        /// </summary>
        public static Vector3 WanderTarget(Vector3 from, Vector3 center, float radius, ref MRandom rng)
        {
            float angle = rng.NextFloat(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(rng.NextFloat(0.05f, 1f)) * Mathf.Max(2f, radius);
            Vector3 target = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            // あまりに近い散歩先は「その場で足踏み」に見えるので少し押し出す。
            Vector3 d = target - from; d.y = 0f;
            if (d.sqrMagnitude < 9f)
            {
                Vector3 push = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 6f;
                target = from + push;
            }
            target.y = from.y;
            return target;
        }
    }
}
