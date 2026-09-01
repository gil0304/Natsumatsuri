using Matsuri.Script.Ast;
using Matsuri.Script.Lexing;

namespace Matsuri.Script.Parsing
{
    /// <summary>
    /// 条件式の構文解析 (§14 / §16 / §17)。
    ///
    ///   式      := かつ式 ( ("または"|"or") かつ式 )*
    ///   かつ式  := 比較   ( ("かつ"|"and")   比較   )*
    ///   比較    := 指標 演算子 数値
    ///   指標    := 名前 | 名前 "." 名前        // 「たこ焼き.待ち人数」
    ///
    /// 「かつ」は「または」より強く結びつく。
    /// </summary>
    internal static class ExpressionParser
    {
        const string ExampleCondition = "もし 来場者数 > 500 {\n    屋台 \"焼きそば\" { 場所 20, 10 }\n}";

        public static ExpressionNode ParseExpression(ParseContext ctx) => ParseOr(ctx);

        static ExpressionNode ParseOr(ParseContext ctx)
        {
            var left = ParseAnd(ctx);
            if (left == null) return null;

            while (ctx.Check(TokenType.Or))
            {
                var opToken = ctx.Advance();
                var right = ParseAnd(ctx);
                if (right == null)
                {
                    ctx.Error(opToken, "「または」のあとに、もう1つ条件を書いてください。",
                        "もし 来場者数 > 300 または 売上 > 100000 {\n}");
                    return left;
                }
                var node = new LogicalNode { IsAnd = false, Left = left, Right = right };
                node.SetPosition(left.Line, left.Column, left.Length);
                left = node;
            }
            return left;
        }

        static ExpressionNode ParseAnd(ParseContext ctx)
        {
            var left = ParseComparison(ctx);
            if (left == null) return null;

            while (ctx.Check(TokenType.And))
            {
                var opToken = ctx.Advance();
                var right = ParseComparison(ctx);
                if (right == null)
                {
                    ctx.Error(opToken, "「かつ」のあとに、もう1つ条件を書いてください。",
                        "もし 来場者数 > 300 かつ 売上 > 100000 {\n}");
                    return left;
                }
                var node = new LogicalNode { IsAnd = true, Left = left, Right = right };
                node.SetPosition(left.Line, left.Column, left.Length);
                left = node;
            }
            return left;
        }

        static ExpressionNode ParseComparison(ParseContext ctx)
        {
            if (!ctx.Check(TokenType.Identifier))
            {
                ctx.Error(ctx.Current,
                    "条件には「来場者数」「売上」「たこ焼き.待ち人数」のような数値を書きます。",
                    ExampleCondition,
                    MatsuriKeywords.MetricKeywords);
                return null;
            }

            var metricToken = ctx.Advance();
            string target = null;
            string metric = metricToken.Text;

            // 「たこ焼き.待ち人数」(§17)
            if (ctx.Check(TokenType.Dot))
            {
                ctx.Advance();
                if (!ctx.Check(TokenType.Identifier))
                {
                    ctx.Error(ctx.Current,
                        $"「{metric}.」のあとに「待ち人数」「売上」「軒数」のどれかを書いてください。",
                        "もし たこ焼き.待ち人数 > 20 {\n    屋台 \"たこ焼き\" { 場所 20, 10 }\n}",
                        MatsuriKeywords.StallMetricKeywords);
                    return null;
                }
                target = metric;
                metric = ctx.Advance().Text;
            }

            if (!ctx.Current.IsCompareOperator)
            {
                ctx.Error(ctx.Current,
                    $"「{(target == null ? metric : target + "." + metric)}」のあとに、くらべる記号（> < >= <= == !=）が必要です。",
                    ExampleCondition);
                return null;
            }

            var opToken = ctx.Advance();
            string op = OperatorText(opToken.Type);

            double right;
            if (ctx.Check(TokenType.Number) || ctx.Check(TokenType.Time))
            {
                right = ctx.Advance().Number;
            }
            else
            {
                ctx.Error(ctx.Current,
                    $"「{op}」のあとに、くらべる数字がありません。",
                    ExampleCondition);
                return null;
            }

            var node = new ComparisonNode
            {
                LeftMetric = metric,
                LeftTarget = target,
                Op = op,
                Right = right
            };
            node.SetPosition(metricToken.Line, metricToken.Column, metricToken.Length);
            return node;
        }

        static string OperatorText(TokenType type) => type switch
        {
            TokenType.Greater      => ">",
            TokenType.GreaterEqual => ">=",
            TokenType.Less         => "<",
            TokenType.LessEqual    => "<=",
            TokenType.EqualEqual   => "==",
            TokenType.NotEqual     => "!=",
            _ => ">"
        };
    }
}
