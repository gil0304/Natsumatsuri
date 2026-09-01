using System.Collections.Generic;
using Matsuri.Script.Commands;

namespace Matsuri.Script.Lexing
{
    /// <summary>言語のキーワード種別。日本語と英語のどちらで書かれても同じ値になる。</summary>
    public enum KeywordKind
    {
        None,
        Festival,
        Stall,
        Decoration,
        Facility,
        Event,
        If,
        Time,
        Fireworks,
        BonOdori,
        Taiko,
        And,
        Or,
        Position,
        Price,
        Rotation,
        Name
    }

    /// <summary>
    /// 予約語の辞書 (§11 / §13)。日本語・英語・表記ゆれをすべてここに集約する。
    /// Lexer（くっつき分割）・Parser（構文判定）・補完 (§43) が共有する。
    /// </summary>
    public static class MatsuriKeywords
    {
        // ── 文のキーワード ──────────────────────────────────────────
        static readonly Dictionary<string, KeywordKind> Table = new Dictionary<string, KeywordKind>();

        /// <summary>「もし来場者数」のようにくっついて書かれたときに切り離す接頭辞。長い順。</summary>
        public static readonly string[] SplitPrefixes =
        {
            "盆踊り", "盆おどり", "イベント", "装飾", "設備", "屋台", "祭り",
            "もし", "時間", "時刻", "花火", "太鼓", "場所", "値段", "向き", "名前"
        };

        /// <summary>補完 (§43) に出す代表的なキーワード。表示は日本語。</summary>
        public static readonly string[] StatementKeywords =
        {
            "祭り", "屋台", "装飾", "設備", "イベント", "もし", "時間", "花火", "盆踊り", "太鼓"
        };

        /// <summary>屋台ブロックの中に書ける設定。</summary>
        public static readonly string[] PropertyKeywords = { "場所", "値段", "向き", "名前" };

        /// <summary>条件に書ける指標の代表表記 (§14 / §16 / §17)。</summary>
        public static readonly string[] MetricKeywords =
        {
            "来場者数", "現在の来場者", "売上", "予算", "満足度", "時刻"
        };

        /// <summary>「たこ焼き.○○」の○○に書ける指標。</summary>
        public static readonly string[] StallMetricKeywords = { "待ち人数", "売上", "軒数" };

        static MatsuriKeywords()
        {
            Add(KeywordKind.Festival,   "祭り", "祭", "まつり", "festival", "matsuri");
            Add(KeywordKind.Stall,      "屋台", "やたい", "stall", "shop", "booth");
            Add(KeywordKind.Decoration, "装飾", "飾り", "かざり", "decoration", "decor");
            Add(KeywordKind.Facility,   "設備", "施設", "facility", "amenity");
            Add(KeywordKind.Event,      "イベント", "催し", "event");
            Add(KeywordKind.If,         "もし", "もしも", "if");
            Add(KeywordKind.Time,       "時間", "時刻", "time", "at");
            Add(KeywordKind.Fireworks,  "花火", "はなび", "fireworks", "firework");
            Add(KeywordKind.BonOdori,   "盆踊り", "盆おどり", "ぼんおどり", "踊り", "bonodori", "bon_odori", "bon odori");
            Add(KeywordKind.Taiko,      "太鼓", "たいこ", "和太鼓", "taiko", "drum");
            Add(KeywordKind.And,        "かつ", "そして", "and", "&&");
            Add(KeywordKind.Or,         "または", "もしくは", "or", "||");
            Add(KeywordKind.Position,   "場所", "位置", "座標", "position", "pos", "place");
            Add(KeywordKind.Price,      "値段", "価格", "料金", "price", "cost");
            Add(KeywordKind.Rotation,   "向き", "回転", "角度", "rotation", "rotate", "angle");
            Add(KeywordKind.Name,       "名前", "表示名", "name", "label");
        }

        static void Add(KeywordKind kind, params string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                string key = ScriptText.Normalize(words[i]);
                if (!Table.ContainsKey(key)) Table[key] = kind;
            }
        }

