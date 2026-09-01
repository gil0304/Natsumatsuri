using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// 仕様書 §61。花火専用のパーティクル部品。
    /// 汎用エフェクトは MatsuriVfx.cs 側にある（同じクラスを分割している）。
    /// 形の作り分け（菊・牡丹・柳・ハート・大玉・スペシャル）は
    /// FireworksController がここの部品を組み合わせて行う。
    /// </summary>
    public static partial class MatsuriVfx
    {
        // ── 花火の部品 (§61) ───────────────────────────────────

        /// <summary>打ち上げの光の尾。上昇中の玉にぶら下げる。</summary>
        public static ParticleSystem CreateFireworkTrail(Transform parent, Color tint)
        {
            var ps = Create("VFX_打上尾", parent, Additive(), ParticleSystemRenderMode.Stretch);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.lengthScale = 3.2f;
            renderer.velocityScale = 0.06f;

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            main.startColor = tint;
            main.gravityModifier = 0.08f;
            main.maxParticles = 260;

            var emission = ps.emission;
            emission.rateOverTime = 220f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.06f;
            shape.rotation = new Vector3(90f, 0f, 0f);   // 下向きに吹き出す

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(tint, new Color(1f, 0.45f, 0.1f), 1f);
            return ps;
        }

        /// <summary>
        /// 開花の星。菊・牡丹・柳などの違いは引数で作り分ける。
        /// trail を true にすると尾を引く（菊・柳）。
        /// </summary>
        public static ParticleSystem CreateFireworkBurst(Transform parent, Color inner, Color outer,
                                                         float speed, float lifetime, float gravity,
                                                         float size, int count, bool trail)
        {
            var ps = Create("VFX_開花", parent, Additive());
            var main = ps.main;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.72f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(inner, outer);
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.Max(count, 64);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            shape.radiusThickness = 1f;

            // 空気抵抗で球が広がりきったところで止まる＝本物らしい動き。
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;
            limit.limit = new ParticleSystem.MinMaxCurve(speed * 0.9f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.02f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(inner, 0.12f),
                    new GradientColorKey(outer, 0.65f),
                    new GradientColorKey(new Color(outer.r * 0.4f, outer.g * 0.2f, 0f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0.65f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

            if (trail)
            {
                var trails = ps.trails;
                trails.enabled = true;
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.ratio = 1f;
                trails.lifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
                trails.dieWithParticles = false;
                trails.inheritParticleColor = true;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.55f,
                    AnimationCurve.Linear(0f, 1f, 1f, 0f));
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.trailMaterial = Streak();
            }

            return ps;
        }

        /// <summary>開花のあと空に残る煙。</summary>
        public static ParticleSystem CreateFireworkSmoke(Transform parent, Color tint)
        {
            var ps = CreateSmoke(parent, tint, 0f);
            ps.gameObject.name = "VFX_花火の煙";
            var main = ps.main;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)14) });

            var shape = ps.shape;
            shape.radius = 2.2f;
            return ps;
        }

        /// <summary>ハート型の配置座標。0..1 の単位円相当に正規化して返す (§61)。</summary>
        public static Vector3[] HeartPoints(int count, float scale)
        {
            count = Mathf.Max(8, count);
            var pts = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count * Mathf.PI * 2f;
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t)
                          - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                pts[i] = new Vector3(x, y, 0f) * (scale / 17f);
            }
            return pts;
        }
    }
}
