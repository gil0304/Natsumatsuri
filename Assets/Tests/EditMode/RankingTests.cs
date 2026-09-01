using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Matsuri.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Matsuri.Tests
{
    /// <summary>
    /// 仕様書 §37 ランキングのテスト。
    /// ローカルJSONの順位付けと、オンライン版の「未設定なら通信しない」
    /// 「失敗したら貯めて次に送る」を確かめる。
    /// </summary>
    public class RankingTests
    {
        /// <summary>通信のフェイク。実際には何処へも繋がず、呼ばれた回数だけ数える。</summary>
        sealed class FakeTransport : IRankingTransport
        {
            public int PostCount;
            public int GetCount;
            public string LastUrl = "";
            public string LastJson = "";
            public string LastApiKey = "";
            public bool Succeed = true;
            public string ResponseBody = "";

            public int TotalCalls => PostCount + GetCount;

            public void Post(string url, string apiKey, string json, float timeoutSeconds, Action<bool, string> onCompleted)
            {
                PostCount++;
                LastUrl = url;
                LastJson = json;
                LastApiKey = apiKey;
                onCompleted?.Invoke(Succeed, ResponseBody);
            }

            public void Get(string url, string apiKey, float timeoutSeconds, Action<bool, string> onCompleted)
            {
                GetCount++;
                LastUrl = url;
                LastApiKey = apiKey;
                onCompleted?.Invoke(Succeed, ResponseBody);
            }
        }

        string _directory;
        LocalJsonRanking _local;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "MatsuriRankingTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            _local = new LocalJsonRanking(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            RankingService.UseLocal(new LocalJsonRanking());

            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
            catch (IOException) { /* テスト用の一時フォルダなので消せなくても無視する */ }
        }

        static FestivalResult MakeResult(string name, long revenue, long score = 0, string date = null)
        {
            return new FestivalResult
            {
                FestivalName = name,
                Revenue = revenue,
                TotalScore = score == 0 ? revenue : score,
                CreatedDate = date ?? "2026-08-21 18:00:00",
                SourceCode = $"祭り「{name}」"
            };
        }

        // ── ローカルランキング ────────────────────────────────

        [Test]
        public void 登録した結果が上位一覧に出る()
        {
            _local.Submit(MakeResult("たこ焼き祭", 500000L));

            var top = _local.GetTop(10);

            Assert.That(top.Count, Is.EqualTo(1));
            Assert.That(top[0].FestivalName, Is.EqualTo("たこ焼き祭"));
            Assert.That(top[0].Revenue, Is.EqualTo(500000L));
        }

        [Test]
        public void 上位一覧は売上の降順になる()
        {
            _local.Submit(MakeResult("A", 1105000L));
            _local.Submit(MakeResult("B", 1482500L));
            _local.Submit(MakeResult("C", 1320300L));

            var top = _local.GetTop(3);

            Assert.That(top[0].Revenue, Is.EqualTo(1482500L));
            Assert.That(top[1].Revenue, Is.EqualTo(1320300L));
            Assert.That(top[2].Revenue, Is.EqualTo(1105000L));
        }

        [Test]
        public void 件数が足りなければあるだけ返す()
        {
            _local.Submit(MakeResult("A", 100L));
            _local.Submit(MakeResult("B", 200L));

            Assert.That(_local.GetTop(50).Count, Is.EqualTo(2));
        }

        [Test]
        public void 件数が0以下なら空を返す()
        {
            _local.Submit(MakeResult("A", 100L));

            Assert.That(_local.GetTop(0).Count, Is.EqualTo(0));
            Assert.That(_local.GetTop(-3).Count, Is.EqualTo(0));
        }

        [Test]
        public void 一番売れた結果が1位になる()
        {
            var best = MakeResult("最高", 900000L);
            _local.Submit(MakeResult("A", 100000L));
            _local.Submit(best);
            _local.Submit(MakeResult("C", 300000L));

            Assert.That(_local.GetRank(best), Is.EqualTo(1));
        }

        [Test]
        public void 未登録の結果でも何位になるか分かる()
        {
            _local.Submit(MakeResult("A", 100000L));
            _local.Submit(MakeResult("B", 300000L));

            var candidate = MakeResult("これから", 200000L, 200000L, "2026-08-21 20:00:00");

            Assert.That(_local.GetRank(candidate), Is.EqualTo(2));
        }

        [Test]
        public void 同じ結果は二重登録されない()
        {
            var result = MakeResult("同じ祭", 500000L);

            _local.Submit(result);
            _local.Submit(result.Clone());

            Assert.That(_local.Count, Is.EqualTo(1));
        }

        [Test]
        public void 壊れたファイルは読み飛ばして空から始まる()
        {
            LogAssert.ignoreFailingMessages = true;

            File.WriteAllText(Path.Combine(_directory, LocalJsonRanking.FileName),
                "{{ これは壊れたJSON ]]", new UTF8Encoding(false));

            var broken = new LocalJsonRanking(_directory);

            Assert.That(broken.Count, Is.EqualTo(0));

            broken.Submit(MakeResult("復旧", 1000L));

            Assert.That(broken.Count, Is.EqualTo(1));
            Assert.That(broken.GetTop(1)[0].FestivalName, Is.EqualTo("復旧"));
        }

        [Test]
        public void 長すぎるソースコードは切り詰められる()
        {
            var result = MakeResult("長い祭", 100L);
            result.SourceCode = new string('あ', LocalJsonRanking.MaxSourceLength + 500);

            _local.Submit(result);

            Assert.That(_local.GetTop(1)[0].SourceCode.Length, Is.EqualTo(LocalJsonRanking.MaxSourceLength));
        }

        [Test]
        public void ローカルランキングはオンラインではない()
        {
            Assert.That(_local.IsOnline, Is.False);
            Assert.That(_local.DisplayName, Is.Not.Empty);
        }

        [Test]
        public void 送信キューに積んで取り出して消せる()
        {
            Assert.That(_local.PendingCount, Is.EqualTo(0));

            _local.EnqueuePending(MakeResult("A", 100L));
            _local.EnqueuePending(MakeResult("B", 200L));

            Assert.That(_local.PendingCount, Is.EqualTo(2));

            var pending = _local.GetPending();
            Assert.That(pending[0].FestivalName, Is.EqualTo("A"));

            _local.RemovePending(pending[0]);
            Assert.That(_local.PendingCount, Is.EqualTo(1));

            _local.ClearPending();
            Assert.That(_local.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void 送信キューは同じ結果を二重に積まない()
        {
            var result = MakeResult("A", 100L);

            _local.EnqueuePending(result);
            _local.EnqueuePending(result.Clone());

            Assert.That(_local.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void 送信キューはファイルに残り読み直せる()
        {
            _local.EnqueuePending(MakeResult("A", 100L));

            var reopened = new LocalJsonRanking(_directory);

            Assert.That(reopened.PendingCount, Is.EqualTo(1));
            Assert.That(reopened.GetPending()[0].FestivalName, Is.EqualTo("A"));
        }

        // ── オンラインランキング ──────────────────────────────

        [Test]
        public void 送信先が未設定なら一切通信しない()
        {
            var fake = new FakeTransport();
            var remote = new RemoteRanking("", "", _local, fake);

            remote.Submit(MakeResult("A", 100L));
            remote.FlushQueue();
            remote.GetTop(10);
            remote.RefreshTop(10);

            Assert.That(remote.IsConfigured, Is.False);
            Assert.That(fake.TotalCalls, Is.EqualTo(0));
            Assert.That(remote.IsOnline, Is.False);
        }

        [Test]
        public void 送信先が未設定なら送信キューにも積まない()
        {
            var fake = new FakeTransport();
            var remote = new RemoteRanking("", "", _local, fake);

            remote.Submit(MakeResult("A", 100L));

            Assert.That(_local.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void httpでないURLは未設定として扱う()
        {
            var fake = new FakeTransport();
            var remote = new RemoteRanking("ftp://example.test/ranking", "", _local, fake);

            remote.Submit(MakeResult("A", 100L));

            Assert.That(remote.IsConfigured, Is.False);
            Assert.That(fake.TotalCalls, Is.EqualTo(0));
        }

        [Test]
        public void 送信先が設定されていればPOSTする()
        {
            var fake = new FakeTransport { Succeed = true };
            var remote = new RemoteRanking("https://example.test/matsuri", "key123", _local, fake);

            remote.Submit(MakeResult("A", 100L));

            Assert.That(fake.PostCount, Is.EqualTo(1));
            Assert.That(fake.LastUrl, Is.EqualTo("https://example.test/matsuri/submit"));
            Assert.That(fake.LastApiKey, Is.EqualTo("key123"));
            Assert.That(fake.LastJson, Does.Contain("MATSURI.exe"));
            Assert.That(remote.IsOnline, Is.True);
            Assert.That(remote.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void 送信に失敗したら貯めてオフラインになる()
        {
            var fake = new FakeTransport { Succeed = false, ResponseBody = "接続できません" };
            var remote = new RemoteRanking("https://example.test/matsuri", "", _local, fake);

            remote.Submit(MakeResult("A", 100L));

            Assert.That(remote.IsOnline, Is.False);
            Assert.That(remote.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void 次にオンラインになったら貯めた分をまとめて送る()
        {
            var fake = new FakeTransport { Succeed = false };
            var remote = new RemoteRanking("https://example.test/matsuri", "", _local, fake);

            remote.Submit(MakeResult("A", 100L, 100L, "2026-08-21 18:00:00"));
            remote.Submit(MakeResult("B", 200L, 200L, "2026-08-21 18:00:01"));
            remote.Submit(MakeResult("C", 300L, 300L, "2026-08-21 18:00:02"));

            Assert.That(remote.PendingCount, Is.EqualTo(3));

            fake.Succeed = true;
            int postsBefore = fake.PostCount;
            remote.FlushQueue();

            Assert.That(fake.PostCount, Is.EqualTo(postsBefore + 1));   // 1回でまとめて送る
            Assert.That(remote.PendingCount, Is.EqualTo(0));
            Assert.That(remote.IsOnline, Is.True);
        }

        [Test]
        public void 取得した一覧が上位一覧に反映される()
        {
            var fake = new FakeTransport
            {
                Succeed = true,
                ResponseBody = "{\"Entries\":[{\"FestivalName\":\"小\",\"Revenue\":500}," +
                               "{\"FestivalName\":\"大\",\"Revenue\":900}]}"
            };
            var remote = new RemoteRanking("https://example.test/matsuri", "", _local, fake);

            var top = remote.GetTop(2);

            Assert.That(fake.GetCount, Is.EqualTo(1));
            Assert.That(top.Count, Is.EqualTo(2));
            Assert.That(top[0].Revenue, Is.EqualTo(900L));
            Assert.That(top[1].Revenue, Is.EqualTo(500L));
        }

        [Test]
        public void 配列だけを返すサーバーの応答も読める()
        {
            var fake = new FakeTransport
            {
                Succeed = true,
                ResponseBody = "[{\"FestivalName\":\"大\",\"Revenue\":900}]"
            };
            var remote = new RemoteRanking("https://example.test/matsuri", "", _local, fake);

            var top = remote.GetTop(5);

            Assert.That(top.Count, Is.EqualTo(1));
            Assert.That(top[0].Revenue, Is.EqualTo(900L));
        }

        [Test]
        public void まだ取得できていなければローカルの記録を見せる()
        {
            _local.Submit(MakeResult("ローカルの記録", 777L));

            var fake = new FakeTransport { Succeed = false };
            var remote = new RemoteRanking("https://example.test/matsuri", "", _local, fake);

            var top = remote.GetTop(5);

            Assert.That(top.Count, Is.EqualTo(1));
            Assert.That(top[0].FestivalName, Is.EqualTo("ローカルの記録"));
            Assert.That(remote.GetRank(top[0]), Is.EqualTo(1));
        }

        // ── RankingService ────────────────────────────────────

        [Test]
        public void SubmitBothはローカルに必ず残す()
        {
            RankingService.UseLocal(_local);

            RankingService.SubmitBoth(MakeResult("両方", 12345L));

            Assert.That(_local.Count, Is.EqualTo(1));
            Assert.That(RankingService.GetTop(1)[0].Revenue, Is.EqualTo(12345L));
        }

        [Test]
        public void SubmitBothはオンライン側にも送る()
        {
            RankingService.UseLocal(_local);

            var fake = new FakeTransport { Succeed = true };
            RankingService.Current = new RemoteRanking("https://example.test/matsuri", "", _local, fake);

            RankingService.SubmitBoth(MakeResult("両方", 999L));

            Assert.That(_local.Count, Is.EqualTo(1));
            Assert.That(fake.PostCount, Is.EqualTo(1));
        }

        [Test]
        public void 送信先が空ならローカルのままにする()
        {
            RankingService.UseLocal(_local);

            bool switched = RankingService.UseRemote("");

            Assert.That(switched, Is.False);
            Assert.That(RankingService.Current, Is.SameAs(RankingService.Local));
            Assert.That(RankingService.IsOnline, Is.False);
        }

        [Test]
        public void nullの結果を送っても落ちない()
        {
            RankingService.UseLocal(_local);

            Assert.DoesNotThrow(() => RankingService.SubmitBoth(null));
            Assert.That(RankingService.GetRank(null), Is.EqualTo(0));
            Assert.That(_local.Count, Is.EqualTo(0));
        }

        [Test]
        public void 上位一覧の取得は例外を投げない()
        {
            RankingService.UseLocal(_local);

            List<FestivalResult> top = null;

            Assert.DoesNotThrow(() => top = RankingService.GetTop(5));
            Assert.That(top, Is.Not.Null);
            Assert.That(top.Count, Is.EqualTo(0));
        }
    }
}
