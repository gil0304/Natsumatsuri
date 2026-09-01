using System.Collections.Generic;
using Matsuri.Script.Ast;
using Matsuri.Script.Lexing;

namespace Matsuri.Script.Parsing
{
    /// <summary>
    /// パーサのうち、「屋台 / 装飾 / 設備 / イベント」とその「{ }」の中身を読む部分。
    /// 1ファイル1責務のため <see cref="Parser"/> から分けてある (§66)。
    /// </summary>
    public static partial class Parser
    {
        // ── 屋台 / 装飾 / 設備 / イベント ─────────────────────────
        static Node ParseEntity(ParseContext ctx, KeywordKind kind)
        {
            var keyword = ctx.Advance();
            string label = MatsuriKeywords.Display(kind);

            string name = null;
            if (ctx.Check(TokenType.String)) name = ctx.Advance().Text;
            else if (ctx.Check(TokenType.Identifier) && !MatsuriKeywords.IsKeyword(ctx.Current.Text))
                name = ctx.Advance().Text;

            if (string.IsNullOrEmpty(name))
            {
                ctx.Error(keyword,
                    $"「{label}」のあとに名前がありません。{label}の名前を \" \" で囲んで書いてください。",
                    ExampleStall);
                ctx.SkipToNextStatement();
                return null;
            }

            var properties = new List<PropertyNode>();
            if (ctx.CheckBlockStart())
            {
                var open = ctx.ConsumeBlockStart();
                ParseProperties(ctx, properties, open, label);
            }

            Node node = kind switch
            {
                KeywordKind.Stall      => new StallNode { Name = name, Properties = properties },
                KeywordKind.Decoration => new DecorationNode { Name = name, Properties = properties },
                KeywordKind.Facility   => new FacilityNode { Name = name, Properties = properties },
                _                      => new EventNode { Name = name, Properties = properties }
            };
            node.SetPosition(keyword.Line, keyword.Column, keyword.Length);
            return node;
        }

        /// <summary>「花火 "大玉"」「盆踊り」「太鼓 { 場所 0, 0 }」の短い形 (§22)。</summary>
        static Node ParseShortEvent(ParseContext ctx, string canonicalName)
        {
            var keyword = ctx.Advance();

            string argument = null;
            if (ctx.Check(TokenType.String)) argument = ctx.Advance().Text;
            else if (ctx.Check(TokenType.Identifier) && !MatsuriKeywords.IsKeyword(ctx.Current.Text))
                argument = ctx.Advance().Text;

            var properties = new List<PropertyNode>();
            if (ctx.CheckBlockStart())
            {
                var open = ctx.ConsumeBlockStart();
                ParseProperties(ctx, properties, open, canonicalName);
            }

            var node = new EventNode { Name = canonicalName, Argument = argument, Properties = properties };
            node.SetPosition(keyword.Line, keyword.Column, keyword.Length);
            return node;
        }

        // ── ブロックの中の設定 ───────────────────────────────────
        static void ParseProperties(ParseContext ctx, List<PropertyNode> properties, Token openBrace, string ownerLabel)
        {
            while (true)
            {
                ctx.SkipNewlinesAndUnknown();

                if (ctx.Check(TokenType.RBrace)) { ctx.Advance(); return; }

                if (ctx.IsAtEnd)
                {
                    ctx.Error(openBrace,
                        $"「{ownerLabel}」の「{{」が閉じられていません。最後に「}}」を書いてください。",
                        ExampleStall);
                    return;
                }

                if (!ctx.Check(TokenType.Identifier))
                {
                    var bad = ctx.Current;
                    ctx.Error(bad,
                        $"「{ownerLabel}」の中に「{bad.DisplayText}」は書けません。書けるのは 場所 / 値段 / 向き / 名前 です。",
                        ExampleStall);
                    ctx.SkipToNextStatement();
                    continue;
                }

                var word = ctx.Current;
                var kind = MatsuriKeywords.Classify(word.Text);

                switch (kind)
                {
                    case KeywordKind.Position:
                    case KeywordKind.Price:
                    case KeywordKind.Rotation:
                    case KeywordKind.Name:
                        properties.Add(ParseProperty(ctx, kind));
                        continue;

                    case KeywordKind.Stall:
                    case KeywordKind.Decoration:
                    case KeywordKind.Facility:
                    case KeywordKind.Event:
                    case KeywordKind.If:
                    case KeywordKind.Time:
                    case KeywordKind.Fireworks:
                    case KeywordKind.BonOdori:
                    case KeywordKind.Taiko:
                        // 「}」の書き忘れが濃厚。ブロックを閉じたことにして立て直す。
                        ctx.Error(openBrace,
                            $"「{ownerLabel}」の「{{」が閉じられていません。「{word.Text}」を書く前に「}}」を書いてください。",
                            ExampleStall);
                        return;

                    default:
                    {
                        var t = ctx.Advance();
                        ctx.Error(t,
                            $"「{t.Text}」という設定はありません。",
                            ExampleStall,
                            SuggestProperties(t.Text));
                        ctx.SkipToNextStatement();
                        continue;
                    }
                }
            }
        }

        static IReadOnlyList<string> SuggestProperties(string written)
        {
            var pool = MatsuriKeywords.PropertyKeywords;
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

        static PropertyNode ParseProperty(ParseContext ctx, KeywordKind kind)
        {
            var keyword = ctx.Advance();
            var node = new PropertyNode { Keyword = MatsuriKeywords.CanonicalProperty(kind) };
            node.SetPosition(keyword.Line, keyword.Column, keyword.Length);

            if (kind == KeywordKind.Name)
            {
                if (ctx.Check(TokenType.String) || ctx.Check(TokenType.Identifier))
                {
                    node.Text = ctx.Advance().Text;
                }
                else
                {
                    ctx.Error(keyword, "「名前」のあとに名前がありません。\" \" で囲んで書いてください。",
                        "屋台 \"たこ焼き\" {\n    場所 5, 5\n    名前 \"元祖たこ焼き\"\n}");
                    ctx.SkipToNextStatement();
                }
                return node;
            }

            int wanted = kind == KeywordKind.Position ? 2 : 1;
            for (int n = 0; n < wanted; n++)
            {
                if (n > 0) ctx.Match(TokenType.Comma);   // 「,」は省略してもよい

                if (ctx.Check(TokenType.Number))
                {
                    node.Numbers.Add(ctx.Advance().Number);
                }
                else if (ctx.Check(TokenType.Time) && kind != KeywordKind.Position)
                {
                    node.Numbers.Add(ctx.Advance().Number);
                }
                else
                {
                    break;
                }
            }

            if (node.Numbers.Count < wanted)
            {
                if (kind == KeywordKind.Position)
                {
                    ctx.Error(keyword,
                        "「場所」には、よこ(X)とたて(Z)の2つの数字が必要です。",
                        "場所 5, 10");
                }
                else
                {
                    ctx.Error(keyword,
                        $"「{MatsuriKeywords.Display(kind)}」のあとに数字がありません。",
                        kind == KeywordKind.Price ? "値段 500" : "向き 90");
                }
                ctx.SkipToNextStatement();
            }

            return node;
        }
    }
}
