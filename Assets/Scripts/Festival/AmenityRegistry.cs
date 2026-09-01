using System.Collections.Generic;
using Matsuri.Data;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 「居場所」になる施設の正規ID (§20 の拡張)。
    /// 屋台と同じく、表示名ではなくこのIDでデータ・セーブ・スコアを扱う。
    /// </summary>
    public static class AmenityIds
    {
        /// <summary>盆踊り場。やぐらを囲んで踊る。</summary>
        public const string BonOdoriGround = "bon_odori_ground";

        /// <summary>休憩所。縁台に座って休む。</summary>
        public const string RestArea = "rest_area";

        /// <summary>神社。賽銭箱の前で参拝する。</summary>
        public const string ShrineGround = "shrine_ground";

        /// <summary>手水舎。水盤で手を清める。</summary>
        public const string Temizuya = "temizuya";
    }

    /// <summary>
    /// 施設に滞在したときの効き目 (§34)。
    /// FacilityData は屋台と違って「滞在時間」「毎秒の増減」を持っていないので、
    /// 効果種別ごとの標準値をここに1か所だけ置く (§31 ハードコードを散らさない)。
    ///
    /// - StayMinutes は**ゲーム内の分**。実秒は BalanceConfig.ToRealSeconds() で換算する (§7)
    /// - 毎秒の増減は**実時間の1秒あたり**
    /// - Fun は「遊びたさ」なので、踊って満たされると**減る**（負の値になる）
    /// </summary>
    public readonly struct AmenityProfile
    {
        /// <summary>滞在時間（ゲーム内の分）。</summary>
        public readonly float StayMinutes;

        /// <summary>滞在中に毎秒増える満足度。</summary>
        public readonly float SatisfactionPerSecond;

        /// <summary>滞在中に毎秒増える体力。踊りのように疲れるものは負。</summary>
        public readonly float EnergyPerSecond;

        /// <summary>滞在中に毎秒増える「遊びたさ」。踊れば満たされるので負。</summary>
        public readonly float FunPerSecond;

        public AmenityProfile(float stayMinutes, float satisfaction, float energy, float fun)
        {
            StayMinutes = stayMinutes;
            SatisfactionPerSecond = satisfaction;
            EnergyPerSecond = energy;
            FunPerSecond = fun;
        }

        /// <summary>滞在の効き目を持つ施設か。持たないものは「立ち寄り先」にならない。</summary>
        public bool IsPlaceToStay => StayMinutes > 0f;

        /// <summary>EffectStrength を基準値と比べたときの倍率の基準。ベンチ25／休憩所50 など。</summary>
        public const float ReferenceStrength = 50f;

        /// <summary>効果種別ごとの標準値。</summary>
        public static AmenityProfile ForEffect(FacilityEffect effect)
        {
            switch (effect)
            {
                // 盆踊り：長く踊る。遊びたさが大きく満たされ、満足度が大きく上がる。少し疲れる。
                case FacilityEffect.Dance:   return new AmenityProfile(12f, 3.2f, -0.9f, -6.0f);

                // 休憩：座って体力を戻す。満足度もゆっくり上がる。
                case FacilityEffect.Rest:    return new AmenityProfile(8f, 1.8f, 11.0f, 0f);

                // 参拝：短いが満足度がよく上がる。
                case FacilityEffect.Worship: return new AmenityProfile(6f, 4.2f, 0.6f, -1.0f);

                // 手水：ごく短い。ちょっとした気分転換。
                case FacilityEffect.Purify:  return new AmenityProfile(3f, 2.6f, 0.4f, 0f);

                default:                     return new AmenityProfile(0f, 0f, 0f, 0f);
            }
        }

        /// <summary>
        /// EffectStrength による効き目の倍率。
        /// 基準 50 で等倍。ベンチ(25)は半分、立派な施設ほど強い。効き過ぎないよう上下を締める。
        /// </summary>
        public static float StrengthScale(float effectStrength)
            => Mathf.Clamp(effectStrength / ReferenceStrength, 0.4f, 1.6f);
    }

    /// <summary>
    /// 施設の立ち位置の並べ方。施設ごとに形が違う (§79 「全部同じ並び」を避ける)。
    /// 手続き生成の見た目 (ProceduralAmenityFactory) と、
    /// Prefab に差し替えられたときの保険 (Facility の自前生成) の両方がここを使う。
    /// 返すのは施設のローカル座標。
    /// </summary>
    public static class AmenitySlotLayout
    {
        /// <summary>やぐらを囲む一番内側の輪の半径 (m)。</summary>
        public const float DanceInnerRadius = 4.2f;

        /// <summary>輪と輪の間隔 (m)。</summary>
        public const float DanceRingSpacing = 1.75f;

        /// <summary>同じ輪の中の人と人の間隔 (m)。</summary>
        public const float DanceSlotSpacing = 1.6f;

        /// <summary>縁台の座面の高さ (m)。</summary>
        public const float BenchSeatHeight = 0.46f;

        public static Vector3 Local(FacilityEffect effect, int index, int capacity)
        {
            if (index < 0) index = 0;
            capacity = Mathf.Max(1, capacity);

            switch (effect)
            {
                case FacilityEffect.Dance:   return DanceRing(index);
                case FacilityEffect.Worship: return WorshipRow(index, capacity);
                case FacilityEffect.Purify:  return BasinSide(index);
                default:                     return BenchRow(index, capacity);
            }
        }

        /// <summary>盆踊り場：やぐらを中心にした同心円。内側の輪から順に埋まる。</summary>
        public static Vector3 DanceRing(int index)
        {
            int ring = 0;
            int remaining = index;
            int countInRing;
            float radius;

            // 内側の輪から順に「その輪に何人入るか」を数え、あふれたら外側の輪へ。
            while (true)
            {
                radius = DanceInnerRadius + ring * DanceRingSpacing;
                countInRing = Mathf.Max(4, Mathf.FloorToInt(2f * Mathf.PI * radius / DanceSlotSpacing));
                if (remaining < countInRing) break;
                remaining -= countInRing;
                ring++;
                if (ring > 32) break;   // 異常な人数でも無限ループしない
            }

            // 輪ごとに少し回して、内外の人が一直線に重ならないようにする。
            float angle = Mathf.PI * 2f * remaining / countInRing + ring * 0.42f;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        /// <summary>休憩所・ベンチ：縁台の上に等間隔。5人以上なら縁台2つに分ける。</summary>
        public static Vector3 BenchRow(int index, int capacity)
        {
            int rows = capacity <= 4 ? 1 : 2;
            int perRow = Mathf.Max(1, Mathf.CeilToInt(capacity / (float)rows));
            int row = Mathf.Min(rows - 1, index / perRow);
            int col = index - row * perRow;

            float span = Mathf.Max(1.2f, perRow * 0.82f);
            float x = perRow <= 1 ? 0f : -span * 0.5f + span * col / (perRow - 1);
            float z = rows == 1 ? 0f : (row == 0 ? -1.15f : 1.15f);
            return new Vector3(x, BenchSeatHeight, z);
        }

        /// <summary>神社：賽銭箱の前に横並び。あふれたぶんは階段下の待機列になる。</summary>
        public static Vector3 WorshipRow(int index, int capacity)
        {
            int front = Mathf.Clamp(Mathf.CeilToInt(capacity * 0.5f), 1, 6);
            if (index < front)
            {
                float span = Mathf.Max(1.4f, front * 0.85f);
                float x = front <= 1 ? 0f : -span * 0.5f + span * index / (front - 1);
                return new Vector3(x, 0f, 4.1f);   // 賽銭箱のすぐ前
            }

            int back = index - front;
            int perRow = Mathf.Max(1, front);
            int row = back / perRow;
            int col = back - row * perRow;
            float bspan = Mathf.Max(1.4f, perRow * 0.95f);
            float bx = perRow <= 1 ? 0f : -bspan * 0.5f + bspan * col / (perRow - 1);
            return new Vector3(bx, 0f, 5.9f + row * 1.3f);   // 階段下の待機列
        }

        /// <summary>手水舎：水盤の四辺に立つ。5人目からは一歩下がった外周。</summary>
        public static Vector3 BasinSide(int index)
        {
            int ring = index / 4;
            int side = index - ring * 4;
            float r = 1.15f + ring * 0.85f;
            switch (side)
            {
                case 0:  return new Vector3(0f, 0f, r);
                case 1:  return new Vector3(r, 0f, 0f);
                case 2:  return new Vector3(0f, 0f, -r);
                default: return new Vector3(-r, 0f, 0f);
            }
        }
    }

    /// <summary>
    /// 会場に建っている <see cref="Facility"/> の台帳。
    /// NPC が「近くの空いている休憩所」を探すために毎秒引くので、
    /// FindObjectsOfType を使わず、建った／消えたときに登録・解除する (§57)。
    ///
    /// static なので Play モードを抜けても中身が残りうる。
    /// 破棄済みの参照は参照時に取り除き、<see cref="Clear"/> で明示的に空にできる。
    /// </summary>
    public static class AmenityRegistry
    {
        static readonly List<Facility> s_All = new List<Facility>(32);
        static readonly Dictionary<FacilityEffect, List<Facility>> s_ByEffect =
            new Dictionary<FacilityEffect, List<Facility>>(8);

        static readonly List<Facility> s_Empty = new List<Facility>(0);

        /// <summary>建った施設を台帳に載せる。Facility 自身が呼ぶ。</summary>
        internal static void Register(Facility f)
        {
            if (f == null || s_All.Contains(f)) return;
            s_All.Add(f);
            Bucket(f.Effect).Add(f);
        }

        /// <summary>壊された施設を台帳から外す。Facility 自身が呼ぶ。</summary>
        internal static void Unregister(Facility f)
        {
            if (f == null) return;
            s_All.Remove(f);
            foreach (var kv in s_ByEffect) kv.Value.Remove(f);
        }

        /// <summary>いま建っている全施設。</summary>
        public static IReadOnlyList<Facility> All
        {
            get { Prune(); return s_All; }
        }

        /// <summary>効果種別で絞った一覧。無ければ空。</summary>
        public static IReadOnlyList<Facility> OfEffect(FacilityEffect e)
        {
            if (!s_ByEffect.TryGetValue(e, out var list)) return s_Empty;
            PruneList(list);
            return list;
        }

        /// <summary>その効果の施設が何軒あるか。スコア (§35) の多彩さ判定にも使える。</summary>
        public static int CountOf(FacilityEffect e) => OfEffect(e).Count;

        /// <summary>
        /// from から一番近い「空きのある」施設。maxDistance より遠いものは無視する。
        /// 見つからなければ null。
        /// </summary>
        public static Facility FindNearestWithSlot(Vector3 from, FacilityEffect e, float maxDistance = 9999f)
        {
            var list = OfEffect(e);
            if (list.Count == 0) return null;

            Facility best = null;
            float bestSq = maxDistance * maxDistance;

            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f == null || !f.HasFreeSlot) continue;

                Vector3 d = f.transform.position - from;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq > bestSq) continue;

                bestSq = sq;
                best = f;
            }
            return best;
        }

        /// <summary>台帳を空にする。祭りをやり直すときに呼ぶ。</summary>
        public static void Clear()
        {
            s_All.Clear();
            foreach (var kv in s_ByEffect) kv.Value.Clear();
        }

        static List<Facility> Bucket(FacilityEffect e)
        {
            if (!s_ByEffect.TryGetValue(e, out var list))
            {
                list = new List<Facility>(8);
                s_ByEffect[e] = list;
            }
            return list;
        }

        /// <summary>破棄済み（Destroy 済み）の参照を取り除く。</summary>
        static void Prune()
        {
            PruneList(s_All);
            foreach (var kv in s_ByEffect) PruneList(kv.Value);
        }

        static void PruneList(List<Facility> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == null) list.RemoveAt(i);
        }
    }
}
