using System.Collections.Generic;
using System.Text;
using Matsuri.Script;

namespace Matsuri.UI
{
    /// <summary>
    /// Matsuri Script のシンタックスハイライト (§10)。
    ///
    /// 重要な制約:
    ///   入力層 (TextField) と表示層 (Label) を重ね合わせて描画するため、
    ///   <b>描画される文字の並び・改行位置を元テキストから一切変えてはならない</b>。
    ///   色付けは &lt;color=#RRGGBB&gt; タグでのみ行い、文字は追加も削除もしない。
    ///
    /// リッチテキストの記号エスケープは <see cref="EscapeRichText"/> に集約する。
    /// 入力層はリッチテキスト解釈を行わないため恒等変換であり、
    /// 表示層のみがこの関数を通る、という対応関係で管理する。
    /// </summary>
    public static class MatsuriSyntaxHighlighter
    {
        // ── 語彙 ───────────────────────────────────────────────

        static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "祭り", "祭", "屋台", "装飾", "設備", "イベント",
            "もし", "そうでなければ", "時間", "毎回", "一度だけ",
            "花火", "盆踊り", "太鼓",
            "かつ", "または", "以上", "以下", "より大きい", "より小さい",
            "matsuri", "festival", "stall", "decoration", "facility", "event",
            "if", "else", "when", "time", "fireworks", "bonodori", "taiko",
            "and", "or", "once", "every"
        };

        static readonly HashSet<string> Properties = new HashSet<string>
        {
            "場所", "値段", "価格", "向き", "名前", "種類", "色", "大きさ", "数",
            "position", "price", "rotation", "name", "kind", "color", "size", "count"
        };

        static readonly HashSet<string> Metrics = new HashSet<string>
        {
            "来場者数", "現在来場者数", "売上", "予算", "満足度", "平均満足度",
            "待ち人数", "行列", "軒数", "時刻",
            "visitors", "currentvisitors", "revenue", "budget",
            "satisfaction", "queue", "clock", "stallcount"
        };

        // ── 公開API ────────────────────────────────────────────

