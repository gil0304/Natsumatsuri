using Matsuri.Data;
using Matsuri.Visitors;
using NUnit.Framework;
using UnityEngine;

namespace Matsuri.Tests
{
    /// <summary>
    /// 施設（盆踊り場・休憩所・神社・手水舎）の選び方を検査する。
    /// 「疲れていれば休憩所、退屈なら盆踊り場」が成立していないと、
    /// 施設を建てても満足度が上がらない。
    /// </summary>
    public sealed class AmenityScorerTests
    {
        BalanceConfig _balance;

        [SetUp]
        public void SetUp() => _balance = ScriptableObject.CreateInstance<BalanceConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_balance);

        static AmenityDesire Desire(float energy = 80f, float fun = 50f,
            float satisfaction = 60f, float patience = 50f, float fireworks = 60f)
            => new AmenityDesire(energy, fun, satisfaction, patience, fireworks);

        static AmenityCandidate Cand(FacilityEffect effect, float distance = 10f,
            int freeSlots = 5, int capacity = 10)
            => new AmenityCandidate(effect, distance, freeSlots, capacity);

        float S(AmenityDesire d, AmenityCandidate c) => AmenityScorer.Score(d, c, _balance, 0f);

        // ── 空き・妥当性 ──────────────────────────────────

        [Test]
        public void 空きが無い施設は選ばれない()
        {
            float score = S(Desire(), Cand(FacilityEffect.Rest, freeSlots: 0));
            Assert.AreEqual(float.NegativeInfinity, score);
        }

        [Test]
        public void 滞在できない設備は選ばれない()
        {
            Assert.AreEqual(float.NegativeInfinity, S(Desire(), Cand(FacilityEffect.Cleanliness)));
            Assert.AreEqual(float.NegativeInfinity, S(Desire(), Cand(FacilityEffect.Entrance)));
            Assert.AreEqual(float.NegativeInfinity, S(Desire(), Cand(FacilityEffect.Exit)));
        }

        [Test]
        public void バランス設定が無ければ選ばれない()
        {
            Assert.AreEqual(float.NegativeInfinity,
                AmenityScorer.Score(Desire(), Cand(FacilityEffect.Rest), null, 0f));
        }

        // ── 欲求に応じた選択 ──────────────────────────────

        [Test]
        public void 疲れているほど休憩所のスコアが上がる()
        {
            float tired = S(Desire(energy: 8f), Cand(FacilityEffect.Rest));
            float fresh = S(Desire(energy: 95f), Cand(FacilityEffect.Rest));
            Assert.Greater(tired, fresh, "体力が減っても休憩所の魅力が上がっていません。");
        }

        // VisitorArchetype の定義どおり、Fun は「遊びたさ（欲求）」であって
        // 「満たされ度」ではない。高いほど遊びに行きたい。
        [Test]
        public void 遊びたい人ほど盆踊り場のスコアが上がる()
        {
            float wantsToPlay = S(Desire(fun: 95f), Cand(FacilityEffect.Dance));
            float notInterested = S(Desire(fun: 5f), Cand(FacilityEffect.Dance));
            Assert.Greater(wantsToPlay, notInterested, "遊びたさが盆踊り場の魅力に効いていません。");
        }

        [Test]
        public void 満足度が低いほど神社のスコアが上がる()
        {
            float low = S(Desire(satisfaction: 10f), Cand(FacilityEffect.Worship));
            float high = S(Desire(satisfaction: 95f), Cand(FacilityEffect.Worship));
            Assert.Greater(low, high, "満足度が低いときに神社へ向かう動機がありません。");
        }

        [Test]
        public void 疲れているときは盆踊り場より休憩所を選ぶ()
        {
            // 遊びたさは高いままでも、体力が尽きていれば休憩が優先されるべき。
            var d = Desire(energy: 4f, fun: 85f);
            Assert.Greater(S(d, Cand(FacilityEffect.Rest)), S(d, Cand(FacilityEffect.Dance)),
                "疲れ切っているのに踊りに行こうとしています。");
        }

        [Test]
        public void 元気で遊びたいときは休憩所より盆踊り場を選ぶ()
        {
            var d = Desire(energy: 98f, fun: 95f);
            Assert.Greater(S(d, Cand(FacilityEffect.Dance)), S(d, Cand(FacilityEffect.Rest)),
                "元気で遊びたいのに休憩所へ行こうとしています。");
        }

        // ── 距離 ──────────────────────────────────────────

        [Test]
        public void 遠いほどスコアが下がる()
        {
            var d = Desire(energy: 20f);
            float near = S(d, Cand(FacilityEffect.Rest, distance: 3f));
            float far = S(d, Cand(FacilityEffect.Rest, distance: 70f));
            Assert.Greater(near, far, "距離がスコアに効いていません。");
        }

        [Test]
        public void 距離の効きはバランス設定で変えられる()
        {
            var d = Desire(energy: 20f);
            _balance.WeightDistance = 0f;
            float flatNear = S(d, Cand(FacilityEffect.Rest, distance: 3f));
            float flatFar = S(d, Cand(FacilityEffect.Rest, distance: 70f));
            Assert.AreEqual(flatNear, flatFar, 0.001f, "重み0にしても距離が効いています。");
        }

        // ── 混雑 ──────────────────────────────────────────

        [Test]
        public void 空きが少ないほどスコアが下がる()
        {
            var d = Desire(energy: 20f);
            float roomy = S(d, Cand(FacilityEffect.Rest, freeSlots: 10, capacity: 10));
            float crowded = S(d, Cand(FacilityEffect.Rest, freeSlots: 1, capacity: 10));
            Assert.Greater(roomy, crowded, "混雑がスコアに効いていません。");
        }

        // ── ゆらぎ ────────────────────────────────────────

        [Test]
        public void ゆらぎはスコアに加算される()
        {
            var d = Desire(energy: 20f);
            var c = Cand(FacilityEffect.Rest);
            float baseScore = AmenityScorer.Score(d, c, _balance, 0f);
            float noisy = AmenityScorer.Score(d, c, _balance, 5f);
            Assert.AreEqual(baseScore + 5f, noisy, 0.001f);
        }

        [Test]
        public void 手水舎は効果が小さいが選択肢には入る()
        {
            float score = S(Desire(satisfaction: 30f), Cand(FacilityEffect.Purify));
            Assert.AreNotEqual(float.NegativeInfinity, score, "手水舎が常に選ばれません。");
            Assert.Less(score, S(Desire(satisfaction: 30f), Cand(FacilityEffect.Worship)),
                "手水舎が神社より魅力的になっています。");
        }
    }
}
