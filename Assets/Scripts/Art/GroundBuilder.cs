using Matsuri.Script;
using UnityEngine;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Art
{
    /// <summary>
    /// 会場の地面を作る。土のメッシュ（ゆるい起伏つき）・参道・外周の木立・奥の神社の丘。
    /// NavMesh のベイク対象になるよう MeshCollider を付ける。
    /// </summary>
    public static class GroundBuilder
    {
        /// <summary>敷地の外側にどれだけ地面を伸ばすか (m)。会場の縁で世界が途切れないように。</summary>
        const float Margin = 55f;

        /// <summary>地面メッシュの1マスの大きさ (m)。</summary>
        const float Cell = 2.5f;

        /// <summary>参道の幅 (m)。</summary>
        const float PathWidth = 7.0f;

        static float s_HillZ;
        static float s_HillRadius;
        static float s_HillHeight;

        /// <summary>神社の丘の頂上のワールド座標。カメラ演出などから参照できる。</summary>
        public static Vector3 ShrinePosition { get; private set; }

        public static GameObject Build(Transform parent, GroundBounds bounds)
        {
            var root = ArtParts.Empty("Ground", parent);

            float minX = bounds.MinX - Margin, maxX = bounds.MaxX + Margin;
            float minZ = bounds.MinZ - Margin, maxZ = bounds.MaxZ + Margin;

            s_HillRadius = Mathf.Max(20f, (bounds.MaxX - bounds.MinX) * 0.20f);
            s_HillZ = bounds.MaxZ + s_HillRadius * 0.55f;
            s_HillHeight = 6.5f;

            BuildTerrain(root.transform, minX, maxX, minZ, maxZ);
            BuildApproach(root.transform, bounds);
            BuildTreeLine(root.transform, bounds);
            BuildShrineHill(root.transform, bounds);

            return root;
        }

        // ------------------------------------------------------------------ 地形

        /// <summary>
        /// 会場の中央は平ら（屋台が浮かない）。外へ行くほど起伏を強め、
        /// 奥には神社の丘を持ち上げる。
        /// </summary>
        public static float SampleHeight(float x, float z, GroundBounds bounds)
        {
            // 敷地からどれだけ外に出ているか
            float outX = Mathf.Max(0f, Mathf.Max(bounds.MinX - x, x - bounds.MaxX));
            float outZ = Mathf.Max(0f, Mathf.Max(bounds.MinZ - z, z - bounds.MaxZ));
            float outside = Mathf.Sqrt(outX * outX + outZ * outZ);
            float edge = Mathf.Clamp01(outside / 22f);

            float wave = Mathf.Sin(x * 0.081f) * Mathf.Cos(z * 0.067f) * 0.55f
                       + Mathf.Sin((x + z) * 0.043f) * 0.32f
                       + Mathf.Sin(x * 0.19f + z * 0.14f) * 0.13f;

            // 会場の中央はほぼ平ら（屋台が浮いたり沈んだりしない）
            float h = wave * (0.02f + edge * 1.35f);

            // 神社の丘（なめらかな盛り上がり）
            float dx = x, dz = z - s_HillZ;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist < s_HillRadius)
            {
                float t = 1f - dist / s_HillRadius;
                h += s_HillHeight * t * t * (3f - 2f * t);
            }
            return h;
        }

        static void BuildTerrain(Transform root, float minX, float maxX, float minZ, float maxZ)
        {
            var bounds = new GroundBounds(minX + Margin, maxX - Margin, minZ + Margin, maxZ - Margin);
            int nx = Mathf.Max(2, Mathf.RoundToInt((maxX - minX) / Cell));
            int nz = Mathf.Max(2, Mathf.RoundToInt((maxZ - minZ) / Cell));
            float sx = (maxX - minX) / nx, sz = (maxZ - minZ) / nz;

            var heights = new float[(nx + 1) * (nz + 1)];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                    heights[j * (nx + 1) + i] = SampleHeight(minX + i * sx, minZ + j * sz, bounds);

            Vector3 P(int i, int j) => new Vector3(minX + i * sx, heights[j * (nx + 1) + i], minZ + j * sz);
            Vector3 N(int i, int j)
            {
                int im = Mathf.Max(0, i - 1), ip = Mathf.Min(nx, i + 1);
                int jm = Mathf.Max(0, j - 1), jp = Mathf.Min(nz, j + 1);
                float hx = heights[j * (nx + 1) + ip] - heights[j * (nx + 1) + im];
                float hz = heights[jp * (nx + 1) + i] - heights[jm * (nx + 1) + i];
                float dx = (ip - im) * sx, dz = (jp - jm) * sz;
                return new Vector3(-hx / Mathf.Max(0.001f, dx), 1f, -hz / Mathf.Max(0.001f, dz)).normalized;
            }
            Vector2 U(int i, int j) => new Vector2((minX + i * sx) / 6f, (minZ + j * sz) / 6f);

            var b = new MeshBuilder();
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                    b.AddQuadSmooth(
                        P(i, j), N(i, j), U(i, j),
                        P(i + 1, j), N(i + 1, j), U(i + 1, j),
                        P(i + 1, j + 1), N(i + 1, j + 1), U(i + 1, j + 1),
                        P(i, j + 1), N(i, j + 1), U(i, j + 1));

            var mesh = b.ToMesh("GroundMesh");
            var go = ArtParts.Part("Terrain", root, mesh, MatsuriMaterials.Ground(new Color(0.26f, 0.21f, 0.16f)), Vector3.zero);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.GetComponent<MeshRenderer>().receiveShadows = true;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
        }

        // ------------------------------------------------------------------ 参道

        /// <summary>会場を十字に貫く参道。地面より少し明るい砂利の帯。</summary>
        static void BuildApproach(Transform root, GroundBounds bounds)
        {
            var mat = MatsuriMaterials.Ground(new Color(0.47f, 0.43f, 0.37f));
            var node = ArtParts.Empty("Approach", root);

            // 入口(-Z)から神社の丘(+Z)へ抜ける縦の参道
            AddPathStrip(node.transform, mat, "PathMain", bounds,
                new Vector2(0f, bounds.MinZ - Margin * 0.6f), new Vector2(0f, s_HillZ - s_HillRadius * 0.35f), PathWidth);
            // 会場を横切る参道
            AddPathStrip(node.transform, mat, "PathCross", bounds,
                new Vector2(bounds.MinX + 4f, 0f), new Vector2(bounds.MaxX - 4f, 0f), PathWidth * 0.8f);
        }

        static void AddPathStrip(Transform parent, Material mat, string name, GroundBounds bounds,
            Vector2 from, Vector2 to, float width)
        {
            Vector2 dir = (to - from);
            float length = dir.magnitude;
            if (length < 1f) return;
            dir /= length;
            Vector2 side = new Vector2(-dir.y, dir.x) * (width * 0.5f);

            int segs = Mathf.Max(2, Mathf.RoundToInt(length / Cell));
            var b = new MeshBuilder();
            const float lift = 0.06f;

            Vector3 Pt(int i, float s)
            {
                Vector2 p = from + dir * (length * (i / (float)segs)) + side * s;
                return new Vector3(p.x, SampleHeight(p.x, p.y, bounds) + lift, p.y);
            }

            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = Pt(i, -1f), p1 = Pt(i, 1f), p2 = Pt(i + 1, 1f), p3 = Pt(i + 1, -1f);
                float v0 = i * (length / segs) / 4f, v1 = (i + 1) * (length / segs) / 4f;
                b.AddQuad(p0, p1, p2, p3, Vector3.up,
                    new Vector2(0f, v0), new Vector2(width / 4f, v0), new Vector2(width / 4f, v1), new Vector2(0f, v1));
            }

            var mesh = b.ToMesh(name);
            var go = ArtParts.Part(name, parent, mesh, mat, Vector3.zero);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
        }

        // ------------------------------------------------------------------ 外周の木立

        static void BuildTreeLine(Transform root, GroundBounds bounds)
        {
            var node = ArtParts.Empty("TreeLine", root);
            var rng = new MRandom(20240801u);
            var leafBase = new Color(0.18f, 0.32f, 0.19f);

            float cx = (bounds.MinX + bounds.MaxX) * 0.5f;
            float cz = (bounds.MinZ + bounds.MaxZ) * 0.5f;
            float rx = (bounds.MaxX - bounds.MinX) * 0.5f;
            float rz = (bounds.MaxZ - bounds.MinZ) * 0.5f;

            const int count = 38;
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f + rng.NextFloat(-0.05f, 0.05f);
                float band = rng.NextFloat(6f, 26f);
                float x = cx + Mathf.Cos(a) * (rx + band);
                float z = cz + Mathf.Sin(a) * (rz + band);

                // 参道の抜けと神社の丘は空けておく
                if (Mathf.Abs(x) < PathWidth && z < cz) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(0f, s_HillZ)) < s_HillRadius * 0.9f) continue;

                var tree = ArtParts.Empty("Tree" + i.ToString("00"), node.transform,
                    new Vector3(x, SampleHeight(x, z, bounds) - 0.15f, z),
                    Quaternion.Euler(0f, rng.NextFloat(0f, 360f), 0f));

                var leaf = Color.Lerp(leafBase, new Color(0.26f, 0.40f, 0.22f), rng.NextFloat());
                float scale = rng.NextFloat(0.85f, 1.55f);
                var canopy = ProceduralDecorationFactory.BuildTree(tree.transform, leaf, scale, i);
                SwayAnimator.Attach(canopy, SwayMode.Rotate, 1.8f, 0.3f + rng.NextFloat(0f, 0.2f)).Axis = Vector3.forward;

                LodBuilder.AddLod(tree, new[] { 0.20f, 0.05f, 0.006f });
            }
        }

        // ------------------------------------------------------------------ 神社の丘

        static void BuildShrineHill(Transform root, GroundBounds bounds)
        {
            var node = ArtParts.Empty("ShrineHill", root);

            float topY = SampleHeight(0f, s_HillZ, bounds);
            ShrinePosition = new Vector3(0f, topY, s_HillZ);

            // 社殿
            var shrine = ArtParts.Empty("Shrine", node.transform, new Vector3(0f, topY, s_HillZ));
            ProceduralDecorationFactory.BuildShrine(shrine.transform, new Color(0.72f, 0.20f, 0.15f));
            LodBuilder.AddLod(shrine, new[] { 0.34f, 0.08f, 0.004f });

            // 丘の手前に鳥居
            float toriiZ = s_HillZ - s_HillRadius * 0.62f;
            var torii = ArtParts.Empty("Torii", node.transform, new Vector3(0f, SampleHeight(0f, toriiZ, bounds), toriiZ));
            ProceduralDecorationFactory.BuildTorii(torii.transform, new Color(0.80f, 0.22f, 0.14f), 6.4f, 7.8f);
            LodBuilder.AddLod(torii, new[] { 0.30f, 0.07f, 0.004f });

            // 参道からの石段
            var steps = ArtParts.Empty("Steps", node.transform);
            var stone = MatsuriMaterials.Painted(new Color(0.50f, 0.49f, 0.46f), 0.18f);
            var stepMesh = MatsuriMeshes.Box(new Vector3(PathWidth * 0.8f, 0.22f, 0.85f));
            float z0 = toriiZ + 1.2f;
            int stepCount = Mathf.Max(4, Mathf.RoundToInt((s_HillZ - s_HillRadius * 0.18f - z0) / 0.85f));
            for (int i = 0; i < stepCount; i++)
            {
                float z = z0 + i * 0.85f;
                float y = Mathf.Lerp(SampleHeight(0f, z0, bounds), topY, i / (float)Mathf.Max(1, stepCount - 1));
                ArtParts.Part("Step" + i.ToString("00"), steps.transform, stepMesh, stone, new Vector3(0f, y + 0.11f, z));
            }

            // 石段の両脇の常夜灯
            var lights = ArtParts.Empty("Lantern", node.transform);
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                float z = Mathf.Lerp(z0, s_HillZ - s_HillRadius * 0.22f, t);
                float y = Mathf.Lerp(SampleHeight(0f, z0, bounds), topY, t);
                for (int s = -1; s <= 1; s += 2)
                {
                    var post = ArtParts.Empty("StoneLantern" + i + (s < 0 ? "L" : "R"), lights.transform,
                        new Vector3(s * (PathWidth * 0.5f + 0.9f), y, z));
                    ArtParts.Part("Base", post.transform, MatsuriMeshes.Box(new Vector3(0.5f, 0.22f, 0.5f)), stone, new Vector3(0f, 0.11f, 0f));
                    ArtParts.Part("Shaft", post.transform, MatsuriMeshes.Cylinder(0.11f, 1.15f, 10), stone, new Vector3(0f, 0.80f, 0f));
                    ArtParts.Part("Housing", post.transform, MatsuriMeshes.Box(new Vector3(0.42f, 0.40f, 0.42f)),
                        MatsuriMaterials.GlowingPaper(new Color(1f, 0.78f, 0.48f), 3.5f), new Vector3(0f, 1.58f, 0f));
                    ArtParts.Part("Cap", post.transform, MatsuriMeshes.Cone(0.40f, 0.28f, 4), stone, new Vector3(0f, 1.92f, 0f));
                }
            }
            ProceduralDecorationFactory.AttachLight(node.transform, new Color(1f, 0.80f, 0.55f), 2600f, 26f, topY + 4.0f)
                .transform.localPosition = new Vector3(0f, topY + 4.0f, s_HillZ - 2f);
        }
    }
}