        /// <summary>書かれた語がどのキーワードか。予約語でなければ None。</summary>
        public static KeywordKind Classify(string text)
        {
            if (string.IsNullOrEmpty(text)) return KeywordKind.None;
            return Table.TryGetValue(ScriptText.Normalize(text), out var kind) ? kind : KeywordKind.None;
        }

        public static bool IsKeyword(string text) => Classify(text) != KeywordKind.None;

        /// <summary>PropertyNode.Keyword に入れる正規表記（日本語）。</summary>
        public static string CanonicalProperty(KeywordKind kind) => kind switch
        {
            KeywordKind.Position => "場所",
            KeywordKind.Price    => "値段",
            KeywordKind.Rotation => "向き",
            KeywordKind.Name     => "名前",
            _ => null
        };

        /// <summary>キーワード種別 → 表示用の日本語。エラーメッセージで使う。</summary>
        public static string Display(KeywordKind kind) => kind switch
        {
            KeywordKind.Festival   => "祭り",
            KeywordKind.Stall      => "屋台",
            KeywordKind.Decoration => "装飾",
            KeywordKind.Facility   => "設備",
            KeywordKind.Event      => "イベント",
            KeywordKind.If         => "もし",
            KeywordKind.Time       => "時間",
            KeywordKind.Fireworks  => "花火",
            KeywordKind.BonOdori   => "盆踊り",
            KeywordKind.Taiko      => "太鼓",
            KeywordKind.And        => "かつ",
            KeywordKind.Or         => "または",
            KeywordKind.Position   => "場所",
            KeywordKind.Price      => "値段",
            KeywordKind.Rotation   => "向き",
            KeywordKind.Name       => "名前",
            _ => "?"
        };

        // ── 指標 (§14 / §16 / §17) ────────────────────────────────────
        static readonly Dictionary<string, MetricKind> GlobalMetrics = new Dictionary<string, MetricKind>
        {
            { ScriptText.Normalize("来場者数"),       MetricKind.Visitors },
            { ScriptText.Normalize("来場者"),         MetricKind.Visitors },
            { ScriptText.Normalize("客数"),           MetricKind.Visitors },
            { ScriptText.Normalize("お客さん"),       MetricKind.Visitors },
            { ScriptText.Normalize("visitors"),       MetricKind.Visitors },
            { ScriptText.Normalize("visitor_count"),  MetricKind.Visitors },
            { ScriptText.Normalize("現在の来場者"),   MetricKind.CurrentVisitors },
            { ScriptText.Normalize("現在の来場者数"), MetricKind.CurrentVisitors },
            { ScriptText.Normalize("今の来場者"),     MetricKind.CurrentVisitors },
            { ScriptText.Normalize("current_visitors"), MetricKind.CurrentVisitors },
            { ScriptText.Normalize("売上"),           MetricKind.Revenue },
            { ScriptText.Normalize("売り上げ"),       MetricKind.Revenue },
            { ScriptText.Normalize("revenue"),        MetricKind.Revenue },
            { ScriptText.Normalize("sales"),          MetricKind.Revenue },
            { ScriptText.Normalize("予算"),           MetricKind.Budget },
            { ScriptText.Normalize("残り予算"),       MetricKind.Budget },
            { ScriptText.Normalize("budget"),         MetricKind.Budget },
            { ScriptText.Normalize("満足度"),         MetricKind.Satisfaction },
            { ScriptText.Normalize("satisfaction"),   MetricKind.Satisfaction },
            { ScriptText.Normalize("時刻"),           MetricKind.Clock },
            { ScriptText.Normalize("時間"),           MetricKind.Clock },
            { ScriptText.Normalize("clock"),          MetricKind.Clock },
            { ScriptText.Normalize("time"),           MetricKind.Clock },
        };

