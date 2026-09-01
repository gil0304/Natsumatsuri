using Matsuri.Script.Lexing;

namespace Matsuri.Script.Completion
{
    /// <summary>カーソルがどこにいるか (§43)。</summary>
    internal enum CaretPlace
    {
        /// <summary>行のあたま。命令のキーワードを出す。</summary>
        Statement,

        /// <summary>屋台などの { } の中。設定のキーワードを出す。</summary>
        Property,

        /// <summary>"…" の中。名前の一覧を出す。</summary>
        InString,

        /// <summary>「もし 」のあと。指標の一覧を出す。</summary>
        Condition,

        /// <summary>「もし たこ焼き.」のあと。屋台の指標を出す。</summary>
        StallMetric,

        /// <summary>コメントの中。何も出さない。</summary>
        Comment
    }

    /// <summary>
    /// カーソル位置の解析結果。文字列を舐めるだけの軽い処理で、
    /// タイプするたびに呼ばれても平気なようにしてある。
    /// </summary>
    internal sealed class CompletionContext
    {
        public CaretPlace Place;

        /// <summary>いま打ちかけの語。これで候補を絞る。</summary>
        public string Word = string.Empty;

        /// <summary>InString のとき、その文字列が誰のものか（屋台 / 装飾 / 設備 / イベント / 花火）。</summary>
        public KeywordKind StringOwner = KeywordKind.None;

        /// <summary>Property のとき、その { } が誰のものか。</summary>
        public KeywordKind BlockOwner = KeywordKind.None;

        public static CompletionContext Analyze(string source, int caretIndex)
        {
            var ctx = new CompletionContext();
            if (source == null) source = string.Empty;
            if (caretIndex < 0) caretIndex = 0;
            if (caretIndex > source.Length) caretIndex = source.Length;

            string before = source.Substring(0, caretIndex);

            int lineStart = before.LastIndexOf('\n') + 1;
            string line = before.Substring(lineStart);

            // コメントの中なら何も出さない
            int comment = FindCommentStart(line);
            if (comment >= 0)
            {
                ctx.Place = CaretPlace.Comment;
                return ctx;
            }

            // いま打ちかけの語
            ctx.Word = TrailingWord(line);

            // 文字列の中かどうか
            if (TryFindOpenString(line, out int quoteIndex))
            {
                ctx.Place = CaretPlace.InString;
                ctx.Word = line.Substring(quoteIndex + 1);
                ctx.StringOwner = WordBefore(line, quoteIndex);
                return ctx;
            }

            // 「たこ焼き.」の直後
            string trimmed = line.TrimEnd(' ', '\t', '　');
            if (trimmed.Length > 0 && (trimmed[trimmed.Length - 1] == '.' || trimmed[trimmed.Length - 1] == '．'))
            {
                ctx.Place = CaretPlace.StallMetric;
                ctx.Word = string.Empty;
                return ctx;
            }
            int dot = LastDotInWordRun(line);
            if (dot >= 0)
            {
                ctx.Place = CaretPlace.StallMetric;
                ctx.Word = line.Substring(dot + 1);
                return ctx;
            }

            // 「もし …」の途中
            var firstKeyword = FirstKeywordOfLine(line);
            if (firstKeyword == KeywordKind.If && HasSpaceAfterFirstWord(line))
            {
                ctx.Place = CaretPlace.Condition;
                return ctx;
            }

            // { } の中かどうか
            ctx.BlockOwner = EnclosingBlockOwner(before);
            bool insideBlock = ctx.BlockOwner != KeywordKind.None;

            if (insideBlock && IsEntityBlock(ctx.BlockOwner) && firstKeyword == KeywordKind.None)
            {
                ctx.Place = CaretPlace.Property;
                return ctx;
            }

            ctx.Place = CaretPlace.Statement;
            return ctx;
        }

        internal static bool IsEntityBlock(KeywordKind kind)
            => kind == KeywordKind.Stall || kind == KeywordKind.Decoration
            || kind == KeywordKind.Facility || kind == KeywordKind.Event
            || kind == KeywordKind.Fireworks || kind == KeywordKind.BonOdori || kind == KeywordKind.Taiko;

        // ── 文字列の解析 ─────────────────────────────────────────
        static int FindCommentStart(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = ScriptText.NormalizePunctuation(line[i]);
                if (c == '"' || line[i] == '「' || line[i] == '」') { inString = !inString; continue; }
                if (inString) continue;
                if (c == '#') return i;
                if (c == '/' && i + 1 < line.Length && ScriptText.NormalizePunctuation(line[i + 1]) == '/') return i;
            }
            return -1;
        }

