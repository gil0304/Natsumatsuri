using Matsuri.Script.Ast;
using Matsuri.Script.Commands;
using Matsuri.Script.Lexing;

namespace Matsuri.Script.Validation
{
    /// <summary>
    /// 条件式の検証 (§14 / §16 / §17)。
    /// 「読めない指標」「知らない屋台名」「ありえない比較」を日本語で指摘する。
    /// </summary>
    internal static class ConditionValidator
    {
        const string ExampleMetric = "もし 来場者数 > 500 {\n    屋台 \"焼きそば\" { 場所 20, 10 }\n}";
        const string ExampleStallMetric = "もし たこ焼き.待ち人数 > 20 {\n    屋台 \"たこ焼き\" { 場所 20, 10 }\n}";

        public static void Validate(ExpressionNode expression, ValidationContext ctx)
        {
            switch (expression)
            {
                case null:
                    return;

                case LogicalNode logical:
                    Validate(logical.Left, ctx);
                    Validate(logical.Right, ctx);
                    return;

                case ComparisonNode comparison:
                    ValidateComparison(comparison, ctx);
                    return;
            }
        }

        static void ValidateComparison(ComparisonNode node, ValidationContext ctx)
        {
            if (node.LeftTarget != null)
            {
                ValidateStallMetric(node, ctx);
                return;
            }

            if (MatsuriKeywords.TryGlobalMetric(node.LeftMetric, out MetricKind kind))
            {
                ValidateRange(node, kind, ctx);
                return;
            }

            // 「もし たこ焼き > 20」のように、屋台名だけを書いてしまった場合
            if (ctx.Catalog.TryResolve(node.LeftMetric, MatsuriEntryKind.Stall, out CatalogEntry stall))
            {
                ctx.Error(node,
                    $"「{node.LeftMetric}」だけでは、何の数か分かりません。"
                    + $"「{node.LeftMetric}.待ち人数」のように、何を見るのかを書いてください。",
                    $"もし {stall.DisplayName}.待ち人数 > 20 {{\n    屋台 \"{stall.DisplayName}\" {{ 場所 20, 10 }}\n}}",
                    MatsuriKeywords.StallMetricKeywords);
                return;
            }

            ctx.Error(node,
                $"「{node.LeftMetric}」という数値は読めません。"
                + "使えるのは 来場者数 / 現在の来場者 / 売上 / 予算 / 満足度 / 時刻 と、屋台名.待ち人数 です。",
                ExampleMetric,
                MatsuriKeywords.SuggestMetrics(node.LeftMetric));
        }

        static void ValidateStallMetric(ComparisonNode node, ValidationContext ctx)
        {
            if (!ctx.Catalog.TryResolve(node.LeftTarget, MatsuriEntryKind.Stall, out CatalogEntry stall))
            {
                ctx.Error(node,
                    $"屋台「{node.LeftTarget}」は見つかりません。条件に書けるのは、実際にある屋台の名前だけです。",
                    ExampleStallMetric,
                    ctx.Catalog.SuggestNames(node.LeftTarget, MatsuriEntryKind.Stall, 3));
                return;
            }

            if (!MatsuriKeywords.TryStallMetric(node.LeftMetric, out MetricKind kind))
            {
                ctx.Error(node,
                    $"「{stall.DisplayName}.{node.LeftMetric}」は読めません。"
                    + "屋台について見られるのは 待ち人数 / 売上 / 軒数 です。",
                    ExampleStallMetric,
                    MatsuriKeywords.StallMetricKeywords);
                return;
            }

            ValidateRange(node, kind, ctx);
        }

        /// <summary>比べている数が現実的かどうか。まちがいに気づける範囲だけ警告する。</summary>
        static void ValidateRange(ComparisonNode node, MetricKind kind, ValidationContext ctx)
        {
            switch (kind)
            {
                case MetricKind.Satisfaction:
                    if (node.Right > 100.0 || node.Right < 0.0)
                    {
                        ctx.Warn(node,
                            $"満足度は 0〜100 の値です。{node.Right:0.##} とはくらべられません。",
                            "もし 満足度 > 70 {\n    花火 \"大玉\"\n}");
                    }
                    break;

                case MetricKind.Clock:
                    if (node.Right < Validator.FestivalStartMinutes || node.Right > Validator.FestivalEndMinutes)
                    {
                        ctx.Warn(node,
                            $"時刻 {ScriptText.ClockText((int)node.Right)} は祭りの時間の外です。"
                            + "「時間 19:00 { }」の書き方のほうが分かりやすいです。",
                            "時間 19:00 {\n    盆踊り\n}");
                    }
                    break;

                case MetricKind.StallQueue:
                    if (node.Right < 0.0)
                    {
                        ctx.Warn(node, "待ち人数は 0 より小さくなりません。", ExampleStallMetric);
                    }
                    break;

                case MetricKind.Visitors:
                case MetricKind.CurrentVisitors:
                case MetricKind.Revenue:
                case MetricKind.Budget:
                    if (node.Right < 0.0)
                    {
                        ctx.Warn(node, "マイナスの数とくらべても、条件はいつも成り立ってしまいます。", ExampleMetric);
                    }
                    break;
            }
        }
    }
}
