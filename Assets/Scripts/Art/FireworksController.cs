using System.Collections;
using Matsuri.Audio;
using Matsuri.Core;
using Matsuri.Script;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>仕様書 §61。花火の種類。</summary>
    public enum FireworkKind
    {
        /// <summary>菊：尾を引く球状。</summary>
        Kiku,
        /// <summary>牡丹：尾のない球状。</summary>
        Botan,
        /// <summary>柳：下に垂れる。</summary>
        Yanagi,
        /// <summary>ハート：ハート型に配置。</summary>
        Heart,
        /// <summary>大玉：大きく多段。</summary>
        Oodama,
        /// <summary>スペシャル：多重＋色変化。</summary>
        Special
    }

    /// <summary>開花1発ぶんの注文書。描画方式を差し替えてもこの形は変えない。</summary>
    public readonly struct FireworkRequest
    {
        public readonly FireworkKind Kind;
        public readonly Vector3 BurstPoint;
        public readonly Color Inner;
        public readonly Color Outer;
        public readonly float Scale;
        public readonly Camera Viewer;

        public FireworkRequest(FireworkKind kind, Vector3 burstPoint, Color inner, Color outer,
                               float scale, Camera viewer)
        {
            Kind = kind; BurstPoint = burstPoint; Inner = inner; Outer = outer;
            Scale = scale; Viewer = viewer;
        }
    }

    /// <summary>
    /// 花火の描画方式の差し替え口 (§61)。
    /// 既定は ParticleSystem 実装。将来 VFX Graph の .vfx を用意したら
    /// この インターフェース を実装したクラスに差し替えるだけでよい。
    /// </summary>
    public interface IFireworkRenderer
    {
        void Initialize(Transform parent);
        void Burst(in FireworkRequest request);
        void StopAll();
    }

    /// <summary>
    /// 仕様書 §61 / §80。本作の象徴である花火。
    /// 打ち上げ（光の尾を引きながら上昇）→ 開花（種類ごとの形）→ 煙と火の粉。
    /// 光ってから遅れて破裂音が届く（距離感）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FireworksController : MonoBehaviour
    {
        [Header("打ち上げ")]
        public float MinBurstHeight = 26f;
        public float MaxBurstHeight = 42f;
        public float MinRiseSeconds = 1.25f;
        public float MaxRiseSeconds = 1.9f;

        [Tooltip("Launch に渡された座標がこの高さより上なら、すでに開花地点だとみなして上昇を省く。" +
                 "打ち上げの進行を EventManager 側 (FireworksDirector) が持つ場合の入口。")]
        public float AirburstHeight = 12f;

        [Header("閃光 (§59 LightingRig と連動)")]
        public LightingRig Lighting;
        public float FlashDuration = 0.45f;

        [Tooltip("上昇を省いた呼び出しでは、音と閃光は呼び出し側が出す前提にして二重再生を防ぐ。")]
        public bool DeferEffectsOnAirburst = true;

        [Header("音 (§61 光のあとから遅れて鳴らす)")]
        [Tooltip("音速 m/s。この速さで破裂音が観測者まで届く。")]
        public float SpeedOfSound = 340f;
        public float MinBurstSoundDelay = 0.12f;

        IFireworkRenderer _renderer;
        Transform _root;
        int _shellsInFlight;
        float _busyUntil;

        /// <summary>打ち上げ中か、開花の余韻が残っている間 true。</summary>
        public bool IsActive => _shellsInFlight > 0 || Time.time < _busyUntil;

        void Awake()
        {
            EnsureReady();
        }

        void EnsureReady()
        {
            if (_root == null)
            {
                var go = new GameObject("Fireworks");
                go.transform.SetParent(transform, false);
                _root = go.transform;
            }
            if (_renderer == null) SetRenderer(new ParticleFireworkRenderer());
            if (Lighting == null) Lighting = FindFirstObjectByType<LightingRig>();
        }

        /// <summary>描画方式を差し替える (§61 将来の VFX Graph 対応)。</summary>
        public void SetRenderer(IFireworkRenderer renderer)
        {
            if (renderer == null) return;
            _renderer?.StopAll();
            _renderer = renderer;
            if (_root == null)
            {
                var go = new GameObject("Fireworks");
                go.transform.SetParent(transform, false);
                _root = go.transform;
            }
            _renderer.Initialize(_root);
        }

        /// <summary>
        /// 花火を1発打ち上げる。kind は MatsuriIds の花火IDまたは日本語名。
        /// origin が地上なら「打ち上げ台」として上昇から始め、
        /// すでに上空 (AirburstHeight より上) ならそこで開花させる。
        /// </summary>
        public void Launch(string kind, Vector3 origin)
        {
            EnsureReady();
            var k = Parse(kind);
            if (origin.y >= AirburstHeight) BurstAt(k, origin, DeferEffectsOnAirburst);
            else StartCoroutine(LaunchRoutine(k, origin));
        }

        /// <summary>上昇を省き、指定の空中座標でいきなり開花させる。</summary>
        public void BurstAt(FireworkKind kind, Vector3 burstPoint, bool deferEffects = false)
        {
            EnsureReady();
            StartCoroutine(BurstRoutine(kind, burstPoint, deferEffects));
        }

        /// <summary>すべて止める。</summary>
        public void StopAll()
        {
            StopAllCoroutines();
            _renderer?.StopAll();
            _shellsInFlight = 0;
            _busyUntil = 0f;
        }

        /// <summary>ID・日本語名のどちらからでも種類を求める。分からなければ菊。</summary>
        public static FireworkKind Parse(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return FireworkKind.Kiku;
            string k = kind.Trim().ToLowerInvariant();
            if (k == MatsuriIds.FireworkBotan || k.Contains("牡丹") || k.Contains("ぼたん")) return FireworkKind.Botan;
            if (k == MatsuriIds.FireworkYanagi || k.Contains("柳") || k.Contains("やなぎ")) return FireworkKind.Yanagi;
            if (k == MatsuriIds.FireworkHeart || k.Contains("ハート") || k.Contains("はーと")) return FireworkKind.Heart;
            if (k == MatsuriIds.FireworkOodama || k.Contains("大玉") || k.Contains("おおだま")) return FireworkKind.Oodama;
            if (k == MatsuriIds.FireworkSpecial || k.Contains("スペシャル") || k.Contains("特大")) return FireworkKind.Special;
            return FireworkKind.Kiku;
        }

        /// <summary>種類ごとの色 (§61)。</summary>
        public static void GetColors(FireworkKind kind, out Color inner, out Color outer)
        {
            switch (kind)
            {
                case FireworkKind.Botan:
                    inner = new Color(1f, 0.55f, 0.72f); outer = new Color(0.62f, 0.28f, 0.95f); break;
                case FireworkKind.Yanagi:
                    inner = new Color(1f, 0.92f, 0.58f); outer = new Color(0.95f, 0.52f, 0.12f); break;
                case FireworkKind.Heart:
                    inner = new Color(1f, 0.42f, 0.48f); outer = new Color(1f, 0.72f, 0.80f); break;
                case FireworkKind.Oodama:
                    inner = new Color(0.85f, 0.94f, 1f); outer = new Color(0.35f, 0.62f, 1f); break;
                case FireworkKind.Special:
                    inner = new Color(0.6f, 1f, 0.85f); outer = new Color(1f, 0.75f, 0.25f); break;
                default:
                    inner = new Color(1f, 0.88f, 0.55f); outer = new Color(1f, 0.42f, 0.10f); break;
            }
        }

        static float ScaleOf(FireworkKind kind) => kind switch
        {
            FireworkKind.Oodama => 1.75f,
            FireworkKind.Special => 1.45f,
            FireworkKind.Yanagi => 1.15f,
            FireworkKind.Heart => 1.0f,
            _ => 1f
        };

        IEnumerator LaunchRoutine(FireworkKind kind, Vector3 origin)
        {
            _shellsInFlight++;

            float scale = ScaleOf(kind);
            float height = Random.Range(MinBurstHeight, MaxBurstHeight) * Mathf.Lerp(1f, 1.25f, scale - 1f);
            float rise = Random.Range(MinRiseSeconds, MaxRiseSeconds);
            Vector3 drift = new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
            Vector3 burstPoint = origin + Vector3.up * height + drift;

            GetColors(kind, out var inner, out _);

            // ── 打ち上げ：光の尾を引きながら上昇する ──
            var shell = new GameObject("FireworkShell");
            shell.transform.SetParent(_root, false);
            shell.transform.position = origin;
            var trail = MatsuriVfx.CreateFireworkTrail(shell.transform, inner);
            trail.Play();

            GameManager.Instance?.Audio?.PlaySfx(MatsuriSfx.FireworkLaunch, origin, 0.75f);

            float t = 0f;
            while (t < rise)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / rise);
                // 減速しながら上がる（打ち上げの重さを出す）。
                float eased = 1f - (1f - p) * (1f - p);
                shell.transform.position = Vector3.Lerp(origin, burstPoint, eased);
                yield return null;
            }

            var emission = trail.emission;
            emission.rateOverTime = 0f;
            trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(shell, 1.6f);

            _shellsInFlight--;
            yield return BurstRoutine(kind, burstPoint, deferEffects: false);
        }

        /// <summary>開花からあとの流れ。光が先、音はあとから届く (§61)。</summary>
        IEnumerator BurstRoutine(FireworkKind kind, Vector3 burstPoint, bool deferEffects)
        {
            float scale = ScaleOf(kind);
            GetColors(kind, out var inner, out var outer);

            var viewer = GameManager.Instance != null && GameManager.Instance.Cameras != null
                ? GameManager.Instance.Cameras.MainCamera
                : Camera.main;

            _renderer.Burst(new FireworkRequest(kind, burstPoint, inner, outer, scale, viewer));

            float lifetime = 4.5f * scale;
            _busyUntil = Mathf.Max(_busyUntil, Time.time + lifetime);

            // 光は音より先に届く。
            if (!deferEffects && Lighting != null)
                Lighting.FlashFromFireworks(Color.Lerp(inner, Color.white, 0.35f),
                                            scale * (kind == FireworkKind.Oodama ? 1.5f : 1f),
                                            FlashDuration);

            if (deferEffects) yield break;

            // ── 音は距離ぶん遅れて届く ──
            float distance = viewer != null ? Vector3.Distance(viewer.transform.position, burstPoint) : 30f;
            float delay = Mathf.Max(MinBurstSoundDelay, distance / Mathf.Max(1f, SpeedOfSound));
            yield return new WaitForSeconds(delay);
            GameManager.Instance?.Audio?.PlaySfx(MatsuriSfx.FireworkBurst, burstPoint,
                                                 Mathf.Clamp01(0.7f * scale));

            // 大玉・スペシャルは追い打ちの段がある。
            if (kind == FireworkKind.Oodama || kind == FireworkKind.Special)
            {
                yield return new WaitForSeconds(0.35f);
                GameManager.Instance?.Audio?.PlaySfx(MatsuriSfx.FireworkBurst, burstPoint, 0.45f);
            }
        }

        void OnDisable()
        {
            _renderer?.StopAll();
        }
    }

    /// <summary>
    /// 既定の花火描画。ParticleSystem をその場で組み立てて再生し、
    /// 寿命が尽きたら自分で片付ける (§61)。
    /// </summary>
    public sealed class ParticleFireworkRenderer : IFireworkRenderer
    {
        Transform _parent;

        public void Initialize(Transform parent) => _parent = parent;

        public void StopAll()
        {
            if (_parent == null) return;
            for (int i = _parent.childCount - 1; i >= 0; i--)
            {
                var child = _parent.GetChild(i);
                if (child.name.StartsWith("Burst_")) Object.Destroy(child.gameObject);
            }
        }

        public void Burst(in FireworkRequest r)
        {
            var go = new GameObject($"Burst_{r.Kind}");
            if (_parent != null) go.transform.SetParent(_parent, false);
            go.transform.position = r.BurstPoint;

            // カメラの方を向けておく（ハートなど平面的な形のため）。
            if (r.Viewer != null)
            {
                Vector3 toViewer = r.Viewer.transform.position - r.BurstPoint;
                toViewer.y = 0f;
                if (toViewer.sqrMagnitude > 0.01f)
                    go.transform.rotation = Quaternion.LookRotation(-toViewer.normalized, Vector3.up);
            }

            float life = 4.5f * r.Scale;
            switch (r.Kind)
            {
                case FireworkKind.Botan: BuildBotan(go.transform, r); break;
                case FireworkKind.Yanagi: BuildYanagi(go.transform, r); life += 2f; break;
                case FireworkKind.Heart: BuildHeart(go.transform, r); break;
                case FireworkKind.Oodama: BuildOodama(go.transform, r); life += 1.5f; break;
                case FireworkKind.Special: BuildSpecial(go.transform, r); life += 1.5f; break;
                default: BuildKiku(go.transform, r); break;
            }

            AddSmokeAndEmbers(go.transform, r);
            Object.Destroy(go, life + 4f);
        }

        // ── 種類ごとの形 (§61) ─────────────────────────────────

        /// <summary>菊：尾を引く球状。花火の基本形。</summary>
        static void BuildKiku(Transform parent, in FireworkRequest r)
        {
            var ps = MatsuriVfx.CreateFireworkBurst(parent, r.Inner, r.Outer,
                speed: 17f * r.Scale, lifetime: 2.6f, gravity: 0.22f,
                size: 0.42f * r.Scale, count: Mathf.RoundToInt(260 * r.Scale), trail: true);
            ps.Play();
        }

        /// <summary>牡丹：尾を引かない、丸くくっきりした球。</summary>
        static void BuildBotan(Transform parent, in FireworkRequest r)
        {
            var ps = MatsuriVfx.CreateFireworkBurst(parent, r.Inner, r.Outer,
                speed: 15f * r.Scale, lifetime: 2.0f, gravity: 0.12f,
                size: 0.62f * r.Scale, count: Mathf.RoundToInt(220 * r.Scale), trail: false);
            ps.Play();
        }

        /// <summary>柳：ゆっくり開き、重力で長く垂れ下がる。</summary>
        static void BuildYanagi(Transform parent, in FireworkRequest r)
        {
            var ps = MatsuriVfx.CreateFireworkBurst(parent, r.Inner, r.Outer,
                speed: 9f * r.Scale, lifetime: 4.4f, gravity: 0.85f,
                size: 0.36f * r.Scale, count: Mathf.RoundToInt(200 * r.Scale), trail: true);
            var trails = ps.trails;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            ps.Play();
        }

        /// <summary>ハート：ハート型の輪郭に沿って星を並べる。</summary>
        static void BuildHeart(Transform parent, in FireworkRequest r)
        {
            // 手動配置するので emission は使わない。speed は「広がりきったら止まる」
            // 空気抵抗の上限としてだけ効く。
            var ps = MatsuriVfx.CreateFireworkBurst(parent, r.Inner, r.Outer,
                speed: 9f * r.Scale, lifetime: 3.0f, gravity: 0.10f,
                size: 0.5f * r.Scale, count: 1, trail: true);
            var emission = ps.emission;
            emission.enabled = false;
            var main = ps.main;
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ps.Play();

            const int Count = 190;
            float radius = 11f * r.Scale;
            var pts = MatsuriVfx.HeartPoints(Count, radius);
            var t = parent;

            for (int i = 0; i < Count; i++)
            {
                Vector3 local = pts[i];
                Vector3 world = t.TransformPoint(local * 0.06f);   // 開花直後は小さく固まっている
                Vector3 dir = (t.TransformPoint(local) - t.position).normalized;

                var ep = new ParticleSystem.EmitParams
                {
                    position = world,
                    velocity = dir * (local.magnitude * 0.55f) + Random.insideUnitSphere * 0.35f,
                    startLifetime = Random.Range(2.4f, 3.1f),
                    startSize = Random.Range(0.35f, 0.6f) * r.Scale,
                    startColor = Color.Lerp(r.Inner, r.Outer, i / (float)Count)
                };
                ps.Emit(ep, 1);
            }
        }

        /// <summary>大玉：大きく、時間差で3段に開く。</summary>
        static void BuildOodama(Transform parent, in FireworkRequest r)
        {
            for (int stage = 0; stage < 3; stage++)
            {
                float k = 1f - stage * 0.28f;
                var ps = MatsuriVfx.CreateFireworkBurst(parent, r.Inner, r.Outer,
                    speed: 24f * r.Scale * k, lifetime: 3.2f, gravity: 0.2f,
                    size: 0.55f * r.Scale * k, count: Mathf.RoundToInt(300 * r.Scale * k), trail: stage != 1);
                var main = ps.main;
                main.startDelay = stage * 0.28f;
                ps.Play();
            }
        }

        /// <summary>スペシャル：多重に開き、色が移り変わる。</summary>
        static void BuildSpecial(Transform parent, in FireworkRequest r)
        {
            Color[] palette =
            {
                new Color(1f, 0.35f, 0.35f),
                new Color(0.4f, 0.85f, 1f),
                new Color(1f, 0.9f, 0.4f),
                new Color(0.75f, 0.45f, 1f)
            };

            for (int i = 0; i < 4; i++)
            {
                var inner = palette[i];
                var outer = palette[(i + 1) % palette.Length];
                var ps = MatsuriVfx.CreateFireworkBurst(parent, inner, outer,
                    speed: (13f + i * 3.5f) * r.Scale, lifetime: 2.4f + i * 0.35f, gravity: 0.16f,
                    size: (0.5f - i * 0.06f) * r.Scale, count: Mathf.RoundToInt(150 * r.Scale),
                    trail: i % 2 == 0);
                var main = ps.main;
                main.startDelay = i * 0.18f;
                ps.Play();
            }

            // 消えぎわに散るきらめき
            var sparkle = MatsuriVfx.CreateSparks(parent, new Color(1f, 0.95f, 0.8f), 0f);
            var sparkMain = sparkle.main;
            sparkMain.loop = false;
            sparkMain.duration = 0.5f;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f);
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            sparkMain.gravityModifier = 0.35f;
            var sparkEmission = sparkle.emission;
            sparkEmission.rateOverTime = 0f;
            sparkEmission.SetBursts(new[] { new ParticleSystem.Burst(0.6f, (short)140) });
            var sparkShape = sparkle.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = 1.2f;
            sparkle.Play();
        }

        /// <summary>煙の残りと、消えていく火の粉 (§61)。</summary>
        static void AddSmokeAndEmbers(Transform parent, in FireworkRequest r)
        {
            var smoke = MatsuriVfx.CreateFireworkSmoke(parent, new Color(0.32f, 0.33f, 0.38f, 1f));
            smoke.Play();

            var embers = MatsuriVfx.CreateSparks(parent, Color.Lerp(r.Outer, new Color(1f, 0.5f, 0.15f), 0.5f), 0f);
            var main = embers.main;
            main.loop = false;
            main.duration = 1.2f;
            main.gravityModifier = 0.55f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 5f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            var emission = embers.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.35f, (short)Mathf.RoundToInt(70 * r.Scale)) });
            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2f * r.Scale;
            embers.Play();
        }
    }
}
