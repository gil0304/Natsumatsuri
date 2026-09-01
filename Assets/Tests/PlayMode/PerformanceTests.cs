using System;
using System.Collections;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Visitors;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Matsuri.Tests
{
    /// <summary>
    /// 仕様書 §56 / §57 の負荷試験。**300人と1000人で実際に祭りを回して数字を出す**。
    ///
    /// 通常のテスト実行を重くしないため <see cref="ExplicitAttribute"/> を付けてある。
    /// Test Runner で明示的に選ぶか、バッチ実行なら
    ///   -runTests -testCategory Performance
    /// を指定したときだけ走る。
    ///
    /// 結果は <c>[MATSURI-PERF]</c> の1行にまとめて出力する。ログを grep すれば拾える。
    ///
    /// **fps の値そのものは assert しない**。テストが走る機械の性能にも、
    /// -nographics かどうかにも左右されるので、しきい値を書くと嘘になるため (§67)。
    /// 代わりに、環境に左右されない次の3つを assert する:
    ///   1. 例外が1件も出ないこと
    ///   2. 目標人数まで全員がスポーンできること
    ///   3. 管理メモリが人数に対して線形を超えて増えないこと
    /// </summary>
    [Explicit("性能計測は重く、結果が環境依存のため、通常のテスト実行には含めない (§56)。")]
    [Category("Performance")]
    public sealed class PerformanceTests
    {
        /// <summary>計測時間（実時間・秒）。</summary>
        const float MeasureSeconds = 12f;

        /// <summary>計測前に人が散らばるのを待つ時間（実時間・秒）。</summary>
        const float SettleSeconds = 3f;

        /// <summary>1フレームに出現を頼む人数の上限。1000人を1フレームで出すと計測前に山ができる。</summary>
        const int SpawnRequestPerFrame = 120;

        /// <summary>負荷試験用の祭り。屋台6軒＋装飾で、行列も装飾効果も動く状態にする。</summary>
        const string PerfScript =
            "祭り \"負荷試験\" {\n" +
            "    屋台 \"たこ焼き\"   { 場所 -12, 10  値段 500 }\n" +
            "    屋台 \"かき氷\"     { 場所 -4, 10   値段 400 }\n" +
            "    屋台 \"焼きそば\"   { 場所 4, 10    値段 600 }\n" +
            "    屋台 \"金魚すくい\" { 場所 12, 10   値段 300 }\n" +
            "    屋台 \"射的\"       { 場所 -12, -6  値段 300 }\n" +
            "    屋台 \"りんご飴\"   { 場所 4, -6    値段 350 }\n" +
            "    装飾 \"提灯\"       { 場所 0, 2 }\n" +
            "    装飾 \"提灯\"       { 場所 -8, 2 }\n" +
            "}\n";

        MatsuriBootstrap _bootstrap;
        BalanceConfig _clonedBalance;
        int _exceptionCount;
        string _firstException;

        // 直近の計測結果（フェーズをまたいで比べるので持ち回る）
        MatsuriPerformance.PerfReport _report;
        long _managedDelta;
        int _spawnedPeak;

        // ================================================================
        // 準備・後始末
        // ================================================================

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (Resources.Load<MatsuriCatalog>("MatsuriCatalog") == null)
            {
                Assert.Ignore("MatsuriCatalog が Resources に無いためスキップします。" +
                              "メニュー Matsuri/1. Generate Data Assets を実行してください。");
            }

            // 祭りの進行中に出る警告でテストを落とさない。例外だけを自分で数える。
            LogAssert.ignoreFailingMessages = true;
            _exceptionCount = 0;
            _firstException = null;
            Application.logMessageReceived += OnLogMessage;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= OnLogMessage;
            LogAssert.ignoreFailingMessages = false;
            DestroyWorld();
            yield return null;
        }

        void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception) return;
            _exceptionCount++;
            if (_firstException == null) _firstException = condition;
        }

        IEnumerator CreateWorld()
        {
            var go = new GameObject("MatsuriBootstrap (Perf)");
            _bootstrap = go.AddComponent<MatsuriBootstrap>();
            yield return null;   // Awake を通す
            yield return null;
        }

        void DestroyWorld()
        {
            // Destroy は遅延するため、次のフェーズの GameManager.Awake が
            // 古い Instance を見て自滅してしまう。必ず即時破棄する。
            if (_bootstrap != null) UnityEngine.Object.DestroyImmediate(_bootstrap.gameObject);
            _bootstrap = null;

            var root = GameObject.Find("FESTIVAL_ROOT");
            if (root != null) UnityEngine.Object.DestroyImmediate(root);

            if (_clonedBalance != null)
            {
                UnityEngine.Object.DestroyImmediate(_clonedBalance);
                _clonedBalance = null;
            }
        }

        // ── 待機ヘルパ（実時間ベース） ───────────────────────

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            float t = 0f;
            while (t < timeoutSeconds)
            {
                if (condition()) yield break;
                t += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail($"{timeoutSeconds} 秒待っても「{what}」になりませんでした。");
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ================================================================
        // 本体
        // ================================================================

        [UnityTest]
        public IEnumerator Perf_300And1000Visitors()
        {
            // ── 300人 ────────────────────────────────────────
            yield return CreateWorld();
            yield return RunPhase(300);

            var report300 = _report;
            long managed300 = _managedDelta;
            int peak300 = _spawnedPeak;

            DestroyWorld();
            yield return null;
            yield return null;

            // ── 1000人 (§56 の最終目標) ──────────────────────
            yield return CreateWorld();
            yield return RunPhase(1000);

            var report1000 = _report;
            long managed1000 = _managedDelta;
            int peak1000 = _spawnedPeak;

            // ── 判定 ─────────────────────────────────────────

            // 1) 例外が出ていないこと。人数を増やしたときに壊れる箇所はここで出る。
            Assert.Zero(_exceptionCount,
                $"計測中に例外が {_exceptionCount} 件出ました。最初の1件: {_firstException}");

            // 2) 目標人数まで全員が出せること (§56)。
            Assert.GreaterOrEqual(peak300, 300, "300人を出し切れませんでした。");
            Assert.GreaterOrEqual(peak1000, 1000,
                "1000人を出し切れませんでした。プールの上限か生成枠が足りていません (§56)。");

            // 3) メモリが人数に対して線形を超えて増えないこと。
            //    1人あたりの管理メモリで比べる。計測誤差を吸収するため 1人 4KB の下駄をはかせ、
            //    3倍まで許容する。O(n^2) のような増え方をすればここで確実に落ちる。
            long per300 = Math.Max(0L, managed300) / 300L;
            long per1000 = Math.Max(0L, managed1000) / 1000L;
            long allowed = per300 * 3L + 4096L;

            Debug.Log($"{MatsuriPerformance.Prefix} memory per-visitor: " +
                      $"300人={per300}B 1000人={per1000}B 上限={allowed}B " +
                      $"(合計 300人={managed300}B 1000人={managed1000}B)");

            Assert.LessOrEqual(per1000, allowed,
                $"1人あたりの管理メモリが 300人時の3倍を超えました" +
                $"（300人 {per300}B → 1000人 {per1000}B）。人数に対して線形を超えて増えています。");

            // fps は参考値。環境依存なので assert しない。
            Debug.Log($"{MatsuriPerformance.Prefix} 参考: 300人 avgFps={report300.AverageFps:F2} / " +
                      $"1000人 avgFps={report1000.AverageFps:F2}");
        }

        /// <summary>指定人数で祭りを回して計測する。結果はフィールドに置く。</summary>
        IEnumerator RunPhase(int target)
        {
            var game = GameManager.Instance;
            Assert.IsNotNull(game, "GameManager が生成されていません。");
            var visitors = game.Visitors;
            Assert.IsNotNull(visitors, "VisitorManager が生成されていません。");

            // ── 祭りを建てる ─────────────────────────────
            game.RunCode(PerfScript);
            yield return WaitUntil(() => game.Stalls.Stalls.Count >= 6 && !game.Festival.IsBuilding,
                                   45f, "屋台が建ち終わる");

            // ── 上限を目標人数に変える ───────────────────
            // BalanceConfig はプロジェクトのアセットなので、必ず複製に対して行う。
            var source = visitors.Balance != null ? visitors.Balance : game.Balance;
            Assert.IsNotNull(source, "BalanceConfig がありません。");

            _clonedBalance = UnityEngine.Object.Instantiate(source);
            _clonedBalance.name = "BalanceConfig (Perf)";
            _clonedBalance.MaxConcurrentVisitors = target;
            _clonedBalance.MaxTotalVisitors = Mathf.Max(_clonedBalance.MaxTotalVisitors, target * 4);

            // いまのプールを捨てて 0体から作り直す。メモリの増え方を人数で比べるため。
            visitors.ResetAll();
            if (visitors.Pool != null) visitors.Pool.Clear();
            yield return null;

            long baseline = CollectAndMeasure();

            visitors.Initialize(game.Catalog, _clonedBalance);

            // ── 事前生成を待つ（数フレームに分散している）──
            yield return WaitUntil(() => visitors.Pool != null && visitors.Pool.IsPrewarmComplete,
                                   180f, $"{target}体の事前生成が終わる");

            // ── 目標人数まで出す ─────────────────────────
            // 祭りを始める前に出しておく。始めてから出すと、
            // 出し切る前に帰り始める人が出て「全員出せたか」が測れない。
            float spawnTime = 0f;
            while (visitors.CurrentVisitors < target && spawnTime < 60f)
            {
                int want = Mathf.Min(SpawnRequestPerFrame, target - visitors.CurrentVisitors);
                visitors.SpawnBatch(want);
                spawnTime += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }
            _spawnedPeak = visitors.PeakVisitors;

            Debug.Log($"{MatsuriPerformance.Prefix} spawn: 目標={target} 実際={visitors.CurrentVisitors} " +
                      $"プール生成数={(visitors.Pool != null ? visitors.Pool.CreatedCount : 0)} " +
                      $"所要={spawnTime:F2}s");

            // ── 祭りを開催して落ち着かせる ───────────────
            game.StartFestival();
            yield return WaitSeconds(SettleSeconds);

            // ── 計測 ─────────────────────────────────────
            MatsuriPerformance.BeginSample(2);
            yield return WaitSeconds(MeasureSeconds);
            _report = MatsuriPerformance.EndSample();

            long after = CollectAndMeasure();
            _managedDelta = after - baseline;

            // ── 出力 ─────────────────────────────────────
            visitors.Lod.RecountLevels();

            Debug.Log(MatsuriPerformance.Format(in _report, $"{target}人"));
            Debug.Log($"{MatsuriPerformance.Prefix} {target}人 " +
                      $"lodNear={visitors.Lod.NearCount} lodMid={visitors.Lod.MidCount} " +
                      $"lodFar={visitors.Lod.FarCount} " +
                      $"navAgents={visitors.NavAgentsInUse}/{visitors.MaxNavAgents} " +
                      $"simplifyDist={visitors.Lod.SimplifyDistance:F1}m " +
                      $"buckets={visitors.ThinkBucketCount} " +
                      $"nowVisitors={visitors.CurrentVisitors} " +
                      $"avgSatisfaction={visitors.AverageSatisfaction:F1}");

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log($"{MatsuriPerformance.Prefix} 注意: -nographics で実行されています。" +
                          "描画が行われないため fps は参考値です（実機の下限ではありません）。");
            }
        }

        /// <summary>GC を回し切ってから管理メモリを測る。</summary>
        static long CollectAndMeasure()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return GC.GetTotalMemory(true);
        }
    }
}
