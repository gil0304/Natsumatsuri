using System.Linq;
using Matsuri.Script;
using Matsuri.Script.Commands;
using NUnit.Framework;

namespace Matsuri.Tests
{
    /// <summary>入口 MatsuriCompiler と、同梱サンプルのテスト。</summary>
    public class CompilerTests
    {
        static FestivalPlan Compile(string source) => MatsuriCompiler.Compile(source, new FakeCatalog());

        [Test]
        public void 一行のコードが計画になる()
        {
            var plan = Compile("屋台 \"たこ焼き\" { 場所 5, 5 }");
            Assert.That(plan.HasErrors, Is.False);
            Assert.That(plan.ImmediateCommands.Count, Is.EqualTo(1));
            Assert.That(plan.Diagnostics.Count, Is.EqualTo(0));
        }

        [Test]
        public void 空のコードは案内を返すがエラーにはしない()
        {
            var plan = Compile("   \n\n");
            Assert.That(plan.HasErrors, Is.False);
            Assert.That(plan.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(plan.Diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Info));
        }

        [Test]
        public void カタログが無ければ日本語で知らせる()
        {
            var plan = MatsuriCompiler.Compile("屋台 \"たこ焼き\" { 場所 5, 5 }", null);
            Assert.That(plan.HasErrors, Is.True);
            Assert.That(plan.Diagnostics[0].Message, Does.Contain("祭りのデータ"));
        }

        [Test]
        public void 診断は行の順に並ぶ()
        {
            var plan = Compile("屋台 \"うどん\" { 場所 1, 1 }\n屋台 \"そば\" { 場所 5, 5 }\n屋台 \"ラーメン\" { 場所 9, 9 }");
            Assert.That(plan.Diagnostics.Count, Is.GreaterThanOrEqualTo(3));
            for (int i = 1; i < plan.Diagnostics.Count; i++)
                Assert.That(plan.Diagnostics[i].Line, Is.GreaterThanOrEqualTo(plan.Diagnostics[i - 1].Line));
        }

        [Test]
        public void エラーがあるときは世界を変えない()
        {
            var plan = Compile("屋台 \"たこ焼き\" { 場所 1, 1 }\n屋台 \"うどん\" { 場所 5, 5 }");
            Assert.That(plan.HasErrors, Is.True);
            Assert.That(plan.ImmediateCommands.Count, Is.EqualTo(0));
            Assert.That(plan.Rules.Count, Is.EqualTo(0));
        }

        [Test]
        public void 警告だけなら計画は作られる()
        {
            var plan = Compile("屋台 \"たこ焼き\" {\n  場所 1, 1\n  値段 9999\n}");
            Assert.That(plan.HasErrors, Is.False);
            Assert.That(plan.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning), Is.True);
            Assert.That(plan.ImmediateCommands.Count, Is.EqualTo(1));
        }

        [Test]
        public void 例外を投げずに必ず計画を返す()
        {
            string[] broken =
            {
                "{{{{",
                "}}}}",
                "屋台",
                "もし",
                "時間",
                "\"",
                "屋台 \"\" { 場所 }",
                "★★★",
                "もし > 5 { }",
                "19:00",
            };

            foreach (var source in broken)
            {
                var plan = Compile(source);
                Assert.That(plan, Is.Not.Null, source);
                Assert.That(plan.Diagnostics, Is.Not.Null, source);
            }
        }

        [Test]
        public void はじめのサンプルはそのまま動く()
        {
            var plan = Compile(MatsuriSamples.Starter);
            Assert.That(plan.HasErrors, Is.False);
            Assert.That(plan.ImmediateCommands.Count, Is.EqualTo(1));
            Assert.That(((CreateStallCommand)plan.ImmediateCommands[0]).StallId, Is.EqualTo(MatsuriIds.Takoyaki));
        }

        [Test]
        public void 同梱サンプルはすべてエラーなし()
        {
            foreach (var sample in MatsuriSamples.All)
            {
                var plan = Compile(sample.Code);
                var errors = plan.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                Assert.That(errors.Count, Is.EqualTo(0),
                    sample.Id + " : " + (errors.Count > 0 ? errors[0].ToDisplayString() : ""));
            }
        }

        [Test]
        public void サンプルは名前で引ける()
        {
            Assert.That(MatsuriSamples.TryGet("full", out var sample), Is.True);
            Assert.That(sample.Code, Is.EqualTo(MatsuriSamples.Full));
            Assert.That(MatsuriSamples.TryGet("そんなものはない", out _), Is.False);
        }

        [Test]
        public void 練習用サンプルは屋台と装飾とイベントを含む()
        {
            var plan = Compile(MatsuriSamples.Tutorial);
            Assert.That(plan.ImmediateCommands.OfType<CreateStallCommand>().Count(), Is.GreaterThanOrEqualTo(4));
            Assert.That(plan.ImmediateCommands.OfType<CreateDecorationCommand>().Count(), Is.GreaterThanOrEqualTo(3));
            Assert.That(plan.Rules.Count, Is.EqualTo(2));
        }

        [Test]
        public void チャレンジ用サンプルは条件を含む()
        {
            var plan = Compile(MatsuriSamples.Challenge);
            Assert.That(plan.Rules.Count, Is.EqualTo(5));
            Assert.That(plan.Rules.Any(r => r.Condition is LogicalCondition), Is.True);
        }
    }
}
