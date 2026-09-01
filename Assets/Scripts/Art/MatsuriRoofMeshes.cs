using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// 屋根まわりのメッシュ (§23 / §79)。
    ///
    /// 「板を斜めに置いただけ」に見せないために、屋根は次の4つの部品に分けて作る。
    ///   1. 屋根面（Surface）  … 厚みを持ち、軒に向かって<b>反る</b>曲面。真横から見ても板1枚にならない
    ///   2. 骨組み（Frame）    … 棟木・破風板・鼻隠し。稜線と妻側の輪郭を立たせる
    ///   3. 垂木（Rafters）    … 軒下から見える細い角材と母屋。屋根裏の情報量を作る
    ///   4. 縞（Stripes）      … 紅白の帯。屋根面と同じ曲面を法線方向へ浮かせて重ねる
    ///
    /// いずれも <see cref="MatsuriMeshes"/> のキャッシュを通るので、同じ寸法なら1つを共有する。
    /// </summary>
    public static partial class MatsuriMeshes
    {
        /// <summary>曲面をパラメータ (u, v) で表す関数。u,v とも 0〜1。</summary>
        internal delegate Vector3 SurfaceFunc(float u, float v);

        /// <summary>屋根面の板厚。</summary>
        const float RoofThickness = 0.075f;

        // ================================================================== 曲面の共通処理

        /// <summary>
        /// パラメータ曲面から「厚みのある殻」を作る。表面・裏面・指定した縁の帯を張る。
        /// 法線は必ず上向き（+Y 側）に揃える。屋根・テント幕はすべて上を向いているのでこれでよい。
        /// </summary>
        internal static void AppendShell(MeshBuilder b, SurfaceFunc f, int segU, int segV, float thickness,
            bool bandU0, bool bandU1, bool bandV0, bool bandV1, float uvU, float uvV)
        {
            segU = Mathf.Max(1, segU);
            segV = Mathf.Max(1, segV);
            int nu = segU + 1, nv = segV + 1;
            var top = new Vector3[nu * nv];
            var nrm = new Vector3[nu * nv];
            var bot = new Vector3[nu * nv];

            const float e = 0.004f;
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    float u = i / (float)segU, v = j / (float)segV;
                    Vector3 p = f(u, v);
                    Vector3 du = f(Mathf.Min(1f, u + e), v) - f(Mathf.Max(0f, u - e), v);
                    Vector3 dv = f(u, Mathf.Min(1f, v + e)) - f(u, Mathf.Max(0f, v - e));
                    Vector3 n = Vector3.Cross(dv, du);
                    if (n.sqrMagnitude < 1e-10f) n = Vector3.up;
                    n.Normalize();
                    if (n.y < 0f) n = -n;
                    int k = i * nv + j;
                    top[k] = p;
                    nrm[k] = n;
                    bot[k] = p - n * thickness;
                }

            Vector2 UV(int i, int j) => new Vector2(i / (float)segU * uvU, j / (float)segV * uvV);

            // 表面
            for (int i = 0; i < segU; i++)
                for (int j = 0; j < segV; j++)
                {
                    int a = i * nv + j, c = (i + 1) * nv + j, d = (i + 1) * nv + j + 1, e2 = i * nv + j + 1;
                    b.AddQuadSmooth(
                        top[a], nrm[a], UV(i, j),
                        top[c], nrm[c], UV(i + 1, j),
                        top[d], nrm[d], UV(i + 1, j + 1),
                        top[e2], nrm[e2], UV(i, j + 1));
                }

            if (thickness > 1e-4f)
            {
                // 裏面（軒下から見える面）
                for (int i = 0; i < segU; i++)
                    for (int j = 0; j < segV; j++)
                    {
                        int a = i * nv + j, c = (i + 1) * nv + j, d = (i + 1) * nv + j + 1, e2 = i * nv + j + 1;
                        b.AddQuadSmooth(
                            bot[a], -nrm[a], UV(i, j),
                            bot[c], -nrm[c], UV(i + 1, j),
                            bot[d], -nrm[d], UV(i + 1, j + 1),
                            bot[e2], -nrm[e2], UV(i, j + 1));
                    }

                // 縁の帯（ここが無いと真横から見たときに紙のように見える）
                if (bandV1) AppendBandAlongU(b, top, bot, nv, segU, segV, true, f, uvU);
                if (bandV0) AppendBandAlongU(b, top, bot, nv, segU, 0, false, f, uvU);
                if (bandU1) AppendBandAlongV(b, top, bot, nv, segU, segV, true, f, uvV);
                if (bandU0) AppendBandAlongV(b, top, bot, nv, 0, segV, false, f, uvV);
            }
        }

        /// <summary>v が端（軒先または棟）の縁を、X 方向に沿って閉じる。</summary>
        static void AppendBandAlongU(MeshBuilder b, Vector3[] top, Vector3[] bot, int nv,
            int segU, int j, bool outwardPlus, SurfaceFunc f, float uvU)
        {
            for (int i = 0; i < segU; i++)
            {
                int a = i * nv + j, c = (i + 1) * nv + j;
                float u = (i + 0.5f) / segU;
                float v = j / (float)(nv - 1);
                Vector3 dir = f(u, Mathf.Min(1f, v + 0.01f)) - f(u, Mathf.Max(0f, v - 0.01f));
                Vector3 n = (outwardPlus ? dir : -dir);
                n.y *= 0.25f;                       // 帯は横を向かせたい。上下成分は弱める
                if (n.sqrMagnitude < 1e-10f) n = Vector3.forward;
                n.Normalize();
                b.AddQuad(top[a], top[c], bot[c], bot[a], n,
                    new Vector2(i / (float)segU * uvU, 0f), new Vector2((i + 1) / (float)segU * uvU, 0f),
                    new Vector2((i + 1) / (float)segU * uvU, 1f), new Vector2(i / (float)segU * uvU, 1f));
            }
        }

        /// <summary>u が端（妻側）の縁を、勾配に沿って閉じる。</summary>
        static void AppendBandAlongV(MeshBuilder b, Vector3[] top, Vector3[] bot, int nv,
            int i, int segV, bool outwardPlus, SurfaceFunc f, float uvV)
        {
            for (int j = 0; j < segV; j++)
            {
                int a = i * nv + j, c = i * nv + j + 1;
                float u = i / (float)Mathf.Max(1, (top.Length / nv) - 1);
                float v = (j + 0.5f) / segV;
                Vector3 dir = f(Mathf.Min(1f, u + 0.01f), v) - f(Mathf.Max(0f, u - 0.01f), v);
                Vector3 n = (outwardPlus ? dir : -dir);
                n.y *= 0.25f;
                if (n.sqrMagnitude < 1e-10f) n = Vector3.right;
                n.Normalize();
                b.AddQuad(top[a], top[c], bot[c], bot[a], n,
                    new Vector2(j / (float)segV * uvV, 0f), new Vector2((j + 1) / (float)segV * uvV, 0f),
                    new Vector2((j + 1) / (float)segV * uvV, 1f), new Vector2(j / (float)segV * uvV, 1f));
            }
        }

        /// <summary>曲面に沿って細い角材を1本流す。垂木・鼻隠し・破風板の共通処理。</summary>
        internal static void AppendCurvedBar(MeshBuilder b, SurfaceFunc f, bool alongV, float fixedParam,
            int seg, float offset, float barWidth, float barHeight)
        {
            seg = Mathf.Max(1, seg);
            Vector3 At(float t, out Vector3 n)
            {
                float u = alongV ? fixedParam : t;
                float v = alongV ? t : fixedParam;
                const float e = 0.004f;
                Vector3 p = f(u, v);
                Vector3 du = f(Mathf.Min(1f, u + e), v) - f(Mathf.Max(0f, u - e), v);
                Vector3 dv = f(u, Mathf.Min(1f, v + e)) - f(u, Mathf.Max(0f, v - e));
                n = Vector3.Cross(dv, du);
                if (n.sqrMagnitude < 1e-10f) n = Vector3.up;
                n.Normalize();
                if (n.y < 0f) n = -n;
                return p;
            }

            for (int k = 0; k < seg; k++)
            {
                Vector3 n0, n1;
                Vector3 p0 = At(k / (float)seg, out n0) - n0 * offset;
                Vector3 p1 = At((k + 1) / (float)seg, out n1) - n1 * offset;
                Vector3 dir = p1 - p0;
                float len = dir.magnitude;
                if (len < 1e-5f) continue;
                Vector3 up = (n0 + n1).normalized;
                var rot = Quaternion.LookRotation(dir / len, up);
                AppendBox(b, (p0 + p1) * 0.5f, new Vector3(barWidth, barHeight, len * 1.02f), rot);
            }
        }

        // ================================================================== 断面（反り）

        /// <summary>
        /// 屋根の勾配断面。t=0 が棟、t=1 が軒先。
        /// 直線から少し垂らして、軒先で跳ね上げる＝日本建築の「反り」。
        /// </summary>
        static float RoofProfileY(float t, float height, float sori)
            => height * (1f - t)
             - sori * 0.50f * Mathf.Sin(Mathf.PI * t)
             + sori * 0.55f * Mathf.Pow(t, 3.5f);

        static float Sori(float height) => Mathf.Max(0.02f, height * 0.34f);

        /// <summary>テント幕の軒先の高さ。幕板の垂れを吊る位置になる。</summary>
        public static float AwningEaveY(float rise)
        {
            rise = Mathf.Max(0.05f, rise);
            return RoofProfileY(1f, rise, Sori(rise) * 0.7f);
        }

        /// <summary>妻側の隅ほど軒先が持ち上がる量。</summary>
        static float CornerRise(float height) => Mathf.Max(0.01f, height * 0.12f);

        // ================================================================== 切妻屋根

        /// <summary>
        /// 片側の勾配面。zSign=+1 で手前(+Z)へ、-1 で奥へ降りる。
        /// u: 0→1 が X の -halfW→+halfW。v: 0 が棟、1 が軒先。
        /// </summary>
        static SurfaceFunc GableSurface(float halfW, float halfD, float height, float zSign)
        {
            float sori = Sori(height);
            float corner = CornerRise(height);
            return (u, v) =>
            {
                float x = Mathf.Lerp(-halfW, halfW, u);
                float edge = Mathf.Abs(u * 2f - 1f);
                float y = RoofProfileY(v, height, sori) + corner * Mathf.Pow(edge, 2.5f) * v * v;
                return new Vector3(x, y, zSign * halfD * v);
            };
        }

        /// <summary>
        /// 切妻屋根の屋根面。棟が X 方向に走り、軒が Z の正負へ降りる。
        /// 軒の出 overhang ぶん四方に張り出す。原点は軒先の高さ、棟が y=height。
        /// 板厚があり、軒に向かって反る。
        /// </summary>
        public static Mesh GableRoof(float width, float depth, float height, float overhang)
            => Cached(K("GableSurf", width, depth, height, overhang), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                height = Mathf.Max(0.05f, height);
                var b = new MeshBuilder();
                float slope = Mathf.Sqrt(halfD * halfD + height * height);
                for (int s = 0; s < 2; s++)
                {
                    var f = GableSurface(halfW, halfD, height, s == 0 ? 1f : -1f);
                    AppendShell(b, f, 8, 8, RoofThickness, true, true, false, true, halfW * 2f, slope);
                }
                // 棟の真下（2つの勾配の裏面の間）を塞ぐ
                AppendRidgeUnderside(b, halfW, height, RoofThickness);
                return b.ToMesh("GableRoof");
            });

        static void AppendRidgeUnderside(MeshBuilder b, float halfW, float height, float thickness)
        {
            float y = height - thickness;
            float z = thickness * 0.9f;
            b.AddQuad(
                new Vector3(-halfW, y, -z), new Vector3(halfW, y, -z),
                new Vector3(halfW, y, z), new Vector3(-halfW, y, z),
                Vector3.down,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
        }

        /// <summary>
        /// 切妻屋根の骨組み。棟木（丸太）・破風板（妻側の板）・鼻隠し（軒先の板）。
        /// 屋根面とは別マテリアル（濃い木）で使う想定。
        /// </summary>
        public static Mesh GableRoofFrame(float width, float depth, float height, float overhang)
            => Cached(K("GableFrame", width, depth, height, overhang), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                height = Mathf.Max(0.05f, height);
                var b = new MeshBuilder();

                float logR = Mathf.Clamp(height * 0.14f, 0.055f, 0.11f);
                // 棟木：屋根のてっぺんを走る丸太
                AppendLatheInto(b, new[]
                {
                    new Vector2(logR, -halfW - 0.10f),
                    new Vector2(logR, halfW + 0.10f)
                }, 10, Quaternion.Euler(0f, 0f, -90f), new Vector3(0f, height + logR * 0.55f, 0f));
                // 棟包み：丸太の下の板。棟の線を太くして稜線を強調する
                AppendBox(b, new Vector3(0f, height + logR * 0.05f, 0f),
                    new Vector3(halfW * 2f + 0.16f, logR * 0.55f, logR * 2.4f), Quaternion.identity);

                float boardDrop = Mathf.Clamp(height * 0.42f, 0.16f, 0.30f);
                const float boardThick = 0.055f;
                for (int s = 0; s < 2; s++)
                {
                    float zSign = s == 0 ? 1f : -1f;
                    var f = GableSurface(halfW, halfD, height, zSign);
                    // 鼻隠し：軒先を横に走る板
                    AppendCurvedBar(b, f, false, 1f, 8, RoofThickness * 0.5f + boardDrop * 0.42f,
                        boardThick, boardDrop);
                    // 破風板：妻側の輪郭に沿って降りる板
                    AppendCurvedBar(b, f, true, 0f, 8, RoofThickness * 0.5f + boardDrop * 0.34f,
                        boardDrop * 0.86f, boardThick);
                    AppendCurvedBar(b, f, true, 1f, 8, RoofThickness * 0.5f + boardDrop * 0.34f,
                        boardDrop * 0.86f, boardThick);
                }
                return b.ToMesh("GableRoofFrame");
            });

        /// <summary>
        /// 切妻屋根の垂木と母屋。軒下を見上げたときに見える細い角材。
        /// 遠景では LOD で落とす前提の細部。
        /// </summary>
        public static Mesh GableRafters(float width, float depth, float height, float overhang)
            => Cached(K("GableRaft", width, depth, height, overhang), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                height = Mathf.Max(0.05f, height);
                var b = new MeshBuilder();

                int count = Mathf.Clamp(Mathf.RoundToInt(halfW * 2f / 0.34f), 4, 14);
                for (int s = 0; s < 2; s++)
                {
                    var f = GableSurface(halfW, halfD, height, s == 0 ? 1f : -1f);
                    for (int i = 0; i <= count; i++)
                    {
                        float u = i / (float)count;
                        AppendCurvedBar(b, f, true, u, 5, RoofThickness + 0.030f, 0.040f, 0.062f);
                    }
                    // 母屋：垂木と直交する2本
                    AppendCurvedBar(b, f, false, 0.42f, 6, RoofThickness + 0.095f, 0.055f, 0.055f);
                    AppendCurvedBar(b, f, false, 0.86f, 6, RoofThickness + 0.095f, 0.055f, 0.055f);
                }
                return b.ToMesh("GableRafters");
            });

        /// <summary>
        /// 切妻屋根に重ねる紅白の縞。屋根面と同じ曲面を法線方向へ少し浮かせるので、
        /// Z ファイティングを起こさずに屋根の反りへぴったり乗る。
        /// </summary>
        public static Mesh GableRoofStripes(float width, float depth, float height, float overhang, float stripeWidth)
            => Cached(K("GableStripe", width, depth, height, overhang, stripeWidth), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                height = Mathf.Max(0.05f, height);
                var b = new MeshBuilder();
                float slope = Mathf.Sqrt(halfD * halfD + height * height);
                int bands = Mathf.Clamp(Mathf.RoundToInt(halfW * 2f / Mathf.Max(0.12f, stripeWidth)), 3, 15);
                if (bands % 2 == 0) bands++;
                for (int s = 0; s < 2; s++)
                {
                    var f = GableSurface(halfW, halfD, height, s == 0 ? 1f : -1f);
                    AppendStripeBands(b, f, bands, slope);
                }
                return b.ToMesh("GableRoofStripes");
            });

        /// <summary>曲面の u 方向を bands 本に割って、1本おきに薄い帯を浮かせる。</summary>
        static void AppendStripeBands(MeshBuilder b, SurfaceFunc f, int bands, float uvV)
        {
            const float lift = 0.022f;
            for (int k = 0; k < bands; k += 2)
            {
                float u0 = k / (float)bands, u1 = (k + 1) / (float)bands;
                float pad = (u1 - u0) * 0.06f;
                float a = u0 + pad, c = u1 - pad;
                SurfaceFunc band = (u, v) =>
                {
                    float uu = Mathf.Lerp(a, c, u);
                    const float e = 0.004f;
                    Vector3 p = f(uu, v);
                    Vector3 du = f(Mathf.Min(1f, uu + e), v) - f(Mathf.Max(0f, uu - e), v);
                    Vector3 dv = f(uu, Mathf.Min(1f, v + e)) - f(uu, Mathf.Max(0f, v - e));
                    Vector3 n = Vector3.Cross(dv, du);
                    if (n.sqrMagnitude < 1e-10f) n = Vector3.up;
                    n.Normalize();
                    if (n.y < 0f) n = -n;
                    return p + n * lift;
                };
                AppendShell(b, band, 2, 8, 0.012f, true, true, true, true, 1f, uvV);
            }
        }

        // ================================================================== 片流れ屋根

        static SurfaceFunc ShedSurface(float halfW, float halfD, float rise)
        {
            float sori = Sori(rise);
            float corner = CornerRise(rise);
            return (u, v) =>
            {
                float x = Mathf.Lerp(-halfW, halfW, u);
                float edge = Mathf.Abs(u * 2f - 1f);
                float y = RoofProfileY(v, rise, sori) + corner * Mathf.Pow(edge, 2.5f) * v * v;
                return new Vector3(x, y, Mathf.Lerp(-halfD, halfD, v));
            };
        }

        /// <summary>片流れ屋根。奥(-Z)が高く手前(+Z)が低い。厚みと軒の反りを持つ。</summary>
        public static Mesh ShedRoof(float width, float depth, float rise, float overhang)
            => Cached(K("ShedSurf", width, depth, rise, overhang), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                rise = Mathf.Max(0.05f, rise);
                var b = new MeshBuilder();
                float slope = Mathf.Sqrt(halfD * halfD * 4f + rise * rise);
                AppendShell(b, ShedSurface(halfW, halfD, rise), 8, 8, RoofThickness,
                    true, true, true, true, halfW * 2f, slope);
                return b.ToMesh("ShedRoof");
            });

        /// <summary>片流れ屋根の骨組み（棟側の押さえ・鼻隠し・妻の板）。</summary>
        public static Mesh ShedRoofFrame(float width, float depth, float rise, float overhang)
            => Cached(K("ShedFrame", width, depth, rise, overhang), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                rise = Mathf.Max(0.05f, rise);
                var b = new MeshBuilder();
                var f = ShedSurface(halfW, halfD, rise);
                float boardDrop = Mathf.Clamp(rise * 0.40f, 0.15f, 0.28f);
                AppendCurvedBar(b, f, false, 1f, 8, RoofThickness * 0.5f + boardDrop * 0.42f, 0.055f, boardDrop);
                AppendCurvedBar(b, f, false, 0f, 4, RoofThickness * 0.5f + 0.05f, 0.07f, 0.10f);
                AppendCurvedBar(b, f, true, 0f, 8, RoofThickness * 0.5f + boardDrop * 0.30f, boardDrop * 0.80f, 0.055f);
                AppendCurvedBar(b, f, true, 1f, 8, RoofThickness * 0.5f + boardDrop * 0.30f, boardDrop * 0.80f, 0.055f);
                return b.ToMesh("ShedRoofFrame");
            });

        /// <summary>片流れ屋根の垂木と母屋。</summary>
        public static Mesh ShedRafters(float width, float depth, float rise, float overhang)
            => Cached(K("ShedRaft", width, depth, rise, overhang), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                rise = Mathf.Max(0.05f, rise);
                var b = new MeshBuilder();
                var f = ShedSurface(halfW, halfD, rise);
                int count = Mathf.Clamp(Mathf.RoundToInt(halfW * 2f / 0.34f), 4, 14);
                for (int i = 0; i <= count; i++)
                    AppendCurvedBar(b, f, true, i / (float)count, 5, RoofThickness + 0.030f, 0.040f, 0.062f);
                AppendCurvedBar(b, f, false, 0.30f, 6, RoofThickness + 0.095f, 0.055f, 0.055f);
                AppendCurvedBar(b, f, false, 0.80f, 6, RoofThickness + 0.095f, 0.055f, 0.055f);
                return b.ToMesh("ShedRafters");
            });

        /// <summary>片流れ屋根に重ねる紅白の縞。</summary>
        public static Mesh ShedRoofStripes(float width, float depth, float rise, float overhang, float stripeWidth)
            => Cached(K("ShedStripe", width, depth, rise, overhang, stripeWidth), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                rise = Mathf.Max(0.05f, rise);
                var b = new MeshBuilder();
                int bands = Mathf.Clamp(Mathf.RoundToInt(halfW * 2f / Mathf.Max(0.12f, stripeWidth)), 3, 15);
                if (bands % 2 == 0) bands++;
                AppendStripeBands(b, ShedSurface(halfW, halfD, rise), bands, halfD * 2f);
                return b.ToMesh("ShedRoofStripes");
            });

        // ================================================================== テント（幕）屋根

        /// <summary>
        /// テント幕の面。骨（パイプ）の間で幕が垂れるので、X 方向に波打つ。
        /// bays は骨の間の数。
        /// </summary>
        static SurfaceFunc AwningSurface(float halfW, float halfD, float rise, int bays)
        {
            float sori = Sori(rise) * 0.7f;
            float sag = Mathf.Clamp(halfW * 2f / Mathf.Max(1, bays) * 0.16f, 0.02f, 0.09f);
            return (u, v) =>
            {
                float x = Mathf.Lerp(-halfW, halfW, u);
                float y = RoofProfileY(v, rise, sori);
                // 骨と骨の間で幕が垂れる。両端（骨の位置）では垂れない
                float bay = Mathf.Repeat(u * bays, 1f);
                y -= sag * Mathf.Sin(bay * Mathf.PI) * Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI);
                return new Vector3(x, y, Mathf.Lerp(-halfD, halfD, v));
            };
        }

        /// <summary>テント幕の屋根。波打ちのぶんだけ分割を細かくする。</summary>
        public static Mesh AwningRoof(float width, float depth, float rise, float overhang, int bays)
            => Cached(K("Awning", width, depth, rise, overhang, bays), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                rise = Mathf.Max(0.05f, rise);
                bays = Mathf.Clamp(bays, 2, 8);
                var b = new MeshBuilder();
                AppendShell(b, AwningSurface(halfW, halfD, rise, bays), bays * 4, 8, 0.030f,
                    true, true, true, true, halfW * 2f, halfD * 2f);
                return b.ToMesh("AwningRoof");
            });

        /// <summary>テント幕に重ねる縞。</summary>
        public static Mesh AwningStripes(float width, float depth, float rise, float overhang, int bays, float stripeWidth)
            => Cached(K("AwnStripe", width, depth, rise, overhang, bays, stripeWidth), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                rise = Mathf.Max(0.05f, rise);
                bays = Mathf.Clamp(bays, 2, 8);
                var b = new MeshBuilder();
                int stripes = Mathf.Clamp(Mathf.RoundToInt(halfW * 2f / Mathf.Max(0.12f, stripeWidth)), 3, 15);
                if (stripes % 2 == 0) stripes++;
                AppendStripeBands(b, AwningSurface(halfW, halfD, rise, bays), stripes, halfD * 2f);
                return b.ToMesh("AwningStripes");
            });

        /// <summary>
        /// テントの骨組み。幕の下を横に走るパイプと、妻側のアーチ。
        /// </summary>
        public static Mesh AwningFrame(float width, float depth, float rise, float overhang, int bays)
            => Cached(K("AwnFrame", width, depth, rise, overhang, bays), () =>
            {
                float halfW = width * 0.5f + overhang;
                float halfD = depth * 0.5f + overhang;
                rise = Mathf.Max(0.05f, rise);
                bays = Mathf.Clamp(bays, 2, 8);
                var b = new MeshBuilder();
                var f = AwningSurface(halfW, halfD, rise, bays);
                for (int i = 0; i <= bays; i++)
                    AppendCurvedBar(b, f, true, i / (float)bays, 6, 0.048f, 0.038f, 0.038f);
                AppendCurvedBar(b, f, false, 0.04f, 6, 0.052f, 0.044f, 0.044f);
                AppendCurvedBar(b, f, false, 0.96f, 6, 0.052f, 0.044f, 0.044f);
                return b.ToMesh("AwningFrame");
            });

        /// <summary>
        /// 幕板の垂れ。テントの軒先から下がる帯で、裾が波形（スカラップ）に切ってある。
        /// 上辺が y=0、下へ drop ぶん垂れる。法線は ±Z の両面。
        /// </summary>
        public static Mesh AwningValance(float width, float drop, int scallops)
            => Cached(K("Valance", width, drop, scallops), () =>
            {
                scallops = Mathf.Clamp(scallops, 2, 24);
                drop = Mathf.Max(0.06f, drop);
                var b = new MeshBuilder();
                int segX = scallops * 4;
                const int segY = 3;

                float BottomY(float u)
                {
                    float s = Mathf.Repeat(u * scallops, 1f);
                    // 半円の裾。両端が浅く中央が深い
                    return -drop * (0.55f + 0.45f * Mathf.Sin(s * Mathf.PI));
                }

                for (int i = 0; i < segX; i++)
                    for (int j = 0; j < segY; j++)
                    {
                        float u0 = i / (float)segX, u1 = (i + 1) / (float)segX;
                        float t0 = j / (float)segY, t1 = (j + 1) / (float)segY;
                        float x0 = Mathf.Lerp(-width * 0.5f, width * 0.5f, u0);
                        float x1 = Mathf.Lerp(-width * 0.5f, width * 0.5f, u1);
                        float y00 = Mathf.Lerp(0f, BottomY(u0), t0), y01 = Mathf.Lerp(0f, BottomY(u0), t1);
                        float y10 = Mathf.Lerp(0f, BottomY(u1), t0), y11 = Mathf.Lerp(0f, BottomY(u1), t1);
                        // 布のうねり
                        float z0 = Mathf.Sin(u0 * Mathf.PI * scallops) * 0.012f * t1;
                        float z1 = Mathf.Sin(u1 * Mathf.PI * scallops) * 0.012f * t1;
                        Vector3 p0 = new Vector3(x0, y00, z0 * t0), p1 = new Vector3(x1, y10, z1 * t0);
                        Vector3 p2 = new Vector3(x1, y11, z1), p3 = new Vector3(x0, y01, z0);
                        Vector2 a = new Vector2(u0, 1f - t0), c = new Vector2(u1, 1f - t0);
                        Vector2 d = new Vector2(u1, 1f - t1), e = new Vector2(u0, 1f - t1);
                        b.AddQuad(p0, p1, p2, p3, Vector3.forward, a, c, d, e);
                        b.AddQuad(p0, p1, p2, p3, Vector3.back, a, c, d, e);
                    }
                return b.ToMesh("AwningValance");
            });

        // ================================================================== 補助

        /// <summary>Lathe の結果を既存の MeshBuilder に取り込む（丸太などを他部品と合成するため）。</summary>
        static void AppendLatheInto(MeshBuilder b, Vector2[] profile, int seg, Quaternion rot, Vector3 offset)
        {
            var tmp = Lathe(profile, seg, true, true, "tmp");
            var verts = tmp.vertices;
            var norms = tmp.normals;
            var uvs = tmp.uv;
            var tris = tmp.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                Vector3 p0 = rot * verts[i0] + offset;
                Vector3 p1 = rot * verts[i1] + offset;
                Vector3 p2 = rot * verts[i2] + offset;
                Vector3 n0 = rot * norms[i0], n1 = rot * norms[i1], n2 = rot * norms[i2];
                int a = b.Add(p0, n0, uvs[i0]);
                int c = b.Add(p1, n1, uvs[i1]);
                int d = b.Add(p2, n2, uvs[i2]);
                b.Triangles.Add(a); b.Triangles.Add(c); b.Triangles.Add(d);
            }
            SafeDestroy(tmp);
        }
    }
}
