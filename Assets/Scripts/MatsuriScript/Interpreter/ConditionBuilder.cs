using Matsuri.Script.Ast;
using Matsuri.Script.Commands;
using Matsuri.Script.Lexing;

namespace Matsuri.Script.Interpreting
{
    /// <summary>
    /// 条件式の AST を、祭り開催中に評価できる <see cref="ICondition"/> に変換する。
    /// 読めない指標や知らない屋台名は Validator がすでに指摘しているので、
    /// ここでは黙って null を返す（同じ内容を二重に出さない）。
    /// </summary>
    internal static class ConditionBuilder
    {
        public static ICondition Build(ExpressionNode expression, IMatsuriCatalog catalog)
        {
            switch (expression)
            {
                case null:
                    return null;

                case LogicalNode logical:
                {
                    var left = Build(logical.Left, catalog);
                    var right = Build(logical.Right, catalog);
                    if (left == null) return right;
                    if (right == null) return left;
                    return new LogicalCondition { IsAnd = logical.IsAnd, Left = left, Right = right };
                }

                case ComparisonNode comparison:
                    return BuildComparison(comparison, catalog);
            }

            return null;
        }

        static ICondition BuildComparison(ComparisonNode node, IMatsuriCatalog catalog)
        {
            CompareOp op = ToCompareOp(node.Op);

            if (node.LeftTarget != null)
            {
                if (!catalog.TryResolve(node.LeftTarget, MatsuriEntryKind.Stall, out CatalogEntry stall)) return null;
                if (!MatsuriKeywords.TryStallMetric(node.LeftMetric, out MetricKind stallKind)) return null;

                return new MetricCondition
                {
                    Kind = stallKind,
                    StallId = stall.Id,
                    StallName = stall.DisplayName,
                    Op = op,
                    Value = node.Right
                };
            }

            if (!MatsuriKeywords.TryGlobalMetric(node.LeftMetric, out MetricKind kind)) return null;

            return new MetricCondition
            {
                Kind = kind,
                Op = op,
                Value = node.Right
            };
        }

        public static CompareOp ToCompareOp(string op) => op switch
        {
            ">"  => CompareOp.Greater,
            ">=" => CompareOp.GreaterEqual,
            "<"  => CompareOp.Less,
            "<=" => CompareOp.LessEqual,
            "==" => CompareOp.Equal,
            "!=" => CompareOp.NotEqual,
            _    => CompareOp.Greater
        };
    }
}
