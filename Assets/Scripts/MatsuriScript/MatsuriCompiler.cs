using System.Collections.Generic;
using Matsuri.Script.Commands;
using Matsuri.Script.Interpreting;
using Matsuri.Script.Lexing;
using Matsuri.Script.Parsing;
using Matsuri.Script.Validation;

namespace Matsuri.Script
{
    /// <summary>
    /// Matsuri Script のただ一つの入口 (§51)。
    /// UI も ScriptManager も、字句解析器や構文解析器を直接は呼ばない。
    ///
    ///   ソース → Lexer → Parser → Validator → Interpreter → FestivalPlan
    ///
    /// エラーがあっても例外は投げない。必ず FestivalPlan を返し、
    /// 中の Diagnostics に日本語の説明が入っている (§41 / §42)。
    /// </summary>
    public static class MatsuriCompiler
    {
        public static FestivalPlan Compile(string source, IMatsuriCatalog catalog)
        {
            var diagnostics = new List<Diagnostic>();

            if (catalog == null)
            {
                diagnostics.Add(Diagnostic.Error(1, 1, 1,
                    "祭りのデータがまだ読み込まれていません。少し待ってからもう一度 RUN してください。"));
                return FestivalPlan.Failed(diagnostics);
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                diagnostics.Add(Diagnostic.Info(1, 1, 1,
                    "まだ何も書かれていません。まずは屋台を1つ置いてみましょう。",
                    "屋台 \"たこ焼き\" {\n    場所 5, 5\n}"));
                return new FestivalPlan { Diagnostics = diagnostics };
            }

            FestivalPlan plan;
            try
            {
                var tokens = Lexer.Tokenize(source, diagnostics);
                var program = Parser.Parse(tokens, diagnostics);
                Validator.Validate(program, catalog, diagnostics);
                plan = Interpreter.Build(program, catalog, diagnostics);
            }
            catch (System.Exception e)
            {
                // ここに来るのは処理系のバグ。プレイヤーには「こちらの不具合」と伝える。
                diagnostics.Add(Diagnostic.Error(1, 1, 1,
                    "コードを読んでいる途中で、ゲーム側の不具合が起きました。書き方の問題ではありません。 (" + e.GetType().Name + ")"));
                return FestivalPlan.Failed(Sort(diagnostics));
            }

            var sorted = Sort(diagnostics);
            plan.Diagnostics = sorted;

            if (plan.HasErrors)
            {
                // エラーが1つでもあれば、世界は変更しない。名前だけは引き継いで UI に見せる。
                var failed = FestivalPlan.Failed(sorted);
                failed.FestivalName = plan.FestivalName;
                return failed;
            }

            return plan;
        }

        /// <summary>診断を行→列の順に並べ、まったく同じ内容の重複を取り除く。</summary>
        static List<Diagnostic> Sort(List<Diagnostic> diagnostics)
        {
            diagnostics.Sort((a, b) =>
            {
                int byLine = a.Line.CompareTo(b.Line);
                if (byLine != 0) return byLine;
                int byColumn = a.Column.CompareTo(b.Column);
                if (byColumn != 0) return byColumn;
                return ((int)a.Severity).CompareTo((int)b.Severity);
            });

            var result = new List<Diagnostic>(diagnostics.Count);
            for (int i = 0; i < diagnostics.Count; i++)
            {
                var d = diagnostics[i];
                bool duplicate = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if (result[j].Line == d.Line && result[j].Column == d.Column
                        && result[j].Severity == d.Severity && result[j].Message == d.Message)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) result.Add(d);
            }
            return result;
        }
    }
}
