using System.Collections;
using System.IO;
using System.Linq;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Visitors;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Matsuri.Tests
{
    /// <summary>
    /// ゲームバランスの実測。デモ構成で祭りを等倍で最後まで回して数字を見る。
    /// 数値の合否は環境と乱数で揺れるので assert はしない。人が読むための計測。
    /// </summary>
    [Explicit]
    public sealed class BalanceProbe
    {
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator MeasureDemoFestival()
        {
            var go = new GameObject("Boot");
            go.AddComponent<MatsuriBootstrap>();
            yield return null; yield return null;

            var game = GameManager.Instance;
            Assert.IsNotNull(game);

            var path = Path.Combine(Application.streamingAssetsPath, "MatsuriSamples", "demo_6_honki.matsuri");
            Assert.IsTrue(File.Exists(path), $"デモが見つかりません: {path}");
            game.RunCode(File.ReadAllText(path));

            float t = 0f;
            while (t < 40f && game.Festival.IsBuilding)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < 10; i++) yield return null;

            Debug.Log($"[BAL] 建設完了 屋台={game.Stalls.Stalls.Count} " +
                      $"施設={Matsuri.Festival.AmenityRegistry.All.Count} " +
                      $"残予算=¥{game.Economy.Budget:N0} " +
                      $"（祭りの長さ {game.Balance.RealSecondsTotal:0} 秒）");

            game.StartFestival();
            game.Time.Speed = 1f;

            int hour = 17;
            while (game.Phase != GamePhase.Finished && t < 400f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;

                if (game.Time.Clock.Hour > hour)
                {
                    hour = game.Time.Clock.Hour;
                    var active = game.Visitors.Active;
                    int dancing = active.Count(v => v.State == VisitorStateKind.Dancing);
                    int resting = active.Count(v => v.State == VisitorStateKind.Resting);
                    int praying = active.Count(v => v.State == VisitorStateKind.Praying);
                    int toAmenity = active.Count(v => v.State == VisitorStateKind.MovingToAmenity);

                    int queued = game.Stalls.Stalls.Sum(s => s.QueueLength);

                    Debug.Log($"[BAL] {game.Time.Clock} 現在={game.Visitors.CurrentVisitors} " +
                              $"累計={game.Visitors.TotalVisitors} 売上=¥{game.Economy.Revenue:N0} " +
                              $"販売={game.Economy.SalesCount} 行列={queued} " +
                              $"踊り={dancing} 休憩={resting} 参拝={praying} 移動中={toAmenity} " +
                              $"満足度={game.Visitors.AverageSatisfaction:0.0}");
                }
            }

            Debug.Log($"[BAL] === 最終 === 売上=¥{game.Economy.Revenue:N0} 販売={game.Economy.SalesCount} " +
                      $"来場={game.Visitors.TotalVisitors} 最高同時={game.Visitors.PeakVisitors} " +
                      $"満足度={game.Visitors.AverageSatisfaction:0.0} " +
                      $"使った金額=¥{game.Economy.Spent:N0}");

            foreach (var s in game.Stalls.Stalls.OrderByDescending(x => x.Revenue))
                Debug.Log($"[BAL]   {s.Data.DisplayName}: 売上¥{s.Revenue:N0} 販売{s.SalesCount} 人気{s.Popularity:0}");

            Object.DestroyImmediate(go);
            var root = GameObject.Find("FESTIVAL_ROOT");
            if (root != null) Object.DestroyImmediate(root);
        }
    }
}
