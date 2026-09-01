using UnityEngine;

namespace Matsuri.Data
{
    /// <summary>装飾1種類ぶんの設定 (§21)。</summary>
    [CreateAssetMenu(fileName = "DecorationData", menuName = "Matsuri/Decoration Data", order = 3)]
    public sealed class DecorationData : ScriptableObject
    {
        [Header("識別")]
        public string Id = "lantern";
        public string DisplayName = "提灯";
        public string[] Aliases = System.Array.Empty<string>();

        [Header("見た目")]
        public GameObject Prefab;
        public DecorationVisualKind Visual = DecorationVisualKind.Lantern;
        public Color MainColor = new Color(0.92f, 0.24f, 0.16f);

        [Tooltip("風で揺れるか (§63)。")]
        public bool SwaysInWind = true;

        [Header("経営")]
        public long BuildCost = 5000;

        [Header("効果 (§34)")]
        public DecorationEffect Effect = DecorationEffect.Ambience;

        [Tooltip("効果が届く半径 (m)。")]
        public float EffectRadius = 10f;

        [Range(0f, 100f)]
        [Tooltip("周囲のNPCの満足度をどれだけ上げるか（毎秒）。")]
        public float AmbienceValue = 3f;

        [Header("照明 (§59)")]
        [Tooltip("光るか。提灯・屋台用ライトは光る。")]
        public bool EmitsLight = true;
        public Color LightColor = new Color(1f, 0.68f, 0.42f);
        public float LightIntensity = 350f;
        public float LightRange = 9f;

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

    public enum DecorationVisualKind
    {
        Lantern,       // 提灯
        Nobori,        // のぼり
        Shrine,        // 神社
        Torii,         // 鳥居
        Tree,          // 木
        StallLight,    // 屋台用ライト
        FestivalSign   // 夏祭り看板
    }
}
