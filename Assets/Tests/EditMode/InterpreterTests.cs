using System.Linq;
using Matsuri.Script;
using Matsuri.Script.Commands;
using NUnit.Framework;

namespace Matsuri.Tests
{
    /// <summary>インタプリタのテスト。即時コマンド・ルール・費用。</summary>
    public class InterpreterTests
    {
        static FestivalPlan Build(string source, FakeCatalog catalog = null)
            => MatsuriCompiler.Compile(source, catalog ?? new FakeCatalog());

        [Test]
        public void 屋台は即時コマンドになる()
        {
            var plan = Build("屋台 \"たこ焼き\" {\n  場所 5, 10\n  値段 450\n}");
            Assert.That(plan.HasErrors, Is.False);
            Assert.That(plan.ImmediateCommands.Count, Is.EqualTo(1));

            var command = plan.ImmediateCommands[0] as CreateStallCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.StallId, Is.EqualTo(MatsuriIds.Takoyaki));
            Assert.That(command.SourceName, Is.EqualTo("たこ焼き"));
            Assert.That(command.Position.X, Is.EqualTo(5f));
            Assert.That(command.Position.Z, Is.EqualTo(10f));
            Assert.That(command.Price, Is.EqualTo(450));
            Assert.That(command.SourceLine, Is.EqualTo(1));
        }

        [Test]
        public void 建設費用がコマンドに入る()
        {
            var plan = Build("屋台 \"たこ焼き\" { 場所 5, 10 }");
            Assert.That(plan.ImmediateCommands[0].Cost, Is.EqualTo(45000L));
        }

        [Test]
        public void 見積り費用は即時コマンドの合計()
        {
            var plan = Build("屋台 \"たこ焼き\" { 場所 1, 1 }\n屋台 \"焼きそば\" { 場所 5, 5 }\n装飾 \"提灯\" { 場所 9, 9 }");
            Assert.That(plan.EstimatedCost, Is.EqualTo(45000L + 48000L + 4000L));
        }

        [Test]
        public void 値段を書かなければnullのまま()
        {
            var plan = Build("屋台 \"たこ焼き\" { 場所 5, 10 }");
            Assert.That(((CreateStallCommand)plan.ImmediateCommands[0]).Price, Is.Null);
        }

        [Test]
        public void 範囲外の値段は自動でおさめる()
        {
            var plan = Build("屋台 \"たこ焼き\" {\n  場所 5, 10\n  値段 5000\n}");
            Assert.That(((CreateStallCommand)plan.ImmediateCommands[0]).Price, Is.EqualTo(1200));
        }

