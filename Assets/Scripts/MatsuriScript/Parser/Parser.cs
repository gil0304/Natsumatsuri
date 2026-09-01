using System.Collections.Generic;
using Matsuri.Script.Ast;
using Matsuri.Script.Lexing;

namespace Matsuri.Script.Parsing
{
    /// <summary>
    /// 再帰下降パーサ (§51)。
    ///
    /// 重要な性質:
    ///  - 最初のエラーで止まらない。1行ぶん読み飛ばして解析を続け、エラーをまとめて返す (§42)。
    ///  - 日本語でも英語でも同じ AST になる (§11)。
    ///  - 「祭り { }」で囲っても囲わなくてもよい。
    /// </summary>
    public static partial class Parser
    {
        const string ExampleStall = "屋台 \"たこ焼き\" {\n    場所 5, 5\n    値段 500\n}";
        const string ExampleIf = "もし 来場者数 > 500 {\n    屋台 \"焼きそば\" { 場所 20, 10 }\n}";
        const string ExampleTime = "時間 19:00 {\n    盆踊り\n}";

        public static FestivalProgram Parse(List<Token> tokens, List<Diagnostic> diagnostics)
        {
            var ctx = new ParseContext(tokens, diagnostics);
            var program = new FestivalProgram();
            program.SetPosition(1, 1, 0);

            while (!ctx.IsAtEnd)
            {
                ctx.SkipNewlinesAndUnknown();
                if (ctx.IsAtEnd) break;

                if (ctx.Check(TokenType.RBrace))
                {
                    var stray = ctx.Advance();
                    ctx.Error(stray, "「}」が1つ多いようです。対応する「{」がありません。", ExampleStall);
                    continue;
                }

                // 「祭り "夏の宴" { … }」は、中身をそのままトップレベルに展開する
                if (ctx.Check(TokenType.Identifier)
                    && MatsuriKeywords.Classify(ctx.Current.Text) == KeywordKind.Festival)
                {
                    ParseFestivalHeader(ctx, program);
                    continue;
                }

                ParseStatementInto(ctx, program.Body);
            }

            return program;
        }

        // ── 祭り宣言 ─────────────────────────────────────────────
        static void ParseFestivalHeader(ParseContext ctx, FestivalProgram program)
        {
            ctx.Advance();   // 祭り

            if (ctx.Check(TokenType.String))
            {
                var nameToken = ctx.Advance();
                if (!string.IsNullOrEmpty(nameToken.Text)) program.Name = nameToken.Text;
            }
            else if (ctx.Check(TokenType.Identifier) && !MatsuriKeywords.IsKeyword(ctx.Current.Text))
            {
                program.Name = ctx.Advance().Text;
            }

            if (!ctx.CheckBlockStart())
            {
                // 「祭り "夏の宴"」だけ書かれた場合。名前だけ受け取って続ける。
                return;
            }

            var open = ctx.ConsumeBlockStart();
            var body = ParseBlock(ctx, open, "祭り");
            program.Body.AddRange(body);
        }

        // ── ブロック ─────────────────────────────────────────────
        static List<Node> ParseBlock(ParseContext ctx, Token openBrace, string ownerLabel)
        {
            var body = new List<Node>();

            while (true)
            {
                ctx.SkipNewlinesAndUnknown();

                if (ctx.Check(TokenType.RBrace)) { ctx.Advance(); return body; }

                if (ctx.IsAtEnd)
                {
                    ctx.Error(openBrace,
                        $"{ownerLabel}の「{{」が閉じられていません。最後に「}}」を書いてください。",
                        ExampleStall);
                    return body;
                }

                ParseStatementInto(ctx, body);
            }
        }

        // ── 文 ───────────────────────────────────────────────────
        /// <summary>
        /// 文を1つ読んで target に足す。「祭り { }」のように中身が複数になる文があるので、
        /// 戻り値ではなくリストへの追加という形にしてある。
        /// </summary>
        static void ParseStatementInto(ParseContext ctx, List<Node> target)
        {
            if (ctx.Check(TokenType.Identifier)
                && MatsuriKeywords.Classify(ctx.Current.Text) == KeywordKind.Festival)
            {
                var inner = new FestivalProgram();
                ParseFestivalHeader(ctx, inner);
                target.AddRange(inner.Body);
                return;
            }

            var node = ParseStatement(ctx);
            if (node != null) target.Add(node);
        }

