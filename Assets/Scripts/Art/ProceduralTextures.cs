using System.Collections.Generic;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// AI生成テクスチャが入るまでのつなぎ (§69)。木目・和紙・布・法線・看板文字を C# で描く。
    /// 生成物はキーでキャッシュして共有する。
    /// </summary>
    public static partial class ProceduralTextures
    {
        static readonly Dictionary<string, Texture2D> s_Cache = new Dictionary<string, Texture2D>(64);

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

        static Texture2D Cached(string key, System.Func<Texture2D> factory)
        {
            if (s_Cache.TryGetValue(key, out var t) && t != null) return t;
            t = factory();
            t.name = key;
            s_Cache[key] = t;
            return t;
        }

        static Texture2D NewTex(int w, int h, bool linear = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, linear)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            return t;
        }

        // ------------------------------------------------------------------ ノイズ

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + seed * 1274126177;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        /// <summary>タイル境界で継ぎ目が出ないよう、周期 period で折り返す値ノイズ。</summary>
        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int W(int v) => ((v % period) + period) % period;
            float a = Hash(W(x0), W(y0), seed);
            float b = Hash(W(x0 + 1), W(y0), seed);
            float c = Hash(W(x0), W(y0 + 1), seed);
            float d = Hash(W(x0 + 1), W(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(x * period, y * period, period, seed + o * 71) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / Mathf.Max(0.0001f, norm);
        }

        // ------------------------------------------------------------------ 木目

        /// <summary>タイル境界をまたいでも切れないトーラス距離。</summary>
        static float TorusDistance(float ax, float ay, float bx, float by)
        {
            float dx = Mathf.Abs(ax - bx); if (dx > 0.5f) dx = 1f - dx;
            float dy = Mathf.Abs(ay - by); if (dy > 0.5f) dy = 1f - dy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 木目。BaseColor に乗算する前提の濃淡。
        /// 年輪（濃い線）・導管（細かい縦筋）・節（芯の周りを年輪が回り込む）の3層で作る。
        /// </summary>
        public static Texture2D Wood(int size, int seed) => Cached("wood_" + size + "_" + seed, () =>
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];

            // 節の位置と大きさ。タイルの中に2つ置く
            const int knotCount = 2;
            var knotX = new float[knotCount];
            var knotY = new float[knotCount];
            var knotR = new float[knotCount];
            for (int i = 0; i < knotCount; i++)
            {
                knotX[i] = Hash(i + 1, 7, seed + 301);
                knotY[i] = Hash(i + 1, 19, seed + 977);
                knotR[i] = Mathf.Lerp(0.055f, 0.105f, Hash(i + 1, 31, seed + 55));
            }

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;

                    // 節に近いほど年輪が押し出されて回り込む
                    float bulge = 0f;
                    float nearest = 1f;
                    for (int i = 0; i < knotCount; i++)
                    {
                        float d = TorusDistance(u, v, knotX[i], knotY[i]);
                        nearest = Mathf.Min(nearest, d / knotR[i]);
                        bulge += Mathf.Exp(-(d * d) / (knotR[i] * knotR[i] * 1.6f)) * 2.6f;
                    }

                    // 年輪：縦に伸ばした縞をノイズで歪ませ、節の周りで膨らませる
                    float warp = Fbm(u, v * 0.22f, 4, 4, seed) - 0.5f;
                    float ringCoord = u * 7.0f + warp * 2.2f + bulge;
                    float ring = Mathf.Abs(Mathf.Sin(ringCoord * Mathf.PI));
                    ring = Mathf.Pow(ring, 0.38f);                    // 濃い線を細く、はっきりと
                    float k = Mathf.Lerp(0.58f, 1.06f, ring);

                    // 導管：木の繊維に沿った細かい筋
                    float pore = Fbm(u * 2.5f, v * 26f, 32, 2, seed + 13);
                    k *= Mathf.Lerp(0.92f, 1.06f, pore);

                    // 節の芯
                    if (nearest < 1f)
                    {
                        float core = Mathf.Clamp01(1f - nearest);
                        k *= Mathf.Lerp(1f, 0.34f, Mathf.Pow(core, 2.2f));
                    }

                    k = Mathf.Clamp01(k);
                    byte g = (byte)(k * 255f);
                    byte r = (byte)(Mathf.Clamp01(k * 1.05f) * 255f);
                    byte bl = (byte)(Mathf.Clamp01(k * 0.90f) * 255f);
                    px[y * size + x] = new Color32(r, g, bl, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        });

        /// <summary>
        /// 板張り。横に並んだ板と、板と板の間の細い溝（目地）を描く。
        /// 屋台の側面・背面に貼ると、のっぺりした一枚板に見えなくなる。
        /// </summary>
        public static Texture2D Planks(int size, int planks, int seed)
            => Cached("planks_" + size + "_" + planks + "_" + seed, () =>
        {
            planks = Mathf.Clamp(planks, 2, 16);
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float row = v * planks;
                    int index = Mathf.FloorToInt(row);
                    float inRow = row - index;

                    // 板ごとに色と木目の位相を変える
                    float tone = Mathf.Lerp(0.82f, 1.06f, Hash(index, 3, seed));
                    float phase = Hash(index, 11, seed + 41) * 3.7f;

                    float warp = Fbm(u + phase, inRow * 0.3f, 4, 3, seed + index) - 0.5f;
                    float ring = Mathf.Abs(Mathf.Sin((u * 6f + phase + warp * 1.8f) * Mathf.PI));
                    float k = tone * Mathf.Lerp(0.72f, 1.04f, Mathf.Pow(ring, 0.45f));

                    // 目地：板の合わせ目の細い影
                    float edge = Mathf.Min(inRow, 1f - inRow) * planks;   // 端からのピクセル距離（板単位）
                    float groove = Mathf.Clamp01(edge / (planks * 0.018f));
                    k *= Mathf.Lerp(0.30f, 1f, groove);

                    // 板の中の縦の継ぎ目を数枚おきに入れる
                    float seam = Mathf.Abs(Mathf.Repeat(u * 3f + Hash(index, 23, seed) * 3f, 1f) - 0.5f) * 2f;
                    if (seam > 0.985f) k *= 0.55f;

                    k = Mathf.Clamp01(k);
                    byte g = (byte)(k * 255f);
                    px[y * size + x] = new Color32((byte)(Mathf.Clamp01(k * 1.05f) * 255f), g,
                        (byte)(Mathf.Clamp01(k * 0.89f) * 255f), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        });

        /// <summary>板張りの溝を凹ませるノーマルマップ。<see cref="Planks"/> と同じ枚数で使う。</summary>
        public static Texture2D PlankNormal(int size, int planks, int seed)
            => Cached("planknrm_" + size + "_" + planks + "_" + seed, () =>
        {
            planks = Mathf.Clamp(planks, 2, 16);
            var tex = NewTex(size, size, true);
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float row = v * planks;
                    int index = Mathf.FloorToInt(row);
                    float inRow = row - index;
                    float edge = Mathf.Min(inRow, 1f - inRow) * planks;
                    // 溝は V 字に凹ませる。板の面はわずかに反らせる
                    float groove = Mathf.Clamp01(edge / (planks * 0.030f));
                    float h = Mathf.Lerp(-0.55f, 0f, 1f - groove);
                    h += Mathf.Sin(inRow * Mathf.PI) * 0.06f;
                    h += (Fbm(u * 3f, v * 8f, 16, 3, seed + index) - 0.5f) * 0.10f;
                    height[y * size + x] = h;
                }
            WriteNormal(tex, height, size, 1.5f);
            return tex;
        });

        /// <summary>高さ配列からノーマルマップを書き出す（周期境界）。</summary>
        static void WriteNormal(Texture2D tex, float[] height, int size, float strength)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size, xp = (x + 1) % size;
                    int ym = (y - 1 + size) % size, yp = (y + 1) % size;
                    float dx = (height[y * size + xp] - height[y * size + xm]) * strength * size * 0.02f;
                    float dy = (height[yp * size + x] - height[ym * size + x]) * strength * size * 0.02f;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    byte r = (byte)((n.x * 0.5f + 0.5f) * 255f);
                    byte g = (byte)((n.y * 0.5f + 0.5f) * 255f);
                    byte b = (byte)((n.z * 0.5f + 0.5f) * 255f);
                    px[y * size + x] = new Color32(r, g, b, r);
                }
            tex.SetPixels32(px);
            tex.Apply(true, false);
        }

        // ------------------------------------------------------------------ 和紙

        /// <summary>
        /// 提灯・障子の和紙。漉きムラ（雲のような濃淡）と、絡んだ繊維の筋を重ねる。
        /// </summary>
        public static Texture2D Washi(int size, int seed) => Cached("washi_" + size + "_" + seed, () =>
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;

                    // 漉きムラ：大きく緩やかな濃淡
                    float mottle = Fbm(u, v, 4, 4, seed);
                    float k = Mathf.Lerp(0.80f, 1.02f, mottle);
                    // 雲のような塊
                    float cloud = Fbm(u * 0.6f, v * 0.6f, 3, 2, seed + 61);
                    k *= Mathf.Lerp(0.94f, 1.05f, cloud);

                    // 繊維：長く伸びた筋を向きを変えて3方向ぶん
                    float f1 = Fbm(u * 26f, v * 1.2f, 32, 2, seed + 7);
                    if (f1 > 0.74f) k += (f1 - 0.74f) * 0.85f;
                    float f2 = Fbm(u * 1.2f, v * 26f, 32, 2, seed + 29);
                    if (f2 > 0.78f) k += (f2 - 0.78f) * 0.62f;
                    float f3 = Fbm((u + v) * 14f, (u - v) * 3f, 24, 2, seed + 83);
                    if (f3 > 0.80f) k += (f3 - 0.80f) * 0.50f;

                    // 繊維の塊（節）。和紙らしい濃い点
                    float lump = Fbm(u * 9f, v * 9f, 12, 2, seed + 137);
                    if (lump > 0.88f) k *= Mathf.Lerp(1f, 0.80f, (lump - 0.88f) / 0.12f);

                    k = Mathf.Clamp01(k);
                    byte c = (byte)(k * 255f);
                    px[y * size + x] = new Color32(c, c, (byte)(Mathf.Clamp01(k * 0.965f) * 255f), 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        });

        // ------------------------------------------------------------------ 布

        /// <summary>
        /// 暖簾・のぼりの綿布。縦糸と横糸が交互に浮く平織りを、糸の太さのゆらぎ込みで描く。
        /// </summary>
        public static Texture2D Fabric(int size, int seed) => Cached("fabric_" + size + "_" + seed, () =>
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            int weave = Mathf.Max(3, size / 64);           // 糸1本ぶんのピクセル数
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    int tx = x / weave, ty = y / weave;
                    float fx = (x % weave) / (float)weave;
                    float fy = (y % weave) / (float)weave;
                    bool warpOver = ((tx + ty) % 2) == 0;

                    // 浮いている糸は明るく、沈んでいる糸は影になる
                    float top = warpOver
                        ? Mathf.Sin(fy * Mathf.PI)
                        : Mathf.Sin(fx * Mathf.PI);
                    float under = warpOver
                        ? Mathf.Sin(fx * Mathf.PI)
                        : Mathf.Sin(fy * Mathf.PI);
                    float k = Mathf.Lerp(0.66f, 1.06f, top) * Mathf.Lerp(0.88f, 1.0f, under);

                    // 糸の太さのばらつき（節糸）
                    float slub = Hash(warpOver ? tx : ty, warpOver ? 0 : 1, seed + 17);
                    k *= Mathf.Lerp(0.93f, 1.05f, slub);
                    // 染めムラ
                    k *= Mathf.Lerp(0.93f, 1.05f, Fbm(u, v, 6, 3, seed));

                    k = Mathf.Clamp01(k);
                    byte c = (byte)(k * 255f);
                    px[y * size + x] = new Color32(c, c, c, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        });

        // ------------------------------------------------------------------ 法線

        /// <summary>細かい凹凸のノーマルマップ。木や土のディテール用。</summary>
        public static Texture2D NoiseNormal(int size, float strength, int seed)
            => Cached("nrm_" + size + "_" + strength.ToString("0.##") + "_" + seed, () =>
        {
            var tex = NewTex(size, size, true);
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    height[y * size + x] = Fbm(x / (float)size, y / (float)size, 16, 4, seed);
            // Unity(HDRP) は DXT5nm。R を A にも入れておくと両対応しやすい
            WriteNormal(tex, height, size, strength);
            return tex;
        });

        /// <summary>パーティクル用の柔らかい円。湯気・煙・光の粒に使う。</summary>
        public static Texture2D SoftCircle(int size) => Cached("soft_" + size, () =>
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        });

    }
}
