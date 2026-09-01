using System.Globalization;
using System.IO;
using Matsuri.Save;
using NUnit.Framework;
using UnityEngine;

namespace Matsuri.Tests
{
    /// <summary>
    /// 仕様書 §46 / §77 BATTLE MODE のテスト。
    /// 「同じ条件で作って売上で比べる」ことと、その公平性（同じ乱数種）を確かめる。
    /// </summary>
    public class BattleModeTests
    {
        string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "MatsuriBattleTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            BattleMode.UseDirectory(_directory);
            BattleMode.EndSession();
        }

        [TearDown]
        public void TearDown()
        {
            BattleMode.EndSession();
            BattleMode.UseDirectory(null);

            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
            catch (IOException) { /* テスト用の一時フォルダなので消せなくても無視する */ }
        }

        static FestivalResult MakeResult(string name, long revenue, long score = 0,
                                         int visitors = 0, float satisfaction = 0f)
        {
            return new FestivalResult
            {
                FestivalName = name,
                Revenue = revenue,
                TotalScore = score == 0 ? revenue : score,
                VisitorCount = visitors,
                AverageSatisfaction = satisfaction,
                CreatedDate = "2026-08-21 18:00:00",
                SourceCode = $"祭り「{name}」"
            };
        }

        static ChallengeDefinition MakeChallenge()
        {
            return new ChallengeDefinition("battle_test", "テスト勝負", "同じ条件で勝負する。", 750000L)
            {
                TimeLimitMinutes = 180,
                MinX = -30f,
                MaxX = 30f,
                MinZ = -25f,
                MaxZ = 25f
            };
        }

        // ── セッション ────────────────────────────────────────

        [Test]
        public void セッションを始めるとお題と予算と乱数種が入る()
        {
            var session = BattleMode.StartSession(MakeChallenge(), 12345);

            Assert.That(session, Is.Not.Null);
            Assert.That(session.Id, Is.Not.Empty);
            Assert.That(session.ChallengeId, Is.EqualTo("battle_test"));
            Assert.That(session.Budget, Is.EqualTo(750000L));
            Assert.That(session.Seed, Is.EqualTo(12345));
            Assert.That(session.EntryCount, Is.EqualTo(0));
            Assert.That(BattleMode.Current, Is.SameAs(session));
        }

        [Test]
        public void お題を渡さなければ既定のお題で始まる()
        {
            var session = BattleMode.StartSession(null, 7);

            Assert.That(session, Is.Not.Null);
            Assert.That(session.ChallengeId, Is.Not.Empty);
            Assert.That(session.Budget, Is.GreaterThan(0L));
        }

        [Test]
        public void セッションが無いあいだは乱数種を持たない()
        {
            Assert.That(BattleMode.Current, Is.Null);
            Assert.That(BattleMode.CurrentSeed.HasValue, Is.False);
        }

        [Test]
        public void セッション中だけ乱数種が読める()
        {
            BattleMode.StartSession(MakeChallenge(), 424242);

            Assert.That(BattleMode.CurrentSeed.HasValue, Is.True);
            Assert.That(BattleMode.CurrentSeed.Value, Is.EqualTo(424242));

            BattleMode.EndSession();

            Assert.That(BattleMode.CurrentSeed.HasValue, Is.False);
            Assert.That(BattleMode.Current, Is.Null);
        }

        [Test]
        public void 同じ乱数種なら同じ乱数列になる()
        {
            // 公平性の前提。VisitorManager はこの種で来場者の乱数を初期化する。
            var a = BattleMode.StartSession(MakeChallenge(), 999);
            int seed = a.Seed;

            Random.InitState(seed);
            float first1 = Random.value;
            float first2 = Random.value;

            Random.InitState(seed);
            Assert.That(Random.value, Is.EqualTo(first1));
            Assert.That(Random.value, Is.EqualTo(first2));
        }

        [Test]
        public void セッション中の乱数はセッションの種で作られる()
        {
            BattleMode.StartSession(MakeChallenge(), 20260821);

            var a = BattleMode.CreateRandom(1);
            var b = BattleMode.CreateRandom(999);   // fallback は無視される

            Assert.That(a.Next(), Is.EqualTo(b.Next()));
            Assert.That(a.Next(), Is.EqualTo(b.Next()));
        }

        [Test]
        public void セッションが無ければ渡した種で乱数を作る()
        {
            Assert.That(BattleMode.CurrentSeed.HasValue, Is.False);

            var a = BattleMode.CreateRandom(555);
            var b = BattleMode.CreateRandom(555);

            Assert.That(a.Next(), Is.EqualTo(b.Next()));
            Assert.That(a.Next(), Is.EqualTo(b.Next()));
        }

