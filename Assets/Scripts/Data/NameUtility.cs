using System.Text;

namespace Matsuri.Data
{
    /// <summary>
    /// 表記ゆれの吸収。プレイヤーが「たこ焼き」「たこやき」「タコ焼き」「takoyaki」の
    /// どれで書いても同じ屋台を指せるようにする (§13 / §43)。
    /// </summary>
    public static class NameUtility
    {
        /// <summary>比較用に正規化する。全角→半角、カタカナ→ひらがな、大文字→小文字、空白除去。</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                // 空白・記号のゆれを落とす
                if (c == ' ' || c == '　' || c == '_' || c == '-' || c == '　') continue;

                // 全角英数 → 半角
                if (c >= '０' && c <= '９') c = (char)(c - '０' + '0');
                else if (c >= 'Ａ' && c <= 'Ｚ') c = (char)(c - 'Ａ' + 'a');
                else if (c >= 'ａ' && c <= 'ｚ') c = (char)(c - 'ａ' + 'a');
                else if (c >= 'A' && c <= 'Z') c = (char)(c - 'A' + 'a');

                // カタカナ → ひらがな（長音符はそのまま）
                if (c >= 'ァ' && c <= 'ヶ') c = (char)(c - 'ァ' + 'ぁ');

                sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool Equals(string a, string b) => Normalize(a) == Normalize(b);

        /// <summary>
        /// レーベンシュタイン距離。「もしかして」候補 (§41) の並べ替えに使う。
        /// </summary>
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

        /// <summary>部分一致（補完候補の絞り込み用 §43）。</summary>
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
    }
}
