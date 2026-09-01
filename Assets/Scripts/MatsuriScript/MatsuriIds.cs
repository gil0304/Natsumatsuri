namespace Matsuri.Script
{
    /// <summary>
    /// 全システムで共有される正規ID。表示名（日本語）とは別に、
    /// データ・セーブ・スコア集計はすべてこのIDで行う。
    /// 仕様書 §19 / §20 / §21 / §22 に対応。
    /// </summary>
    public static class MatsuriIds
    {
        // ── 屋台：食べ物 (§19) ────────────────────────────────
        public const string Takoyaki      = "takoyaki";        // たこ焼き
        public const string Yakisoba      = "yakisoba";        // 焼きそば
        public const string Kakigori      = "kakigori";        // かき氷
        public const string RingoAme      = "ringo_ame";       // りんご飴
        public const string Wataame       = "wataame";         // わたあめ
        public const string Frankfurt     = "frankfurt";       // フランクフルト

        // ── 屋台：遊び (§19) ──────────────────────────────────
        public const string Kingyosukui   = "kingyosukui";     // 金魚すくい
        public const string Shateki       = "shateki";         // 射的
        public const string YoyoTsuri     = "yoyo_tsuri";      // ヨーヨー釣り
        public const string SuperBall     = "superball";       // スーパーボールすくい
        public const string Katanuki      = "katanuki";        // 型抜き

        // ── 設備 (§20) ────────────────────────────────────────
        public const string Bench         = "bench";           // ベンチ
        public const string TrashCan      = "trashcan";        // ゴミ箱
        public const string Toilet        = "toilet";          // トイレ
        public const string Entrance      = "entrance";        // 入り口
        public const string Exit          = "exit";            // 出口
        public const string SignBoard     = "signboard";       // 案内板

        // ── 装飾 (§21) ────────────────────────────────────────
        public const string Lantern       = "lantern";         // 提灯
        public const string Nobori        = "nobori";          // のぼり
        public const string Shrine        = "shrine";          // 神社
        public const string Torii         = "torii";           // 鳥居
        public const string Tree          = "tree";            // 木
        public const string StallLight    = "stall_light";     // 屋台用ライト
        public const string FestivalSign  = "festival_sign";   // 夏祭り看板

        // ── イベント (§22) ────────────────────────────────────
        public const string Fireworks     = "fireworks";       // 花火
        public const string BonOdori      = "bon_odori";       // 盆踊り
        public const string Taiko         = "taiko";           // 太鼓演奏

        // ── 花火の種類 (§61) ─────────────────────────────────
        public const string FireworkKiku    = "kiku";          // 菊
        public const string FireworkBotan   = "botan";         // 牡丹
        public const string FireworkYanagi  = "yanagi";        // 柳
        public const string FireworkHeart   = "heart";         // ハート
        public const string FireworkSpecial = "special";       // スペシャル
        public const string FireworkOodama  = "oodama";        // 大玉
    }
}