        static readonly Dictionary<string, MetricKind> StallMetrics = new Dictionary<string, MetricKind>
        {
            { ScriptText.Normalize("待ち人数"),   MetricKind.StallQueue },
            { ScriptText.Normalize("待ち"),       MetricKind.StallQueue },
            { ScriptText.Normalize("行列"),       MetricKind.StallQueue },
            { ScriptText.Normalize("queue"),      MetricKind.StallQueue },
            { ScriptText.Normalize("queue_length"), MetricKind.StallQueue },
            { ScriptText.Normalize("売上"),       MetricKind.StallRevenue },
            { ScriptText.Normalize("売り上げ"),   MetricKind.StallRevenue },
            { ScriptText.Normalize("revenue"),    MetricKind.StallRevenue },
            { ScriptText.Normalize("軒数"),       MetricKind.StallCount },
            { ScriptText.Normalize("数"),         MetricKind.StallCount },
            { ScriptText.Normalize("個数"),       MetricKind.StallCount },
            { ScriptText.Normalize("count"),      MetricKind.StallCount },
        };

        /// <summary>「来場者数」「売上」のような、祭り全体の指標を引く。</summary>
        public static bool TryGlobalMetric(string text, out MetricKind kind)
            => GlobalMetrics.TryGetValue(ScriptText.Normalize(text ?? ""), out kind);

        /// <summary>「たこ焼き.待ち人数」の後半を引く。</summary>
        public static bool TryStallMetric(string text, out MetricKind kind)
            => StallMetrics.TryGetValue(ScriptText.Normalize(text ?? ""), out kind);

        /// <summary>指標名のタイプミスに対する「もしかして」候補 (§41)。</summary>
        public static IReadOnlyList<string> SuggestMetrics(string written, int count = 3)
        {
            var pool = new List<string>(MetricKeywords);
            pool.AddRange(StallMetricKeywords);
            var scored = new List<KeyValuePair<int, string>>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
                scored.Add(new KeyValuePair<int, string>(ScriptText.Distance(written ?? "", pool[i]), pool[i]));
            scored.Sort((a, b) => a.Key.CompareTo(b.Key));

            var result = new List<string>();
            for (int i = 0; i < scored.Count && result.Count < count; i++)
            {
                if (scored[i].Key > 2) break;   // 遠すぎる候補は出さない（かえって迷う）
                result.Add(scored[i].Value);
            }
            return result;
        }

        /// <summary>花火の種類 (§61)。表示名 → 正規ID。</summary>
        public static readonly string[] FireworkKindNames = { "菊", "牡丹", "柳", "ハート", "大玉", "スペシャル" };

        /// <summary>書かれた花火の種類名を正規IDに直す。知らない名前なら false。</summary>
        public static bool TryFireworkKind(string written, out string id)
        {
            id = MatsuriIds.FireworkKiku;
            string n = ScriptText.Normalize(written ?? "");
            if (n.Length == 0) return true;   // 種類の指定なし＝菊

            if (n.Contains(ScriptText.Normalize("大玉")) || n.Contains("oodama")) { id = MatsuriIds.FireworkOodama; return true; }
            if (n.Contains(ScriptText.Normalize("スペシャル")) || n.Contains("special")) { id = MatsuriIds.FireworkSpecial; return true; }
            if (n.Contains(ScriptText.Normalize("牡丹")) || n.Contains(ScriptText.Normalize("ぼたん")) || n.Contains("botan")) { id = MatsuriIds.FireworkBotan; return true; }
            if (n.Contains(ScriptText.Normalize("柳")) || n.Contains(ScriptText.Normalize("やなぎ")) || n.Contains("yanagi")) { id = MatsuriIds.FireworkYanagi; return true; }
            if (n.Contains(ScriptText.Normalize("ハート")) || n.Contains("heart")) { id = MatsuriIds.FireworkHeart; return true; }
            if (n.Contains(ScriptText.Normalize("菊")) || n.Contains("kiku")) { id = MatsuriIds.FireworkKiku; return true; }
            return false;
        }

        /// <summary>知らない種類名でも既定（菊）を返す版。</summary>
        public static string ResolveFireworkKind(string written)
        {
            TryFireworkKind(written, out string id);
            return id;
        }
    }
}
