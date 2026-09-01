using System.Collections.Generic;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// HDRP/Lit をコードで生成してキャッシュするマテリアル置き場 (§69)。
    /// HDRP シェーダが見つからない環境（テスト用の Built-in など）では Standard に落ちる。
    /// </summary>
    public static class MatsuriMaterials
    {
        static readonly Dictionary<string, Material> s_Cache = new Dictionary<string, Material>(64);

        static Shader s_Lit;
        static Shader s_Unlit;
        static bool s_IsHdrp;

        /// <summary>HDRP/Lit（無ければ Standard）。</summary>
        static Shader LitShader
        {
            get
            {
                if (s_Lit != null) return s_Lit;
                s_Lit = Shader.Find("HDRP/Lit");
                s_IsHdrp = s_Lit != null;
                if (s_Lit == null) s_Lit = Shader.Find("Standard");
                if (s_Lit == null) s_Lit = Shader.Find("Universal Render Pipeline/Lit");
                if (s_Lit == null) s_Lit = Shader.Find("Diffuse");
                if (s_Lit == null) s_Lit = Shader.Find("Hidden/InternalErrorShader");
                if (s_Lit == null) Debug.LogError("[MatsuriMaterials] 使えるシェーダが1つも見つかりませんでした。");
                return s_Lit;
            }
        }

        static Shader UnlitShader
        {
            get
            {
                if (s_Unlit != null) return s_Unlit;
                s_Unlit = Shader.Find("HDRP/Unlit");
                if (s_Unlit == null) s_Unlit = Shader.Find("Unlit/Transparent");
                if (s_Unlit == null) s_Unlit = Shader.Find("Sprites/Default");
                if (s_Unlit == null) s_Unlit = LitShader;
                return s_Unlit;
            }
        }

        /// <summary>HDRP のシェーダが使えているか。プロパティ名の出し分けに使う。</summary>
        public static bool IsHdrp { get { _ = LitShader; return s_IsHdrp; } }

        public static void ClearCache()
        {
            foreach (var kv in s_Cache)
            {
                if (kv.Value == null) continue;
                if (Application.isPlaying) Object.Destroy(kv.Value);
                else Object.DestroyImmediate(kv.Value);
            }
            s_Cache.Clear();
        }

        static string Key(string tag, Color c) => tag + "#" + ColorUtility.ToHtmlStringRGBA(c);

        static Material Cached(string key, System.Func<Material> factory)
        {
            if (s_Cache.TryGetValue(key, out var m) && m != null) return m;
            m = factory();
            m.name = "Matsuri_" + key;
            s_Cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ 低レベル設定

        static Material NewLit() => new Material(LitShader);

        /// <summary>基本色。HDRP は _BaseColor、Built-in は _Color。</summary>
        public static void SetBaseColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c);
        }

        static void SetBaseMap(Material m, Texture tex, float tiling)
        {
            if (tex == null) return;
            if (m.HasProperty("_BaseColorMap"))
            {
                m.SetTexture("_BaseColorMap", tex);
                m.SetTextureScale("_BaseColorMap", Vector2.one * tiling);
            }
            if (m.HasProperty("_MainTex"))
            {
                m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_MainTex", Vector2.one * tiling);
            }
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            }
        }

        static void SetNormalMap(Material m, Texture tex, float tiling, float strength)
        {
            if (tex == null) return;
            if (m.HasProperty("_NormalMap"))
            {
                m.SetTexture("_NormalMap", tex);
                m.SetTextureScale("_NormalMap", Vector2.one * tiling);
            }
            if (m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", tex);
                m.SetTextureScale("_BumpMap", Vector2.one * tiling);
            }
            if (m.HasProperty("_NormalScale")) m.SetFloat("_NormalScale", strength);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", strength);
            m.EnableKeyword("_NORMALMAP");
        }

        static void SetSmoothMetal(Material m, float smoothness, float metallic)
        {
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_SmoothnessRemapMin")) m.SetFloat("_SmoothnessRemapMin", 0f);
            if (m.HasProperty("_SmoothnessRemapMax")) m.SetFloat("_SmoothnessRemapMax", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_MetallicRemapMin")) m.SetFloat("_MetallicRemapMin", metallic);
            if (m.HasProperty("_MetallicRemapMax")) m.SetFloat("_MetallicRemapMax", metallic);
        }

        /// <summary>半透明にする。HDRP と Built-in の両方に効くよう両系統のプロパティを設定する。</summary>
        static void MakeTransparent(Material m, int queueOffset = 0)
        {
            if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 1f);       // Transparent
            if (m.HasProperty("_BlendMode")) m.SetFloat("_BlendMode", 0f);           // Alpha
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5f);             // SrcAlpha
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 10f);            // OneMinusSrcAlpha
            if (m.HasProperty("_AlphaSrcBlend")) m.SetFloat("_AlphaSrcBlend", 5f);
            if (m.HasProperty("_AlphaDstBlend")) m.SetFloat("_AlphaDstBlend", 10f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_TransparentZWrite")) m.SetFloat("_TransparentZWrite", 0f);
            if (m.HasProperty("_RenderQueueType")) m.SetFloat("_RenderQueueType", 4f);
            if (m.HasProperty("_ZTestDepthEqualForOpaque")) m.SetFloat("_ZTestDepthEqualForOpaque", 4f);
            if (m.HasProperty("_ZTestTransparent")) m.SetFloat("_ZTestTransparent", 4f);
            if (m.HasProperty("_AlphaCutoffEnable")) m.SetFloat("_AlphaCutoffEnable", 0f);
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);                     // Built-in Transparent
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_BLENDMODE_ALPHA");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetShaderPassEnabled("TransparentDepthPrepass", false);
            m.SetShaderPassEnabled("TransparentDepthPostpass", false);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + queueOffset;
        }

        /// <summary>発光を設定する。HDRP の _EmissiveColor と Built-in の _EmissionColor の両方に効かせる。</summary>
        public static void ApplyEmission(Material m, Color color, float intensity)
        {
            Color hdr = color.linear * Mathf.Max(0f, intensity);
            if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", hdr);
            if (m.HasProperty("_EmissiveColorLDR")) m.SetColor("_EmissiveColorLDR", color);
            if (m.HasProperty("_UseEmissiveIntensity")) m.SetFloat("_UseEmissiveIntensity", 1f);
            if (m.HasProperty("_EmissiveIntensity")) m.SetFloat("_EmissiveIntensity", Mathf.Max(0.0001f, intensity));
            if (m.HasProperty("_EmissiveIntensityUnit")) m.SetFloat("_EmissiveIntensityUnit", 0f); // Nits
            if (m.HasProperty("_EmissiveExposureWeight")) m.SetFloat("_EmissiveExposureWeight", 0.35f);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", hdr);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        // ------------------------------------------------------------------ 材質

        /// <summary>屋台の柱・カウンター。木目テクスチャ + 細かい法線。ざらついて艶が無い。</summary>
        public static Material Wood(Color c) => Cached(Key("wood", c), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetBaseMap(m, ProceduralTextures.Wood(256, 11), 1.0f);
            SetNormalMap(m, ProceduralTextures.NoiseNormal(256, 0.55f, 11), 1.0f, 0.7f);
            SetSmoothMetal(m, 0.18f, 0f);
            return m;
        });

        /// <summary>
        /// 板張りの壁。板の合わせ目（目地）が溝として影になる。屋台の側面・背面に貼る。
        /// タイリングは「1m あたり約3枚」になるよう決め打ちしてある。
        /// </summary>
        public static Material Planks(Color c) => Cached(Key("planks", c), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetBaseMap(m, ProceduralTextures.Planks(256, 6, 17), 0.55f);
            SetNormalMap(m, ProceduralTextures.PlankNormal(256, 6, 17), 0.55f, 1.0f);
            SetSmoothMetal(m, 0.16f, 0f);
            return m;
        });

        /// <summary>暖簾・のぼり・法被の布。艶なし。</summary>
        public static Material Fabric(Color c) => Cached(Key("fabric", c), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetBaseMap(m, ProceduralTextures.Fabric(256, 23), 2.0f);
            SetNormalMap(m, ProceduralTextures.NoiseNormal(256, 0.35f, 23), 2.0f, 0.5f);
            SetSmoothMetal(m, 0.10f, 0f);
            if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
            m.EnableKeyword("_DOUBLESIDED_ON");
            if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            return m;
        });

        /// <summary>提灯・障子の和紙。わずかに光を透かす。Emission も乗せられる。</summary>
        public static Material Paper(Color c) => Cached(Key("paper", c), () =>
        {
            var m = NewLit();
            var col = c; col.a = 0.94f;
            SetBaseColor(m, col);
            SetBaseMap(m, ProceduralTextures.Washi(256, 37), 1.0f);
            SetSmoothMetal(m, 0.12f, 0f);
            if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
            m.EnableKeyword("_DOUBLESIDED_ON");
            if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            return m;
        });

        /// <summary>内側から灯りが透ける和紙。提灯・行灯に使う。</summary>
        public static Material GlowingPaper(Color c, float intensity)
            => Cached("glowpaper#" + ColorUtility.ToHtmlStringRGBA(c) + "#" + intensity.ToString("0.##"), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetBaseMap(m, ProceduralTextures.Washi(256, 37), 1.0f);
            SetSmoothMetal(m, 0.14f, 0f);
            if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
            m.EnableKeyword("_DOUBLESIDED_ON");
            ApplyEmission(m, c, intensity);
            return m;
        });

        /// <summary>鉄板・金具・鍋。</summary>
        public static Material Metal(Color c) => Cached(Key("metal", c), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetNormalMap(m, ProceduralTextures.NoiseNormal(256, 0.18f, 53), 3.0f, 0.35f);
            SetSmoothMetal(m, 0.70f, 0.90f);
            return m;
        });

        /// <summary>光る物。裸電球・提灯の内側・花火の芯。</summary>
        public static Material Emissive(Color c, float intensity)
            => Cached("emis#" + ColorUtility.ToHtmlStringRGBA(c) + "#" + intensity.ToString("0.##"), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetSmoothMetal(m, 0.55f, 0f);
            ApplyEmission(m, c, intensity);
            return m;
        });

        /// <summary>金魚すくいの水 (§62)。半透明の青緑。UV は MaterialPropertyBlock でずらせる。</summary>
        public static Material Water() => Cached("water", () =>
        {
            var m = NewLit();
            var c = new Color(0.16f, 0.44f, 0.46f, 0.62f);
            SetBaseColor(m, c);
            SetBaseMap(m, ProceduralTextures.NoiseNormal(256, 0.2f, 71), 3.0f);
            SetNormalMap(m, ProceduralTextures.NoiseNormal(256, 1.1f, 71), 3.0f, 0.8f);
            SetSmoothMetal(m, 0.95f, 0f);
            MakeTransparent(m, 10);
            return m;
        });

        /// <summary>会場の地面（土）。</summary>
        public static Material Ground() => Ground(new Color(0.27f, 0.22f, 0.17f));

        /// <summary>地面の色違い。参道の砂利などに使う。</summary>
        public static Material Ground(Color c) => Cached(Key("ground", c), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetBaseMap(m, ProceduralTextures.Wood(256, 5), 14f);
            SetNormalMap(m, ProceduralTextures.NoiseNormal(256, 1.2f, 5), 14f, 1.0f);
            SetSmoothMetal(m, 0.06f, 0f);
            return m;
        });

        /// <summary>NPC の肌。</summary>
        public static Material Skin(Color c) => Cached(Key("skin", c), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetSmoothMetal(m, 0.30f, 0f);
            return m;
        });

        /// <summary>塗装・プラスチック・景品など、艶を指定したい単色。</summary>
        public static Material Painted(Color c, float smoothness = 0.45f)
            => Cached("paint#" + ColorUtility.ToHtmlStringRGBA(c) + "#" + smoothness.ToString("0.##"), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetSmoothMetal(m, smoothness, 0f);
            return m;
        });

        /// <summary>木の葉。両面表示でざらつく。</summary>
        public static Material Foliage(Color c) => Cached(Key("foliage", c), () =>
        {
            var m = NewLit();
            SetBaseColor(m, c);
            SetBaseMap(m, ProceduralTextures.Fabric(256, 88), 4f);
            SetSmoothMetal(m, 0.16f, 0f);
            if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
            m.EnableKeyword("_DOUBLESIDED_ON");
            return m;
        });

        /// <summary>看板・案内板の面。文字テクスチャをそのまま貼る。</summary>
        public static Material Printed(Texture2D tex, Color tint)
        {
            string key = "printed#" + (tex != null ? tex.name : "none") + "#" + ColorUtility.ToHtmlStringRGB(tint);
            return Cached(key, () =>
            {
                var m = NewLit();
                SetBaseColor(m, tint);
                SetBaseMap(m, tex, 1f);
                SetSmoothMetal(m, 0.14f, 0f);
                return m;
            });
        }

        /// <summary>
        /// 灯りを仕込んだ看板。文字テクスチャをそのまま発光にも使うので、
        /// 光源が届かない高さ（屋根の上の看板など）でも屋号が読める。
        /// intensity は控えめに。上げすぎるとネオンになる。
        /// </summary>
        public static Material PrintedGlow(Texture2D tex, Color tint, float intensity)
        {
            string key = "printglow#" + (tex != null ? tex.name : "none") + "#"
                         + ColorUtility.ToHtmlStringRGB(tint) + "#" + intensity.ToString("0.##");
            return Cached(key, () =>
            {
                var m = NewLit();
                SetBaseColor(m, tint);
                SetBaseMap(m, tex, 1f);
                SetSmoothMetal(m, 0.14f, 0f);
                if (m.HasProperty("_EmissiveColorMap"))
                {
                    m.SetTexture("_EmissiveColorMap", tex);
                    m.SetTextureScale("_EmissiveColorMap", Vector2.one);
                    m.EnableKeyword("_EMISSIVE_COLOR_MAP");
                }
                ApplyEmission(m, Color.white, intensity);
                return m;
            });
        }

        /// <summary>
        /// 文字を刷った布。前掛け・幕板・品書きに使う。両面表示で艶が無い。
        /// </summary>
        public static Material PrintedFabric(Texture2D tex, Color tint)
        {
            string key = "printfab#" + (tex != null ? tex.name : "none") + "#" + ColorUtility.ToHtmlStringRGB(tint);
            return Cached(key, () =>
            {
                var m = NewLit();
                SetBaseColor(m, tint);
                SetBaseMap(m, tex, 1f);
                SetSmoothMetal(m, 0.08f, 0f);
                if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
                m.EnableKeyword("_DOUBLESIDED_ON");
                if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                return m;
            });
        }

        /// <summary>半透明の色板。ビニール袋・水ヨーヨー・ガラス。</summary>
        public static Material Translucent(Color c, float alpha, float smoothness = 0.8f)
            => Cached("trans#" + ColorUtility.ToHtmlStringRGB(c) + "#" + alpha.ToString("0.##") + "#" + smoothness.ToString("0.##"), () =>
        {
            var m = NewLit();
            var col = c; col.a = alpha;
            SetBaseColor(m, col);
            SetSmoothMetal(m, smoothness, 0f);
            MakeTransparent(m);
            return m;
        });

        /// <summary>湯気・煙・冷気のパーティクル用。ライティングを受けない半透明。</summary>
        public static Material Particle(Color c)
            => Cached(Key("particle", c), () =>
        {
            var m = new Material(UnlitShader);
            SetBaseColor(m, c);
            SetBaseMap(m, ProceduralTextures.SoftCircle(64), 1f);
            MakeTransparent(m);
            if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
            m.EnableKeyword("_DOUBLESIDED_ON");
            return m;
        });
    }
}