        /// <summary>ソースをリッチテキストに変換する。診断があればその範囲を赤くする。</summary>
        public static string ToRichText(string source, IReadOnlyList<Diagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            var errorRanges = BuildErrorRanges(source, diagnostics);
            var sb = new StringBuilder(source.Length + 256);

            int i = 0;
            int n = source.Length;
            while (i < n)
            {
                char c = source[i];

                // 改行はそのまま
                if (c == '\n' || c == '\r')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                // 空白はそのまま（タグを挟まないほうが安全）
                if (c == ' ' || c == '\t' || c == '　')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                int start = i;
                string color;

                if (IsCommentStart(source, i, out int commentSkip))
                {
                    i += commentSkip;
                    while (i < n && source[i] != '\n' && source[i] != '\r') i++;
                    color = MatsuriUiTheme.SynComment;
                }
                else if (c == '"' || c == '“' || c == '「' || c == '\'')
                {
                    char close = c == '「' ? '」' : (c == '“' ? '”' : c);
                    i++;
                    while (i < n && source[i] != close && source[i] != '\n' && source[i] != '\r') i++;
                    if (i < n && source[i] == close) i++;
                    color = MatsuriUiTheme.SynString;
                }
                else if (IsDigit(c) || (c == '-' && i + 1 < n && IsDigit(source[i + 1])))
                {
                    if (c == '-') i++;
                    while (i < n && IsDigit(source[i])) i++;

                    // 時刻 ("20:00" / "20：00")
                    if (i < n && (source[i] == ':' || source[i] == '：')
                        && i + 1 < n && IsDigit(source[i + 1]))
                    {
                        i++;
                        while (i < n && IsDigit(source[i])) i++;
                        color = MatsuriUiTheme.SynTime;
                    }
                    else
                    {
                        if (i < n && (source[i] == '.' || source[i] == '．') && i + 1 < n && IsDigit(source[i + 1]))
                        {
                            i++;
                            while (i < n && IsDigit(source[i])) i++;
                        }
                        // 「500円」「3個」のような単位も数値の一部として扱う
                        while (i < n && (source[i] == '円' || source[i] == '個' || source[i] == '軒' || source[i] == '人')) i++;
                        color = MatsuriUiTheme.SynNumber;
                    }
                }
                else if (IsWordChar(c))
                {
                    while (i < n && IsWordChar(source[i])) i++;
                    string word = source.Substring(start, i - start);
                    color = ClassifyWord(word);
                }
                else
                {
                    i++;
                    color = MatsuriUiTheme.SynDefault;
                }

                if (OverlapsError(errorRanges, start, i)) color = MatsuriUiTheme.SynError;

                sb.Append("<color=").Append(color).Append('>');
                AppendEscaped(sb, source, start, i - start);
                sb.Append("</color>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// リッチテキストとして解釈されうる記号を無害化する。
        /// 描画される文字数を変えないため、タグに化けそうな '&lt;' だけを noparse で包む。
        /// （'&lt;' の直後が空白・数字・'=' の場合、テキスト生成側はそのまま文字として描く）
        /// </summary>
        public static string EscapeRichText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;
            var sb = new StringBuilder(raw.Length + 16);
            AppendEscaped(sb, raw, 0, raw.Length);
            return sb.ToString();
        }

        // ── 内部 ───────────────────────────────────────────────

        static void AppendEscaped(StringBuilder sb, string s, int start, int length)
        {
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                char c = s[i];
                if (c == '<')
                {
                    char next = (i + 1 < s.Length) ? s[i + 1] : '\0';
                    bool tagLike = next == '/' || next == '#'
                                   || (next >= 'a' && next <= 'z')
                                   || (next >= 'A' && next <= 'Z');
                    if (tagLike) sb.Append("<noparse><</noparse>");
                    else sb.Append('<');
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        static string ClassifyWord(string word)
        {
            if (Keywords.Contains(word)) return MatsuriUiTheme.SynKeyword;
            if (Metrics.Contains(word)) return MatsuriUiTheme.SynMetric;
            if (Properties.Contains(word)) return MatsuriUiTheme.SynProperty;

            string lower = word.ToLowerInvariant();
            if (Keywords.Contains(lower)) return MatsuriUiTheme.SynKeyword;
            if (Metrics.Contains(lower)) return MatsuriUiTheme.SynMetric;
            if (Properties.Contains(lower)) return MatsuriUiTheme.SynProperty;

            return MatsuriUiTheme.SynDefault;
        }

        static bool IsCommentStart(string s, int i, out int skip)
        {
            skip = 0;
            char c = s[i];
            if (c == '#' || c == '＃' || c == '※')   // # ＃ ※
            {
                skip = 1;
                return true;
            }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                skip = 2;
                return true;
            }
            return false;
        }

        static bool IsDigit(char c) => (c >= '0' && c <= '9') || (c >= '０' && c <= '９');

        static bool IsWordChar(char c)
        {
            if (c == '_') return true;
            if (c == '.' || c == '．') return false;
            return char.IsLetterOrDigit(c);
        }

        // ── 診断範囲 ───────────────────────────────────────────

        readonly struct Range
        {
            public readonly int Start;
            public readonly int End;
            public Range(int start, int end) { Start = start; End = end; }
        }

        static List<Range> BuildErrorRanges(string source, IReadOnlyList<Diagnostic> diagnostics)
        {
            var list = new List<Range>();
            if (diagnostics == null || diagnostics.Count == 0) return list;

            var lineStarts = new List<int> { 0 };
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == '\n') lineStarts.Add(i + 1);
            }

            for (int d = 0; d < diagnostics.Count; d++)
            {
                var diag = diagnostics[d];
                if (diag == null || diag.Severity != DiagnosticSeverity.Error) continue;

                int lineIndex = diag.Line - 1;
                if (lineIndex < 0 || lineIndex >= lineStarts.Count) continue;

                int lineStart = lineStarts[lineIndex];
                int lineEnd = (lineIndex + 1 < lineStarts.Count) ? lineStarts[lineIndex + 1] - 1 : source.Length;
                if (lineEnd > source.Length) lineEnd = source.Length;

                int start = lineStart + (diag.Column - 1);
                if (start < lineStart) start = lineStart;
                if (start > lineEnd) start = lineEnd;

                int end = diag.Length > 0 ? start + diag.Length : lineEnd;
                if (end > lineEnd) end = lineEnd;
                if (end <= start) continue;

                list.Add(new Range(start, end));
            }
            return list;
        }

        static bool OverlapsError(List<Range> ranges, int start, int end)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                if (start < ranges[i].End && end > ranges[i].Start) return true;
            }
            return false;
        }
    }
}
