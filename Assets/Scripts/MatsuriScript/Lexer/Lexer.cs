using System.Collections.Generic;
using System.Text;

namespace Matsuri.Script.Lexing
{
    /// <summary>
    /// Matsuri Script の字句解析 (§51)。
    ///
    /// 方針:
    ///  - 日本語（ひらがな・カタカナ・漢字）の連なりは1トークンにする。
    ///  - IME で入りがちな全角記号 ｛｝，＞＜．”“「」： は半角と同じに扱う (§13)。
    ///  - 「19:00」は Time トークン。「Number : Number」とは区別する (§15)。
    ///  - 読めない文字でも止まらない。Unknown トークンを置き、日本語の Diagnostic を足して続ける (§41)。
    /// </summary>
    public static class Lexer
    {
        public static List<Token> Tokenize(string source, List<Diagnostic> diagnostics)
        {
            var tokens = new List<Token>();
            var diags = diagnostics ?? new List<Diagnostic>();
            if (source == null) source = string.Empty;

            int i = 0;
            int line = 1;
            int lineStart = 0;
            bool warnedAboutParen = false;

            while (i < source.Length)
            {
                char raw = source[i];
                char c = ScriptText.NormalizePunctuation(raw);
                int col = i - lineStart + 1;

                // ── 改行と空白 ────────────────────────────────
                if (c == '\r') { i++; continue; }
                if (c == '\n')
                {
                    tokens.Add(new Token(TokenType.Newline, "\n", 0, line, col, 1));
                    i++; line++; lineStart = i; warnedAboutParen = false;
                    continue;
                }
                if (c == ' ' || c == '\t') { i++; continue; }

                // 「；」「;」は文の区切りとして改行と同じ扱いにする
                if (c == ';' || raw == '；')
                {
                    tokens.Add(new Token(TokenType.Newline, ";", 0, line, col, 1));
                    i++; continue;
                }

                // ── コメント (§11) ────────────────────────────
                if (c == '#' || (c == '/' && i + 1 < source.Length && ScriptText.NormalizePunctuation(source[i + 1]) == '/'))
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    continue;
                }

                // ── 文字列 ────────────────────────────────────
                if (c == '"' || raw == '「' || raw == '『')
                {
                    i = ReadString(source, i, raw, line, col, ref lineStart, tokens, diags);
                    continue;
                }

                // ── 数値・時刻 ────────────────────────────────
                if (ScriptText.IsAsciiDigit(c) ||
                    (c == '-' && i + 1 < source.Length && ScriptText.IsAsciiDigit(ScriptText.NormalizePunctuation(source[i + 1]))))
                {
                    i = ReadNumberOrTime(source, i, line, col, tokens, diags);
                    continue;
                }

                // ── 識別子・キーワード ────────────────────────
                if (ScriptText.IsIdentifierStart(c))
                {
                    i = ReadIdentifier(source, i, line, col, tokens);
                    continue;
                }

                // ── 記号 ──────────────────────────────────────
                switch (c)
                {
                    case '{':
                        tokens.Add(new Token(TokenType.LBrace, "{", 0, line, col, 1)); i++; continue;
                    case '}':
                        tokens.Add(new Token(TokenType.RBrace, "}", 0, line, col, 1)); i++; continue;
                    case ',':
                        tokens.Add(new Token(TokenType.Comma, ",", 0, line, col, 1)); i++; continue;
                    case ':':
                        tokens.Add(new Token(TokenType.Colon, ":", 0, line, col, 1)); i++; continue;
                    case '.':
                        tokens.Add(new Token(TokenType.Dot, ".", 0, line, col, 1)); i++; continue;
                    case '>':
                        if (Next(source, i) == '=')
                        {
                            tokens.Add(new Token(TokenType.GreaterEqual, ">=", 0, line, col, 2)); i += 2;
                        }
                        else
                        {
                            tokens.Add(new Token(TokenType.Greater, ">", 0, line, col, 1)); i++;
                        }
                        continue;
                    case '<':
                        if (Next(source, i) == '=')
                        {
                            tokens.Add(new Token(TokenType.LessEqual, "<=", 0, line, col, 2)); i += 2;
                        }
                        else if (Next(source, i) == '>')
                        {
                            tokens.Add(new Token(TokenType.NotEqual, "<>", 0, line, col, 2)); i += 2;
                        }
                        else
                        {
                            tokens.Add(new Token(TokenType.Less, "<", 0, line, col, 1)); i++;
                        }
                        continue;
                    case '=':
                        if (Next(source, i) == '=')
                        {
                            tokens.Add(new Token(TokenType.EqualEqual, "==", 0, line, col, 2)); i += 2;
                        }
                        else
                        {
                            // 「== のつもりで = と書いた」を親切に受け取る
                            tokens.Add(new Token(TokenType.EqualEqual, "=", 0, line, col, 1)); i++;
                        }
                        continue;
                    case '!':
                        if (Next(source, i) == '=')
                        {
                            tokens.Add(new Token(TokenType.NotEqual, "!=", 0, line, col, 2)); i += 2;
                            continue;
                        }
                        break;
                    case '&':
                        if (Next(source, i) == '&')
                        {
                            tokens.Add(new Token(TokenType.And, "&&", 0, line, col, 2)); i += 2;
                            continue;
                        }
                        break;
                    case '|':
                        if (Next(source, i) == '|')
                        {
                            tokens.Add(new Token(TokenType.Or, "||", 0, line, col, 2)); i += 2;
                            continue;
                        }
                        break;
                    case '(':
                    case ')':
                        if (!warnedAboutParen)
                        {
                            warnedAboutParen = true;
                            diags.Add(Diagnostic.Warning(line, col, 1,
                                "かっこ ( ) は Matsuri Script では使いません。条件は「かつ」「または」でつなげてください。",
                                "もし 来場者数 > 300 かつ 売上 > 100000 {\n}"));
                        }
                        i++;
                        continue;
                }

                // 全角の比較記号 ≧ ≦ ≠
                if (ScriptText.IsWideCompare(raw))
                {
                    TokenType t = (raw == '≧' || raw == '≥') ? TokenType.GreaterEqual
                                : (raw == '≦' || raw == '≤') ? TokenType.LessEqual
                                : TokenType.NotEqual;
                    tokens.Add(new Token(t, raw.ToString(), 0, line, col, 1));
                    i++;
                    continue;
                }

                // 句点は「文の終わり」として親切に読み飛ばす
                if (raw == '。')
                {
                    diags.Add(Diagnostic.Warning(line, col, 1,
                        "行の終わりに「。」は書きません。消してしまって大丈夫です。",
                        "屋台 \"たこ焼き\" { 場所 5, 5 }"));
                    i++;
                    continue;
                }

                // ── 読めない文字 ──────────────────────────────
                tokens.Add(new Token(TokenType.Unknown, raw.ToString(), 0, line, col, 1));
                diags.Add(Diagnostic.Error(line, col, 1,
                    $"「{raw}」はここでは使えない文字です。消すか、別の書き方にしてください。",
                    "屋台 \"たこ焼き\" {\n    場所 5, 5\n}"));
                i++;
            }

