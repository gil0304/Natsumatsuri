using System;
using System.Collections;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Script;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Matsuri.Tests
{
    /// <summary>
    /// 仕様書 §73 の MVP 完成条件を、そのまま一本の自動テストにしたもの。
    ///
    ///   コードを書く → RUN → 屋台が建つ → 祭り開始 → NPCが来る
    ///   → 屋台へ歩く → 並ぶ → 購入 → 売上が増える → 22:00 → 結果
    ///
    /// これが緑なら MVP は動いている。
    ///
    /// 注意: バッチモードのフレームレートは実時間と無関係に速いので、
    /// 待機は必ず「実時間の秒」で行い、フレーム数で待たないこと。
    /// </summary>
    public sealed class FestivalIntegrationTests
    {
        MatsuriBootstrap _bootstrap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (Resources.Load<MatsuriCatalog>("MatsuriCatalog") == null)
            {
                Assert.Ignore("MatsuriCatalog が Resources に無いためスキップします。" +
                              "メニュー Matsuri/1. Generate Data Assets を実行してください。");
            }

            var go = new GameObject("MatsuriBootstrap (Test)");
            _bootstrap = go.AddComponent<MatsuriBootstrap>();
            yield return null;   // Awake を通す
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Destroy は遅延するため、次のテストの GameManager.Awake が
            // 古い Instance を見て自滅してしまう。必ず即時破棄する。
            if (_bootstrap != null) UnityEngine.Object.DestroyImmediate(_bootstrap.gameObject);

            var root = GameObject.Find("FESTIVAL_ROOT");
            if (root != null) UnityEngine.Object.DestroyImmediate(root);

            _bootstrap = null;
            yield return null;
        }

        // ── 待機ヘルパ（実時間ベース） ───────────────────────

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            float t = 0f;
            while (t < timeoutSeconds)
            {
                if (condition()) yield break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail($"{timeoutSeconds} 秒待っても「{what}」になりませんでした。");
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ── §73 の各ステップ ─────────────────────────────────

        [UnityTest]
        public IEnumerator Step1_RunCode_BuildsStall()
        {
            var game = GameManager.Instance;
            Assert.IsNotNull(game, "GameManager が生成されていません。");
            Assert.IsNotNull(game.Catalog, "カタログが読み込まれていません。");

            // §73 のプレイヤーが最初に書くコード、そのまま
            game.RunCode("屋台 \"たこ焼き\" {\n    場所 5, 10\n    値段 500\n}\n");

            yield return WaitUntil(() => game.Stalls.Stalls.Count >= 1, 20f, "屋台が建つ");

            var stall = game.Stalls.Stalls[0];
            Assert.AreEqual(MatsuriIds.Takoyaki, stall.Data.Id, "建ったのがたこ焼き屋ではありません。");
            Assert.AreEqual(500, stall.Price, "値段が反映されていません。");

            // §23: 屋台は箱一個ではなく、多パーツ構成であること
            var renderers = stall.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 8,
                $"屋台のパーツが {renderers.Length} 個しかありません。§79「Unity Cube丸出し」を禁止しています。");

            // §30: 行列の受け皿があること
            Assert.Greater(stall.QueuePoints.Length, 0, "QueuePoint がありません。");

            // §31: 予算から建設費が引かれていること
            Assert.Less(game.Economy.Budget, game.Catalog.InitialBudget, "予算が減っていません。");
        }

        [UnityTest]
        public IEnumerator Step2_ErrorCode_DoesNotBuild()
        {
            var game = GameManager.Instance;

            // 「場所」が無い → §41 のエラーになり、祭りは作られない
            game.RunCode("屋台 \"たこ焼き\" {\n    値段 500\n}\n");
            yield return WaitSeconds(1.5f);

            Assert.AreEqual(0, game.Stalls.Stalls.Count, "エラーコードなのに屋台が建ってしまいました。");

            var plan = game.Script.LastPlan;
            Assert.IsNotNull(plan);
            Assert.IsTrue(plan.HasErrors, "「場所」未設定がエラーになっていません。");

            var diag = plan.Diagnostics[0];
            Assert.AreEqual(DiagnosticSeverity.Error, diag.Severity);
            StringAssert.Contains("場所", diag.Message, "エラーメッセージが日本語で場所に言及していません。");
            Assert.Greater(diag.Line, 0, "エラーに行番号がありません (§42)。");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Step3_FullFestival_ProducesRevenue()
        {
            var game = GameManager.Instance;

            game.RunCode(
                "屋台 \"たこ焼き\"   { 場所 5, 10   値段 500 }\n" +
                "屋台 \"かき氷\"     { 場所 12, 10  値段 400 }\n" +
                "屋台 \"金魚すくい\" { 場所 -5, 8   値段 300 }\n" +
                "装飾 \"提灯\"       { 場所 3, 9 }\n" +
                "設備 \"ベンチ\"     { 場所 0, 4 }\n");

            yield return WaitUntil(() => game.Stalls.Stalls.Count >= 3, 25f, "屋台が3軒建つ");

            game.StartFestival();
            Assert.AreEqual(GamePhase.Running, game.Phase, "開催しても Running になっていません。");

            // NPC が来ること (§28)
            yield return WaitUntil(() => game.Visitors.CurrentVisitors > 0, 30f, "NPCが入場する");

            // 誰かが買うこと (§32)
            // 祭りはもともと実時間2分に圧縮されている (§7)。
            // ここでさらに早送りすると、NPC が屋台へ歩き着く前に祭りが終わってしまう。
            // 等速のまま待つ。
            yield return WaitUntil(() => game.Economy.Revenue > 0, 70f, "売上が発生する");

            Debug.Log($"[MATSURI-BUILD] 購入発生: 売上={game.Economy.Revenue} " +
                      $"来場={game.Visitors.TotalVisitors} 現在={game.Visitors.CurrentVisitors}");

            // 22:00 まで進めて結果画面まで到達すること (§8, §36)。
            // 残りは客が歩き終わっていればよいので、控えめに早送りする。
            game.Time.Speed = 4f;
            yield return WaitUntil(() => game.Phase == GamePhase.Finished, 150f, "22:00で祭りが終わる");

            Assert.Greater(game.Visitors.TotalVisitors, 10, "来場者が少なすぎます。");
            Assert.Greater(game.Economy.Revenue, 0, "売上が 0 のままです。");
            Assert.Greater(game.Visitors.PeakVisitors, 0, "最高同時来場者が記録されていません。");

            Debug.Log($"[MATSURI-BUILD] 結果: 売上=¥{game.Economy.Revenue:N0} " +
                      $"来場者={game.Visitors.TotalVisitors}人 " +
                      $"最高同時={game.Visitors.PeakVisitors}人 " +
                      $"平均満足度={game.Visitors.AverageSatisfaction:0.##} " +
                      $"人気No.1={(game.Stalls.MostPopular != null ? game.Stalls.MostPopular.Data.DisplayName : "-")}");
        }

        [UnityTest]
        public IEnumerator Step4_TimeTrigger_FiresFireworks()
        {
            var game = GameManager.Instance;

            // §80 の象徴的な瞬間: プレイヤーが書いた時刻に花火が上がる
            game.RunCode(
                "屋台 \"たこ焼き\" { 場所 5, 10  値段 500 }\n" +
                "時間 20:00 {\n    花火 \"大玉\"\n}\n");

            yield return WaitUntil(() => game.Stalls.Stalls.Count >= 1, 20f, "屋台が建つ");

            var plan = game.Script.LastPlan;
            Assert.AreEqual(1, plan.Rules.Count, "時間ルールが登録されていません (§15)。");

            game.StartFestival();
            game.Time.Speed = 8f;

            yield return WaitUntil(() => game.Time.Clock.MinutesOfDay >= 20 * 60, 90f, "20:00になる");

            // 20:00 を過ぎたらルールが発火していること
            yield return WaitSeconds(1.0f);
            Assert.IsTrue(plan.Rules[0].Fired, "20:00 になっても花火のルールが発火していません。");

            Debug.Log("[MATSURI-BUILD] 20:00 の花火トリガー発火を確認");
        }

        [UnityTest]
        public IEnumerator Step5_ConditionTrigger_IsEvaluated()
        {
            var game = GameManager.Instance;

            // §14 の条件分岐が開催中に評価されること
            game.RunCode(
                "屋台 \"たこ焼き\" { 場所 5, 10  値段 500 }\n" +
                "もし 来場者数 > 5 {\n" +
                "    屋台 \"焼きそば\" { 場所 12, 10  値段 600 }\n" +
                "}\n");

            yield return WaitUntil(() => game.Stalls.Stalls.Count >= 1, 20f, "最初の屋台が建つ");
            Assert.AreEqual(1, game.Stalls.Stalls.Count, "条件の中の屋台が最初から建ってしまっています。");

            game.StartFestival();
            game.Time.Speed = 1f;

            yield return WaitUntil(() => game.Stalls.Stalls.Count >= 2, 90f, "条件成立で焼きそばが建つ");

            Debug.Log($"[MATSURI-BUILD] 条件分岐で増築を確認 (来場者={game.Visitors.TotalVisitors})");
        }
    }
}