        static Node ParseStatement(ParseContext ctx)
        {
            // 「20:00 { … }」の省略形 (§15)
            if (ctx.Check(TokenType.Time)) return ParseTimeBlock(ctx, ctx.Advance());

            if (!ctx.Check(TokenType.Identifier))
            {
                var bad = ctx.Current;
                ctx.Error(bad,
                    $"ここに「{bad.DisplayText}」は書けません。行のはじめは「屋台」「装飾」「設備」「もし」「時間」などで始めます。",
                    ExampleStall);
                ctx.SkipToNextStatement();
                return null;
            }

            var word = ctx.Current;
            var kind = MatsuriKeywords.Classify(word.Text);

            switch (kind)
            {
                case KeywordKind.Stall:      return ParseEntity(ctx, kind);
                case KeywordKind.Decoration: return ParseEntity(ctx, kind);
                case KeywordKind.Facility:   return ParseEntity(ctx, kind);
                case KeywordKind.Event:      return ParseEntity(ctx, kind);

                case KeywordKind.Fireworks:  return ParseShortEvent(ctx, "花火");
                case KeywordKind.BonOdori:   return ParseShortEvent(ctx, "盆踊り");
                case KeywordKind.Taiko:      return ParseShortEvent(ctx, "太鼓");

                case KeywordKind.If:         return ParseIf(ctx);
                case KeywordKind.Time:       return ParseTimeKeyword(ctx);

                case KeywordKind.Position:
                case KeywordKind.Price:
                case KeywordKind.Rotation:
                case KeywordKind.Name:
                {
                    var t = ctx.Advance();
                    ctx.Error(t,
                        $"「{MatsuriKeywords.Display(kind)}」は屋台などの「{{ }}」の中に書きます。",
                        ExampleStall);
                    ctx.SkipToNextStatement();
                    return null;
                }

                default:
                {
                    var t = ctx.Advance();
                    ctx.Error(t,
                        $"「{t.Text}」という命令はありません。",
                        ExampleStall,
                        SuggestStatementKeywords(t.Text));
                    ctx.SkipToNextStatement();
                    return null;
                }
            }
        }

        static IReadOnlyList<string> SuggestStatementKeywords(string written)
        {
            var pool = MatsuriKeywords.StatementKeywords;
            var scored = new List<KeyValuePair<int, string>>(pool.Length);
            for (int i = 0; i < pool.Length; i++)
                scored.Add(new KeyValuePair<int, string>(ScriptText.Distance(written ?? "", pool[i]), pool[i]));
            scored.Sort((a, b) => a.Key.CompareTo(b.Key));

            var result = new List<string>();
            for (int i = 0; i < scored.Count && result.Count < 3; i++)
            {
                if (scored[i].Key > 3) break;
                result.Add(scored[i].Value);
            }
            return result;
        }

        // ── もし ─────────────────────────────────────────────────
        static Node ParseIf(ParseContext ctx)
        {
            var keyword = ctx.Advance();
            var condition = ExpressionParser.ParseExpression(ctx);

            if (condition == null)
            {
                ctx.SkipToNextStatement();
                return null;
            }

            if (!ctx.CheckBlockStart())
            {
                ctx.Error(keyword, "「もし」の条件のあとに「{ }」がありません。中に、条件が成り立ったときにすることを書きます。", ExampleIf);
                ctx.SkipToNextStatement();
                return null;
            }

            var open = ctx.ConsumeBlockStart();
            var body = ParseBlock(ctx, open, "もし");

            var node = new IfNode { Condition = condition, Body = body };
            node.SetPosition(keyword.Line, keyword.Column, keyword.Length);
            return node;
        }

        // ── 時間 ─────────────────────────────────────────────────
        static Node ParseTimeKeyword(ParseContext ctx)
        {
            var keyword = ctx.Advance();

            if (!ctx.Check(TokenType.Time))
            {
                ctx.Error(keyword, "「時間」のあとに 19:00 のような時刻がありません。", ExampleTime);
                ctx.SkipToNextStatement();
                return null;
            }

            var timeToken = ctx.Advance();
            return ParseTimeBlock(ctx, timeToken, keyword);
        }

        static Node ParseTimeBlock(ParseContext ctx, Token timeToken, Token? keyword = null)
        {
            var anchor = keyword ?? timeToken;

            if (!ctx.CheckBlockStart())
            {
                ctx.Error(anchor,
                    $"{timeToken.Text} のあとに「{{ }}」がありません。その時刻にすることを中に書きます。",
                    ExampleTime);
                ctx.SkipToNextStatement();
                return null;
            }

            var open = ctx.ConsumeBlockStart();
            var body = ParseBlock(ctx, open, "時間");

            var node = new TimeNode { MinutesOfDay = (int)timeToken.Number, Body = body };
            node.SetPosition(anchor.Line, anchor.Column, anchor.Length);
            return node;
        }
    }
}
