using Matsuri.Core;
using Matsuri.TimeSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Matsuri.Art
{
    /// <summary>
    /// 仕様書 §59。「暗い夜の中で、屋台の暖かい光が主役になる」ための照明装置。
    /// 17:00 の夕焼け → 19:00 の藍色 → 20:00 以降の濃紺の夜 を nightAmount で補間する (§8)。
    /// 影を落とすのは月光ただ一つに絞る（HDRP では影が重い）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightingRig : MonoBehaviour
    {
        [Header("夕方 (nightAmount = 0)")]
        public Color DuskLightColor = new Color(1f, 0.60f, 0.34f);
        public float DuskIntensity = 1600f;
        public Color DuskSkyTop = new Color(0.15f, 0.19f, 0.42f);
        public Color DuskSkyHorizon = new Color(0.96f, 0.48f, 0.24f);
        public Color DuskSkyBottom = new Color(0.26f, 0.17f, 0.19f);
        public float DuskElevation = 5f;
        public float DuskExposure = 9.6f;

        [Header("宵 (nightAmount = 0.5) 藍色")]
        public Color TwilightLightColor = new Color(0.48f, 0.55f, 0.86f);
        public float TwilightIntensity = 24f;
        public Color TwilightSkyTop = new Color(0.035f, 0.050f, 0.130f);
        public Color TwilightSkyHorizon = new Color(0.115f, 0.135f, 0.290f);
        public Color TwilightSkyBottom = new Color(0.045f, 0.055f, 0.100f);
        public float TwilightExposure = 5.6f;

        [Header("夜 (nightAmount = 1) 濃紺")]
        public Color NightLightColor = new Color(0.62f, 0.72f, 1f);
        public float NightIntensity = 0.75f;
        public Color NightSkyTop = new Color(0.016f, 0.020f, 0.045f);
        public Color NightSkyHorizon = new Color(0.043f, 0.055f, 0.102f);   // #0B0E1A 系
        public Color NightSkyBottom = new Color(0.063f, 0.075f, 0.122f);   // #10131F 系
        public float NightElevation = 52f;
        public float NightExposure = 3.1f;

        [Header("霧 (§59 提灯の光をにじませる)")]
        public bool UseVolumetricFog = true;
        public float DuskFogDistance = 900f;
        public float NightFogDistance = 240f;

        [Header("露出の微調整")]
        [Tooltip("全体が明るすぎ／暗すぎるときはここで持ち上げる。")]
        public float ExposureBias = 0f;

        [Tooltip("空の明るさを露出に対してどれだけずらすか。\n" +
                 "0 で「見た目どおり」、マイナスにすると空だけ落ち着く。")]
        public float SkyExposureBias = -0.6f;

        [Header("駆動")]
        [Tooltip("TimeManager の時刻に自動追従する (§8)。外から SetTimeOfDay を叩く場合は切ってよい。")]
        public bool AutoFollowClock = true;

        [Header("花火の閃光 (§61)")]
        [Tooltip("FlashFromFireworks の intensity 1 あたりの照度 (lux)。")]
        public float FlashLuxPerUnit = 55f;

        Light _moon;
        HDAdditionalLightData _moonHd;
        Light _fill;
        Light _flash;

        Volume _skyVolume;
        VolumeProfile _skyProfile;
        VisualEnvironment _visualEnv;
        GradientSky _sky;
        Fog _fog;
        Exposure _exposure;

        float _flashTimer;
        float _flashDuration;
        float _flashPeak;

        bool _initialized;
        float _lastNightAmount;

        /// <summary>直近に適用された夜の度合い 0(夕方)〜1(真夜中)。</summary>
        public float NightAmount => _lastNightAmount;

        /// <summary>空と霧を司る Volume。外から weight を触りたいときに使う。</summary>
        public Volume SkyVolume => _skyVolume;

        /// <summary>月光。屋台以外で唯一影を落とす光源 (§59)。</summary>
        public Light Moon => _moon;

        void Awake()
        {
            if (!_initialized) Initialize();
        }

        /// <summary>光源と空を組み立てる。二度呼んでも安全。</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _moon = CreateDirectional("Moon", castShadows: true);
            _moonHd = _moon.GetComponent<HDAdditionalLightData>();
            if (_moonHd != null)
            {
                _moonHd.angularDiameter = 1.2f;
                _moonHd.affectSpecular = true;
                _moonHd.volumetricDimmer = 0.8f;
                _moonHd.shadowDimmer = 1f;
            }

            // 補助光。影は落とさない。夜の輪郭がまったく見えなくなるのを防ぐだけ。
            _fill = CreateDirectional("FillLight", castShadows: false);
            var fillHd = _fill.GetComponent<HDAdditionalLightData>();
            if (fillHd != null)
            {
                fillHd.affectSpecular = false;
                fillHd.volumetricDimmer = 0f;
            }
            _fill.transform.rotation = Quaternion.Euler(28f, 200f, 0f);

            // 花火の閃光専用。普段は消しておく。
            _flash = CreateDirectional("FireworkFlash", castShadows: false);
            var flashHd = _flash.GetComponent<HDAdditionalLightData>();
            if (flashHd != null)
            {
                flashHd.affectSpecular = true;
                flashHd.volumetricDimmer = 1.2f;
            }
            _flash.transform.rotation = Quaternion.Euler(62f, 20f, 0f);
            _flash.intensity = 0f;
            _flash.enabled = false;

            BuildSkyVolume();
            SetTimeOfDay(FestivalClock.AtStart, 0f);

            MatsuriLog.Info("LightingRig を初期化しました。");
        }

        /// <summary>
        /// HDRP の Directional は Lux で扱う。
        /// HDAdditionalLightData を AddComponent しただけだと単位が Candela のままになり、
        /// 意図した照度にならないので必ず単位ごと指定する。
        /// </summary>
        static void SetLux(Light light, float lux)
        {
            if (light == null) return;
            var hd = light.GetComponent<HDAdditionalLightData>();
            if (hd != null) hd.SetIntensity(lux, LightUnit.Lux);
            else light.intensity = lux;
        }

        Light CreateDirectional(string name, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            light.useColorTemperature = false;
            light.intensity = 0f;

            // HDRP は Light 単体では既定値が入らないので必ず足す。
            if (go.GetComponent<HDAdditionalLightData>() == null)
                go.AddComponent<HDAdditionalLightData>();
            return light;
        }

        void BuildSkyVolume()
        {
            var go = new GameObject("Sky and Fog Volume");
            go.transform.SetParent(transform, false);

            _skyProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _skyProfile.name = "PROC_SkyAndFog";

            _visualEnv = _skyProfile.Add<VisualEnvironment>(true);
            _visualEnv.skyType.overrideState = true;
            _visualEnv.skyType.value = (int)SkyType.Gradient;
            _visualEnv.skyAmbientMode.overrideState = true;
            _visualEnv.skyAmbientMode.value = SkyAmbientMode.Dynamic;

            _sky = _skyProfile.Add<GradientSky>(true);
            _sky.top.overrideState = true;
            _sky.middle.overrideState = true;
            _sky.bottom.overrideState = true;
            _sky.gradientDiffusion.overrideState = true;
            _sky.gradientDiffusion.value = 1.6f;

            _fog = _skyProfile.Add<Fog>(true);
            _fog.enabled.overrideState = true;
            _fog.enabled.value = true;
            _fog.colorMode.overrideState = true;
            _fog.colorMode.value = FogColorMode.ConstantColor;
            _fog.color.overrideState = true;
            _fog.meanFreePath.overrideState = true;
            _fog.baseHeight.overrideState = true;
            _fog.baseHeight.value = 0f;
            _fog.maximumHeight.overrideState = true;
            _fog.maximumHeight.value = 28f;
            _fog.enableVolumetricFog.overrideState = true;
            _fog.enableVolumetricFog.value = UseVolumetricFog;
            _fog.depthExtent.overrideState = true;
            _fog.depthExtent.value = 55f;
            _fog.anisotropy.overrideState = true;
            _fog.anisotropy.value = 0.35f;
            _fog.albedo.overrideState = true;
            _fog.albedo.value = new Color(0.82f, 0.80f, 0.86f);

            _exposure = _skyProfile.Add<Exposure>(true);
            _exposure.mode.overrideState = true;
            _exposure.mode.value = ExposureMode.Fixed;
            _exposure.fixedExposure.overrideState = true;

            _skyVolume = go.AddComponent<Volume>();
            _skyVolume.isGlobal = true;
            _skyVolume.priority = 0f;
            _skyVolume.weight = 1f;
            _skyVolume.sharedProfile = _skyProfile;
        }

        // ── 時間帯 (§8 / §59) ──────────────────────────────────

        /// <summary>時刻に応じて空・月・霧・露出をまとめて動かす。</summary>
        public void SetTimeOfDay(FestivalClock clock, float nightAmount)
        {
            if (!_initialized) Initialize();

            float n = Mathf.Clamp01(nightAmount);
            _lastNightAmount = n;

            Color lightColor = TriLerp(DuskLightColor, TwilightLightColor, NightLightColor, n);
            // 照度は桁が大きく変わるので対数で補間する。
            float intensity = TriLerpLog(DuskIntensity, TwilightIntensity, NightIntensity, n);

            if (_moon != null)
            {
                _moon.color = lightColor;
                SetLux(_moon, intensity);
                // 夕日は低く、夜の月は高い。方位もゆっくり回る。
                float elevation = Mathf.Lerp(DuskElevation, NightElevation, Mathf.SmoothStep(0f, 1f, n));
                float azimuth = Mathf.Lerp(248f, 158f, n);
                _moon.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
                // 完全な闇では影の計算が無駄なので落とす。
                _moon.shadows = intensity > 0.35f ? LightShadows.Soft : LightShadows.None;
            }

            if (_fill != null)
            {
                _fill.color = Color.Lerp(new Color(0.55f, 0.62f, 0.9f), new Color(0.30f, 0.40f, 0.72f), n);
                SetLux(_fill, TriLerpLog(DuskIntensity * 0.06f, TwilightIntensity * 0.25f,
                                         NightIntensity * 0.55f, n));
            }

            if (_sky != null)
            {
                _sky.top.value = TriLerp(DuskSkyTop, TwilightSkyTop, NightSkyTop, n);
                _sky.middle.value = TriLerp(DuskSkyHorizon, TwilightSkyHorizon, NightSkyHorizon, n);
                _sky.bottom.value = TriLerp(DuskSkyBottom, TwilightSkyBottom, NightSkyBottom, n);
            }

            if (_fog != null)
            {
                _fog.color.value = TriLerp(
                    new Color(0.42f, 0.30f, 0.28f),
                    new Color(0.10f, 0.12f, 0.24f),
                    new Color(0.045f, 0.055f, 0.105f), n);
                _fog.meanFreePath.value = Mathf.Lerp(DuskFogDistance, NightFogDistance, Mathf.SmoothStep(0f, 1f, n));
                _fog.enableVolumetricFog.value = UseVolumetricFog;
            }

            float ev = TriLerp(DuskExposure, TwilightExposure, NightExposure, n) + ExposureBias;

            if (_exposure != null)
                _exposure.fixedExposure.value = ev;

            // 空は「露出に対してどれくらい明るいか」で決まる。
            // GradientSky の色は 0〜1 の値なので、露出と同じ EV を与えて初めて
            // 夕焼けが夕焼けとして写る。ここを 0 のままにすると空が真っ黒になり、
            // Dynamic な環境光も真っ暗になって会場全体が沈む (§59)。
            if (_sky != null)
            {
                _sky.exposure.overrideState = true;
                _sky.exposure.value = ev + SkyExposureBias;
            }

            // 時刻表示のためだけに clock を受け取っているわけではない。
            // 22:00 を回ったら空をわずかに沈ませ、祭りの終わりを絵で伝える (§8)。
            if (_exposure != null && clock.IsAfter(FestivalClock.EndMinutes))
                _exposure.fixedExposure.value -= 0.4f;
        }

        // ── 花火の閃光 (§61) ───────────────────────────────────

        /// <summary>花火が開いた瞬間、会場を一瞬照らす。</summary>
        public void FlashFromFireworks(Color c, float intensity, float duration)
        {
            if (!_initialized) Initialize();
            if (_flash == null) return;

            _flashDuration = Mathf.Max(0.05f, duration);
            _flashTimer = _flashDuration;
            _flashPeak = Mathf.Max(0f, intensity) * FlashLuxPerUnit;

            _flash.color = c;
            _flash.intensity = _flashPeak;
            _flash.enabled = true;
        }

        void Update()
        {
            FollowClock();

            if (_flashTimer <= 0f) return;

            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                _flashTimer = 0f;
                if (_flash != null)
                {
                    _flash.intensity = 0f;
                    _flash.enabled = false;
                }
                return;
            }

            float k = _flashTimer / _flashDuration;
            // 立ち上がりは一瞬、消えるのはゆっくり。
            if (_flash != null) _flash.intensity = _flashPeak * k * k;
        }

        /// <summary>TimeManager が進むのに合わせて空を動かす (§8)。</summary>
        void FollowClock()
        {
            if (!AutoFollowClock) return;
            var game = GameManager.Instance;
            if (game == null || game.Time == null) return;

            float night = game.Time.NightAmount;
            // 1分ぶんの変化にも満たない差なら Volume を触らない（毎フレームの再構築を避ける）。
            if (Mathf.Abs(night - _lastNightAmount) < 0.0015f) return;
            SetTimeOfDay(game.Time.Clock, night);
        }

        // ── 補間の道具 ─────────────────────────────────────────

        /// <summary>夕方 → 宵 → 夜 の3点補間。</summary>
        static Color TriLerp(Color a, Color b, Color c, float t)
            => t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);

        static float TriLerp(float a, float b, float c, float t)
            => t < 0.5f ? Mathf.Lerp(a, b, t * 2f) : Mathf.Lerp(b, c, (t - 0.5f) * 2f);

        /// <summary>照度のように桁が変わる値は対数空間で補間する。</summary>
        static float TriLerpLog(float a, float b, float c, float t)
        {
            float la = Mathf.Log(Mathf.Max(1e-4f, a));
            float lb = Mathf.Log(Mathf.Max(1e-4f, b));
            float lc = Mathf.Log(Mathf.Max(1e-4f, c));
            return Mathf.Exp(TriLerp(la, lb, lc, t));
        }

        void OnDestroy()
        {
            if (_skyProfile != null) Destroy(_skyProfile);
        }
    }
}