        /// <summary>行の中で閉じられていない「"」があれば、その位置を返す。</summary>
        static bool TryFindOpenString(string line, out int quoteIndex)
        {
            quoteIndex = -1;
            bool open = false;
            for (int i = 0; i < line.Length; i++)
            {
                char raw = line[i];
                char c = ScriptText.NormalizePunctuation(raw);
                if (c == '"' || raw == '「' || raw == '『')
                {
                    if (!open) { open = true; quoteIndex = i; }
                    else if (c == '"') { open = false; quoteIndex = -1; }
                    continue;
                }
                if (raw == '」' || raw == '』')
                {
                    open = false;
                    quoteIndex = -1;
                }
            }
            return open;
        }

        /// <summary>引用符の手前にある語（「屋台」など）を返す。</summary>
        static KeywordKind WordBefore(string line, int index)
        {
            int i = index - 1;
            while (i >= 0 && (line[i] == ' ' || line[i] == '\t' || line[i] == '　')) i--;
            int end = i;
            while (i >= 0 && ScriptText.IsIdentifierStart(ScriptText.NormalizePunctuation(line[i]))) i--;
            if (end <= i) return KeywordKind.None;
            return MatsuriKeywords.Classify(line.Substring(i + 1, end - i));
        }

        /// <summary>カーソル直前の、打ちかけの語。</summary>
        static string TrailingWord(string line)
        {
            int i = line.Length - 1;
            while (i >= 0)
            {
                char c = ScriptText.NormalizePunctuation(line[i]);
                if (!ScriptText.IsIdentifierPart(c, true)) break;
                i--;
            }
            return line.Substring(i + 1);
        }

        /// <summary>「たこ焼き.待ち」のように、打ちかけの語の中に「.」があればその位置。</summary>
        static int LastDotInWordRun(string line)
        {
            int i = line.Length - 1;
            bool sawDot = false;
            int dotIndex = -1;
            while (i >= 0)
            {
                char c = ScriptText.NormalizePunctuation(line[i]);
                if (c == '.' && !sawDot) { sawDot = true; dotIndex = i; i--; continue; }
                if (!ScriptText.IsIdentifierPart(c, true)) break;
                i--;
            }
            // 「.」の手前に語があるときだけ有効
            if (!sawDot || dotIndex <= 0) return -1;
            char prev = ScriptText.NormalizePunctuation(line[dotIndex - 1]);
            return ScriptText.IsIdentifierStart(prev) ? dotIndex : -1;
        }

        static KeywordKind FirstKeywordOfLine(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t' || line[i] == '　')) i++;
            int start = i;
            while (i < line.Length && ScriptText.IsIdentifierStart(ScriptText.NormalizePunctuation(line[i]))) i++;
            if (i <= start) return KeywordKind.None;
            return MatsuriKeywords.Classify(line.Substring(start, i - start));
        }

        static bool HasSpaceAfterFirstWord(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t' || line[i] == '　')) i++;
            while (i < line.Length && ScriptText.IsIdentifierStart(ScriptText.NormalizePunctuation(line[i]))) i++;
            return i < line.Length;
        }

        /// <summary>いちばん内側の、閉じられていない「{」の持ち主を返す。</summary>
        static KeywordKind EnclosingBlockOwner(string before)
        {
            var stack = new System.Collections.Generic.Stack<int>();
            bool inString = false;
            bool inComment = false;

            for (int i = 0; i < before.Length; i++)
            {
                char raw = before[i];
                char c = ScriptText.NormalizePunctuation(raw);

                if (raw == '\n') { inComment = false; inString = false; continue; }
                if (inComment) continue;

                if (inString)
                {
                    if (c == '"' || raw == '」' || raw == '』') inString = false;
                    continue;
                }

                if (c == '"' || raw == '「' || raw == '『') { inString = true; continue; }
                if (c == '#' || (c == '/' && i + 1 < before.Length && ScriptText.NormalizePunctuation(before[i + 1]) == '/'))
                {
                    inComment = true;
                    continue;
                }

                if (c == '{') stack.Push(i);
                else if (c == '}' && stack.Count > 0) stack.Pop();
            }

            if (stack.Count == 0) return KeywordKind.None;

            int bracePos = stack.Peek();
            int lineStart = before.LastIndexOf('\n', bracePos > 0 ? bracePos - 1 : 0) + 1;
            string header = before.Substring(lineStart, bracePos - lineStart);
            return FirstKeywordOfLine(header);
        }
    }
}
