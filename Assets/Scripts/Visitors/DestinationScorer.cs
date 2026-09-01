using Matsuri.Data;
using UnityEngine;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場者から見た「この屋台の評価値」を作るための入力 (§29)。
    /// VisitorAgent を経由せずに値だけで評価できるようにして、EditMode テストを可能にする。
    /// </summary>
    public readonly struct VisitorScoreProfile
    {
        public readonly Vector3 Position;
        public readonly float Money;
        public readonly float Hunger;
        public readonly float Fun;
        public readonly float Patience;
        public readonly float PriceSensitivity;

        public VisitorScoreProfile(Vector3 position, float money, float hunger, float fun,
                                   float patience, float priceSensitivity)
        {
            Position = position;
            Money = money;
            Hunger = hunger;
            Fun = fun;
            Patience = patience;
            PriceSensitivity = priceSensitivity;
        }
    }

    /// <summary>屋台側の評価入力 (§29)。</summary>
    public readonly struct StallScoreProfile
    {
        public readonly Vector3 Position;
        public readonly StallCategory Category;
        /// <summary>この来場者のこの屋台への好み 0-100。</summary>
        public readonly float Preference;
        public readonly int Price;
        public readonly int QueueLength;
        public readonly int MaxQueueLength;
        public readonly float Popularity;
        public readonly bool IsOpen;
        public readonly bool CanAcceptQueue;

        public StallScoreProfile(Vector3 position, StallCategory category, float preference, int price,
                                 int queueLength, int maxQueueLength, float popularity,
                                 bool isOpen, bool canAcceptQueue)
        {
            Position = position;
            Category = category;
            Preference = preference;
            Price = price;
            QueueLength = queueLength;
            MaxQueueLength = maxQueueLength;
            Popularity = popularity;
            IsOpen = isOpen;
            CanAcceptQueue = canAcceptQueue;
        }
    }

    /// <summary>
    /// 仕様書 §29「NPCの目的地決定アルゴリズム」。
    ///
    ///   Score = Preference * WeightPreference
    ///         + Need       * WeightNeed          （食べ物なら空腹度、遊びなら遊びたさ）
    ///         - Distance   * WeightDistance
    ///         - Queue      * WeightQueue         （行列の長さ ÷ 我慢強さ）
    ///         - Price      * WeightPrice         （価格 ÷ 基準価格 × 価格敏感さ）
    ///         + Popularity * WeightPopularity
    ///         + noise                            （全員が同じ屋台に殺到しないためのゆらぎ）
    ///
    /// 各項は「0〜100点」のスケールに正規化してから重みを掛ける。
    /// こうしておくと BalanceConfig の重み（既定 0.5〜2.4）がそのまま「効き具合」として読める。
    ///
    /// LLM は一切使わない (§25)。純粋な計算だけで決める。
    /// このクラスは Unity のシーンに依存しないので EditMode テストで直接検証できる。
    /// </summary>
    public static class DestinationScorer
    {
        /// <summary>距離ペナルティのスケール。DistanceUnit(既定10m) 離れるごとに 10 点。</summary>
        public const float DistancePenaltyScale = 10f;

        /// <summary>行列ペナルティのスケール。行列が満杯なら 100 点ぶんのペナルティ。</summary>
        public const float QueuePenaltyScale = 100f;

        /// <summary>価格ペナルティのスケール。「買う気が半減する価格」でちょうど 100 点。</summary>
        public const float PricePenaltyScale = 100f;

        /// <summary>安い屋台がもらえるボーナスの上限（＝マイナスのペナルティ）。</summary>
        public const float MaxCheapBonus = 25f;

        /// <summary>我慢強さの基準値。これより我慢強い人は行列を気にしなくなる。</summary>
        public const float PatienceReference = 50f;

        /// <summary>
        /// コントラクトのシグネチャ。実体は下の純関数版に委譲する。
        /// 買えない屋台・満員の屋台は float.NegativeInfinity を返して「選ばれない」ことを保証する。
        /// </summary>
        public static float Score(VisitorAgent v, Stalls.Stall s, BalanceConfig b, float noise)
        {
            if (v == null || s == null || b == null) return float.NegativeInfinity;

            var data = s.Data;
            if (data == null) return float.NegativeInfinity;

            float preference = v.Archetype != null
                ? v.Archetype.GetPreference(data.Id, data.Category)
                : 50f;

            var vp = new VisitorScoreProfile(
                v.Position, v.Money, v.Hunger, v.Fun, v.Patience, v.PriceSensitivity);

            var sp = new StallScoreProfile(
                s.transform.position, data.Category, preference, s.Price,
                s.QueueLength, data.MaxQueueLength, s.Popularity,
                s.IsOpen, s.CanAcceptQueue);

            return Score(in vp, in sp, b, noise);
        }

        /// <summary>純関数版 (§29)。テストはこちらを叩く。</summary>
        public static float Score(in VisitorScoreProfile v, in StallScoreProfile s, BalanceConfig b, float noise)
        {
            if (b == null) return float.NegativeInfinity;

            // --- 選択肢から外れる条件 ---
            if (!s.IsOpen) return float.NegativeInfinity;                 // 営業していない
            if (!s.CanAcceptQueue) return float.NegativeInfinity;         // 満員で並べない
            int maxQueue = Mathf.Max(1, s.MaxQueueLength);
            if (s.QueueLength >= maxQueue) return float.NegativeInfinity; // 行列が上限
            if (s.Price > v.Money) return float.NegativeInfinity;         // 所持金で買えない

            // --- 好み (0-100) ---
            float preferenceTerm = Mathf.Clamp(s.Preference, 0f, 100f) * b.WeightPreference;

            // --- 欲求 (0-100)。食べ物なら空腹、遊びなら遊びたさ ---
            float need = s.Category == StallCategory.Food ? v.Hunger : v.Fun;
            float needTerm = Mathf.Clamp(need, 0f, 100f) * b.WeightNeed;

            // --- 距離 ---
            float unit = Mathf.Max(0.01f, b.DistanceUnit);
            Vector3 delta = s.Position - v.Position;
            delta.y = 0f;
            float distance = delta.magnitude;
            float distanceTerm = (distance / unit) * DistancePenaltyScale * b.WeightDistance;

            // --- 行列。我慢強い人ほどペナルティが小さい (§34) ---
            float patienceFactor = Mathf.Max(0.25f, Mathf.Clamp(v.Patience, 1f, 200f) / PatienceReference);
            float queueRatio = Mathf.Clamp01(s.QueueLength / (float)maxQueue);
            float queueTerm = (queueRatio / patienceFactor) * QueuePenaltyScale * b.WeightQueue;

            // --- 価格 (§32)。基準価格より安ければ逆にボーナス ---
            float priceTerm = PricePenalty(s.Price, v.PriceSensitivity, b) * b.WeightPrice;

            // --- 人気度 (§33) ---
            float maxPopularity = Mathf.Max(1f, b.MaxPopularity);
            float popularityTerm = (Mathf.Clamp(s.Popularity, 0f, maxPopularity) / maxPopularity)
                                   * 100f * b.WeightPopularity;

            return preferenceTerm + needTerm - distanceTerm - queueTerm - priceTerm + popularityTerm + noise;
        }

        /// <summary>
        /// 価格ペナルティ。基準価格 (ReferencePrice) と同額なら 0、
        /// 「買う気が半減する価格」(ReferencePrice × PriceHalfPoint) でちょうど 100 点。
        /// 基準より安い場合は負の値（＝ボーナス）を返す。
        /// </summary>
        public static float PricePenalty(int price, float priceSensitivity, BalanceConfig b)
        {
            if (b == null) return 0f;
            float reference = Mathf.Max(1f, b.ReferencePrice);
            float half = Mathf.Max(1.05f, b.PriceHalfPoint);
            float ratio = price / reference;
            float sensitivity = Mathf.Max(0.05f, priceSensitivity);

            float raw = (ratio - 1f) / (half - 1f) * PricePenaltyScale * sensitivity;
            return Mathf.Max(raw, -MaxCheapBonus);
        }

        /// <summary>
        /// 「その屋台は選択肢に入るか」だけを判定する軽い版。
        /// 帰宅判定 (§28) で「もう買える屋台が何も無い」を調べるのに使う。
        /// </summary>
        public static bool IsCandidate(float money, in StallScoreProfile s)
        {
            if (!s.IsOpen || !s.CanAcceptQueue) return false;
            if (s.QueueLength >= Mathf.Max(1, s.MaxQueueLength)) return false;
            return s.Price <= money;
        }
    }
}
