using System.Collections.Generic;
using Matsuri.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;

namespace Matsuri.Art
{
    /// <summary>
    /// 文字が載るテクスチャ（看板・暖簾・品書き・案内板）。
    ///
    /// 文字は同梱の日本語フォント (Noto Sans JP) を <see cref="FontAsset"/> の
    /// SDF アトラスへ焼き、そこから <see cref="Texture2D"/> へ転写して描く。
    /// SDF なので、どんな大きさに引き伸ばしても輪郭がぼやけない。
    /// フォントが使えない環境（アトラスが読み出せない等）では、
    /// 従来どおり筆画を手続き的に描いて「墨文字らしきもの」に落とす。
    /// </summary>
    public static partial class ProceduralTextures
    {
        // ================================================================== 看板

        /// <summary>屋号を書いた看板テクスチャ。</summary>
        public static Texture2D KanjiSign(string text, int w, int h, Color bg, Color fg)
            => KanjiSign(text, w, h, bg, fg, false);

        /// <summary>屋号を書いた看板テクスチャ。vertical=true で縦書き。</summary>
        public static Texture2D KanjiSign(string text, int w, int h, Color bg, Color fg, bool vertical)
        {
            string key = "sign_" + text + "_" + w + "x" + h + "_" + ColorUtility.ToHtmlStringRGB(bg)
                         + "_" + ColorUtility.ToHtmlStringRGB(fg) + (vertical ? "_v" : "_h");
            return Cached(key, () =>
            {
                var tex = NewSignTexture(w, h);
                var px = new Color[w * h];
                FillWashiBackground(px, w, h, bg, 4242);

                var chars = Sanitize(text, 6);
                if (chars.Count > 0)
                {
                    var bake = FontBake.Prepare(chars);
                    int n = chars.Count;
                    const float margin = 0.09f;
                    for (int i = 0; i < n; i++)
                    {
                        float cx, cy, cell;
                        if (vertical)
                        {
                            cell = Mathf.Min(w * (1f - margin * 2f), h * (1f - margin * 2f) / n);
                            cx = w * 0.5f;
                            cy = h * (1f - margin) - (h * (1f - margin * 2f)) * ((i + 0.5f) / n);
                        }
                        else
                        {
                            cell = Mathf.Min(h * (1f - margin * 2f), w * (1f - margin * 2f) / n);
                            cx = w * margin + (w * (1f - margin * 2f)) * ((i + 0.5f) / n);
                            cy = h * 0.5f;
                        }
                        DrawCharacter(px, w, h, chars[i], cx, cy, cell * 0.94f, fg, bake);
                    }
                }

                tex.SetPixels(px);
                tex.Apply(true, false);
                return tex;
            });
        }

        // ================================================================== 品書き

