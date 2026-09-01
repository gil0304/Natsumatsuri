using UnityEngine;

namespace Matsuri.Data
{
    /// <summary>設備1種類ぶんの設定 (§20)。</summary>
    [CreateAssetMenu(fileName = "FacilityData", menuName = "Matsuri/Facility Data", order = 2)]
    public sealed class FacilityData : ScriptableObject
    {
        [Header("識別")]
        public string Id = "bench";
        public string DisplayName = "ベンチ";
        public string[] Aliases = System.Array.Empty<string>();

        [Header("見た目")]
        public GameObject Prefab;
        public FacilityVisualKind Visual = FacilityVisualKind.Bench;

        [Header("経営")]
        public long BuildCost = 10000;

        [Header("効果 (§34)")]
        public FacilityEffect Effect = FacilityEffect.Rest;

        [Tooltip("効果が届く半径 (m)。")]
        public float EffectRadius = 8f;

        [Range(0f, 100f)]
        [Tooltip("効果の強さ。休憩なら回復量、清潔さなら満足度低下の抑制量。")]
        public float EffectStrength = 20f;

        [Tooltip("同時に使える人数（ベンチなら座れる数）。0 なら無制限。")]
        public int Capacity = 3;

        public bool Matches(string writtenName)
        {
            if (string.IsNullOrEmpty(writtenName)) return false;
            if (NameUtility.Equals(writtenName, Id)) return true;
            if (NameUtility.Equals(writtenName, DisplayName)) return true;
            for (int i = 0; i < Aliases.Length; i++)
                if (NameUtility.Equals(writtenName, Aliases[i])) return true;
            return false;
        }
    }

    public enum FacilityVisualKind
    {
        Bench,
        TrashCan,
        Toilet,
        Gate,        // 入り口／出口
        SignBoard
    }
}
