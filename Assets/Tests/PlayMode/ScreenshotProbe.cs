using System.Collections;
using System.IO;
using Matsuri.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Matsuri.Tests
{
    /// <summary>
    /// 見た目の確認用。祭りを建てて時間を進め、カメラの絵を PNG に書き出す。
    /// §78「普通に遊びたい3D経営ゲーム」に見えるかを人が判断するための材料。
    /// 通常のテスト実行では走らせない（[Explicit]）。
    /// </summary>
    [Explicit]
    public sealed class ScreenshotProbe
    {
        static string OutDir =>
            System.Environment.GetEnvironmentVariable("MATSURI_SHOT_DIR")
            ?? Path.Combine(Application.dataPath, "../../shots");

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CaptureFestival()
        {
            Directory.CreateDirectory(OutDir);

            var go = new GameObject("Boot");
            go.AddComponent<MatsuriBootstrap>();
            yield return null; yield return null;

            var game = GameManager.Instance;
            Assert.IsNotNull(game, "GameManager がありません。");

            game.RunCode(
                "祭り \"MATSURI.exe\" {\n" +
                "    屋台 \"たこ焼き\"     { 場所 -7, 6    値段 550 }\n" +
                "    屋台 \"かき氷\"       { 場所 0, 6     値段 450 }\n" +
                "    屋台 \"りんご飴\"     { 場所 7, 6     値段 350 }\n" +
                "    屋台 \"金魚すくい\"   { 場所 -7, -6   値段 400 }\n" +
                "    屋台 \"射的\"         { 場所 7, -6    値段 500 }\n" +
                "    設備 \"盆踊り場\"     { 場所 0, -22 }\n" +
                "    設備 \"休憩所\"       { 場所 -16, -2 }\n" +
                "    装飾 \"提灯\" { 場所 0, 0 }\n" +
                "    装飾 \"鳥居\" { 場所 0, -34 }\n" +
                "}\n");

            float t = 0f;
            while (t < 40f && game.Stalls.Stalls.Count < 5) { t += Time.unscaledDeltaTime; yield return null; }
            Debug.Log($"[SHOT] 建設 屋台={game.Stalls.Stalls.Count} 残予算=¥{game.Economy.Budget:N0}");

            game.StartFestival();
            game.Time.Speed = 1f;

            // 17:30 / 19:00 / 20:30 の3枚
            yield return CaptureAt(game, 17 * 60 + 30, "01_yugata_1730");
            yield return CaptureAt(game, 19 * 60, "02_yoru_1900");
            yield return CaptureAt(game, 20 * 60 + 30, "03_peak_2030");

            Object.DestroyImmediate(go);
            var root = GameObject.Find("FESTIVAL_ROOT");
            if (root != null) Object.DestroyImmediate(root);
        }

        IEnumerator CaptureAt(GameManager game, int minutesOfDay, string name)
        {
            while (game.Time.Clock.MinutesOfDay < minutesOfDay && game.Phase == GamePhase.Running)
                yield return null;

            yield return null;
            Shoot(name, new Vector3(0f, 15f, -34f), new Vector3(20f, 0f, 0f));
            // 屋台の正面に正確に回り込んで撮る（看板・暖簾・前掛けの文字を確認するため）
            var s0 = game.Stalls.Stalls.Count > 0 ? game.Stalls.Stalls[0] : null;
            if (s0 != null)
            {
                Vector3 look = s0.transform.position + Vector3.up * 1.4f;
                foreach (var (tag, dir) in new[] { ("_yataiA", 1f), ("_yataiB", -1f) })
                {
                    Vector3 cam = s0.transform.position + s0.transform.forward * (4.6f * dir) + Vector3.up * 1.9f;
                    var rot = Quaternion.LookRotation((look - cam).normalized, Vector3.up).eulerAngles;
                    Shoot(name + tag, cam, rot);
                }
                Debug.Log($"[SHOT] 屋台正面カメラ: {s0.Data.DisplayName} @ {s0.transform.position} " +
                          $"forward={s0.transform.forward}");
            }
            Shoot(name + "_odori", new Vector3(0f, 7f, -34f), new Vector3(16f, 0f, 0f));
            Debug.Log($"[SHOT] {name} @ {game.Time.Clock} 現在={game.Visitors.CurrentVisitors}人 " +
                      $"売上=¥{game.Economy.Revenue:N0} NightAmount={game.Time.NightAmount:0.000}");

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                var hd = l.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                Debug.Log($"[SHOT]   dirLight '{l.name}' enabled={l.enabled} Light.intensity={l.intensity:0.###} " +
                          $"hd.intensity={(hd != null ? hd.intensity.ToString("0.###") : "-")} " +
                          $"hd.unit={(hd != null ? hd.lightUnit.ToString() : "-")} color={l.color}");
            }

            var rig = Object.FindFirstObjectByType<Matsuri.Art.LightingRig>();
            if (rig != null && rig.SkyVolume != null && rig.SkyVolume.sharedProfile != null)
            {
                if (rig.SkyVolume.sharedProfile.TryGet<UnityEngine.Rendering.HighDefinition.Exposure>(out var ex))
                    Debug.Log($"[SHOT]   exposure mode={ex.mode.value} fixed={ex.fixedExposure.value:0.##} " +
                              $"volWeight={rig.SkyVolume.weight} prio={rig.SkyVolume.priority}");
                if (rig.SkyVolume.sharedProfile.TryGet<UnityEngine.Rendering.HighDefinition.VisualEnvironment>(out var ve))
                    Debug.Log($"[SHOT]   skyType={ve.skyType.value} ambient={ve.skyAmbientMode.value}");
                if (rig.SkyVolume.sharedProfile.TryGet<UnityEngine.Rendering.HighDefinition.GradientSky>(out var gs))
                    Debug.Log($"[SHOT]   gradient top={gs.top.value} mid={gs.middle.value} bot={gs.bottom.value} " +
                              $"exposure={gs.exposure.value} multiplier={gs.multiplier.value}");
            }
        }

        static void Shoot(string name, Vector3 pos, Vector3 euler)
        {
            // プレイヤーが実際に見るカメラで撮る。
            // 素の Camera を新規に作ると HDRP のカメラ設定（Volume・露出・空）が
            // 既定値のままになり、本来の絵にならない。
            var game = GameManager.Instance;
            var cam = game != null && game.Cameras != null ? game.Cameras.MainCamera : Camera.main;
            if (cam == null) { Debug.LogWarning("[SHOT] カメラが見つかりません"); return; }

            // Cinemachine がフレーム後半に姿勢を上書きするので、撮影中だけ止める
            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            bool brainWasEnabled = brain != null && brain.enabled;
            if (brain != null) brain.enabled = false;

            var prevPos = cam.transform.position;
            var prevRot = cam.transform.rotation;
            var prevTarget = cam.targetTexture;

            cam.transform.position = pos;
            cam.transform.eulerAngles = euler;

            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            var path = Path.Combine(OutDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[SHOT] wrote {path}");

            cam.targetTexture = prevTarget;
            cam.transform.position = prevPos;
            cam.transform.rotation = prevRot;
            if (brain != null) brain.enabled = brainWasEnabled;

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