            tokens.Add(Token.EndOfFile(line, i - lineStart + 1));
            return tokens;
        }

        static char Next(string source, int i)
            => i + 1 < source.Length ? ScriptText.NormalizePunctuation(source[i + 1]) : '\0';

        // ── 文字列 ────────────────────────────────────────────────
        static int ReadString(string source, int start, char openRaw, int line, int col,
            ref int lineStart, List<Token> tokens, List<Diagnostic> diags)
        {
            char closeA, closeB;
            if (openRaw == '「') { closeA = '」'; closeB = '」'; }
            else if (openRaw == '『') { closeA = '』'; closeB = '』'; }
            else { closeA = '"'; closeB = '"'; }

            int i = start + 1;
            var sb = new StringBuilder();
            bool closed = false;

            while (i < source.Length)
            {
                char raw = source[i];
                if (raw == '\n') break;                       // 閉じ忘れは行をまたがせない

                char norm = ScriptText.NormalizePunctuation(raw);
                bool isClose = (closeA == '"') ? (norm == '"') : (raw == closeA || raw == closeB);
                if (isClose) { i++; closed = true; break; }

                sb.Append(raw);
                i++;
            }

            int length = i - start;
            tokens.Add(new Token(TokenType.String, sb.ToString(), 0, line, col, length));

            if (!closed)
            {
                diags.Add(Diagnostic.Error(line, col, length,
                    $"文字列「{sb}」が閉じられていません。終わりにも \" を書いてください。",
                    "屋台 \"たこ焼き\" {\n    場所 5, 5\n}"));
            }
            return i;
        }

