using Matsuri.Core;
using UnityEngine;

namespace Matsuri.Audio
{
    /// <summary>
    /// 仕様書 §24 / §63。祭りの音を統括する。
    /// BGM（祭囃子）・環境音（ざわめき／セミ／虫）・SE（3D位置指定）の3系統を持つ。
    /// 3D SE は AudioSource プールで鳴らし、毎回 AddComponent しない。
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [Header("音量")]
        [Range(0f, 1f)] public float BgmVolume = 0.45f;
        [Range(0f, 1f)] public float AmbienceVolume = 0.55f;
        [Range(0f, 1f)] public float SfxVolume = 0.9f;

        [Header("3D SE")]
        [Tooltip("同時に鳴らせる 3D SE の数。")]
        public int PoolSize = 24;
        public float SfxMinDistance = 4f;
        public float SfxMaxDistance = 70f;

        [Header("時間帯 (§63)")]
        [Tooltip("セミが鳴き止む時刻（分）。既定は 19:00。")]
        public int CicadaEndMinutes = 19 * 60;
        [Tooltip("虫の音が鳴き始める時刻（分）。既定は 18:40。")]
        public int NightInsectStartMinutes = 18 * 60 + 40;

        float _master = 1f;
        bool _initialized;
        float _intensity;
        float _intensityTarget;

        AudioSource _bgm;
        AudioSource _crowd;
        AudioSource _cicada;
        AudioSource _insects;
        AudioSource[] _pool;
        int _poolCursor;
        Transform _sfxRoot;
        bool _ambienceOn;

        /// <summary>全体音量 (0〜1)。</summary>
        public float MasterVolume
        {
            get => _master;
            set
            {
                _master = Mathf.Clamp01(value);
                ApplyVolumes();
            }
        }

        /// <summary>祭りの盛り上がり 0(静か) 〜 1(ピーク)。</summary>
        public float FestivalIntensity => _intensity;

        void Awake()
        {
            if (!_initialized) Initialize();
        }

        /// <summary>音源を生成して待機状態にする。二度呼んでも安全。</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _bgm = CreateSource("BGM_祭囃子", loop: true, spatial: false);
            _crowd = CreateSource("AMB_ざわめき", loop: true, spatial: false);
            _cicada = CreateSource("AMB_セミ", loop: true, spatial: false);
            _insects = CreateSource("AMB_虫の音", loop: true, spatial: false);

            var rootGo = new GameObject("SfxPool");
            rootGo.transform.SetParent(transform, false);
            _sfxRoot = rootGo.transform;

