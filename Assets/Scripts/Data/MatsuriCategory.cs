namespace Matsuri.Data
{
    /// <summary>屋台のカテゴリ (§19)。</summary>
    public enum StallCategory
    {
        /// <summary>食べ物：たこ焼き・焼きそば・かき氷 など。</summary>
        Food,

        /// <summary>遊び：金魚すくい・射的 など。</summary>
        Game
    }

    /// <summary>設備の効果種別 (§20 / §34)。</summary>
    public enum FacilityEffect
    {
        /// <summary>ベンチ：休憩して Energy を回復する。</summary>
        Rest,

        /// <summary>ゴミ箱：会場の清潔さを保ち、満足度低下を防ぐ。</summary>
        Cleanliness,

        /// <summary>トイレ：長時間滞在の許容度を上げる。</summary>
        Relief,

        /// <summary>入り口：NPC のスポーン地点。</summary>
        Entrance,

        /// <summary>出口：NPC の退場地点。</summary>
        Exit,

        /// <summary>案内板：周囲の屋台の認知度を上げ、行列の偏りを減らす。</summary>
        Guidance,

        /// <summary>盆踊り場：やぐらを囲んで踊る。楽しさと満足度が大きく上がる。</summary>
        Dance,

        /// <summary>神社：参拝する。満足度が大きく上がる。</summary>
        Worship,

        /// <summary>手水舎など：小さな雰囲気づくり。小さく満足度が上がる。</summary>
        Purify
    }

    /// <summary>装飾の効果 (§34「装飾で満足度が上がる」)。</summary>
    public enum DecorationEffect
    {
        /// <summary>雰囲気：周囲のNPCの満足度をゆっくり上げる。</summary>
        Ambience,

        /// <summary>照明：周囲を明るくし、夜間の屋台の魅力を上げる。</summary>
        Lighting,

        /// <summary>誘導：会場の目印になり、遠くのNPCを引き寄せる。</summary>
        Landmark
    }
}
