using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// 仕様書 §62。ParticleSystem をコードで組み立てる汎用ヘルパ。
    /// 湯気・煙・火の粉・土埃・水紋・紙吹雪をここで作る。
    /// 花火専用の部品は FireworkVfx.cs 側にある（同じクラスを分割している）。
    /// 生成したマテリアルとテクスチャはキャッシュして使い回す。
    /// </summary>
    public static partial class MatsuriVfx
    {
        static Material _additive;
        static Material _softAlpha;
        static Material _streak;
        static Texture2D _dot;
        static Texture2D _streakTex;

        // ── マテリアルとテクスチャ ─────────────────────────────

        /// <summary>中心が明るく外に向かって消える丸いテクスチャ。</summary>
        public static Texture2D SoftDot()
        {
            if (_dot != null) return _dot;
            const int Size = 64;
            _dot = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
            {
                name = "PROC_SoftDot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[Size * Size];
            float half = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);          // なめらかな減衰
                    float core = Mathf.Clamp01(1f - r * 2.2f);
                    px[y * Size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * 0.85f + core * 0.4f));
                }
            }
            _dot.SetPixels(px);
            _dot.Apply(true, false);
            return _dot;
        }

        /// <summary>縦に伸びた光の筋。花火の尾に使う。</summary>
        public static Texture2D SoftStreak()
        {
            if (_streakTex != null) return _streakTex;
            const int W = 16, H = 64;
            _streakTex = new Texture2D(W, H, TextureFormat.RGBA32, true, true)
            {
                name = "PROC_SoftStreak",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float ty = y / (float)(H - 1);
                float along = Mathf.Sin(Mathf.PI * ty);
                for (int x = 0; x < W; x++)
                {
                    float tx = Mathf.Abs((x + 0.5f) / W * 2f - 1f);
                    float across = Mathf.Clamp01(1f - tx);
                    across *= across;
                    px[y * W + x] = new Color(1f, 1f, 1f, along * across);
                }
            }
            _streakTex.SetPixels(px);
            _streakTex.Apply(true, false);
            return _streakTex;
        }

        /// <summary>加算合成のパーティクル用マテリアル（光るもの）。</summary>
        public static Material Additive()
        {
            if (_additive != null) return _additive;
            _additive = BuildParticleMaterial("PROC_VFX_Additive", SoftDot(), additive: true);
            return _additive;
        }

        /// <summary>半透明のパーティクル用マテリアル（煙・湯気）。</summary>
        public static Material SoftAlpha()
        {
            if (_softAlpha != null) return _softAlpha;
            _softAlpha = BuildParticleMaterial("PROC_VFX_Alpha", SoftDot(), additive: false);
            return _softAlpha;
        }

        /// <summary>花火の尾に使う加算マテリアル。</summary>
        public static Material Streak()
        {
            if (_streak != null) return _streak;
            _streak = BuildParticleMaterial("PROC_VFX_Streak", SoftStreak(), additive: true);
            return _streak;
        }

        static Material BuildParticleMaterial(string name, Texture2D tex, bool additive)
        {
            // HDRP/Unlit を第一候補にし、見つからない環境ではビルトインへ落とす。
            Shader shader = Shader.Find("HDRP/Unlit");
            bool hdrp = shader != null;
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var mat = new Material(shader) { name = name };

            if (hdrp)
            {
                if (mat.HasProperty("_UnlitColorMap")) mat.SetTexture("_UnlitColorMap", tex);
                if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", Color.white);
                if (mat.HasProperty("_EmissiveColorMap")) mat.SetTexture("_EmissiveColorMap", tex);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", Color.white);
                if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f);            // Transparent
                if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", additive ? 1f : 0f);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                if (mat.HasProperty("_TransparentZWrite")) mat.SetFloat("_TransparentZWrite", 0f);
                if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
                if (mat.HasProperty("_EnableFogOnTransparent")) mat.SetFloat("_EnableFogOnTransparent", 1f);
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mat);
                mat.renderQueue = 3000;
            }
            else
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                mat.renderQueue = 3000;
            }

            mat.enableInstancing = true;
            return mat;
        }

        public static void ClearCache()
        {
            DestroySafe(_additive); _additive = null;
            DestroySafe(_softAlpha); _softAlpha = null;
            DestroySafe(_streak); _streak = null;
            DestroySafe(_dot); _dot = null;
            DestroySafe(_streakTex); _streakTex = null;
        }

        static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        // ── 生成の土台 ─────────────────────────────────────────

        /// <summary>空の ParticleSystem を1つ作る。共通の初期設定だけ入れる。</summary>
        public static ParticleSystem Create(string name, Transform parent, Material material,
                                            ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.alignment = ParticleSystemRenderSpace.View;
            return ps;
        }

        static ParticleSystem.MinMaxGradient FadeGradient(Color start, Color end, float peakAlpha)
        {
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, 0.18f),
                    new GradientAlphaKey(peakAlpha * 0.7f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            return new ParticleSystem.MinMaxGradient(grad);
        }

        // ── 汎用エフェクト ─────────────────────────────────────

        /// <summary>湯気。たこ焼きや焼きそばの鉄板から立ちのぼる (§23)。</summary>
        public static ParticleSystem CreateSteam(Transform parent, Color tint, float rate)
        {
            var ps = Create("VFX_湯気", parent, SoftAlpha());
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.6f);
            main.startColor = tint;
            main.gravityModifier = -0.02f;
            main.maxParticles = 120;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.22f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.4f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(tint, tint, 0.32f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 0.35f;
            return ps;
        }

        /// <summary>煙。花火の残りや炭火に使う。</summary>
        public static ParticleSystem CreateSmoke(Transform parent, Color tint, float rate)
        {
            var ps = Create("VFX_煙", parent, SoftAlpha());
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 2.2f);
            main.startColor = tint;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.015f;
            main.maxParticles = 180;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 3.2f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(tint, tint * 0.4f, 0.28f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.2f;
            return ps;
        }

        /// <summary>火の粉。焼き台や花火の消えぎわ。</summary>
        public static ParticleSystem CreateSparks(Transform parent, Color tint, float rate)
        {
            var ps = Create("VFX_火の粉", parent, Additive(), ParticleSystemRenderMode.Stretch);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.lengthScale = 2.2f;
            renderer.velocityScale = 0.12f;

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startColor = tint;
            main.gravityModifier = -0.12f;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(tint, new Color(tint.r, tint.g * 0.35f, 0f), 1f);
            return ps;
        }

        /// <summary>土埃。歩いた足元や建設の瞬間 (§39)。</summary>
        public static ParticleSystem CreateDust(Transform parent, Color tint, float rate)
        {
            var ps = Create("VFX_土埃", parent, SoftAlpha());
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startColor = tint;
            main.gravityModifier = 0.04f;
            main.maxParticles = 90;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.9f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(tint, tint, 0.35f);
            return ps;
        }

        /// <summary>水紋。金魚すくいやヨーヨー釣りの水槽 (§62)。</summary>
        public static ParticleSystem CreateWaterRipple(Transform parent, Color tint, float rate)
        {
            var ps = Create("VFX_水紋", parent, SoftAlpha(), ParticleSystemRenderMode.HorizontalBillboard);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = tint;
            main.gravityModifier = 0f;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
            shape.radiusThickness = 1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 3.4f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(tint, tint, 0.5f);
            return ps;
        }

        /// <summary>紙吹雪。結果画面や大成功の演出に使う (§36)。</summary>
        public static ParticleSystem CreateConfetti(Transform parent, Color tint, float rate)
        {
            var ps = Create("VFX_紙吹雪", parent, SoftAlpha());
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(tint, Color.white);
            main.gravityModifier = 0.35f;
            main.maxParticles = 300;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.3f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.6f;
            return ps;
        }
    }
}
