using System;
using System.Collections.Generic;
using System.Text;
using Matsuri.Script;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §46 / §47。CHALLENGE MODE のお題1件。
    /// JsonUtility でも扱えるよう、範囲は struct ではなく float 4つで持ち、
    /// <see cref="ToBounds"/> で <see cref="GroundBounds"/> に変換する。
    /// </summary>
    [Serializable]
    public sealed class ChallengeDefinition
    {
        /// <summary>正規ID。セーブやランキングの識別に使う。</summary>
        public string Id = "";

        /// <summary>画面に出す名前。</summary>
        public string DisplayName = "";

        /// <summary>お題の説明（日本語・短く）。</summary>
        public string Description = "";

        /// <summary>使える予算。</summary>
        public long Budget = 1000000;

        /// <summary>制限時間（ゲーム内の分）。17:00〜22:00 なら 300。0以下なら制限なし。</summary>
        public int TimeLimitMinutes = 300;

        /// <summary>建ててよい屋台のID。空なら全部使ってよい。</summary>
        public string[] AllowedStallIds = Array.Empty<string>();

        /// <summary>必ず建てなければならない屋台のID。空なら指定なし。</summary>
        public string[] RequiredStallIds = Array.Empty<string>();

        /// <summary>花火を上げることが条件か (§22)。</summary>
        public bool RequireFireworks;

        /// <summary>会場の範囲（メートル）。</summary>
        public float MinX = -60f;
        public float MaxX = 60f;
        public float MinZ = -60f;
        public float MaxZ = 60f;

        public ChallengeDefinition() { }

        public ChallengeDefinition(string id, string displayName, string description, long budget)
        {
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            Budget = budget;
        }

        /// <summary>会場範囲を Validator が使う形に変換する。</summary>
        public GroundBounds ToBounds() => new GroundBounds(MinX, MaxX, MinZ, MaxZ);

        /// <summary>コントラクト互換の読み取り用プロパティ。中身は <see cref="ToBounds"/>。</summary>
        public GroundBounds Bounds => ToBounds();

        /// <summary>会場の広さを設定する。</summary>
        public ChallengeDefinition WithBounds(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ;
            return this;
        }

        public ChallengeDefinition WithAllowedStalls(params string[] ids)
        {
            AllowedStallIds = ids ?? Array.Empty<string>();
            return this;
        }

        public ChallengeDefinition WithRequiredStalls(params string[] ids)
        {
            RequiredStallIds = ids ?? Array.Empty<string>();
            return this;
        }

        public ChallengeDefinition WithFireworks(bool required)
        {
            RequireFireworks = required;
            return this;
        }

        /// <summary>屋台の制限があるか。</summary>
        public bool HasStallRestriction => AllowedStallIds != null && AllowedStallIds.Length > 0;

        /// <summary>この屋台を建ててよいか。</summary>
        public bool IsStallAllowed(string stallId)
        {
            if (string.IsNullOrEmpty(stallId)) return false;
            if (!HasStallRestriction) return true;

            for (int i = 0; i < AllowedStallIds.Length; i++)
                if (string.Equals(AllowedStallIds[i], stallId, StringComparison.Ordinal))
                    return true;

            return false;
        }

        /// <summary>その座標が会場の中か。</summary>
        public bool ContainsPosition(float x, float z) => ToBounds().Contains(x, z);

        /// <summary>
        /// 建てたものがお題を満たしているか判定する。
        /// message には満たしていない理由（日本語）が入る。
        /// </summary>
        public bool CheckRequirements(IReadOnlyCollection<string> builtStallIds, bool fireworksPlayed, out string message)
        {
            var missing = new List<string>();

            if (RequiredStallIds != null && RequiredStallIds.Length > 0)
            {
                for (int i = 0; i < RequiredStallIds.Length; i++)
                {
                    string required = RequiredStallIds[i];
                    if (string.IsNullOrEmpty(required)) continue;

                    bool found = false;
                    if (builtStallIds != null)
                    {
                        foreach (var id in builtStallIds)
                        {
                            if (string.Equals(id, required, StringComparison.Ordinal)) { found = true; break; }
                        }
                    }
                    if (!found) missing.Add($"「{required}」を建てる");
                }
            }

            if (RequireFireworks && !fireworksPlayed)
                missing.Add("花火を上げる");

            if (missing.Count == 0)
            {
                message = "お題の条件を満たしています。";
                return true;
            }

            var sb = new StringBuilder("お題の条件が足りません: ");
            for (int i = 0; i < missing.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(missing[i]);
            }
            message = sb.ToString();
            return false;
        }

        /// <summary>お題の条件を並べた説明文 (§47 の選択画面用)。</summary>
        public string DescribeRules()
        {
            var sb = new StringBuilder();
            sb.Append("予算: ").Append(Budget.ToString("N0")).Append(" 円");

            if (HasStallRestriction)
            {
                sb.Append('\n').Append("使える屋台: ");
                for (int i = 0; i < AllowedStallIds.Length; i++)
                {
                    if (i > 0) sb.Append('、');
                    sb.Append(AllowedStallIds[i]);
                }
            }

            if (RequiredStallIds != null && RequiredStallIds.Length > 0)
            {
                sb.Append('\n').Append("必須の屋台: ");
                for (int i = 0; i < RequiredStallIds.Length; i++)
                {
                    if (i > 0) sb.Append('、');
                    sb.Append(RequiredStallIds[i]);
                }
            }

            if (RequireFireworks) sb.Append('\n').Append("花火: 必ず上げること");

            sb.Append('\n').Append("会場: ").Append(ToBounds().ToString());

            if (TimeLimitMinutes > 0)
                sb.Append('\n').Append("制限時間: ゲーム内 ").Append(TimeLimitMinutes).Append(" 分");

            return sb.ToString();
        }

        public ChallengeDefinition Clone()
        {
            return new ChallengeDefinition
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                Budget = Budget,
                TimeLimitMinutes = TimeLimitMinutes,
                AllowedStallIds = (string[])(AllowedStallIds ?? Array.Empty<string>()).Clone(),
                RequiredStallIds = (string[])(RequiredStallIds ?? Array.Empty<string>()).Clone(),
                RequireFireworks = RequireFireworks,
                MinX = MinX, MaxX = MaxX, MinZ = MinZ, MaxZ = MaxZ
            };
        }

        public override string ToString() =>
            string.IsNullOrEmpty(DisplayName) ? Id : $"{DisplayName}（{Description}）";
    }
}
