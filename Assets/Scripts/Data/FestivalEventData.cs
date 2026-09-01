using UnityEngine;

namespace Matsuri.Data
{
    /// <summary>イベント1種類ぶんの設定 (§22)。</summary>
    [CreateAssetMenu(fileName = "FestivalEventData", menuName = "Matsuri/Festival Event Data", order = 4)]
    public sealed class FestivalEventData : ScriptableObject
    {
        [Header("識別")]
        public string Id = "fireworks";
        public string DisplayName = "花火";
        public string[] Aliases = System.Array.Empty<string>();

        [Header("経営")]
        [Tooltip("開催費用 (§31 花火 300,000円)。")]
        public long Cost = 300000;

        [Header("演出")]
        [Tooltip("継続時間（実時間・秒）。")]
        public float Duration = 25f;

        public FestivalEventKind Kind = FestivalEventKind.Fireworks;

        [Header("効果 (§34)")]
        [Range(0f, 100f)]
        [Tooltip("見た人の満足度が一気にどれだけ上がるか。")]
        public float SatisfactionBurst = 35f;

        [Tooltip("効果半径 (m)。花火は会場全体なので大きい。")]
        public float EffectRadius = 200f;

        [Tooltip("この間、来場者の滞在時間が伸びる倍率。")]
        public float StayExtendMultiplier = 1.4f;

        [Tooltip("開催中に新規来場者を呼び込む倍率 (§33)。")]
        public float VisitorAttractMultiplier = 1.6f;

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

    public enum FestivalEventKind
    {
        Fireworks,
        BonOdori,
        Taiko
    }
}