        // ── 数値と時刻 ────────────────────────────────────────────
        static int ReadNumberOrTime(string source, int start, int line, int col,
            List<Token> tokens, List<Diagnostic> diags)
        {
            int i = start;
            var sb = new StringBuilder();

            if (ScriptText.NormalizePunctuation(source[i]) == '-')
            {
                sb.Append('-');
                i++;
            }

            while (i < source.Length && ScriptText.IsAsciiDigit(ScriptText.NormalizePunctuation(source[i])))
            {
                sb.Append(ScriptText.NormalizePunctuation(source[i]));
                i++;
            }

            // 「19:00」→ Time。「:」の直後が数字のときだけ時刻として読む (§15)。
            if (i + 1 < source.Length
                && ScriptText.NormalizePunctuation(source[i]) == ':'
                && ScriptText.IsAsciiDigit(ScriptText.NormalizePunctuation(source[i + 1])))
            {
                var minutes = new StringBuilder();
                int j = i + 1;
                while (j < source.Length && ScriptText.IsAsciiDigit(ScriptText.NormalizePunctuation(source[j])))
                {
                    minutes.Append(ScriptText.NormalizePunctuation(source[j]));
                    j++;
                }

                int.TryParse(sb.ToString(), out int hour);
                int.TryParse(minutes.ToString(), out int minute);
                string text = source.Substring(start, j - start);
                int length = j - start;

                if (minute > 59 || hour > 47 || hour < 0)
                {
                    diags.Add(Diagnostic.Error(line, col, length,
                        $"時刻「{text}」が読めません。時は 0〜23、分は 0〜59 で書いてください。",
                        "時間 19:00 {\n    花火 \"大玉\"\n}"));
                    if (minute > 59) minute = 59;
                    if (hour < 0) hour = 0;
                    if (hour > 47) hour = 47;
                }

                tokens.Add(new Token(TokenType.Time, text, hour * 60 + minute, line, col, length));
                return j;
            }

            // 小数（「.」の直後が数字のときだけ小数点として読む。「たこ焼き.待ち人数」と区別する）
            if (i + 1 < source.Length
                && ScriptText.NormalizePunctuation(source[i]) == '.'
                && ScriptText.IsAsciiDigit(ScriptText.NormalizePunctuation(source[i + 1])))
            {
                sb.Append('.');
                i++;
                while (i < source.Length && ScriptText.IsAsciiDigit(ScriptText.NormalizePunctuation(source[i])))
                {
                    sb.Append(ScriptText.NormalizePunctuation(source[i]));
                    i++;
                }
            }

            string numberText = sb.ToString();
            double value;
            if (!double.TryParse(numberText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                value = 0.0;
                diags.Add(Diagnostic.Error(line, col, i - start,
                    $"数字「{numberText}」が読めません。",
                    "場所 5, 10"));
            }

            tokens.Add(new Token(TokenType.Number, numberText, value, line, col, i - start));
            return i;
        }

        // ── 識別子 ────────────────────────────────────────────────
        static int ReadIdentifier(string source, int start, int line, int col, List<Token> tokens)
        {
            bool startedWithAscii = ScriptText.IsAsciiLetter(ScriptText.NormalizePunctuation(source[start]))
                                 || source[start] == '_';
            int i = start;
            var sb = new StringBuilder();

            while (i < source.Length)
            {
                char norm = ScriptText.NormalizePunctuation(source[i]);
                if (!ScriptText.IsIdentifierPart(norm, startedWithAscii)) break;
                // ASCII 部分は正規化した文字を、日本語はそのままの文字を採る
                sb.Append(ScriptText.IsJapanese(norm) ? source[i] : norm);
                i++;
            }

            EmitIdentifier(sb.ToString(), line, col, tokens);
            return i;
        }

        /// <summary>
        /// 「もし来場者数」のようにキーワードがくっついて書かれた場合に切り離す。
        /// 日本語は分かち書きしないので、これが無いと初心者のコードがほぼ通らない。
        /// </summary>
        static void EmitIdentifier(string text, int line, int col, List<Token> tokens)
        {
            while (text.Length > 0)
            {
                string matched = null;
                for (int k = 0; k < MatsuriKeywords.SplitPrefixes.Length; k++)
                {
                    string p = MatsuriKeywords.SplitPrefixes[k];
                    if (text.Length > p.Length && text.StartsWith(p, System.StringComparison.Ordinal))
                    {
                        matched = p;
                        break;
                    }
                }

                if (matched == null) break;

                AddWord(matched, line, col, tokens);
                col += matched.Length;
                text = text.Substring(matched.Length);
            }

            if (text.Length > 0) AddWord(text, line, col, tokens);
        }

        static void AddWord(string word, int line, int col, List<Token> tokens)
        {
            var kind = MatsuriKeywords.Classify(word);
            if (kind == KeywordKind.And)
            {
                tokens.Add(new Token(TokenType.And, word, 0, line, col, word.Length));
                return;
            }
            if (kind == KeywordKind.Or)
            {
                tokens.Add(new Token(TokenType.Or, word, 0, line, col, word.Length));
                return;
            }
            tokens.Add(new Token(TokenType.Identifier, word, 0, line, col, word.Length));
        }
    }
}