        [Test]
        public void お題に戻すと予算と敷地が引き継がれる()
        {
            var session = BattleMode.StartSession(MakeChallenge(), 3);
            var challenge = session.ToChallenge();

            Assert.That(challenge.Budget, Is.EqualTo(750000L));
            Assert.That(challenge.TimeLimitMinutes, Is.EqualTo(180));
            Assert.That(challenge.MinX, Is.EqualTo(-30f));
            Assert.That(challenge.MaxZ, Is.EqualTo(25f));
        }

        // ── 投稿 ──────────────────────────────────────────────

        [Test]
        public void 結果を投稿すると参加者が増える()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("ひかり", "祭り「ひかり祭」", MakeResult("ひかり祭", 100000L));

            Assert.That(BattleMode.Current.EntryCount, Is.EqualTo(1));

            var entry = BattleMode.Current.Entries[0];
            Assert.That(entry.PlayerName, Is.EqualTo("ひかり"));
            Assert.That(entry.Revenue, Is.EqualTo(100000L));
            Assert.That(entry.SourceCode, Is.EqualTo("祭り「ひかり祭」"));
            Assert.That(entry.SubmittedAt, Is.Not.Empty);
        }

        [Test]
        public void 複数人が投稿できる()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("A", null, MakeResult("A祭", 100L));
            BattleMode.Submit("B", null, MakeResult("B祭", 200L));
            BattleMode.Submit("C", null, MakeResult("C祭", 300L));

            Assert.That(BattleMode.Current.EntryCount, Is.EqualTo(3));
        }

        [Test]
        public void 順位は売上の降順になる()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("A", null, MakeResult("A祭", 1105000L));
            BattleMode.Submit("B", null, MakeResult("B祭", 1482500L));
            BattleMode.Submit("C", null, MakeResult("C祭", 1320300L));

            var ranking = BattleMode.GetRanking();

            Assert.That(ranking.Count, Is.EqualTo(3));
            Assert.That(ranking[0].Revenue, Is.EqualTo(1482500L));
            Assert.That(ranking[1].Revenue, Is.EqualTo(1320300L));
            Assert.That(ranking[2].Revenue, Is.EqualTo(1105000L));
        }

        [Test]
        public void 売上が同点ならスコアの高い方が上になる()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("低スコア", null, MakeResult("A祭", 500000L, 10L));
            BattleMode.Submit("高スコア", null, MakeResult("B祭", 500000L, 900L));

            var ranking = BattleMode.GetRanking();

            Assert.That(ranking[0].PlayerName, Is.EqualTo("高スコア"));
            Assert.That(ranking[1].PlayerName, Is.EqualTo("低スコア"));
        }

        [Test]
        public void 売上もスコアも同点なら投稿が早い方が上になる()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("あと", null, MakeResult("A祭", 500000L, 500L));
            BattleMode.Submit("さき", null, MakeResult("B祭", 500000L, 500L));

            // 秒までしか記録しないので、投稿時刻を明示的にずらして順序を確かめる。
            BattleMode.Current.Entries[0].SubmittedAt = "2026-08-21 19:00:10";
            BattleMode.Current.Entries[1].SubmittedAt = "2026-08-21 19:00:01";

            var ranking = BattleMode.GetRanking();

            Assert.That(ranking[0].PlayerName, Is.EqualTo("さき"));
            Assert.That(ranking[1].PlayerName, Is.EqualTo("あと"));
        }

        [Test]
        public void 同じ名前で投稿したら良い方だけが残る()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("たろう", null, MakeResult("1回目", 200000L));
            BattleMode.Submit("たろう", null, MakeResult("2回目", 900000L));
            BattleMode.Submit("たろう", null, MakeResult("3回目", 300000L));

            Assert.That(BattleMode.Current.EntryCount, Is.EqualTo(1));
            Assert.That(BattleMode.Current.Entries[0].Revenue, Is.EqualTo(900000L));
        }

        [Test]
        public void 名前から順位が引ける()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("A", null, MakeResult("A祭", 100L));
            BattleMode.Submit("B", null, MakeResult("B祭", 300L));

            Assert.That(BattleMode.GetRank("B"), Is.EqualTo(1));
            Assert.That(BattleMode.GetRank("A"), Is.EqualTo(2));
            Assert.That(BattleMode.GetRank("いない人"), Is.EqualTo(0));
        }

        [Test]
        public void セッションが無いときの投稿は何もしない()
        {
            Assert.That(BattleMode.Current, Is.Null);

            Assert.DoesNotThrow(() => BattleMode.Submit("だれか", null, MakeResult("祭", 100L)));
            Assert.That(BattleMode.GetRanking().Count, Is.EqualTo(0));
        }

        [Test]
        public void 結果がnullなら投稿されない()
        {
            BattleMode.StartSession(MakeChallenge(), 1);

            Assert.DoesNotThrow(() => BattleMode.Submit("だれか", null, null));
            Assert.That(BattleMode.Current.EntryCount, Is.EqualTo(0));
        }

        [Test]
        public void 空のセッションの順位表は空になる()
        {
            BattleMode.StartSession(MakeChallenge(), 1);

            var ranking = BattleMode.GetRanking();

            Assert.That(ranking, Is.Not.Null);
            Assert.That(ranking.Count, Is.EqualTo(0));
        }

        [Test]
        public void 同じ条件でもう一度は投稿だけ消して乱数種を保つ()
        {
            var session = BattleMode.StartSession(MakeChallenge(), 5150);
            BattleMode.Submit("A", null, MakeResult("A祭", 100L));
            string firstId = session.Id;

            BattleMode.RestartSameConditions();

            Assert.That(BattleMode.Current.EntryCount, Is.EqualTo(0));
            Assert.That(BattleMode.Current.Seed, Is.EqualTo(5150));
            Assert.That(BattleMode.Current.Budget, Is.EqualTo(750000L));
            Assert.That(BattleMode.Current.Id, Is.Not.EqualTo(firstId));
        }

        // ── 保存と読み込み ────────────────────────────────────

        [Test]
        public void 保存して読み込むと同じ内容になる()
        {
            var session = BattleMode.StartSession(MakeChallenge(), 24680);
            BattleMode.Submit("ひかり", "祭り「ひかり祭」", MakeResult("ひかり祭", 1482500L, 4000L, 120, 0.82f));
            BattleMode.Submit("かえで", null, MakeResult("かえで祭", 1320300L));

            Assert.That(BattleMode.SaveSession(), Is.True);

            var loaded = BattleMode.LoadSession(session.Id);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Id, Is.EqualTo(session.Id));
            Assert.That(loaded.Seed, Is.EqualTo(24680));
            Assert.That(loaded.Budget, Is.EqualTo(750000L));
            Assert.That(loaded.EntryCount, Is.EqualTo(2));

            var top = loaded.Entries[0];
            Assert.That(top.PlayerName, Is.EqualTo("ひかり"));
            Assert.That(top.Revenue, Is.EqualTo(1482500L));
            Assert.That(top.SourceCode, Is.EqualTo("祭り「ひかり祭」"));
            Assert.That(top.Result.VisitorCount, Is.EqualTo(120));
        }

        [Test]
        public void 保存した勝負は一覧に出る()
        {
            var session = BattleMode.StartSession(MakeChallenge(), 11);
            BattleMode.SaveSession();

            var ids = BattleMode.ListSessions();

            Assert.That(ids, Is.Not.Null);
            Assert.That(ids, Contains.Item(session.Id));
        }

        [Test]
        public void 無いIDを読むとnullになる()
        {
            Assert.That(BattleMode.LoadSession("battle_no_such_id"), Is.Null);
            Assert.That(BattleMode.LoadSession(""), Is.Null);
        }

        [Test]
        public void 読み込んだ勝負を再開できる()
        {
            var session = BattleMode.StartSession(MakeChallenge(), 4321);
            BattleMode.Submit("A", null, MakeResult("A祭", 100L));
            BattleMode.SaveSession();
            string id = session.Id;

            BattleMode.EndSession();
            Assert.That(BattleMode.CurrentSeed.HasValue, Is.False);

            var loaded = BattleMode.LoadSession(id);
            BattleMode.ResumeSession(loaded);

            Assert.That(BattleMode.Current, Is.SameAs(loaded));
            Assert.That(BattleMode.CurrentSeed.Value, Is.EqualTo(4321));
            Assert.That(BattleMode.GetRanking().Count, Is.EqualTo(1));
        }

        [Test]
        public void 保存した勝負を削除できる()
        {
            var session = BattleMode.StartSession(MakeChallenge(), 77);
            BattleMode.SaveSession();

            Assert.That(BattleMode.DeleteSession(session.Id), Is.True);
            Assert.That(BattleMode.ListSessions(), Does.Not.Contain(session.Id));
        }

        [Test]
        public void 投稿の説明文に売上が入る()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("ひかり", null, MakeResult("A祭", 1482500L));

            string line = BattleMode.Current.Entries[0].ToString();

            Assert.That(line, Does.Contain("ひかり"));
            Assert.That(line, Does.Contain(1482500L.ToString("N0", CultureInfo.InvariantCulture)));
        }

        [Test]
        public void 満足度は百分率に正規化される()
        {
            BattleMode.StartSession(MakeChallenge(), 1);
            BattleMode.Submit("A", null, MakeResult("A祭", 100L, 100L, 10, 0.75f));
            BattleMode.Submit("B", null, MakeResult("B祭", 90L, 90L, 10, 68f));

            Assert.That(BattleMode.Current.Entries[0].SatisfactionPercent, Is.EqualTo(75f).Within(0.01f));
            Assert.That(BattleMode.Current.Entries[1].SatisfactionPercent, Is.EqualTo(68f).Within(0.01f));
        }
    }
}
