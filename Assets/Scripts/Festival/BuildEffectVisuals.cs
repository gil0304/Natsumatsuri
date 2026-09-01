using System;
using System.Collections.Generic;
using Matsuri.Art;
using Matsuri.Core;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 建設演出 (§39) で使う「光るリング」「光の柱」「砂ぼこり」「点灯できる灯り」を作る。
    /// 演出の進行そのものは <see cref="BuildAnimation"/> が持ち、
    /// ここは見た目の部品作りだけを担当する (§66)。
    /// </summary>
    internal static class BuildEffectVisuals
    {
        internal static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        internal static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        // ────────────────────────────────────────────────────
        // 灯り
        // ────────────────────────────────────────────────────

        /// <summary>屋台の光源1つぶん。実光源と発光マテリアルの両方を同じ扱いにする。</summary>
        internal sealed class Lamp
        {
            public Light Light;
            public float Intensity;
            public Renderer Renderer;
            public Color Emissive;

            public float Height =>
                Light != null ? Light.transform.position.y :
                Renderer != null ? Renderer.transform.position.y : 0f;

            public void TurnOff()
            {
                if (Light != null)
                {
                    Intensity = Light.intensity;
                    Light.intensity = 0f;
                    Light.enabled = false;
                }
                ApplyEmissive(Color.black);
            }

            public void TurnOn()
            {
                if (Light != null)
                {
                    Light.enabled = true;
                    Light.intensity = Intensity;
                }
                ApplyEmissive(Emissive);
            }

            void ApplyEmissive(Color color)
            {
                if (Renderer == null) return;

                var block = new MaterialPropertyBlock();
                Renderer.GetPropertyBlock(block);
                block.SetColor(EmissiveColorId, color);
                block.SetColor(EmissionColorId, color);
                Renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>点灯できる物を集める。低い位置から順に並べ、下から灯りが上がって見えるようにする。</summary>
        internal static List<Lamp> CollectLamps(GameObject target, int maxLamps = 14)
        {
            var result = new List<Lamp>(12);

            var lights = target.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null) continue;
                if (light.type == LightType.Directional) continue;   // 月光などは対象外
                result.Add(new Lamp { Light = light, Intensity = light.intensity });
            }

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                var mat = r.sharedMaterial;
                if (mat == null) continue;

                Color emissive = Color.black;
                if (mat.HasProperty(EmissiveColorId)) emissive = mat.GetColor(EmissiveColorId);
                else if (mat.HasProperty(EmissionColorId)) emissive = mat.GetColor(EmissionColorId);

                if (emissive.maxColorComponent <= 0.001f) continue;

                result.Add(new Lamp { Renderer = r, Emissive = emissive });
            }

            result.Sort((a, b) => a.Height.CompareTo(b.Height));

            if (result.Count <= maxLamps) return result;

            // 灯りが多すぎると点灯だけで何秒もかかる。間引いて順に点け、残りは最後にまとめて点ける。
            var trimmed = new List<Lamp>(maxLamps);
            float step = result.Count / (float)maxLamps;

            for (int i = 0; i < maxLamps; i++)
            {
                int index = Mathf.Min(result.Count - 1, Mathf.FloorToInt(i * step));
                if (!trimmed.Contains(result[index])) trimmed.Add(result[index]);
            }

            for (int i = 0; i < result.Count; i++)
                if (!trimmed.Contains(result[i])) result[i].TurnOn();

            return trimmed;
        }

        // ────────────────────────────────────────────────────
        // 砂ぼこり (§39-4)
        // ────────────────────────────────────────────────────

        internal static void SpawnDust(Vector3 position, float radius, Color tint)
        {
            var holder = new GameObject("BuildDust");
            holder.transform.position = position;

            ParticleSystem ps = null;
            try
            {
                ps = MatsuriVfx.CreateDust(holder.transform, tint, 0f);
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"砂ぼこりVFXの生成に失敗しました: {e.Message}");
            }

            if (ps == null)
            {
                UnityEngine.Object.Destroy(holder);
                return;
            }

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.5f, radius);

            ps.Play();
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt(radius * 14f), 16, 60));

            UnityEngine.Object.Destroy(holder, 3.5f);
        }

        // ────────────────────────────────────────────────────
        // リングと光の柱
        // ────────────────────────────────────────────────────

        internal static GameObject CreateRing(Vector3 position, float radius, Color color)
        {
            var go = new GameObject("BuildRing");
            go.transform.position = position + Vector3.up * 0.03f;
            go.transform.localScale = Vector3.zero;

            go.AddComponent<MeshFilter>().sharedMesh = BuildRingMesh(radius * 0.86f, radius * 1.28f, 48);
            SetUpRenderer(go, color);
            return go;
        }

        internal static GameObject CreatePillar(Vector3 position, float radius, float height, Color color)
        {
            var go = new GameObject("BuildPillar");
            go.transform.position = position;
            go.transform.localScale = new Vector3(1f, 0f, 1f);

            go.AddComponent<MeshFilter>().sharedMesh = BuildPillarMesh(radius, height, 24);
            SetUpRenderer(go, color);
            return go;
        }

        static void SetUpRenderer(GameObject go, Color color)
        {
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MakeGlowMaterial(color, 1f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>演出用のマテリアル（個別インスタンス）を取り出す。</summary>
        internal static Material GetInstanceMaterial(GameObject go)
        {
            if (go == null) return null;
            var r = go.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return null;
            return r.material;   // material で個別インスタンスになる
        }

        /// <summary>発光マテリアルを作る。HDRP が無い環境でも落ちないようフォールバックする。</summary>
        internal static Material MakeGlowMaterial(Color color, float intensity)
        {
            Material source = null;
            try
            {
                source = MatsuriMaterials.Emissive(color, intensity);
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"発光マテリアルの取得に失敗しました: {e.Message}");
            }

            if (source != null) return new Material(source);

            Shader shader = null;
            string[] candidates = { "HDRP/Unlit", "Unlit/Color", "Universal Render Pipeline/Unlit", "Standard", "Sprites/Default" };
            for (int i = 0; i < candidates.Length && shader == null; i++) shader = Shader.Find(candidates[i]);

            if (shader == null)
            {
                MatsuriLog.Warn("建設演出に使えるシェーダーが見つかりませんでした。リングと光の柱は表示されません。");
                return null;
            }

            var mat = new Material(shader);
            SetGlow(mat, color, intensity);
            return mat;
        }

        /// <summary>発光の強さを変える。HDRP と標準シェーダーの両方のプロパティ名に対応する。</summary>
        internal static void SetGlow(Material mat, Color color, float intensity)
        {
            if (mat == null) return;

            Color hdr = color * Mathf.Max(0f, intensity);
            Color tinted = new Color(color.r, color.g, color.b, Mathf.Clamp01(intensity));

            if (mat.HasProperty(EmissiveColorId)) mat.SetColor(EmissiveColorId, hdr);
            if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, hdr);
            if (mat.HasProperty(UnlitColorId)) mat.SetColor(UnlitColorId, hdr);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, tinted);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, tinted);
        }

        // ────────────────────────────────────────────────────
        // メッシュ（演出専用の単純な形なのでここで作る）
        // ────────────────────────────────────────────────────

        static Mesh _cachedRing;
        static float _cachedRingInner, _cachedRingOuter;

        /// <summary>XZ平面の円環（ドーナツ状の板）。</summary>
        static Mesh BuildRingMesh(float inner, float outer, int segments)
        {
            if (_cachedRing != null &&
                Mathf.Approximately(_cachedRingInner, inner) &&
                Mathf.Approximately(_cachedRingOuter, outer))
                return _cachedRing;

            segments = Mathf.Max(8, segments);

            var vertices = new Vector3[segments * 2];
            var uv = new Vector2[segments * 2];
            var normals = new Vector3[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);

                vertices[i * 2 + 0] = new Vector3(cos * inner, 0f, sin * inner);
                vertices[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
                normals[i * 2 + 0] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
                uv[i * 2 + 0] = new Vector2(i / (float)segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
            }

            FillQuadStrip(triangles, segments);

            var mesh = new Mesh { name = "BuildRingMesh" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            _cachedRing = mesh;
            _cachedRingInner = inner;
            _cachedRingOuter = outer;
            return mesh;
        }

        /// <summary>上に向かって細くなる光の筒（側面のみ）。頂点カラーで上端を薄くする。</summary>
        static Mesh BuildPillarMesh(float radius, float height, int segments)
        {
            segments = Mathf.Max(6, segments);

            var vertices = new Vector3[segments * 2];
            var uv = new Vector2[segments * 2];
            var colors = new Color[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);

                vertices[i * 2 + 0] = new Vector3(cos * radius, 0f, sin * radius);
                vertices[i * 2 + 1] = new Vector3(cos * radius * 0.35f, height, sin * radius * 0.35f);
                uv[i * 2 + 0] = new Vector2(i / (float)segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
                colors[i * 2 + 0] = Color.white;
                colors[i * 2 + 1] = new Color(1f, 1f, 1f, 0f);
            }

            FillQuadStrip(triangles, segments);

            var mesh = new Mesh { name = "BuildPillarMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>「内周・外周」を交互に並べた頂点列から、閉じた帯を三角形化する。</summary>
        static void FillQuadStrip(int[] triangles, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = ((i + 1) % segments) * 2;

                triangles[i * 6 + 0] = a;
                triangles[i * 6 + 1] = a + 1;
                triangles[i * 6 + 2] = b + 1;
                triangles[i * 6 + 3] = a;
                triangles[i * 6 + 4] = b + 1;
                triangles[i * 6 + 5] = b;
            }
        }
    }
}
