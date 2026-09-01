using Matsuri.Script;
using Matsuri.Script.Commands;
using NUnit.Framework;

namespace Matsuri.Tests
{
    /// <summary>条件の評価テスト。祭りの状態を手で作って、成り立つかどうかを見る。</summary>
    public class ConditionTests
    {
        [Test]
        public void 来場者数の比較が成り立つ()
        {
            var condition = new MetricCondition { Kind = MetricKind.Visitors, Op = CompareOp.Greater, Value = 500 };
            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 501 }), Is.True);
            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 500 }), Is.False);
        }

        [Test]
        public void 現在の来場者を見る()
        {
            var condition = new MetricCondition { Kind = MetricKind.CurrentVisitors, Op = CompareOp.GreaterEqual, Value = 200 };
            Assert.That(condition.Evaluate(new FakeMetrics { CurrentVisitorCount = 200 }), Is.True);
            Assert.That(condition.Evaluate(new FakeMetrics { CurrentVisitorCount = 199 }), Is.False);
        }

        [Test]
        public void 売上の比較が成り立つ()
        {
            var condition = new MetricCondition { Kind = MetricKind.Revenue, Op = CompareOp.Greater, Value = 500000 };
            Assert.That(condition.Evaluate(new FakeMetrics { Revenue = 500001 }), Is.True);
        }

        [Test]
        public void 予算の比較が成り立つ()
        {
            var condition = new MetricCondition { Kind = MetricKind.Budget, Op = CompareOp.Less, Value = 100000 };
            Assert.That(condition.Evaluate(new FakeMetrics { Budget = 50000 }), Is.True);
        }

        [Test]
        public void 満足度は百分率で比べられる()
        {
            var condition = new MetricCondition { Kind = MetricKind.Satisfaction, Op = CompareOp.Greater, Value = 70 };
            Assert.That(condition.Evaluate(new FakeMetrics { AverageSatisfaction = 0.8f }), Is.True);
            Assert.That(condition.Evaluate(new FakeMetrics { AverageSatisfaction = 0.6f }), Is.False);
        }

        [Test]
        public void 待ち人数は屋台ごとに見る()
        {
            var metrics = new FakeMetrics();
            metrics.Queues[MatsuriIds.Takoyaki] = 25;
            metrics.Queues[MatsuriIds.Yakisoba] = 3;

            var condition = new MetricCondition
            {
                Kind = MetricKind.StallQueue,
                StallId = MatsuriIds.Takoyaki,
                StallName = "たこ焼き",
                Op = CompareOp.Greater,
                Value = 20
            };
            Assert.That(condition.Evaluate(metrics), Is.True);

            condition.StallId = MatsuriIds.Yakisoba;
            Assert.That(condition.Evaluate(metrics), Is.False);
        }

        [Test]
        public void 屋台ごとの売上を見る()
        {
            var metrics = new FakeMetrics();
            metrics.StallRevenues[MatsuriIds.Takoyaki] = 120000;

            var condition = new MetricCondition
            {
                Kind = MetricKind.StallRevenue,
                StallId = MatsuriIds.Takoyaki,
                Op = CompareOp.GreaterEqual,
                Value = 100000
            };
            Assert.That(condition.Evaluate(metrics), Is.True);
        }

        [Test]
        public void 屋台の軒数を見る()
        {
            var metrics = new FakeMetrics();
            metrics.StallCounts[MatsuriIds.Takoyaki] = 2;

            var condition = new MetricCondition
            {
                Kind = MetricKind.StallCount,
                StallId = MatsuriIds.Takoyaki,
                Op = CompareOp.Equal,
                Value = 2
            };
            Assert.That(condition.Evaluate(metrics), Is.True);
        }

        [Test]
        public void 等しくないの比較ができる()
        {
            var condition = new MetricCondition { Kind = MetricKind.Visitors, Op = CompareOp.NotEqual, Value = 100 };
            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 101 }), Is.True);
            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 100 }), Is.False);
        }

        [Test]
        public void 以下の比較ができる()
        {
            var condition = new MetricCondition { Kind = MetricKind.Visitors, Op = CompareOp.LessEqual, Value = 100 };
            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 100 }), Is.True);
            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 101 }), Is.False);
        }

        [Test]
        public void 時刻の条件はその時刻を過ぎたら成り立つ()
        {
            var condition = new TimeCondition { MinutesOfDay = 20 * 60 };
            Assert.That(condition.Evaluate(new FakeMetrics { MinutesOfDay = 19 * 60 + 59 }), Is.False);
            Assert.That(condition.Evaluate(new FakeMetrics { MinutesOfDay = 20 * 60 }), Is.True);
            Assert.That(condition.Evaluate(new FakeMetrics { MinutesOfDay = 21 * 60 }), Is.True);
        }

        [Test]
        public void かつは両方そろって成り立つ()
        {
            var condition = new LogicalCondition
            {
                IsAnd = true,
                Left = new MetricCondition { Kind = MetricKind.Visitors, Op = CompareOp.Greater, Value = 300 },
                Right = new MetricCondition { Kind = MetricKind.Revenue, Op = CompareOp.Greater, Value = 100000 }
            };

            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 400, Revenue = 200000 }), Is.True);
            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 400, Revenue = 50000 }), Is.False);
        }

        [Test]
        public void またはは片方だけで成り立つ()
        {
            var condition = new LogicalCondition
            {
                IsAnd = false,
                Left = new MetricCondition { Kind = MetricKind.Visitors, Op = CompareOp.Greater, Value = 300 },
                Right = new MetricCondition { Kind = MetricKind.Revenue, Op = CompareOp.Greater, Value = 100000 }
            };

            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 10, Revenue = 200000 }), Is.True);
            Assert.That(condition.Evaluate(new FakeMetrics { VisitorCount = 10, Revenue = 10 }), Is.False);
        }

        [Test]
        public void 条件の説明が日本語になる()
        {
            var condition = new MetricCondition
            {
                Kind = MetricKind.StallQueue,
                StallName = "たこ焼き",
                Op = CompareOp.Greater,
                Value = 20
            };
            Assert.That(condition.Describe(), Does.Contain("たこ焼き.待ち人数"));

            var time = new TimeCondition { MinutesOfDay = 20 * 60 };
            Assert.That(time.Describe(), Does.Contain("20:00"));
        }

        [Test]
        public void 建てられていない屋台の待ち人数はゼロ扱い()
        {
            var condition = new MetricCondition
            {
                Kind = MetricKind.StallQueue,
                StallId = MatsuriIds.Shateki,
                Op = CompareOp.Greater,
                Value = 0
            };
            Assert.That(condition.Evaluate(new FakeMetrics()), Is.False);
        }
    }
}
