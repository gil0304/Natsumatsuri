using System.Collections;
using System.IO;
using System.Linq;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Script;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Matsuri.Tests
{
    /// <summary>
    /// 同梱しているサンプル・デモコードが、
    /// 実際のカタログに対して**エラーなくコンパイルできる**ことを保証する。
    /// 屋台名や設備名を変えたときに、デモが黙って壊れるのを防ぐ。
    /// </summary>
    public sealed class SampleCodeTests
    {
        MatsuriBootstrap _bootstrap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (Resources.Load<MatsuriCatalog>("MatsuriCatalog") == null)
                Assert.Ignore("MatsuriCatalog が Resources にありません。");

            var go = new GameObject("MatsuriBootstrap (Test)");
            _bootstrap = go.AddComponent<MatsuriBootstrap>();
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_bootstrap != null) Object.DestroyImmediate(_bootstrap.gameObject);
            var root = GameObject.Find("FESTIVAL_ROOT");
            if (root != null) Object.DestroyImmediate(root);
            yield return null;
        }

        [Test]
        public void EverySampleFileCompilesWithoutErrors()
        {
            var catalog = GameManager.Instance.Catalog;
            var dir = Path.Combine(Application.streamingAssetsPath, "MatsuriSamples");
            Assert.IsTrue(Directory.Exists(dir), $"サンプルの置き場所がありません: {dir}");

            var files = Directory.GetFiles(dir, "*.matsuri").OrderBy(f => f).ToArray();
            Assert.Greater(files.Length, 0, "サンプルが1つもありません。");

            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                var plan = MatsuriCompiler.Compile(source, catalog);

                var errors = plan.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => $"  {d.Line}行目: {d.Message}")
                    .ToArray();

                Assert.IsEmpty(errors,
                    $"{Path.GetFileName(file)} にエラーがあります:\n{string.Join("\n", errors)}");

                // 同梱するサンプルは、標準の予算 (§31) で最後まで建てられること。
                // 予算を超えていると、遊んだ人が「途中から建たない」に出くわす。
                //
                // ただし先頭に「// @freemode」と書いてあるものは
                // FREE MODE（予算無制限 §46）で見せるための構成なので対象外にする。
                bool freeModeOnly = source.Contains("@freemode");
                if (!freeModeOnly)
                {
                    Assert.LessOrEqual(plan.EstimatedCost, catalog.InitialBudget,
                        $"{Path.GetFileName(file)} の見積 ¥{plan.EstimatedCost:N0} が " +
                        $"予算 ¥{catalog.InitialBudget:N0} を超えています。" +
                        "予算内に収めるか、先頭に // @freemode と書いてください。");
                }

                Debug.Log($"[SAMPLE] {Path.GetFileName(file)}: " +
                          $"即時{plan.ImmediateCommands.Count} ルール{plan.Rules.Count} " +
                          $"見積¥{plan.EstimatedCost:N0} " +
                          $"警告{plan.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning)}");
            }
        }

        [Test]
        public void BuiltInSamplesCompile()
        {
            var catalog = GameManager.Instance.Catalog;
            foreach (var (name, src) in new[]
                     {
                         ("Starter", MatsuriSamples.Starter),
                         ("Full", MatsuriSamples.Full),
                     })
            {
                var plan = MatsuriCompiler.Compile(src, catalog);
                var errors = plan.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                Assert.IsEmpty(errors.Select(e => $"{e.Line}行目: {e.Message}"),
                    $"MatsuriSamples.{name} にエラーがあります。");
            }
        }
    }
}
