using Matsuri.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Matsuri.Art
{
    /// <summary>
    /// 仕様書 §60。ポストプロセスは「控えめに」。
    /// 提灯や電球がにじむ程度の Bloom と ACES トーンマップだけで、
    /// 夜祭りらしい絵を作る。派手なグレーディングはかけない。
    /// </summary>
    public static class PostProcessRig
    {
        /// <summary>直近に作った Volume。調整用に外から触れるようにしておく。</summary>
        public static Volume Current { get; private set; }

        /// <summary>ポストプロセス用の Global Volume を組み立てて parent にぶら下げる。</summary>
        public static void Build(Transform parent)
        {
            var go = new GameObject("PostProcess Volume");
            if (parent != null) go.transform.SetParent(parent, false);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "PROC_PostProcess";

            AddTonemapping(profile);
            AddBloom(profile);
            AddColorAdjustments(profile);
            AddAmbientOcclusion(profile);
            AddDepthOfField(profile);
            AddVignette(profile);
            AddFilmGrain(profile);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            Current = volume;
            MatsuriLog.Info("PostProcessRig を構築しました。");
        }

        static void AddTonemapping(VolumeProfile profile)
        {
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.overrideState = true;
            tm.mode.value = TonemappingMode.ACES;
        }

        static void AddBloom(VolumeProfile profile)
        {
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.25f;          // §60 強くしすぎない
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;          // しきい値は高めにして、光源だけを光らせる
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.64f;
            bloom.tint.overrideState = true;
            bloom.tint.value = new Color(1f, 0.93f, 0.82f);   // 提灯の暖色に寄せる
            bloom.dirtIntensity.overrideState = true;
            bloom.dirtIntensity.value = 0f;
        }

        static void AddColorAdjustments(VolumeProfile profile)
        {
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.overrideState = true;
            ca.postExposure.value = 0f;
            ca.contrast.overrideState = true;
            ca.contrast.value = 7f;
            ca.saturation.overrideState = true;
            ca.saturation.value = 5f;
            ca.colorFilter.overrideState = true;
            ca.colorFilter.value = new Color(1f, 0.98f, 0.95f);
        }

        static void AddAmbientOcclusion(VolumeProfile profile)
        {
            var ao = profile.Add<ScreenSpaceAmbientOcclusion>(true);
            ao.rayTracing.overrideState = true;
            ao.rayTracing.value = false;
            ao.intensity.overrideState = true;
            ao.intensity.value = 0.6f;
            ao.radius.overrideState = true;
            ao.radius.value = 1.3f;
            ao.directLightingStrength.overrideState = true;
            ao.directLightingStrength.value = 0.25f;
        }

        static void AddDepthOfField(VolumeProfile profile)
        {
            // §60「弱く」。遠景がほんのり溶けるだけにする。
            var dof = profile.Add<DepthOfField>(true);
            dof.focusMode.overrideState = true;
            dof.focusMode.value = DepthOfFieldMode.Manual;
            dof.nearFocusStart.overrideState = true;
            dof.nearFocusStart.value = 0f;
            dof.nearFocusEnd.overrideState = true;
            dof.nearFocusEnd.value = 0.6f;
            dof.farFocusStart.overrideState = true;
            dof.farFocusStart.value = 55f;
            dof.farFocusEnd.overrideState = true;
            dof.farFocusEnd.value = 160f;
        }

        static void AddVignette(VolumeProfile profile)
        {
            var v = profile.Add<Vignette>(true);
            v.mode.overrideState = true;
            v.mode.value = VignetteMode.Procedural;
            v.intensity.overrideState = true;
            v.intensity.value = 0.22f;
            v.smoothness.overrideState = true;
            v.smoothness.value = 0.5f;
            v.roundness.overrideState = true;
            v.roundness.value = 0.85f;
            v.color.overrideState = true;
            v.color.value = new Color(0.02f, 0.02f, 0.05f);
        }

        static void AddFilmGrain(VolumeProfile profile)
        {
            // 夜の暗部のバンディングを隠す程度。目に付いたら強すぎる。
            var g = profile.Add<FilmGrain>(true);
            g.type.overrideState = true;
            g.type.value = FilmGrainLookup.Thin1;
            g.intensity.overrideState = true;
            g.intensity.value = 0.06f;
            g.response.overrideState = true;
            g.response.value = 0.85f;
        }
    }
}