        /// <summary>
        /// 屋台の側面に貼る品書き（メニューの紙）。
        /// 見出しを縦に、品目を1行ずつ並べる。
        /// </summary>
        public static Texture2D MenuPaper(string title, IReadOnlyList<string> items, int w, int h, Color bg, Color fg)
        {
            var sb = new System.Text.StringBuilder("menu_").Append(title).Append('_').Append(w).Append('x').Append(h);
            if (items != null) for (int i = 0; i < items.Count; i++) sb.Append('|').Append(items[i]);
            sb.Append('_').Append(ColorUtility.ToHtmlStringRGB(bg)).Append('_').Append(ColorUtility.ToHtmlStringRGB(fg));
            return Cached(sb.ToString(), () =>
            {
                var tex = NewSignTexture(w, h);
                var px = new Color[w * h];
                FillWashiBackground(px, w, h, bg, 811);

                // 紙の縁の焼け
                var edge = Color.Lerp(bg, new Color(0.42f, 0.32f, 0.18f), 0.35f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = Mathf.Min(x, w - 1 - x) / (float)w;
                        float dy = Mathf.Min(y, h - 1 - y) / (float)h;
                        float d = Mathf.Min(dx * 2.4f, dy * 2.4f);
                        if (d < 1f) px[y * w + x] = Color.Lerp(edge, px[y * w + x], Mathf.SmoothStep(0f, 1f, d));
                    }

                var all = new List<char>(32);
                var titleChars = Sanitize(title, 5);
                all.AddRange(titleChars);
                int lineCount = items != null ? Mathf.Min(items.Count, 5) : 0;
                var lines = new List<char>[lineCount];
                for (int i = 0; i < lineCount; i++)
                {
                    lines[i] = Sanitize(items[i], 8);
                    all.AddRange(lines[i]);
                }
                var bake = FontBake.Prepare(all);

                // 見出し：上部に大きく
                float titleCell = Mathf.Min(h * 0.22f, w * 0.80f / Mathf.Max(1, titleChars.Count));
                for (int i = 0; i < titleChars.Count; i++)
                {
                    float cx = w * 0.5f + (i - (titleChars.Count - 1) * 0.5f) * titleCell;
                    DrawCharacter(px, w, h, titleChars[i], cx, h * 0.855f, titleCell * 0.94f, fg, bake);
                }
                // 見出しの下の罫線
                DrawRule(px, w, h, w * 0.10f, w * 0.90f, h * 0.755f, Mathf.Max(1.5f, h * 0.008f), fg);

                // 品目
                for (int i = 0; i < lineCount; i++)
                {
                    var line = lines[i];
                    float rowY = h * (0.66f - i * 0.135f);
                    float cell = Mathf.Min(h * 0.115f, w * 0.86f / Mathf.Max(1, line.Count));
                    float x0 = w * 0.09f + cell * 0.5f;
                    for (int j = 0; j < line.Count; j++)
                        DrawCharacter(px, w, h, line[j], x0 + j * cell, rowY, cell * 0.90f, fg, bake);
                }

                tex.SetPixels(px);
                tex.Apply(true, false);
                return tex;
            });
        }

        // ================================================================== 案内板

        /// <summary>案内板 (§20 SignBoard) に貼る、会場マップ風のテクスチャ。</summary>
        public static Texture2D MapBoard(int w, int h, Color bg, Color line, int seed)
            => Cached("map_" + w + "x" + h + "_" + seed, () =>
        {
            var tex = NewSignTexture(w, h);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            // 参道（十字の帯）
            var road = Color.Lerp(bg, line, 0.35f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool onV = Mathf.Abs(x - w * 0.5f) < w * 0.07f;
                    bool onH = Mathf.Abs(y - h * 0.42f) < h * 0.06f;
                    if (onV || onH) px[y * w + x] = road;
                }
            // 屋台を表す小さな四角
            var rng = new System.Random(seed);
            for (int i = 0; i < 14; i++)
            {
                int bx = (int)(w * (0.10f + 0.80f * (float)rng.NextDouble()));
                int by = (int)(h * (0.10f + 0.80f * (float)rng.NextDouble()));
                int bw = Mathf.Max(3, w / 22), bh = Mathf.Max(3, h / 26);
                for (int y = by; y < Mathf.Min(h, by + bh); y++)
                    for (int x = bx; x < Mathf.Min(w, bx + bw); x++)
                        px[y * w + x] = line;
            }
            // 外枠
            int t = Mathf.Max(2, w / 90);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (x < t || y < t || x >= w - t || y >= h - t) px[y * w + x] = line;

            tex.SetPixels(px);
            tex.Apply(true, false);
            return tex;
        });

        // ================================================================== 下ごしらえ

        static Texture2D NewSignTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, true, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4
        };

