using System.Collections.Generic;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// メッシュ組み立て用のバッファ。三角形の巻き方向を「欲しい法線」から自動で決めるので、
    /// 呼び出し側は頂点順を気にせず「外向きはこっち」とだけ指定すればよい。
    /// </summary>
    internal sealed class MeshBuilder
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(256);
        public readonly List<Vector3> Normals = new List<Vector3>(256);
        public readonly List<Vector2> Uvs = new List<Vector2>(256);
        public readonly List<int> Triangles = new List<int>(512);

        public int Add(Vector3 p, Vector3 n, Vector2 uv)
        {
            Vertices.Add(p);
            Normals.Add(n);
            Uvs.Add(uv);
            return Vertices.Count - 1;
        }

        /// <summary>
        /// 三角形 (a, b, c) の表面の向き。
        /// Unity は左手系で「表から見て時計回り」が表。その法線は Cross(b-a, c-a)。
        /// 例: a=(0,0,0) b=(0,0,1) c=(1,0,0) は上から見て時計回りで、法線は +Y になる。
        /// ここを逆にすると全ての面が裏返り、地面が描画されず NavMesh も焼けない。
        /// </summary>
        static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
            => Vector3.Cross(b - a, c - a);

        public void AddTriangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 desiredNormal,
            Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            bool flip = Vector3.Dot(FaceNormal(p0, p1, p2), desiredNormal) < 0f;
            Vector3 n = desiredNormal.sqrMagnitude > 1e-8f ? desiredNormal.normalized : Vector3.up;
            int a = Add(p0, n, uv0);
            int b = Add(flip ? p2 : p1, n, flip ? uv2 : uv1);
            int c = Add(flip ? p1 : p2, n, flip ? uv1 : uv2);
            Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
        }

        /// <summary>平面四角形。フラットシェーディング。</summary>
        public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 desiredNormal,
            Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            bool flip = Vector3.Dot(FaceNormal(p0, p1, p2), desiredNormal) < 0f;
            Vector3 n = desiredNormal.sqrMagnitude > 1e-8f ? desiredNormal.normalized : Vector3.up;
            int i0 = Add(p0, n, uv0);
            int i1 = Add(p1, n, uv1);
            int i2 = Add(p2, n, uv2);
            int i3 = Add(p3, n, uv3);
            if (flip)
            {
                Triangles.Add(i0); Triangles.Add(i2); Triangles.Add(i1);
                Triangles.Add(i0); Triangles.Add(i3); Triangles.Add(i2);
            }
            else
            {
                Triangles.Add(i0); Triangles.Add(i1); Triangles.Add(i2);
                Triangles.Add(i0); Triangles.Add(i2); Triangles.Add(i3);
            }
        }

        /// <summary>頂点ごとに法線を持つ四角形。曲面（円柱・球）用のスムースシェーディング。</summary>
        public void AddQuadSmooth(
            Vector3 p0, Vector3 n0, Vector2 uv0,
            Vector3 p1, Vector3 n1, Vector2 uv1,
            Vector3 p2, Vector3 n2, Vector2 uv2,
            Vector3 p3, Vector3 n3, Vector2 uv3)
        {
            bool flip = Vector3.Dot(FaceNormal(p0, p1, p2), n0 + n1 + n2) < 0f;
            int i0 = Add(p0, n0, uv0);
            int i1 = Add(p1, n1, uv1);
            int i2 = Add(p2, n2, uv2);
            int i3 = Add(p3, n3, uv3);
            if (flip)
            {
                Triangles.Add(i0); Triangles.Add(i2); Triangles.Add(i1);
                Triangles.Add(i0); Triangles.Add(i3); Triangles.Add(i2);
            }
            else
            {
                Triangles.Add(i0); Triangles.Add(i1); Triangles.Add(i2);
                Triangles.Add(i0); Triangles.Add(i2); Triangles.Add(i3);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    /// <summary>
    /// 屋台・提灯・鳥居・NPC などを組み立てるための手続き生成メッシュ置き場 (§69 / §79)。
    /// GameObject.CreatePrimitive は一切使わない。生成結果はキーでキャッシュして共有する。
    /// </summary>
    public static partial class MatsuriMeshes
    {
        static readonly Dictionary<string, Mesh> s_Cache = new Dictionary<string, Mesh>(256);

        static Mesh Cached(string key, System.Func<Mesh> factory)
        {
            if (s_Cache.TryGetValue(key, out var m) && m != null) return m;
            m = factory();
            m.name = key;
            s_Cache[key] = m;
            return m;
        }

        /// <summary>キャッシュを破棄する。シーン切り替え時に呼ぶ。</summary>
        public static void ClearCache()
        {
            foreach (var kv in s_Cache) SafeDestroy(kv.Value);
            s_Cache.Clear();
        }

        static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        static string K(string tag, params float[] args)
        {
            var sb = new System.Text.StringBuilder(tag);
            for (int i = 0; i < args.Length; i++) sb.Append('_').Append(args[i].ToString("0.###"));
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 箱

        /// <summary>直方体。原点が中心。</summary>
        public static Mesh Box(Vector3 size) => Cached(K("Box", size.x, size.y, size.z), () => BuildBox(size));

        static Mesh BuildBox(Vector3 size)
        {
            var b = new MeshBuilder();
            AppendBox(b, Vector3.zero, size, Quaternion.identity);
            return b.ToMesh("Box");
        }

        static void AppendBox(MeshBuilder b, Vector3 center, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            // ローカル8頂点
            Vector3 P(float sx, float sy, float sz) => center + rot * new Vector3(sx * h.x, sy * h.y, sz * h.z);

            Vector3 nx = rot * Vector3.right, ny = rot * Vector3.up, nz = rot * Vector3.forward;
            Vector2 u00 = new Vector2(0, 0), u10, u11, u01;

            // +X / -X
            u10 = new Vector2(size.z, 0); u11 = new Vector2(size.z, size.y); u01 = new Vector2(0, size.y);
            b.AddQuad(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), nx, u00, u10, u11, u01);
            b.AddQuad(P(-1, -1, 1), P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), -nx, u00, u10, u11, u01);
            // +Y / -Y
            u10 = new Vector2(size.x, 0); u11 = new Vector2(size.x, size.z); u01 = new Vector2(0, size.z);
            b.AddQuad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), ny, u00, u10, u11, u01);
            b.AddQuad(P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), P(-1, -1, -1), -ny, u00, u10, u11, u01);
            // +Z / -Z
            u10 = new Vector2(size.x, 0); u11 = new Vector2(size.x, size.y); u01 = new Vector2(0, size.y);
            b.AddQuad(P(1, -1, 1), P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), nz, u00, u10, u11, u01);
            b.AddQuad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), -nz, u00, u10, u11, u01);
        }

        /// <summary>上下で幅が違う箱（台形柱）。原点が中心。</summary>
        public static Mesh TaperedBox(float bottomWidth, float bottomDepth, float topWidth, float topDepth, float height)
            => Cached(K("Taper", bottomWidth, bottomDepth, topWidth, topDepth, height),
                () => BuildTaperedBox(bottomWidth, bottomDepth, topWidth, topDepth, height));

        static Mesh BuildTaperedBox(float bw, float bd, float tw, float td, float h)
        {
            var b = new MeshBuilder();
            float y0 = -h * 0.5f, y1 = h * 0.5f;
            Vector3 b00 = new Vector3(-bw * 0.5f, y0, -bd * 0.5f);
            Vector3 b10 = new Vector3(bw * 0.5f, y0, -bd * 0.5f);
            Vector3 b11 = new Vector3(bw * 0.5f, y0, bd * 0.5f);
            Vector3 b01 = new Vector3(-bw * 0.5f, y0, bd * 0.5f);
            Vector3 t00 = new Vector3(-tw * 0.5f, y1, -td * 0.5f);
            Vector3 t10 = new Vector3(tw * 0.5f, y1, -td * 0.5f);
            Vector3 t11 = new Vector3(tw * 0.5f, y1, td * 0.5f);
            Vector3 t01 = new Vector3(-tw * 0.5f, y1, td * 0.5f);

            Vector2 q0 = new Vector2(0, 0), q1 = new Vector2(1, 0), q2 = new Vector2(1, 1), q3 = new Vector2(0, 1);
            b.AddQuad(b00, b10, t10, t00, Vector3.back, q0, q1, q2, q3);
            b.AddQuad(b11, b01, t01, t11, Vector3.forward, q0, q1, q2, q3);
            b.AddQuad(b10, b11, t11, t10, Vector3.right, q0, q1, q2, q3);
            b.AddQuad(b01, b00, t00, t01, Vector3.left, q0, q1, q2, q3);
            b.AddQuad(t00, t10, t11, t01, Vector3.up, q0, q1, q2, q3);
            b.AddQuad(b01, b11, b10, b00, Vector3.down, q0, q1, q2, q3);
            return b.ToMesh("TaperedBox");
        }

        /// <summary>XY 平面の板。法線 +Z、UV は左下(0,0)〜右上(1,1)。看板やテクスチャ表示用。</summary>
        public static Mesh Quad(float width, float height)
            => Cached(K("Quad", width, height), () =>
            {
                var b = new MeshBuilder();
                float w = width * 0.5f, h = height * 0.5f;
                b.AddQuad(new Vector3(-w, -h, 0f), new Vector3(w, -h, 0f), new Vector3(w, h, 0f), new Vector3(-w, h, 0f),
                    Vector3.forward, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                return b.ToMesh("Quad");
            });

        /// <summary>
        /// 文字を貼るための板。<see cref="Quad"/> と同じ形だが、UV の U だけ反転してある。
        ///
        /// Unity は左手系で、カメラの右手方向は forward と up から
        /// right = Cross(up, forward) で決まる。法線 +Z の面を +Z 側（お客側）から見るカメラは
        /// forward が -Z なので right = -X、つまり<b>ワールドの +X は画面の左</b>に出る。
        /// 素の Quad は U が +X に沿って増えるので、そのまま文字テクスチャを貼ると鏡文字になる。
        /// （Unity 内蔵の Quad プリミティブの法線が -Z 向きなのも同じ理由。）
        /// 看板・品書き・前掛けなど、文字を読ませる面は必ずこちらを使う。
        /// </summary>
        public static Mesh SignQuad(float width, float height)
            => Cached(K("SignQuad", width, height), () =>
            {
                var b = new MeshBuilder();
                float w = width * 0.5f, h = height * 0.5f;
                b.AddQuad(new Vector3(-w, -h, 0f), new Vector3(w, -h, 0f), new Vector3(w, h, 0f), new Vector3(-w, h, 0f),
                    Vector3.forward, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
                return b.ToMesh("SignQuad");
            });

        /// <summary>XZ 平面の板。法線 +Y。原点が中心。</summary>
        public static Mesh Plane(float width, float depth)
            => Cached(K("Plane", width, depth), () =>
            {
                var b = new MeshBuilder();
                float w = width * 0.5f, d = depth * 0.5f;
                b.AddQuad(new Vector3(-w, 0, -d), new Vector3(w, 0, -d), new Vector3(w, 0, d), new Vector3(-w, 0, d),
                    Vector3.up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                return b.ToMesh("Plane");
            });

        // ------------------------------------------------------------------ 旋盤（回転体）

        /// <summary>
        /// 輪郭 (x=半径, y=高さ) を Y 軸まわりに回して回転体を作る。円柱・球・カプセル・提灯の共通基盤。
        /// </summary>
        static Mesh Lathe(Vector2[] profile, int seg, bool capBottom, bool capTop, string name)
        {
            seg = Mathf.Max(3, seg);
            var b = new MeshBuilder();
            int n = profile.Length;

            // 輪郭の法線 (r, y) と、UV 用の弧長
            var pn = new Vector2[n];
            var arc = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = profile[Mathf.Max(0, i - 1)];
                Vector2 next = profile[Mathf.Min(n - 1, i + 1)];
                Vector2 t = next - prev;
                Vector2 nn = new Vector2(t.y, -t.x);
                if (nn.sqrMagnitude < 1e-10f) nn = new Vector2(1f, 0f);
                pn[i] = nn.normalized;
                if (i > 0) total += (profile[i] - profile[i - 1]).magnitude;
                arc[i] = total;
            }
            if (total <= 1e-6f) total = 1f;

            Vector3 Pos(int i, int j)
            {
                float a = (j / (float)seg) * Mathf.PI * 2f;
                return new Vector3(profile[i].x * Mathf.Cos(a), profile[i].y, profile[i].x * Mathf.Sin(a));
            }
            Vector3 Nor(int i, int j)
            {
                float a = (j / (float)seg) * Mathf.PI * 2f;
                var v = new Vector3(pn[i].x * Mathf.Cos(a), pn[i].y, pn[i].x * Mathf.Sin(a));
                return v.sqrMagnitude < 1e-8f ? Vector3.up : v.normalized;
            }
            Vector2 Uv(int i, int j) => new Vector2(j / (float)seg, arc[i] / total);

            for (int i = 0; i < n - 1; i++)
            {
                for (int j = 0; j < seg; j++)
                {
                    int j2 = j + 1;
                    Vector3 p0 = Pos(i, j), p1 = Pos(i, j2), p2 = Pos(i + 1, j2), p3 = Pos(i + 1, j);
                    if ((p0 - p1).sqrMagnitude < 1e-10f && (p3 - p2).sqrMagnitude < 1e-10f) continue;
                    b.AddQuadSmooth(
                        p0, Nor(i, j), Uv(i, j),
                        p1, Nor(i, j2), Uv(i, j2),
                        p2, Nor(i + 1, j2), Uv(i + 1, j2),
                        p3, Nor(i + 1, j), Uv(i + 1, j));
                }
            }

            if (capBottom && profile[0].x > 1e-5f) AppendDisc(b, profile[0].y, profile[0].x, seg, false);
            if (capTop && profile[n - 1].x > 1e-5f) AppendDisc(b, profile[n - 1].y, profile[n - 1].x, seg, true);

            return b.ToMesh(name);
        }

        static void AppendDisc(MeshBuilder b, float y, float r, int seg, bool up)
        {
            Vector3 nrm = up ? Vector3.up : Vector3.down;
            Vector3 c = new Vector3(0, y, 0);
            for (int j = 0; j < seg; j++)
            {
                float a0 = (j / (float)seg) * Mathf.PI * 2f;
                float a1 = ((j + 1) / (float)seg) * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r);
                b.AddTriangle(c, p0, p1, nrm,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f),
                    new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f));
            }
        }

        /// <summary>円柱。原点が中心。</summary>
        public static Mesh Cylinder(float radius, float height, int seg = 16)
            => Cached(K("Cyl", radius, height, seg), () => Lathe(
                new[] { new Vector2(radius, -height * 0.5f), new Vector2(radius, height * 0.5f) },
                seg, true, true, "Cylinder"));

        /// <summary>円錐。底面が -h/2、頂点が +h/2。</summary>
        public static Mesh Cone(float radius, float height, int seg = 16)
            => Cached(K("Cone", radius, height, seg), () => Lathe(
                new[] { new Vector2(radius, -height * 0.5f), new Vector2(0f, height * 0.5f) },
                seg, true, false, "Cone"));

        /// <summary>円板。法線 +Y。</summary>
        public static Mesh Disc(float radius, int seg = 20)
            => Cached(K("Disc", radius, seg), () =>
            {
                var b = new MeshBuilder();
                AppendDisc(b, 0f, radius, Mathf.Max(3, seg), true);
                return b.ToMesh("Disc");
            });

        /// <summary>球。原点が中心。</summary>
        public static Mesh Sphere(float radius, int seg = 16, int rings = 10)
            => Cached(K("Sph", radius, seg, rings), () =>
            {
                rings = Mathf.Max(3, rings);
                var prof = new Vector2[rings + 1];
                for (int i = 0; i <= rings; i++)
                {
                    float t = i / (float)rings;
                    float a = -Mathf.PI * 0.5f + t * Mathf.PI;
                    prof[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                }
                return Lathe(prof, seg, false, false, "Sphere");
            });

        /// <summary>半球。平らな面は下向き（お椀を伏せた形）。</summary>
        public static Mesh Hemisphere(float radius, int seg = 14, int rings = 6)
            => Cached(K("Hemi", radius, seg, rings), () =>
            {
                var prof = new Vector2[rings + 1];
                for (int i = 0; i <= rings; i++)
                {
                    float a = (i / (float)rings) * Mathf.PI * 0.5f;
                    prof[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                }
                return Lathe(prof, seg, true, false, "Hemisphere");
            });

        /// <summary>カプセル。height は全長。原点が中心。</summary>
        public static Mesh Capsule(float radius, float height, int seg = 14)
            => Cached(K("Cap", radius, height, seg), () =>
            {
                radius = Mathf.Max(0.001f, radius);
                float half = Mathf.Max(radius, height * 0.5f);
                float cyl = half - radius;
                const int arcSteps = 5;
                var prof = new List<Vector2>(arcSteps * 2 + 2);
                for (int i = 0; i <= arcSteps; i++)
                {
                    float a = -Mathf.PI * 0.5f + (i / (float)arcSteps) * Mathf.PI * 0.5f;
                    prof.Add(new Vector2(Mathf.Cos(a) * radius, -cyl + Mathf.Sin(a) * radius));
                }
                for (int i = 0; i <= arcSteps; i++)
                {
                    float a = (i / (float)arcSteps) * Mathf.PI * 0.5f;
                    prof.Add(new Vector2(Mathf.Cos(a) * radius, cyl + Mathf.Sin(a) * radius));
                }
                return Lathe(prof.ToArray(), seg, false, false, "Capsule");
            });

        /// <summary>ドーナツ。ポイの輪や輪投げに使う。</summary>
        public static Mesh Torus(float majorRadius, float minorRadius, int segMajor = 20, int segMinor = 8)
            => Cached(K("Torus", majorRadius, minorRadius, segMajor, segMinor), () =>
            {
                var b = new MeshBuilder();
                segMajor = Mathf.Max(3, segMajor); segMinor = Mathf.Max(3, segMinor);
                Vector3 P(int i, int j, out Vector3 n)
                {
                    float u = (i / (float)segMajor) * Mathf.PI * 2f;
                    float v = (j / (float)segMinor) * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                    n = dir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    return dir * majorRadius + n * minorRadius;
                }
                for (int i = 0; i < segMajor; i++)
                    for (int j = 0; j < segMinor; j++)
                    {
                        Vector3 n0, n1, n2, n3;
                        Vector3 p0 = P(i, j, out n0), p1 = P(i + 1, j, out n1);
                        Vector3 p2 = P(i + 1, j + 1, out n2), p3 = P(i, j + 1, out n3);
                        b.AddQuadSmooth(
                            p0, n0, new Vector2(i / (float)segMajor, j / (float)segMinor),
                            p1, n1, new Vector2((i + 1) / (float)segMajor, j / (float)segMinor),
                            p2, n2, new Vector2((i + 1) / (float)segMajor, (j + 1) / (float)segMinor),
                            p3, n3, new Vector2(i / (float)segMajor, (j + 1) / (float)segMinor));
                    }
                return b.ToMesh("Torus");
            });

        // ------------------------------------------------------------------ 提灯

        /// <summary>
        /// 提灯の胴。上下がすぼまり、蛇腹（横の骨）の凹凸が入る。上下に木の口輪も付く。
        /// 原点が中心、高さ height。
        /// </summary>
        public static Mesh Lantern(float radius, float height, int seg = 18)
            => Cached(K("Lant", radius, height, seg), () =>
            {
                const int rings = 20;
                var prof = new Vector2[rings + 1];
                for (int i = 0; i <= rings; i++)
                {
                    float t = i / (float)rings;
                    // 上下がすぼまった紡錘形
                    float shape = 0.34f + 0.66f * Mathf.Sin(Mathf.PI * t);
                    // 蛇腹（骨のふくらみ）
                    float rib = 1f + 0.045f * Mathf.Cos(t * Mathf.PI * 2f * 7f);
                    prof[i] = new Vector2(radius * shape * rib, -height * 0.5f + height * t);
                }
                var body = Lathe(prof, seg, false, false, "LanternBody");

                var parts = new List<CombineInstance>(3)
                {
                    new CombineInstance { mesh = body, transform = Matrix4x4.identity }
                };
                float capR = radius * 0.40f;
                var cap = Cylinder(capR, height * 0.06f, seg);
                parts.Add(new CombineInstance
                {
                    mesh = cap,
                    transform = Matrix4x4.Translate(new Vector3(0, -height * 0.5f + height * 0.02f, 0))
                });
                parts.Add(new CombineInstance
                {
                    mesh = cap,
                    transform = Matrix4x4.Translate(new Vector3(0, height * 0.5f - height * 0.02f, 0))
                });
                var m = new Mesh { name = "Lantern" };
                m.CombineMeshes(parts.ToArray(), true, true);
                m.RecalculateBounds();
                m.RecalculateTangents();
                SafeDestroy(body);
                return m;
            });

        /// <summary>XY 平面の三角形を Z 方向に押し出した柱。妻壁や看板の三角飾りに使う。</summary>
        public static Mesh TrianglePrism(Vector2 a, Vector2 b, Vector2 c, float thickness)
        {
            var mb = new MeshBuilder();
            float hz = thickness * 0.5f;
            Vector3 a0 = new Vector3(a.x, a.y, -hz), b0 = new Vector3(b.x, b.y, -hz), c0 = new Vector3(c.x, c.y, -hz);
            Vector3 a1 = new Vector3(a.x, a.y, hz), b1 = new Vector3(b.x, b.y, hz), c1 = new Vector3(c.x, c.y, hz);
            Vector2 ua = new Vector2(0, 0), ub = new Vector2(1, 0), uc = new Vector2(0.5f, 1);
            mb.AddTriangle(a1, b1, c1, Vector3.forward, ua, ub, uc);
            mb.AddTriangle(a0, b0, c0, Vector3.back, ua, ub, uc);
            void Side(Vector3 p0, Vector3 p1, Vector3 q1, Vector3 q0)
            {
                Vector3 n = Vector3.Cross(p1 - p0, Vector3.forward).normalized;
                if (Vector3.Dot(n, (p0 + p1) * 0.5f - new Vector3((a.x + b.x + c.x) / 3f, (a.y + b.y + c.y) / 3f, 0f)) < 0f) n = -n;
                mb.AddQuad(p0, p1, q1, q0, n, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
            Side(a0, b0, b1, a1);
            Side(b0, c0, c1, b1);
            Side(c0, a0, a1, c1);
            return mb.ToMesh("TrianglePrism");
        }

        // ------------------------------------------------------------------ 鳥居

        /// <summary>鳥居。柱2本 + 笠木 + 島木 + 貫 + 額束。足元が y=0。</summary>
        public static Mesh Torii(float width, float height)
            => Cached(K("Torii", width, height), () =>
            {
                float pillarR = width * 0.048f;
                float lean = width * 0.012f;  // 転び（内側に傾ける）
                var parts = new List<CombineInstance>(12);

                var pillar = Lathe(new[]
                {
                    new Vector2(pillarR * 1.06f, 0f),
                    new Vector2(pillarR, height * 0.5f),
                    new Vector2(pillarR * 0.92f, height)
                }, 12, true, true, "ToriiPillar");

                parts.Add(new CombineInstance
                {
                    mesh = pillar,
                    transform = Matrix4x4.TRS(new Vector3(-width * 0.5f, 0f, 0f), Quaternion.Euler(0f, 0f, -Mathf.Atan2(lean, height) * Mathf.Rad2Deg), Vector3.one)
                });
                parts.Add(new CombineInstance
                {
                    mesh = pillar,
                    transform = Matrix4x4.TRS(new Vector3(width * 0.5f, 0f, 0f), Quaternion.Euler(0f, 0f, Mathf.Atan2(lean, height) * Mathf.Rad2Deg), Vector3.one)
                });

                // 貫（下の横木）
                parts.Add(new CombineInstance
                {
                    mesh = Box(new Vector3(width * 1.16f, height * 0.045f, pillarR * 1.5f)),
                    transform = Matrix4x4.Translate(new Vector3(0f, height * 0.70f, 0f))
                });
                // 島木
                float shimagiY = height * 0.955f;
                parts.Add(new CombineInstance
                {
                    mesh = Box(new Vector3(width * 1.42f, height * 0.052f, pillarR * 1.9f)),
                    transform = Matrix4x4.Translate(new Vector3(0f, shimagiY, 0f))
                });
                // 笠木（わずかに反った上の横木を5分割で表現）
                const int kseg = 5;
                float kw = width * 1.58f;
                for (int i = 0; i < kseg; i++)
                {
                    float t0 = i / (float)kseg, t1 = (i + 1) / (float)kseg;
                    float x0 = -kw * 0.5f + kw * t0, x1 = -kw * 0.5f + kw * t1;
                    float c0 = Mathf.Abs(t0 - 0.5f) * 2f, c1 = Mathf.Abs(t1 - 0.5f) * 2f;
                    float y0 = shimagiY + height * 0.045f + c0 * c0 * height * 0.035f;
                    float y1 = shimagiY + height * 0.045f + c1 * c1 * height * 0.035f;
                    float segLen = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                    float ang = Mathf.Atan2(y1 - y0, x1 - x0) * Mathf.Rad2Deg;
                    parts.Add(new CombineInstance
                    {
                        mesh = Box(new Vector3(segLen * 1.04f, height * 0.05f, pillarR * 2.3f)),
                        transform = Matrix4x4.TRS(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f),
                            Quaternion.Euler(0f, 0f, ang), Vector3.one)
                    });
                }
                // 額束
                parts.Add(new CombineInstance
                {
                    mesh = Box(new Vector3(width * 0.09f, height * 0.19f, pillarR * 1.2f)),
                    transform = Matrix4x4.Translate(new Vector3(0f, height * 0.83f, 0f))
                });

                var m = new Mesh { name = "Torii" };
                m.CombineMeshes(parts.ToArray(), true, true);
                m.RecalculateBounds();
                m.RecalculateTangents();
                SafeDestroy(pillar);
                return m;
            });

        // ------------------------------------------------------------------ 布

        /// <summary>
        /// 暖簾・のぼり用の布。上辺が y=0 にあり、下へ height ぶん垂れる。法線は ±Z の両面。
        /// SwayAnimator が頂点を波打たせるので segX/segY で分割しておく。
        /// </summary>
        public static Mesh ClothStrip(float width, float height, int segX, int segY)
            => Cached(K("Cloth", width, height, segX, segY), () => BuildCloth(width, height, segX, segY));

        /// <summary>頂点アニメ用に、キャッシュを共有しない新しい布メッシュを作る。</summary>
        public static Mesh ClothStripUnique(float width, float height, int segX, int segY)
        {
            var m = BuildCloth(width, height, segX, segY);
            m.MarkDynamic();
            return m;
        }

        static Mesh BuildCloth(float width, float height, int segX, int segY)
        {
            segX = Mathf.Max(1, segX);
            segY = Mathf.Max(1, segY);
            var b = new MeshBuilder();
            Vector3 P(int i, int j) => new Vector3(-width * 0.5f + width * (i / (float)segX), -height * (j / (float)segY), 0f);
            Vector2 U(int i, int j) => new Vector2(i / (float)segX, 1f - j / (float)segY);

            for (int j = 0; j < segY; j++)
                for (int i = 0; i < segX; i++)
                {
                    Vector3 p0 = P(i, j), p1 = P(i + 1, j), p2 = P(i + 1, j + 1), p3 = P(i, j + 1);
                    b.AddQuad(p0, p1, p2, p3, Vector3.forward, U(i, j), U(i + 1, j), U(i + 1, j + 1), U(i, j + 1));
                    b.AddQuad(p0, p1, p2, p3, Vector3.back, U(i, j), U(i + 1, j), U(i + 1, j + 1), U(i, j + 1));
                }
            return b.ToMesh("ClothStrip");
        }

        // ------------------------------------------------------------------ 合成

        /// <summary>複数メッシュを1つにまとめる。屋台の柱まとめなどドローコール削減用。</summary>
        public static Mesh Combine(string name, IList<CombineInstance> parts)
        {
            var arr = new CombineInstance[parts.Count];
            for (int i = 0; i < parts.Count; i++) arr[i] = parts[i];
            var m = new Mesh { name = name };
            if (arr.Length > 0)
            {
                m.CombineMeshes(arr, true, true);
                m.RecalculateBounds();
                m.RecalculateTangents();
            }
            return m;
        }

        /// <summary>
        /// Combine の結果をキーでキャッシュする版。屋台の小物のように
        /// 「同じ組み合わせを何軒ぶんも作る」場面で、メッシュを1つだけにする。
        /// </summary>
        public static Mesh CombineCached(string key, System.Func<IList<CombineInstance>> build)
            => Cached(key, () => Combine(key, build()));

        /// <summary>MeshBuilder に自由に書き込んでメッシュを作り、キーでキャッシュする。</summary>
        internal static Mesh Build(string key, System.Action<MeshBuilder> write)
            => Cached(key, () =>
            {
                var b = new MeshBuilder();
                write(b);
                return b.ToMesh(key);
            });

        // ------------------------------------------------------------------ 板張り

        /// <summary>
        /// 板張りの壁。芯の板に、目地（隙間）を空けた横板を表裏へ張る。
        /// 溝が実際にへこんでいるので、斜めから見ても影が出て一枚板に見えない。
        /// width が X、height が Y、thickness が Z。原点が中心。
        /// </summary>
        public static Mesh SlatPanel(float width, float height, float thickness, int slats)
            => Cached(K("Slat", width, height, thickness, slats), () =>
            {
                slats = Mathf.Clamp(slats, 2, 12);
                width = Mathf.Max(0.05f, width);
                height = Mathf.Max(0.05f, height);
                thickness = Mathf.Max(0.02f, thickness);

                var b = new MeshBuilder();
                float core = thickness * 0.46f;
                AppendBox(b, Vector3.zero, new Vector3(width, height, core), Quaternion.identity);

                float skin = (thickness - core) * 0.5f;
                float gap = Mathf.Min(0.016f, height / (slats * 7f));
                float boardH = (height - gap * (slats - 1)) / slats;
                for (int i = 0; i < slats; i++)
                {
                    float y = -height * 0.5f + boardH * 0.5f + i * (boardH + gap);
                    // 板ごとに厚みを少し変える。並びが機械的に揃わないようにするため
                    float t = skin * (0.78f + 0.22f * ((i % 3) * 0.5f));
                    var size = new Vector3(width, boardH, t);
                    AppendBox(b, new Vector3(0f, y, (core + t) * 0.5f), size, Quaternion.identity);
                    AppendBox(b, new Vector3(0f, y, -(core + t) * 0.5f), size, Quaternion.identity);
                }
                return b.ToMesh("SlatPanel");
            });

        // ------------------------------------------------------------------ 電線

        /// <summary>
        /// 両端で吊られて中央が垂れる電線。X 方向に span、中央が sag ぶん下がる。
        /// 電球をぶら下げる位置は <see cref="CordSagY"/> で拾える。
        /// </summary>
        public static Mesh SaggingCord(float span, float sag, float radius, int seg = 10)
            => Cached(K("Cord", span, sag, radius, seg), () =>
            {
                seg = Mathf.Clamp(seg, 2, 24);
                var b = new MeshBuilder();
                for (int i = 0; i < seg; i++)
                {
                    float t0 = i / (float)seg, t1 = (i + 1) / (float)seg;
                    var p0 = new Vector3(-span * 0.5f + span * t0, CordSagY(t0, sag), 0f);
                    var p1 = new Vector3(-span * 0.5f + span * t1, CordSagY(t1, sag), 0f);
                    Vector3 dir = p1 - p0;
                    float len = dir.magnitude;
                    if (len < 1e-5f) continue;
                    AppendBox(b, (p0 + p1) * 0.5f, new Vector3(radius * 2f, radius * 2f, len * 1.06f),
                        Quaternion.LookRotation(dir / len, Vector3.up));
                }
                return b.ToMesh("SaggingCord");
            });

        /// <summary>電線の垂れ具合。t=0,1 が両端（下がらない）、t=0.5 が最下点。</summary>
        public static float CordSagY(float t, float sag) => -sag * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

        // ------------------------------------------------------------------ 木箱・ケース

        /// <summary>
        /// 木箱。四隅の柱と、隙間を空けた横板でできた「りんご箱」。
        /// 原点は底面の中心。
        /// </summary>
        public static Mesh WoodCrate(float width, float height, float depth)
            => Cached(K("Crate", width, height, depth), () =>
            {
                var b = new MeshBuilder();
                float hw = width * 0.5f, hd = depth * 0.5f;
                float post = Mathf.Min(0.05f, Mathf.Min(width, depth) * 0.16f);

                // 四隅の柱
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        AppendBox(b, new Vector3(sx * (hw - post * 0.5f), height * 0.5f, sz * (hd - post * 0.5f)),
                            new Vector3(post, height, post), Quaternion.identity);

                // 側板。3段ぶんを隙間を空けて張る
                const int rows = 3;
                float boardH = height / (rows + 1.4f);
                float thick = post * 0.55f;
                for (int i = 0; i < rows; i++)
                {
                    float y = height * (0.16f + 0.34f * i);
                    AppendBox(b, new Vector3(0f, y, hd - thick * 0.5f), new Vector3(width, boardH, thick), Quaternion.identity);
                    AppendBox(b, new Vector3(0f, y, -hd + thick * 0.5f), new Vector3(width, boardH, thick), Quaternion.identity);
                    AppendBox(b, new Vector3(hw - thick * 0.5f, y, 0f), new Vector3(thick, boardH, depth), Quaternion.identity);
                    AppendBox(b, new Vector3(-hw + thick * 0.5f, y, 0f), new Vector3(thick, boardH, depth), Quaternion.identity);
                }
                // 底板
                AppendBox(b, new Vector3(0f, thick * 0.5f, 0f), new Vector3(width, thick, depth), Quaternion.identity);
                return b.ToMesh("WoodCrate");
            });

        /// <summary>
        /// ビールケース。上が開いた樹脂の箱で、側面に格子の抜きがある。原点は底面の中心。
        /// </summary>
        public static Mesh BeerCase(float width, float height, float depth)
            => Cached(K("BeerCase", width, height, depth), () =>
            {
                var b = new MeshBuilder();
                float hw = width * 0.5f, hd = depth * 0.5f;
                float wall = Mathf.Min(0.028f, Mathf.Min(width, depth) * 0.09f);

                // 底と、上下の縁
                AppendBox(b, new Vector3(0f, wall * 0.5f, 0f), new Vector3(width, wall, depth), Quaternion.identity);
                AppendBox(b, new Vector3(0f, height - wall * 0.5f, 0f), new Vector3(width, wall, depth * 0.06f + wall), Quaternion.identity);
                AppendBox(b, new Vector3(0f, height - wall * 0.5f, hd - wall * 0.5f), new Vector3(width, wall * 1.6f, wall), Quaternion.identity);
                AppendBox(b, new Vector3(0f, height - wall * 0.5f, -hd + wall * 0.5f), new Vector3(width, wall * 1.6f, wall), Quaternion.identity);
                AppendBox(b, new Vector3(hw - wall * 0.5f, height - wall * 0.5f, 0f), new Vector3(wall, wall * 1.6f, depth), Quaternion.identity);
                AppendBox(b, new Vector3(-hw + wall * 0.5f, height - wall * 0.5f, 0f), new Vector3(wall, wall * 1.6f, depth), Quaternion.identity);

                // 側面の縦桟。ここが格子に見える
                int barsX = Mathf.Clamp(Mathf.RoundToInt(width / 0.09f), 3, 10);
                int barsZ = Mathf.Clamp(Mathf.RoundToInt(depth / 0.09f), 3, 10);
                for (int i = 0; i <= barsX; i++)
                {
                    float x = Mathf.Lerp(-hw + wall, hw - wall, i / (float)barsX);
                    AppendBox(b, new Vector3(x, height * 0.5f, hd - wall * 0.5f), new Vector3(wall, height, wall), Quaternion.identity);
                    AppendBox(b, new Vector3(x, height * 0.5f, -hd + wall * 0.5f), new Vector3(wall, height, wall), Quaternion.identity);
                }
                for (int i = 0; i <= barsZ; i++)
                {
                    float z = Mathf.Lerp(-hd + wall, hd - wall, i / (float)barsZ);
                    AppendBox(b, new Vector3(hw - wall * 0.5f, height * 0.5f, z), new Vector3(wall, height, wall), Quaternion.identity);
                    AppendBox(b, new Vector3(-hw + wall * 0.5f, height * 0.5f, z), new Vector3(wall, height, wall), Quaternion.identity);
                }
                return b.ToMesh("BeerCase");
            });

        /// <summary>
        /// クーラーボックス。角の落ちた本体と、少しはみ出す蓋、両脇の取っ手。原点は底面の中心。
        /// </summary>
        public static Mesh CoolerBox(float width, float height, float depth)
            => Cached(K("Cooler", width, height, depth), () =>
            {
                var b = new MeshBuilder();
                float bodyH = height * 0.76f;
                float lidH = height - bodyH;
                // 本体は上が少し広い（抜き勾配）
                var body = BuildTaperedBox(width * 0.94f, depth * 0.94f, width, depth, bodyH);
                AppendMeshInto(b, body, Quaternion.identity, new Vector3(0f, bodyH * 0.5f, 0f));
                SafeDestroy(body);
                // 蓋
                AppendBox(b, new Vector3(0f, bodyH + lidH * 0.5f, 0f),
                    new Vector3(width * 1.04f, lidH, depth * 1.04f), Quaternion.identity);
                // 取っ手
                float hy = bodyH * 0.62f;
                AppendBox(b, new Vector3(width * 0.53f, hy, 0f), new Vector3(width * 0.06f, height * 0.10f, depth * 0.5f), Quaternion.identity);
                AppendBox(b, new Vector3(-width * 0.53f, hy, 0f), new Vector3(width * 0.06f, height * 0.10f, depth * 0.5f), Quaternion.identity);
                return b.ToMesh("CoolerBox");
            });

        /// <summary>できあがったメッシュを、回転と移動を付けて MeshBuilder に取り込む。</summary>
        static void AppendMeshInto(MeshBuilder b, Mesh src, Quaternion rot, Vector3 offset)
        {
            if (src == null) return;
            var verts = src.vertices;
            var norms = src.normals;
            var uvs = src.uv;
            var tris = src.triangles;
            bool hasUv = uvs != null && uvs.Length == verts.Length;
            bool hasNrm = norms != null && norms.Length == verts.Length;
            for (int i = 0; i < tris.Length; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int vi = tris[i + k];
                    Vector3 p = rot * verts[vi] + offset;
                    Vector3 n = hasNrm ? rot * norms[vi] : Vector3.up;
                    b.Triangles.Add(b.Add(p, n, hasUv ? uvs[vi] : Vector2.zero));
                }
            }
        }
    }
}
