using UnityEngine;

namespace Matsuri.Data
{
    /// <summary>屋台の中身の作り込み (§23)。何を作業台に置くか。</summary>
    public enum StallPropKind
    {
        None,
        /// <summary>たこ焼き器 + ソース瓶 + 舟皿。</summary>
        TakoyakiPlate,
        /// <summary>大鉄板 + ヘラ + 麺の山。</summary>
        Teppan,
        /// <summary>かき氷機 + シロップ瓶。</summary>
        IceShaver,
        /// <summary>りんご飴のスタンド。</summary>
        CandyAppleRack,
        /// <summary>綿あめ機。</summary>
        CottonCandyMachine,
        /// <summary>フランクフルトのグリル。</summary>
        Grill,
        /// <summary>水槽 + 泳ぐ金魚 + ポイ (§62)。</summary>
        FishTank,
        /// <summary>景品棚 + コルク銃。</summary>
        ShootingRack,
        /// <summary>ヨーヨーの浮かぶ水槽。</summary>
        YoyoTub,
        /// <summary>スーパーボールの浮かぶ水槽。</summary>
        BallTub,
        /// <summary>型抜きの作業台。</summary>
        KatanukiDesk
    }

    /// <summary>屋根の形。</summary>
    public enum StallRoofKind
    {
        /// <summary>切妻（三角屋根）。定番の屋台。</summary>
        Gable,
        /// <summary>片流れ。</summary>
        Shed,
        /// <summary>テント（幕）。</summary>
        Awning
    }

    /// <summary>
    /// AI生成モデルが入るまでの間、屋台を「箱じゃないもの」として組み立てるための設計図 (§69 / §79)。
    /// StallData.Prefab に完成モデルを差した時点でこちらは使われなくなる。
    /// </summary>
    [CreateAssetMenu(fileName = "StallVisualRecipe", menuName = "Matsuri/Stall Visual Recipe", order = 1)]
    public sealed class StallVisualRecipe : ScriptableObject
    {
        [Header("寸法 (m)")]
        public float Width = 3.2f;
        public float Depth = 2.2f;
        public float Height = 2.4f;
        public float CounterHeight = 1.0f;

        [Header("屋根")]
        public StallRoofKind Roof = StallRoofKind.Gable;
        public Color RoofColor = new Color(0.72f, 0.16f, 0.14f);
        public Color RoofStripeColor = new Color(0.95f, 0.94f, 0.90f);
        [Tooltip("紅白の縞にするか。")]
        public bool StripedRoof = true;

        [Header("暖簾 (§23 Noren)")]
        public Color NorenColor = new Color(0.70f, 0.12f, 0.12f);
        [Tooltip("暖簾に染め抜く文字。空なら DisplayName を使う。")]
        public string NorenText = "";
        public int NorenSlits = 4;

        [Header("看板 (§23 Sign)")]
        public Color SignBoardColor = new Color(0.92f, 0.86f, 0.70f);
        public Color SignTextColor = new Color(0.10f, 0.09f, 0.08f);

        [Header("木部")]
        public Color WoodColor = new Color(0.42f, 0.29f, 0.18f);
        public Color CounterColor = new Color(0.55f, 0.40f, 0.25f);

        [Header("照明 (§59)")]
        [Tooltip("軒下の裸電球の数。")]
        public int BulbCount = 5;
        public Color BulbColor = new Color(1f, 0.82f, 0.55f);
        [Tooltip("屋台に吊るす提灯の数。")]
        public int LanternCount = 2;
        public Color LanternColor = new Color(0.92f, 0.24f, 0.16f);
        [Tooltip("屋台1軒あたりの実光源の強さ(lumen)。")]
        public float LightIntensity = 900f;

        [Header("中身 (§23)")]
        public StallPropKind Prop = StallPropKind.TakoyakiPlate;

        [Tooltip("商品の色（舟皿の上の食べ物、景品など）。")]
        public Color ProductColor = new Color(0.78f, 0.46f, 0.20f);

        [Header("行列 (§30)")]
        [Tooltip("QueuePoint をいくつ作るか。")]
        public int QueuePointCount = 8;
        [Tooltip("並ぶ人の間隔 (m)。")]
        public float QueueSpacing = 0.75f;
    }
}
