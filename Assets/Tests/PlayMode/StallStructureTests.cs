using System.Collections;
using System.Linq;
using Matsuri.Core;
using Matsuri.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Matsuri.Tests
{
    /// <summary>
    /// 仕様書 §23 の屋台の中身と §79「Unity Cube丸出し」の禁止を機械的に検査する。
    /// AI生成モデルに差し替えたあとも、この構造が壊れていないことを保証する。
    /// </summary>
    public sealed class StallStructureTests
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

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator EveryStallHasTheStructureTheSpecRequires()
        {
            var game = GameManager.Instance;
            var catalog = game.Catalog;
            var all = catalog.GetAll(Script.MatsuriEntryKind.Stall);
            Assert.AreEqual(11, all.Count, "屋台は §19 のとおり11種あるはずです。");

            // 11種の合計は約 ¥1,155,000 で標準予算 (§31) を超えるので、
            // 食べ物6種と遊び5種の2回に分けて建てる。
            var batches = new[] { all.Take(6).ToArray(), all.Skip(6).ToArray() };
            int checkedCount = 0;

            foreach (var batch in batches)
            {
                var code = new System.Text.StringBuilder();
                for (int i = 0; i < batch.Length; i++)
                    code.Append($"屋台 \"{batch[i].DisplayName}\" {{ 場所 {-20 + i * 8}, 6  値段 {batch[i].DefaultPrice} }}\n");

                game.RunCode(code.ToString());

                float t = 0f;
                while (t < 60f && game.Stalls.Stalls.Count < batch.Length)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                Assert.AreEqual(batch.Length, game.Stalls.Stalls.Count,
                    $"この組の屋台がすべて建っていません（{string.Join("/", batch.Select(b => b.DisplayName))}）。");

                CheckStalls(game);
                checkedCount += batch.Length;
            }

            Assert.AreEqual(11, checkedCount, "11種すべてを検査できていません。");
        }

        static void CheckStalls(GameManager game)
        {
            // §23 が名指ししている子オブジェクト
            string[] required = { "MainStructure", "Roof", "Noren", "Sign", "Counter",
                                  "LightBulbs", "StaffPosition", "CustomerPosition" };

            foreach (var stall in game.Stalls.Stalls)
            {
                var names = stall.GetComponentsInChildren<Transform>(true).Select(x => x.name).ToArray();

                foreach (var req in required)
                    Assert.IsTrue(names.Contains(req),
                        $"「{stall.Data.DisplayName}」に {req} がありません (§23)。");

                Assert.IsTrue(names.Any(n => n.StartsWith("QueuePoint")),
                    $"「{stall.Data.DisplayName}」に QueuePoint がありません (§30)。");

                int renderers = stall.GetComponentsInChildren<MeshRenderer>(true).Length;
                Assert.Greater(renderers, 12,
                    $"「{stall.Data.DisplayName}」のパーツが {renderers} 個しかありません。§79「Unity Cube丸出し」は禁止です。");

                var mats = stall.GetComponentsInChildren<MeshRenderer>(true)
                                .Select(r => r.sharedMaterial).Where(m => m != null)
                                .Select(m => m.name).Distinct().Count();
                Assert.Greater(mats, 2,
                    $"「{stall.Data.DisplayName}」のマテリアルが {mats} 種しかありません。§79「全オブジェクト同じ材質」は禁止です。");

                Debug.Log($"[STRUCT] {stall.Data.DisplayName}: パーツ{renderers} マテリアル{mats} " +
                          $"行列点{names.Count(n => n.StartsWith("QueuePoint"))} " +
                          $"実光源{stall.GetComponentsInChildren<Light>(true).Length}");
            }
        }
    }
}
