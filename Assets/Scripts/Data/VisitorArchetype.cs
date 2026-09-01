using UnityEngine;

namespace Matsuri.Data
{
    /// <summary>屋台ごとの好み (§26)。</summary>
    [System.Serializable]
    public struct PreferenceEntry
    {
        [Tooltip("屋台の正規ID。")]
        public string StallId;

        [Range(0f, 100f)]
        public float Value;
    }

    /// <summary>0〜100 の範囲をランダムに引くためのレンジ。</summary>
    [System.Serializable]
    public struct FloatRange
    {
        public float Min;
        public float Max;

        public FloatRange(float min, float max) { Min = min; Max = max; }

        public float Sample(ref Unity.Mathematics.Random rng)
            => Mathf.Lerp(Min, Max, rng.NextFloat());
    }

    /// <summary>
    /// NPCのプリセット (§27)。子ども / 高校生 / カップル / 家族 / 大人。
    /// 実際のNPCは、このレンジからランダムに値を引いて生成される (§27「ランダム性も追加する」)。
    /// </summary>
    [CreateAssetMenu(fileName = "VisitorArchetype", menuName = "Matsuri/Visitor Archetype", order = 5)]
    public sealed class VisitorArchetype : ScriptableObject
    {
        [Header("識別")]
        public string Id = "child";
        public string DisplayName = "子ども";

        [Tooltip("来場者全体に占める出現しやすさ。合計は1でなくてよい。")]
        public float SpawnWeight = 1f;

        [Header("基本パラメータ (§26)")]
        [Tooltip("所持金。これを超える買い物はしない。")]
        public FloatRange Money = new FloatRange(800f, 2500f);

        [Tooltip("空腹度。高いほど食べ物を求める。")]
        public FloatRange Hunger = new FloatRange(30f, 80f);

        [Tooltip("遊びたさ。高いほど遊びの屋台を求める。")]
        public FloatRange Fun = new FloatRange(60f, 100f);

        [Tooltip("体力。0になると帰る。")]
        public FloatRange Energy = new FloatRange(60f, 100f);

        [Tooltip("歩く速さ (m/s)。")]
        public FloatRange WalkingSpeed = new FloatRange(1.1f, 1.6f);

        [Tooltip("我慢強さ。行列の長さをどこまで許容するか (§34)。")]
        public FloatRange Patience = new FloatRange(30f, 70f);

        [Range(0f, 100f)]
        [Tooltip("花火への興味 (§26)。")]
        public float FireworksInterest = 60f;

        [Header("好み (§26)")]
        [Tooltip("食べ物屋台への好み。未記載の屋台は DefaultFoodPreference を使う。")]
        public PreferenceEntry[] PreferenceFood = System.Array.Empty<PreferenceEntry>();

        [Tooltip("遊び屋台への好み。")]
        public PreferenceEntry[] PreferenceGame = System.Array.Empty<PreferenceEntry>();

        [Range(0f, 100f)] public float DefaultFoodPreference = 45f;
        [Range(0f, 100f)] public float DefaultGamePreference = 45f;

        [Header("行動")]
        [Tooltip("何軒くらい回ってから帰るか。")]
        public Vector2Int TargetVisitCount = new Vector2Int(2, 5);

        [Tooltip("価格への敏感さ。高いほど高い商品を避ける。")]
        public FloatRange PriceSensitivity = new FloatRange(0.8f, 1.6f);

        [Header("見た目")]
        [Tooltip("身長 (m)。子どもは低い。")]
        public FloatRange BodyHeight = new FloatRange(1.15f, 1.35f);

        [Tooltip("浴衣・服の色バリエーション。ここから1つ選ばれる (§79 同じNPC大量複製を避ける)。")]
        public Color[] OutfitColors = System.Array.Empty<Color>();

        [Tooltip("肌の色バリエーション。")]
        public Color[] SkinColors = System.Array.Empty<Color>();

        [Tooltip("髪の色バリエーション。")]
        public Color[] HairColors = System.Array.Empty<Color>();

        /// <summary>この屋台への好み値を返す。</summary>
        public float GetPreference(string stallId, StallCategory category)
        {
            var table = category == StallCategory.Food ? PreferenceFood : PreferenceGame;
            for (int i = 0; i < table.Length; i++)
                if (table[i].StallId == stallId) return table[i].Value;
            return category == StallCategory.Food ? DefaultFoodPreference : DefaultGamePreference;
        }
    }
}