        /// <summary>看板の下地。和紙のムラを乗せる。</summary>
        static void FillWashiBackground(Color[] px, int w, int h, Color bg, int seed)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float k = Mathf.Lerp(0.90f, 1.07f, Fbm(x / (float)w, y / (float)h, 6, 3, seed));
                    float fiber = Fbm(x / (float)w * 18f, y / (float)h * 1.4f, 24, 2, seed + 5);
                    if (fiber > 0.80f) k += (fiber - 0.80f) * 0.30f;
                    px[y * w + x] = new Color(bg.r * k, bg.g * k, bg.b * k, 1f);
                }
        }

        static List<char> Sanitize(string text, int max)
        {
            var list = new List<char>(8);
            if (string.IsNullOrEmpty(text)) return list;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                list.Add(c);
                if (list.Count >= max) break;
            }
            return list;
        }

        /// <summary>横に走る罫線。</summary>
        static void DrawRule(Color[] px, int w, int h, float x0, float x1, float y, float thick, Color ink)
        {
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(y - thick * 0.5f));
            int iy1 = Mathf.Min(h - 1, Mathf.CeilToInt(y + thick * 0.5f));
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0));
            int ix1 = Mathf.Min(w - 1, Mathf.CeilToInt(x1));
            for (int yy = iy0; yy <= iy1; yy++)
                for (int xx = ix0; xx <= ix1; xx++)
                {
                    float a = Mathf.Clamp01((thick * 0.5f + 0.5f - Mathf.Abs(yy + 0.5f - y)));
                    if (a <= 0f) continue;
                    int idx = yy * w + xx;
                    px[idx] = Color.Lerp(px[idx], ink, a * 0.85f);
                }
        }

        // ================================================================== 文字を描く

        /// <summary>1文字を (cx, cy) 中心・1辺 size で描く。</summary>
        static void DrawCharacter(Color[] px, int w, int h, char c, float cx, float cy, float size, Color ink, FontBake bake)
        {
            if (bake != null && bake.TryDraw(px, w, h, c, cx, cy, size, ink)) return;
            DrawGlyphStrokes(px, w, h, c, cx, cy, size, ink);
        }

        /// <summary>
        /// フォントアトラスから文字を転写するための道具一式。
        /// 使う文字をまとめてアトラスへ焼いてから、アトラスの画素を1度だけ読み出す。
        /// </summary>
        sealed class FontBake
        {
            FontAsset _font;
            readonly Dictionary<int, AtlasPixels> _atlases = new Dictionary<int, AtlasPixels>(2);
            int _padding;
            float _pointSize;

            /// <summary>使えない場合は null を返す。</summary>
            public static FontBake Prepare(IReadOnlyList<char> chars)
            {
                if (chars == null || chars.Count == 0) return null;
                var font = MatsuriFontProvider.JapaneseFontAsset;
                if (font == null) return null;

                try
                {
                    var sb = new System.Text.StringBuilder(chars.Count);
                    for (int i = 0; i < chars.Count; i++) sb.Append(chars[i]);
                    // Dynamic なフォントアセットなので、必要な文字だけをその場でアトラスへ焼く
                    font.TryAddCharacters(sb.ToString(), out string _);

                    float pointSize = font.faceInfo.pointSize;
                    if (pointSize <= 0f) return null;

                    var bake = new FontBake
                    {
                        _font = font,
                        _padding = Mathf.Max(0, font.atlasPadding),
                        _pointSize = pointSize
                    };
                    return bake;
                }
                catch (System.Exception e)
                {
                    MatsuriLog.Warn("フォントから看板文字を焼けませんでした: " + e.Message + " — 手描きの字形で代用します。");
                    return null;
                }
            }

            /// <summary>1文字ぶんをアトラスから転写する。失敗したら false（呼び出し側が手描きに落とす）。</summary>
            public bool TryDraw(Color[] px, int w, int h, char c, float cx, float cy, float size, Color ink)
            {
                try
                {
                    if (_font == null) return false;
                    var table = _font.characterLookupTable;
                    if (table == null || !table.TryGetValue(c, out Character ch) || ch == null) return false;
                    Glyph glyph = ch.glyph;
                    if (glyph == null) return false;

                    GlyphRect rect = glyph.glyphRect;
                    if (rect.width <= 0 || rect.height <= 0) return false;

                    var atlas = GetAtlas(glyph.atlasIndex);
                    if (atlas == null) return false;

                    // 転写元：グリフ矩形をパディングぶん広げた範囲（SDF の外側の勾配まで含める）
                    int sx = rect.x - _padding, sy = rect.y - _padding;
                    int sw = rect.width + _padding * 2, sh = rect.height + _padding * 2;

                    // 転写先：文字の実体をセルの中央へ置く。size は1文字ぶんの正方形の1辺
                    float scale = size / _pointSize;
                    float inkW = rect.width * scale, inkH = rect.height * scale;
                    float padPx = _padding * scale;
                    float dx0 = cx - inkW * 0.5f - padPx;
                    float dy0 = cy - inkH * 0.5f - padPx;
                    float dw = inkW + padPx * 2f, dh = inkH + padPx * 2f;
                    if (dw < 1f || dh < 1f) return false;

                    // SDF の 0.5 を輪郭とする。1画素ぶんでちょうど切り替わる鋭さにする
                    float sharpness = Mathf.Max(2f, 2f * Mathf.Max(1f, padPx));

                    int ix0 = Mathf.Max(0, Mathf.FloorToInt(dx0));
                    int ix1 = Mathf.Min(w - 1, Mathf.CeilToInt(dx0 + dw));
                    int iy0 = Mathf.Max(0, Mathf.FloorToInt(dy0));
                    int iy1 = Mathf.Min(h - 1, Mathf.CeilToInt(dy0 + dh));

                    for (int y = iy0; y <= iy1; y++)
                        for (int x = ix0; x <= ix1; x++)
                        {
                            float u = (x + 0.5f - dx0) / dw;
                            float v = (y + 0.5f - dy0) / dh;
                            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                            float d = atlas.Sample(sx + u * sw, sy + v * sh);
                            float a = Mathf.Clamp01((d - 0.5f) * sharpness + 0.5f);
                            if (a <= 0.002f) continue;
                            int idx = y * w + x;
                            px[idx] = Color.Lerp(px[idx], ink, a);
                        }
                    return true;
                }
                catch (System.Exception)
                {
                    _font = null;
                    return false;
                }
            }

            AtlasPixels GetAtlas(int index)
            {
                if (_atlases.TryGetValue(index, out var cached)) return cached;
                Texture2D tex = null;
                var list = _font.atlasTextures;
                if (list != null && index >= 0 && index < list.Length) tex = list[index];
                if (tex == null) tex = _font.atlasTexture;
                var made = AtlasPixels.From(tex);
                _atlases[index] = made;
                return made;
            }
        }

        /// <summary>フォントアトラスの画素を読み出して、バイリニアで引ける形にした物。</summary>
        sealed class AtlasPixels
        {
            byte[] _gray;
            Color32[] _rgba;
            int _w, _h;

            public static AtlasPixels From(Texture2D tex)
            {
                if (tex == null) return null;
                if (!tex.isReadable) return null;
                var a = new AtlasPixels { _w = tex.width, _h = tex.height };
                if (a._w <= 0 || a._h <= 0) return null;
                try
                {
                    if (tex.format == TextureFormat.Alpha8 || tex.format == TextureFormat.R8)
                    {
                        NativeArray<byte> raw = tex.GetPixelData<byte>(0);
                        if (raw.Length < a._w * a._h) return null;
                        a._gray = raw.ToArray();
                    }
                    else
                    {
                        a._rgba = tex.GetPixels32();
                        if (a._rgba == null || a._rgba.Length < a._w * a._h) return null;
                    }
                }
                catch (System.Exception)
                {
                    return null;
                }
                return a;
            }

            float At(int x, int y)
            {
                x = Mathf.Clamp(x, 0, _w - 1);
                y = Mathf.Clamp(y, 0, _h - 1);
                int i = y * _w + x;
                if (_gray != null) return _gray[i] / 255f;
                var c = _rgba[i];
                // アルファに距離が入っている。RGBA で焼かれている場合に備えて赤も見る
                return Mathf.Max(c.a, c.r) / 255f;
            }

            public float Sample(float x, float y)
            {
                float fx = x - 0.5f, fy = y - 0.5f;
                int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0, ty = fy - y0;
                float a = Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), tx);
                float b = Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), tx);
                return Mathf.Lerp(a, b, ty);
            }
        }

        // ================================================================== 手描きの代替字形

        /// <summary>
        /// フォントが使えないときの逃げ道。文字コードから決定論的に筆画を組み立てて
        /// 「墨で書いた字」に見える物を描く。同じ屋号は必ず同じ字形になる。
        /// </summary>
        static void DrawGlyphStrokes(Color[] px, int w, int h, char c, float cx, float cy, float size, Color ink)
        {
            int seed;
            unchecked { seed = (int)(c * 2654435761u); }
            float half = size * 0.5f;
            int strokes = 4 + (Mathf.Abs(seed) % 4);          // 4〜7画
            float thick = Mathf.Max(1.8f, size * 0.105f);

            // どの字も「横画が1本以上」「縦画が1本以上」あると漢字らしく見える
            for (int s = 0; s < strokes; s++)
            {
                float r1 = Hash(seed & 0xffff, s, 17);
                float r2 = Hash(seed & 0xffff, s, 53);
                float r3 = Hash(seed & 0xffff, s, 91);
                int kind = s == 0 ? 0 : (s == 1 ? 1 : (s == 2 ? 0 : Mathf.FloorToInt(r1 * 4f)));
                float t = -half + size * Mathf.Lerp(0.16f, 0.84f, r2);
                float a = -half * Mathf.Lerp(0.62f, 0.94f, r3);
                float b = half * Mathf.Lerp(0.62f, 0.94f, 1f - r3);
                switch (kind)
                {
                    case 0: // 横画
                        DrawStroke(px, w, h, cx + a, cy + t, cx + b, cy + t + size * 0.018f, thick, ink);
                        break;
                    case 1: // 縦画
                        DrawStroke(px, w, h, cx + t, cy + b, cx + t - size * 0.012f, cy + a, thick, ink);
                        break;
                    case 2: // 右払い
                        DrawStroke(px, w, h, cx + a * 0.7f, cy + b * 0.5f, cx + b * 0.8f, cy + a * 0.8f, thick * 0.85f, ink);
                        break;
                    default: // 左払い
                        DrawStroke(px, w, h, cx + b * 0.7f, cy + b * 0.5f, cx + a * 0.8f, cy + a * 0.8f, thick * 0.85f, ink);
                        break;
                }
            }
        }

        /// <summary>始点で太く終点で細くなる筆画を、アンチエイリアス付きで描く。</summary>
        static void DrawStroke(Color[] px, int w, int h, float x0, float y0, float x1, float y1, float thick, Color ink)
        {
            float minX = Mathf.Min(x0, x1) - thick - 2f, maxX = Mathf.Max(x0, x1) + thick + 2f;
            float minY = Mathf.Min(y0, y1) - thick - 2f, maxY = Mathf.Max(y0, y1) + thick + 2f;
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(minX)), ix1 = Mathf.Min(w - 1, Mathf.CeilToInt(maxX));
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(minY)), iy1 = Mathf.Min(h - 1, Mathf.CeilToInt(maxY));
            float dx = x1 - x0, dy = y1 - y0;
            float len2 = Mathf.Max(1e-4f, dx * dx + dy * dy);

            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    float px0 = x + 0.5f - x0, py0 = y + 0.5f - y0;
                    float t = Mathf.Clamp01((px0 * dx + py0 * dy) / len2);
                    float qx = px0 - dx * t, qy = py0 - dy * t;
                    float dist = Mathf.Sqrt(qx * qx + qy * qy);
                    float radius = thick * Mathf.Lerp(0.62f, 0.26f, t) + thick * 0.32f;
                    float a = Mathf.Clamp01((radius - dist) / 1.1f);
                    if (a <= 0f) continue;
                    int idx = y * w + x;
                    px[idx] = Color.Lerp(px[idx], ink, a);
                }
        }
    }
}