        [Test]
        public void 向きが入る()
        {
            var plan = Build("屋台 \"たこ焼き\" {\n  場所 5, 10\n  向き 90\n}");
            Assert.That(((CreateStallCommand)plan.ImmediateCommands[0]).RotationDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void 名前で表示名を上書きできる()
        {
            var plan = Build("屋台 \"たこ焼き\" {\n  場所 5, 10\n  名前 \"元祖たこ焼き\"\n}");
            Assert.That(((CreateStallCommand)plan.ImmediateCommands[0]).SourceName, Is.EqualTo("元祖たこ焼き"));
        }

        [Test]
        public void 装飾と設備もコマンドになる()
        {
            var plan = Build("装飾 \"提灯\" { 場所 3, 4 }\n設備 \"ベンチ\" { 場所 8, 2 }");
            Assert.That(plan.ImmediateCommands[0], Is.TypeOf<CreateDecorationCommand>());
            Assert.That(((CreateDecorationCommand)plan.ImmediateCommands[0]).DecorationId, Is.EqualTo(MatsuriIds.Lantern));
            Assert.That(plan.ImmediateCommands[1], Is.TypeOf<CreateFacilityCommand>());
            Assert.That(((CreateFacilityCommand)plan.ImmediateCommands[1]).FacilityId, Is.EqualTo(MatsuriIds.Bench));
        }

        [Test]
        public void 花火の種類が正規IDになる()
        {
            var plan = Build("花火 \"大玉\"");
            var command = plan.ImmediateCommands[0] as StartFireworksCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Kind, Is.EqualTo(MatsuriIds.FireworkOodama));
        }

        [Test]
        public void 盆踊りと太鼓もコマンドになる()
        {
            var plan = Build("盆踊り\n太鼓");
            Assert.That(plan.ImmediateCommands[0], Is.TypeOf<StartBonOdoriCommand>());
            Assert.That(plan.ImmediateCommands[1], Is.TypeOf<StartTaikoCommand>());
        }

        [Test]
        public void もしの中の屋台は即時コマンドに入らない()
        {
            var plan = Build("屋台 \"たこ焼き\" { 場所 1, 1 }\nもし 来場者数 > 500 {\n  屋台 \"焼きそば\" { 場所 20, 10 }\n}");
            Assert.That(plan.ImmediateCommands.Count, Is.EqualTo(1));
            Assert.That(plan.Rules.Count, Is.EqualTo(1));
            Assert.That(plan.Rules[0].Body.Count, Is.EqualTo(1));
            Assert.That(plan.EstimatedCost, Is.EqualTo(45000L));
        }

        [Test]
        public void もしの条件が指標条件になる()
        {
            var plan = Build("もし 来場者数 > 500 {\n  太鼓\n}");
            var condition = plan.Rules[0].Condition as MetricCondition;
            Assert.That(condition, Is.Not.Null);
            Assert.That(condition.Kind, Is.EqualTo(MetricKind.Visitors));
            Assert.That(condition.Op, Is.EqualTo(CompareOp.Greater));
            Assert.That(condition.Value, Is.EqualTo(500.0));
        }

        [Test]
        public void 屋台の待ち人数の条件が正規IDを持つ()
        {
            var plan = Build("もし たこ焼き.待ち人数 > 20 {\n  屋台 \"たこ焼き\" { 場所 20, 10 }\n}");
            var condition = (MetricCondition)plan.Rules[0].Condition;
            Assert.That(condition.Kind, Is.EqualTo(MetricKind.StallQueue));
            Assert.That(condition.StallId, Is.EqualTo(MatsuriIds.Takoyaki));
            Assert.That(condition.StallName, Is.EqualTo("たこ焼き"));
        }

        [Test]
        public void 時間ブロックは時刻条件になる()
        {
            var plan = Build("時間 19:00 {\n  盆踊り\n}");
            var condition = plan.Rules[0].Condition as TimeCondition;
            Assert.That(condition, Is.Not.Null);
            Assert.That(condition.MinutesOfDay, Is.EqualTo(19 * 60));
            Assert.That(plan.Rules[0].Body.Count, Is.EqualTo(1));
        }

        [Test]
        public void かつの条件が論理条件になる()
        {
            var plan = Build("もし 来場者数 > 300 かつ 売上 > 100000 {\n  花火 \"大玉\"\n}");
            var condition = plan.Rules[0].Condition as LogicalCondition;
            Assert.That(condition, Is.Not.Null);
            Assert.That(condition.IsAnd, Is.True);
            Assert.That(condition.Left, Is.TypeOf<MetricCondition>());
            Assert.That(condition.Right, Is.TypeOf<MetricCondition>());
        }

        [Test]
        public void ネストしたルールがNestedRulesに入る()
        {
            var plan = Build("もし 来場者数 > 100 {\n  時間 20:00 {\n    花火 \"大玉\"\n  }\n}");
            Assert.That(plan.Rules.Count, Is.EqualTo(1));
            Assert.That(plan.Rules[0].NestedRules.Count, Is.EqualTo(1));
            Assert.That(plan.Rules[0].NestedRules[0].Condition, Is.TypeOf<TimeCondition>());
            Assert.That(plan.Rules[0].NestedRules[0].Body.Count, Is.EqualTo(1));
        }

        [Test]
        public void もしの中の値段だけの屋台は値段変更になる()
        {
            var plan = Build("もし 売上 > 100000 {\n  屋台 \"たこ焼き\" { 値段 400 }\n}");
            Assert.That(plan.HasErrors, Is.False);

            var command = plan.Rules[0].Body[0] as SetPriceCommand;
            Assert.That(command, Is.Not.Null);
            Assert.That(command.StallId, Is.EqualTo(MatsuriIds.Takoyaki));
            Assert.That(command.Price, Is.EqualTo(400));
            Assert.That(command.Cost, Is.EqualTo(0L));
        }

        [Test]
        public void 祭りの名前が計画に入る()
        {
            var plan = Build("祭り \"夏の宴\" {\n  屋台 \"たこ焼き\" { 場所 1, 1 }\n}");
            Assert.That(plan.FestivalName, Is.EqualTo("夏の宴"));
        }

        [Test]
        public void 名前を書かなければ既定の名前になる()
        {
            var plan = Build("屋台 \"たこ焼き\" { 場所 1, 1 }");
            Assert.That(plan.FestivalName, Is.EqualTo("MY MATSURI"));
        }

        [Test]
        public void コマンドは日本語で自分を説明できる()
        {
            var plan = Build("屋台 \"たこ焼き\" {\n  場所 5, 10\n  値段 500\n}");
            string text = plan.ImmediateCommands[0].Describe();
            Assert.That(text, Does.Contain("たこ焼き"));
            Assert.That(text, Does.Contain("500"));
        }

        [Test]
        public void コマンドはシンクに流れる()
        {
            var plan = Build("屋台 \"たこ焼き\" { 場所 5, 10 }\n装飾 \"提灯\" { 場所 3, 4 }\n花火 \"菊\"");
            var sink = new RecordingSink();
            foreach (var command in plan.ImmediateCommands) command.Execute(sink);

            Assert.That(sink.Stalls.Count, Is.EqualTo(1));
            Assert.That(sink.Decorations.Count, Is.EqualTo(1));
            Assert.That(sink.Fireworks.Count, Is.EqualTo(1));
        }

        [Test]
        public void ルールのリセットで再び発火できるようになる()
        {
            var plan = Build("もし 来場者数 > 1 {\n  太鼓\n}");
            plan.Rules[0].Fired = true;
            plan.ResetRules();
            Assert.That(plan.Rules[0].Fired, Is.False);
        }

        [Test]
        public void 知らない屋台があるときは計画を返さない()
        {
            var plan = Build("屋台 \"うどん\" { 場所 1, 1 }");
            Assert.That(plan.HasErrors, Is.True);
            Assert.That(plan.ImmediateCommands.Count, Is.EqualTo(0));
        }

        [Test]
        public void ルールの説明文が日本語になる()
        {
            var plan = Build("もし たこ焼き.待ち人数 > 20 {\n  屋台 \"たこ焼き\" { 場所 20, 10 }\n}");
            Assert.That(plan.Rules[0].Describe(), Does.Contain("たこ焼き.待ち人数"));
        }

        [Test]
        public void 完成形のサンプルはエラーなしで計画になる()
        {
            var plan = Build(MatsuriSamples.Full);
            var errors = plan.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.That(errors.Count, Is.EqualTo(0), errors.Count > 0 ? errors[0].ToDisplayString() : "");
            Assert.That(plan.ImmediateCommands.Count, Is.GreaterThan(20));
            Assert.That(plan.Rules.Count, Is.EqualTo(8));
        }
    }
}
