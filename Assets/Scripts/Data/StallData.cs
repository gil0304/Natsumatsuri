using UnityEngine;

namespace Matsuri.Data
{
    /// <summary>
    /// 仕様書 §48。屋台1種類ぶんの設定。
    /// §31「数値はゲームバランス調整可能なデータとして管理する。ハードコードしない。」
    /// </summary>
    [CreateAssetMenu(fileName = "StallData", menuName = "Matsuri/Stall Data", order = 0)]
    public sealed class StallData : ScriptableObject
    {
        [Header("識別")]
        [Tooltip("正規ID。MatsuriIds の定数と一致させる。")]
        public string Id = "takoyaki";

        [Tooltip("画面に出す名前。")]
        public string DisplayName = "たこ焼き";

        [Tooltip("Matsuri Script で書ける別表記。「たこやき」「takoyaki」など。")]
        public string[] Aliases = System.Array.Empty<string>();

        public StallCategory Category = StallCategory.Food;

        [Header("見た目")]
        [Tooltip("完成モデルの Prefab。null の場合は VisualRecipe から手続き生成する (§69)。")]
        public GameObject Prefab;

        [Tooltip("Prefab が無いときに使う手続き生成レシピ。")]
        public StallVisualRecipe VisualRecipe;

        [Header("経営 (§31)")]
        [Tooltip("建設費用。")]
        public long BuildCost = 100000;

        [Tooltip("「値段」を書かなかったときの既定価格。")]
        public int DefaultPrice = 500;

        [Tooltip("設定できる最低価格。")]
        public int MinPrice = 50;

        [Tooltip("設定できる最高価格。これを超えるとほぼ誰も買わない。")]
        public int MaxPrice = 3000;

        [Header("接客 (§30)")]
        [Tooltip("1人あたりの接客時間（秒・ゲーム内時間ではなく実時間）。")]
        public float ServiceTime = 8f;

        [Tooltip("同時に接客できる人数。\n" +
                 "屋台は列の先頭1人だけを相手にするわけではない。\n" +
                 "鉄板の面数・水槽を囲める人数・店員の数ぶんだけ並行して捌ける。\n" +
                 "ServiceTime (§30) は据え置いたまま、ここで祭りらしい客足を作る。")]
        public int Capacity = 1;

        [Tooltip("行列に並べる最大人数。これを超えると諦める。")]
        public int MaxQueueLength = 12;

        [Header("魅力 (§33 / §34)")]
        [Range(0f, 100f)]
        [Tooltip("初期人気度。NPC の目的地スコアに乗る。")]
        public float BasePopularity = 50f;

        [Range(0f, 100f)]
        [Tooltip("購入したNPCの満足度がどれだけ上がるか。")]
        public float SatisfactionValue = 20f;

        [Range(0f, 100f)]
        [Tooltip("空腹をどれだけ満たすか（食べ物のみ）。")]
        public float HungerRelief = 30f;

        [Range(0f, 100f)]
        [Tooltip("楽しさをどれだけ満たすか（遊びのみ）。")]
        public float FunRelief = 30f;

        [Range(0f, 100f)]
        [Tooltip("体力をどれだけ消費するか。")]
        public float EnergyCost = 5f;

        [Header("演出")]
        [Tooltip("屋台が出す音の種類。")]
        public StallAmbienceKind Ambience = StallAmbienceKind.Sizzle;

        [Tooltip("調理・遊びの VFX を出すか。")]
        public bool HasSteam = true;

        /// <summary>書かれた表記がこの屋台を指すか。</summary>
        public bool Matches(string writtenName)
        {
            if (string.IsNullOrEmpty(writtenName)) return false;
            if (NameUtility.Equals(writtenName, Id)) return true;
            if (NameUtility.Equals(writtenName, DisplayName)) return true;
            for (int i = 0; i < Aliases.Length; i++)
                if (NameUtility.Equals(writtenName, Aliases[i])) return true;
            return false;
        }

        public int ClampPrice(int price) => Mathf.Clamp(price, MinPrice, MaxPrice);
    }

    /// <summary>屋台が鳴らす環境音の種類 (§24 / §63)。</summary>
    public enum StallAmbienceKind
    {
        None,
        /// <summary>鉄板の焼ける音。</summary>
        Sizzle,
        /// <summary>氷を削る音。</summary>
        Shaving,
        /// <summary>水の音（金魚すくい・スーパーボール）。</summary>
        Water,
        /// <summary>射的のコルク音。</summary>
        Pop,
        /// <summary>綿あめ機の回転音。</summary>
        Whirr
    }
}
