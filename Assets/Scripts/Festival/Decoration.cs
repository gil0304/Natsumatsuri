using Matsuri.Core;
using Matsuri.Data;
using Matsuri.TimeSystem;
using Matsuri.Visitors;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 仕様書 §21 / §34。提灯・のぼり・鳥居などの装飾。
    /// 周囲のNPCの満足度をゆっくり上げ、夜になると灯りが強くなる (§8 / §59)。
    ///
    /// 効果判定は毎フレームやらない。装飾が数十個あっても重くならないよう、
    /// 一定間隔でしか周囲を走査しない (§57)。
    /// </summary>
    public sealed class Decoration : FestivalObject
    {
        /// <summary>周囲を走査する間隔（実時間・秒）。</summary>
        const float ScanInterval = 0.5f;

        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("この装飾の設定。")]
        public DecorationData Data;

        public override FestivalObjectKind Kind => FestivalObjectKind.Decoration;

        /// <summary>効果が届く半径。</summary>
        public float EffectRadius => Data != null ? Data.EffectRadius : 8f;

        /// <summary>周囲のNPCに毎秒与える満足度。</summary>
        public float AmbienceValue => Data != null ? Data.AmbienceValue : 0f;

        /// <summary>会場全体の魅力への寄与（来場者数に効く §33）。</summary>
        public float AttractionValue
        {
            get
            {
                if (Data == null) return 1f;
                return Data.Effect switch
                {
                    DecorationEffect.Landmark => 4f,
                    DecorationEffect.Lighting => 1.5f,
                    _ => 1f
                };
            }
        }

        Light[] _lights;
        float[] _baseIntensities;
        Renderer[] _emissiveRenderers;
        Color[] _baseEmissives;

        float _scanTimer;
        bool _lit;

        /// <summary>データを割り当てて見た目の参照を集める。FestivalManager から呼ばれる。</summary>
        public void Configure(DecorationData data)
        {
            Data = data;

            if (data != null)
            {
                ObjectId = data.Id;
                BuildCost = data.BuildCost;
                if (string.IsNullOrEmpty(name) || name.StartsWith("New Game Object")) name = data.DisplayName;
            }

            CacheVisuals();

            // 走査タイミングを個体ごとにずらし、同じフレームに集中させない (§57)。
            _scanTimer = Random.Range(0f, ScanInterval);
        }

        void CacheVisuals()
        {
            _lights = GetComponentsInChildren<Light>(true);
            _baseIntensities = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++)
                _baseIntensities[i] = _lights[i] != null ? _lights[i].intensity : 0f;

            var renderers = GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Renderer>(renderers.Length);
            var colors = new System.Collections.Generic.List<Color>(renderers.Length);

            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (mat == null) continue;

                Color emissive = Color.black;
                if (mat.HasProperty(EmissiveColorId)) emissive = mat.GetColor(EmissiveColorId);
                else if (mat.HasProperty(EmissionColorId)) emissive = mat.GetColor(EmissionColorId);

                if (emissive.maxColorComponent <= 0.001f) continue;

                list.Add(renderers[i]);
                colors.Add(emissive);
            }

            _emissiveRenderers = list.ToArray();
            _baseEmissives = colors.ToArray();
        }

        public override void OnBuilt()
        {
            if (_lights == null) CacheVisuals();
            ApplyLightAmount(0.35f);
        }

        public override void OnFestivalStart()
        {
            // §80「灯りが一斉につく」瞬間。
            _lit = true;
            ApplyLightAmount(1f);
        }

        public override void OnFestivalEnd()
        {
            _lit = false;
        }

        public override void OnTimeOfDayChanged(FestivalClock clock, float nightAmount)
        {
            // 夕方は控えめ、夜は全開 (§8)。
            float amount = _lit ? Mathf.Lerp(0.45f, 1f, nightAmount) : Mathf.Lerp(0.2f, 0.6f, nightAmount);
            ApplyLightAmount(amount);
        }

        void ApplyLightAmount(float amount)
        {
            if (_lights != null)
            {
                for (int i = 0; i < _lights.Length; i++)
                {
                    if (_lights[i] == null) continue;
                    if (_lights[i].type == LightType.Directional) continue;
                    _lights[i].intensity = _baseIntensities[i] * amount;
                    _lights[i].enabled = amount > 0.01f;
                }
            }

            if (_emissiveRenderers != null)
            {
                var block = new MaterialPropertyBlock();
                for (int i = 0; i < _emissiveRenderers.Length; i++)
                {
                    if (_emissiveRenderers[i] == null) continue;
                    _emissiveRenderers[i].GetPropertyBlock(block);
                    Color c = _baseEmissives[i] * amount;
                    block.SetColor(EmissiveColorId, c);
                    block.SetColor(EmissionColorId, c);
                    _emissiveRenderers[i].SetPropertyBlock(block);
                }
            }
        }

        public override void TickFestival(float dt, FestivalClock clock)
        {
            if (Data == null) return;
            if (AmbienceValue <= 0f) return;

            _scanTimer -= dt;
            if (_scanTimer > 0f) return;

            float elapsed = ScanInterval;
            _scanTimer += ScanInterval;

            ApplyAmbience(elapsed);
        }

        /// <summary>半径内のNPCの満足度を上げる (§34「装飾が多いと満足度が上がる」)。</summary>
        void ApplyAmbience(float elapsedSeconds)
        {
            var visitors = GameManager.Instance != null && GameManager.Instance.Visitors != null
                ? GameManager.Instance.Visitors.Active
                : null;

            if (visitors == null || visitors.Count == 0) return;

            float radius = EffectRadius;
            float sqrRadius = radius * radius;
            float gain = AmbienceValue * elapsedSeconds;
            Vector3 origin = transform.position;

            for (int i = 0; i < visitors.Count; i++)
            {
                var v = visitors[i];
                if (v == null) continue;

                Vector3 d = v.Position - origin;
                float sqr = d.x * d.x + d.z * d.z;
                if (sqr > sqrRadius) continue;

                // 近いほど効く。
                float falloff = 1f - Mathf.Sqrt(sqr) / radius;
                v.Satisfaction = Mathf.Min(100f, v.Satisfaction + gain * falloff);
            }
        }
    }
}