            PoolSize = Mathf.Max(4, PoolSize);
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = CreateSource($"SFX_{i:00}", loop: false, spatial: true, parent: _sfxRoot);
                src.playOnAwake = false;
                _pool[i] = src;
            }

            ApplyVolumes();
            MatsuriLog.Info("AudioManager を初期化しました。");
        }

        AudioSource CreateSource(string name, bool loop, bool spatial, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = spatial ? 1f : 0f;
            src.dopplerLevel = 0f;
            if (spatial)
            {
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = SfxMinDistance;
                src.maxDistance = SfxMaxDistance;
            }
            return src;
        }

        // ── BGM ────────────────────────────────────────────────

        /// <summary>祭囃子を鳴らし始める。</summary>
        public void PlayBgm()
        {
            if (!_initialized) Initialize();
            if (_bgm == null) return;
            if (_bgm.clip == null) _bgm.clip = ProceduralAudioLibrary.FestivalBgm();
            if (!_bgm.isPlaying) _bgm.Play();
            ApplyVolumes();
        }

        public void StopBgm()
        {
            if (_bgm != null && _bgm.isPlaying) _bgm.Stop();
        }

        // ── 環境音 ─────────────────────────────────────────────

        /// <summary>ざわめき・セミ・虫の音を鳴らし始める。</summary>
        public void StartAmbience()
        {
            if (!_initialized) Initialize();
            _ambienceOn = true;

            if (_crowd != null)
            {
                if (_crowd.clip == null) _crowd.clip = ProceduralAudioLibrary.CrowdAmbience();
                if (!_crowd.isPlaying) _crowd.Play();
            }
            if (_cicada != null)
            {
                if (_cicada.clip == null) _cicada.clip = ProceduralAudioLibrary.CicadaAmbience();
                if (!_cicada.isPlaying) _cicada.Play();
            }
            if (_insects != null)
            {
                if (_insects.clip == null) _insects.clip = ProceduralAudioLibrary.NightInsectsAmbience();
                if (!_insects.isPlaying) _insects.Play();
            }
            ApplyVolumes();
        }

        public void StopAmbience()
        {
            _ambienceOn = false;
            if (_crowd != null) _crowd.Stop();
            if (_cicada != null) _cicada.Stop();
            if (_insects != null) _insects.Stop();
        }

        // ── SE ─────────────────────────────────────────────────

        /// <summary>
        /// 効果音を鳴らす。at を渡すとその位置から 3D で鳴る。
        /// 渡さないと 2D（UI音など）として鳴る。
        /// </summary>
        public void PlaySfx(MatsuriSfx sfx, Vector3? at = null, float volume = 1f)
        {
            PlayInternal(sfx, at, volume);
        }

        /// <summary>ピッチをずらして鳴らす（同じ音が続いても単調にならないように）。</summary>
        public void PlaySfxVaried(MatsuriSfx sfx, Vector3? at, float volume, float pitchJitter)
        {
            var src = PlayInternal(sfx, at, volume);
            if (src != null) src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        }

        AudioSource PlayInternal(MatsuriSfx sfx, Vector3? at, float volume)
        {
            if (!_initialized) Initialize();
            if (_pool == null || _pool.Length == 0) return null;

            var clip = ProceduralAudioLibrary.Get(sfx);
            if (clip == null) return null;

            var src = RentSource();
            if (src == null) return null;

            if (at.HasValue)
            {
                src.transform.position = at.Value;
                src.spatialBlend = 1f;
            }
            else
            {
                src.transform.localPosition = Vector3.zero;
                src.spatialBlend = 0f;
            }

            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * SfxVolume * _master;
            src.pitch = 1f;
            src.Play();
            return src;
        }

        AudioSource RentSource()
        {
            // 空いているものを優先。全部埋まっていたら一番古いものを奪う。
            for (int i = 0; i < _pool.Length; i++)
            {
                int idx = (_poolCursor + i) % _pool.Length;
                if (_pool[idx] != null && !_pool[idx].isPlaying)
                {
                    _poolCursor = (idx + 1) % _pool.Length;
                    return _pool[idx];
                }
            }
            var fallback = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Length;
            return fallback;
        }

        // ── 盛り上がり (§24) ───────────────────────────────────

        /// <summary>人が増えるほど、ざわめきも祭囃子も賑やかになる。</summary>
        public void SetFestivalIntensity(float t)
        {
            _intensityTarget = Mathf.Clamp01(t);
        }

        void Update()
        {
            // 盛り上がりは急に変えず、なめらかに追従させる。
            _intensity = Mathf.MoveTowards(_intensity, _intensityTarget, Time.deltaTime * 0.25f);
            ApplyVolumes();
        }

        /// <summary>時刻から、セミ／虫の音の鳴らし分けの重みを求める (§63)。</summary>
        void GetInsectWeights(out float cicada, out float night)
        {
            float minutes = TimeSystem.FestivalClock.StartMinutes;
            var game = GameManager.Instance;
            if (game != null && game.Time != null) minutes = game.Time.Clock.MinutesOfDay;

            // セミ: 17:00 で最大、19:00 に向けて消える。
            cicada = 1f - Mathf.InverseLerp(CicadaEndMinutes - 70f, CicadaEndMinutes, minutes);
            // 虫の音: 18:40 頃から立ち上がり、夜に主役になる。
            night = Mathf.InverseLerp(NightInsectStartMinutes, NightInsectStartMinutes + 80f, minutes);
        }

        void ApplyVolumes()
        {
            if (!_initialized) return;

            if (_bgm != null)
            {
                // 静かなときは控えめ、ピークで賑やかに。
                _bgm.volume = BgmVolume * _master * Mathf.Lerp(0.55f, 1f, _intensity);
                _bgm.pitch = Mathf.Lerp(0.97f, 1.03f, _intensity);
            }

            if (_crowd != null)
                _crowd.volume = _ambienceOn ? AmbienceVolume * _master * Mathf.Lerp(0.08f, 1f, _intensity) : 0f;

            GetInsectWeights(out float cicadaWeight, out float nightWeight);
            if (_cicada != null)
                _cicada.volume = _ambienceOn ? AmbienceVolume * _master * 0.6f * Mathf.Clamp01(cicadaWeight) : 0f;
            if (_insects != null)
                _insects.volume = _ambienceOn ? AmbienceVolume * _master * 0.5f * Mathf.Clamp01(nightWeight) : 0f;
        }

        void OnApplicationQuit()
        {
            // 合成したクリップは屋台なども共有しているので、終了時にだけ捨てる。
            ProceduralAudioLibrary.ClearCache();
        }
    }
}
