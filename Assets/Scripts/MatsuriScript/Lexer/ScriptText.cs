using System.Text;

namespace Matsuri.Script
{
    /// <summary>
    /// 言語処理系（Lexer / Parser / Validator / 補完）の中だけで使う文字ユーティリティ。
    ///
    /// 注意: ゲーム側の <c>Matsuri.Data.NameUtility</c> と役割は似ているが、
    /// Matsuri.Script アセンブリは Matsuri.Runtime を参照できないため、ここに独立して持つ。
    /// 挙動（正規化規則・レーベンシュタイン距離）は NameUtility と一致させてある。
    /// </summary>
    public static class ScriptText
    {
        /// <summary>
        /// IME で入力されがちな全角記号・全角英数を、半角の同等物に読み替える。
        /// 日本語（ひらがな・カタカナ・漢字）はそのまま返す。
        /// </summary>
        public static char NormalizePunctuation(char c)
        {
            switch (c)
            {
                case '　': return ' ';   // 全角スペース
                case '｛': return '{';
                case '｝': return '}';
                case '（': return '(';
                case '）': return ')';
                case '［': return '[';
                case '］': return ']';
                case '，': return ',';
                case '、': return ',';
                case '．': return '.';
                case '：': return ':';
                case '＞': return '>';
                case '＜': return '<';
                case '＝': return '=';
                case '！': return '!';
                case '＃': return '#';
                case '／': return '/';
                case '＆': return '&';
                case '｜': return '|';
                case '＋': return '+';
                case '−': return '-';   // 数学記号のマイナス
                case '－': return '-';       // 全角ハイフン
                case '―': return '-';        // ダッシュ
                case '”': return '"';
                case '“': return '"';
                case '＂': return '"';
                case '＊': return '*';
                case '％': return '%';
            }

            if (c >= '０' && c <= '９') return (char)(c - '０' + '0');
            if (c >= 'Ａ' && c <= 'Ｚ') return (char)(c - 'Ａ' + 'A');
            if (c >= 'ａ' && c <= 'ｚ') return (char)(c - 'ａ' + 'a');

            return c;
        }

        /// <summary>「≧」「≦」「≠」のような全角比較記号かどうか。</summary>
        public static bool IsWideCompare(char c) => c == '≧' || c == '≥' || c == '≦' || c == '≤' || c == '≠';

        public static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        public static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        /// <summary>ひらがな・カタカナ・漢字・長音符・半角カナ。</summary>
        public static bool IsJapanese(char c)
        {
            if (c == '\u3005' || c == '\u3006' || c == '\u30FC') return true;   // 々 〆 ー
            if (c >= '\u3041' && c <= '\u309F') return true;                    // ひらがな
            if (c >= '\u30A1' && c <= '\u30FE') return true;                    // カタカナ
            if (c >= '\u3400' && c <= '\u4DBF') return true;                    // 漢字拡張A
            if (c >= '\u4E00' && c <= '\u9FFF') return true;                    // 漢字
            if (c >= '\uF900' && c <= '\uFAFF') return true;                    // 互換漢字
            if (c >= '\uFF66' && c <= '\uFF9D') return true;                    // 半角カナ
            return false;
        }

        /// <summary>識別子の1文字目になれるか。</summary>
        public static bool IsIdentifierStart(char c)
            => IsAsciiLetter(c) || c == '_' || IsJapanese(c);

        /// <summary>識別子の2文字目以降になれるか。ASCII で始まった識別子だけ数字を含められる。</summary>
        public static bool IsIdentifierPart(char c, bool startedWithAscii)
        {
            if (IsIdentifierStart(c)) return true;
            if (startedWithAscii && (IsAsciiDigit(c) || c == '_')) return true;
            return false;
        }

        /// <summary>
        /// 名前比較用の正規化。全角→半角、カタカナ→ひらがな、大文字→小文字、空白と区切り記号を除去。
        /// 「たこ焼き」「タコヤキ」「takoyaki」を同一視するために使う。
        /// </summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '　' || c == '_' || c == '-' || c == '\t') continue;

                if (c >= '０' && c <= '９') c = (char)(c - '０' + '0');
                else if (c >= 'Ａ' && c <= 'Ｚ') c = (char)(c - 'Ａ' + 'a');
                else if (c >= 'ａ' && c <= 'ｚ') c = (char)(c - 'ａ' + 'a');
                else if (c >= 'A' && c <= 'Z') c = (char)(c - 'A' + 'a');

                if (c >= 'ァ' && c <= 'ヶ') c = (char)(c - 'ァ' + 'ぁ');   // カタカナ→ひらがな

                sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool NameEquals(string a, string b) => Normalize(a) == Normalize(b);

        public static bool StartsWith(string candidate, string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return true;
            return Normalize(candidate).StartsWith(Normalize(prefix), System.StringComparison.Ordinal);
        }

        public static bool Contains(string candidate, string part)
        {
            if (string.IsNullOrEmpty(part)) return true;
            return Normalize(candidate).Contains(Normalize(part));
        }

        /// <summary>レーベンシュタイン距離。「もしかして」候補の並べ替えに使う。</summary>
        public static int Distance(string a, string b)
        {
            a = Normalize(a);
            b = Normalize(b);
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int del = prev[j] + 1;
                    int ins = cur[j - 1] + 1;
                    int sub = prev[j - 1] + cost;
                    cur[j] = del < ins ? (del < sub ? del : sub) : (ins < sub ? ins : sub);
                }
                var t = prev; prev = cur; cur = t;
            }
            return prev[b.Length];
        }

        /// <summary>「1,250,000」のような桁区切り。エラーメッセージ用。</summary>
        public static string Yen(long amount) => amount.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>分を "19:00" 形式に整形する。</summary>
        public static string ClockText(int minutesOfDay)
        {
            int h = minutesOfDay / 60;
            int m = minutesOfDay % 60;
            return h.ToString("00") + ":" + m.ToString("00");
        }
    }
}
