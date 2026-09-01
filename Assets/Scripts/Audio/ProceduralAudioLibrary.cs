using System.Collections.Generic;
using UnityEngine;
using URandom = Unity.Mathematics.Random;
using Adsr = Matsuri.Audio.AudioSynth.Adsr;

namespace Matsuri.Audio
{
    /// <summary>効果音の種類 (§24)。</summary>
    public enum MatsuriSfx
    {
        /// <summary>建設完了。木が組み上がる音。</summary>
        Build,
        /// <summary>電球が点く音。</summary>
        Bulb,
        /// <summary>購入成立。</summary>
        Purchase,
        /// <summary>小銭の音。</summary>
        Coin,
        /// <summary>歓声。</summary>
        Cheer,
        /// <summary>花火の打ち上げ（笛）。</summary>
        FireworkLaunch,
        /// <summary>花火の破裂。</summary>
        FireworkBurst,
        /// <summary>太鼓の一打。</summary>
        TaikoHit,
        /// <summary>鉄板の焼ける音。</summary>
        Sizzle,
        /// <summary>かき氷を削る音。</summary>
        Shaving,
        /// <summary>水の音（金魚すくい・ヨーヨー）。</summary>
        Water,
        /// <summary>ぽん、という軽い音。</summary>
        Pop,
        /// <summary>綿あめ機のモーター音。</summary>
        Whirr,
        /// <summary>エラー。</summary>
        Error,
        /// <summary>UI クリック。</summary>
        Click
    }

    /// <summary>
    /// 仕様書 §24 / §63。音源ファイルを持たないので、祭りの音はすべてここで合成する。
    /// 生成は重いため必ずキャッシュする。差し替えたくなったら
    /// <see cref="Get"/> の中身を録音済みクリップのロードに置き換えればよい。
    /// </summary>
    public static partial class ProceduralAudioLibrary
    {
        static readonly Dictionary<MatsuriSfx, AudioClip> Cache = new Dictionary<MatsuriSfx, AudioClip>();
        static AudioClip _bgm;
        static AudioClip _crowd;
        static AudioClip _cicada;
        static AudioClip _nightInsects;

        /// <summary>都節音階 (§24 の「日本の祭りらしさ」)。主音からの半音数。</summary>
        static readonly int[] MiyakoBushi = { 0, 1, 5, 7, 8, 12, 13 };

        /// <summary>主音 D5。篠笛の音域に合わせる。</summary>
        const float Tonic = 587.33f;

        // ── 公開 API ───────────────────────────────────────────

        public static AudioClip Get(MatsuriSfx sfx)
        {
            if (Cache.TryGetValue(sfx, out var cached) && cached != null) return cached;
            var clip = Synthesize(sfx);
            Cache[sfx] = clip;
            return clip;
        }

        /// <summary>祭囃子。篠笛＋太鼓＋鉦。24秒でループする。</summary>
        public static AudioClip FestivalBgm()
        {
            if (_bgm != null) return _bgm;
            _bgm = BuildFestivalBgm();
            return _bgm;
        }

        /// <summary>人のざわめき。20秒ループ。</summary>
        public static AudioClip CrowdAmbience()
        {
            if (_crowd != null) return _crowd;
            _crowd = BuildCrowd();
            return _crowd;
        }

        /// <summary>ヒグラシ。夕方 (17:00〜19:00) に鳴く (§63)。12秒ループ。</summary>
        public static AudioClip CicadaAmbience()
        {
            if (_cicada != null) return _cicada;
            _cicada = BuildCicada();
            return _cicada;
        }

        /// <summary>夜の虫の音（鈴虫）。19:00 以降はセミと入れ替わる (§63)。12秒ループ。</summary>
        public static AudioClip NightInsectsAmbience()
        {
            if (_nightInsects != null) return _nightInsects;
            _nightInsects = BuildNightInsects();
            return _nightInsects;
        }

        public static void ClearCache()
        {
            foreach (var kv in Cache) if (kv.Value != null) Object.Destroy(kv.Value);
            Cache.Clear();
            DestroyIfAny(ref _bgm);
            DestroyIfAny(ref _crowd);
            DestroyIfAny(ref _cicada);
            DestroyIfAny(ref _nightInsects);
        }

        static void DestroyIfAny(ref AudioClip clip)
        {
            if (clip != null) Object.Destroy(clip);
            clip = null;
        }

        static float Note(int scaleIndex, int octave = 0)
        {
            int i = Mathf.Clamp(scaleIndex, 0, MiyakoBushi.Length - 1);
            return Tonic * Mathf.Pow(2f, (MiyakoBushi[i] + octave * 12) / 12f);
        }

        // ── 効果音 ─────────────────────────────────────────────

        static AudioClip Synthesize(MatsuriSfx sfx)
        {
            switch (sfx)
            {
                case MatsuriSfx.Build: return BuildWoodAssemble();
                case MatsuriSfx.Bulb: return BuildBulb();
                case MatsuriSfx.Purchase: return BuildPurchase();
                case MatsuriSfx.Coin: return BuildCoin();
                case MatsuriSfx.Cheer: return BuildCheer();
                case MatsuriSfx.FireworkLaunch: return BuildFireworkLaunch();
                case MatsuriSfx.FireworkBurst: return BuildFireworkBurst();
                case MatsuriSfx.TaikoHit: return BuildTaikoHit();
                case MatsuriSfx.Sizzle: return BuildSizzle();
                case MatsuriSfx.Shaving: return BuildShaving();
                case MatsuriSfx.Water: return BuildWater();
                case MatsuriSfx.Pop: return BuildPop();
                case MatsuriSfx.Whirr: return BuildWhirr();
                case MatsuriSfx.Error: return BuildError();
                default: return BuildClick();
            }
        }

        static AudioClip BuildWoodAssemble()
        {
            var buf = AudioSynth.Buffer(0.9f);
            var rng = URandom.CreateFromIndex(9001u);
            // 木材が次々にはまる音を3回
            float[] at = { 0f, 0.16f, 0.34f };
            float[] pitch = { 168f, 214f, 262f };
            for (int i = 0; i < at.Length; i++)
            {
                int off = AudioSynth.Samples(at[i]);
                AudioSynth.AddTone(buf, off, pitch[i], 0.14f, 0.5f, Adsr.Percussive(0.13f),
                                   WaveKind.Triangle, new[] { 0.3f, 0.15f }, seed: (uint)(i + 5));
                AudioSynth.AddNoise(buf, off, 0.05f, 0.30f, Adsr.Percussive(0.045f), 3800f, 600f, ref rng);
            }
            // 完成の余韻（提灯が揺れるような小さな鈴）
            AudioSynth.AddTone(buf, AudioSynth.Samples(0.5f), 1560f, 0.35f, 0.12f,
                               Adsr.Percussive(0.33f), WaveKind.Sine, null, seed: 77u);
            AudioSynth.Normalize(buf, 0.8f);
            AudioSynth.FadeEdges(buf);
            return AudioSynth.ToClip("PROC_SFX_Build", buf);
        }

        static AudioClip BuildBulb()
        {
            var buf = AudioSynth.Buffer(0.45f);
            var rng = URandom.CreateFromIndex(4111u);
            AudioSynth.AddNoise(buf, 0, 0.012f, 0.35f, Adsr.Percussive(0.01f), 9000f, 2000f, ref rng);
            AudioSynth.AddTone(buf, AudioSynth.Samples(0.005f), 1180f, 0.32f, 0.30f,
                               Adsr.Percussive(0.3f), WaveKind.Sine, new[] { 0.25f, 0.1f }, seed: 12u);
            AudioSynth.AddTone(buf, AudioSynth.Samples(0.02f), 2360f, 0.18f, 0.12f,
                               Adsr.Percussive(0.17f), WaveKind.Sine, null, seed: 13u);
            AudioSynth.Normalize(buf, 0.7f);
            AudioSynth.FadeEdges(buf);
            return AudioSynth.ToClip("PROC_SFX_Bulb", buf);
        }

        static AudioClip BuildPurchase()
        {
            var buf = AudioSynth.Buffer(0.6f);
            AudioSynth.AddTone(buf, 0, Note(0), 0.18f, 0.30f, Adsr.Percussive(0.17f),
                               WaveKind.Sine, new[] { 0.3f, 0.12f }, seed: 21u);
            AudioSynth.AddTone(buf, AudioSynth.Samples(0.11f), Note(3), 0.34f, 0.30f,
                               Adsr.Percussive(0.32f), WaveKind.Sine, new[] { 0.3f, 0.12f }, seed: 22u);
            AudioSynth.Normalize(buf, 0.72f);
            AudioSynth.FadeEdges(buf);
            return AudioSynth.ToClip("PROC_SFX_Purchase", buf);
        }

        static AudioClip BuildCoin()
        {
            var buf = AudioSynth.Buffer(0.5f);
            float[] partials = { 2450f, 3720f, 5180f };
            float[] gains = { 1f, 0.55f, 0.28f };
            for (int i = 0; i < partials.Length; i++)
            {
                AudioSynth.AddTone(buf, 0, partials[i], 0.3f, 0.24f * gains[i],
                                   Adsr.Percussive(0.28f), WaveKind.Sine, null, seed: (uint)(30 + i));
                AudioSynth.AddTone(buf, AudioSynth.Samples(0.06f), partials[i] * 1.02f, 0.22f,
                                   0.14f * gains[i], Adsr.Percussive(0.2f), WaveKind.Sine, null, seed: (uint)(40 + i));
            }
            AudioSynth.Normalize(buf, 0.62f);
            AudioSynth.FadeEdges(buf);
            return AudioSynth.ToClip("PROC_SFX_Coin", buf);
        }

        static AudioClip BuildCheer()
        {
            var buf = AudioSynth.Buffer(2.2f);
            var rng = URandom.CreateFromIndex(6543u);
            for (int v = 0; v < 40; v++)
            {
                float start = rng.NextFloat(0f, 0.5f);
                float dur = rng.NextFloat(0.7f, 1.5f);
                var blob = AudioSynth.Buffer(dur + 0.2f);
                for (int i = 0; i < blob.Length; i++) blob[i] = rng.NextFloat(-1f, 1f);
                AudioSynth.BandPass(blob, rng.NextFloat(400f, 1400f), 4f);
                for (int i = 0; i < blob.Length; i++)
                {
                    float t = i / (float)AudioSynth.SampleRate;
                    float e = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                    blob[i] *= e * e;
                }
                AudioSynth.MixInto(buf, blob, AudioSynth.Samples(start), rng.NextFloat(0.2f, 0.5f));
            }
            AudioSynth.LowPass(buf, 5200f);
            AudioSynth.SoftClip(buf);
            AudioSynth.Normalize(buf, 0.8f);
            AudioSynth.FadeEdges(buf, 0.05f);
            return AudioSynth.ToClip("PROC_SFX_Cheer", buf);
        }

        static AudioClip BuildFireworkLaunch()
        {
            var buf = AudioSynth.Buffer(1.6f);
            var rng = URandom.CreateFromIndex(3131u);
            // 打ち上げの笛：ピューッと上がる
            AudioSynth.AddTone(buf, 0, 420f, 1.25f, 0.26f,
                               new Adsr(0.05f, 0.2f, 0.85f, 0.2f), WaveKind.Sine,
                               new[] { 0.18f, 0.07f }, bendSemitones: 21f,
                               vibratoHz: 7f, vibratoCents: 22f, seed: 51u);
            // 火薬の噴射
            AudioSynth.AddNoise(buf, 0, 1.2f, 0.10f,
                                new Adsr(0.03f, 0.3f, 0.6f, 0.25f), 6000f, 900f, ref rng);
            AudioSynth.Normalize(buf, 0.6f);
            AudioSynth.FadeEdges(buf, 0.03f);
            return AudioSynth.ToClip("PROC_SFX_FireworkLaunch", buf);
        }

        static AudioClip BuildFireworkBurst()
        {
            var buf = AudioSynth.Buffer(2.6f);
            var rng = URandom.CreateFromIndex(2929u);
            // ドン：腹に来る低音
            AudioSynth.AddTone(buf, 0, 52f, 0.75f, 0.95f, new Adsr(0.004f, 0.7f, 0f, 0.1f),
                               WaveKind.Sine, null, bendSemitones: -9f, seed: 60u);
            AudioSynth.AddTone(buf, 0, 96f, 0.35f, 0.4f, Adsr.Percussive(0.32f),
                               WaveKind.Sine, null, bendSemitones: -12f, seed: 61u);
            // 破裂のノイズ
            AudioSynth.AddNoise(buf, 0, 0.55f, 0.65f, new Adsr(0.003f, 0.5f, 0f, 0.05f),
                                5200f, 120f, ref rng);
            // パラパラという火の粉
            for (int i = 0; i < 60; i++)
            {
                float t = rng.NextFloat(0.15f, 1.9f);
                float amp = 0.10f * (1f - t / 2.0f);
                if (amp <= 0f) continue;
                AudioSynth.AddNoise(buf, AudioSynth.Samples(t), 0.02f, amp,
                                    Adsr.Percussive(0.018f), 8000f, 2200f, ref rng);
            }
            AudioSynth.Delay(buf, 0.28f, 0.32f, 0.5f);   // 山や建物からの反射
            AudioSynth.SoftClip(buf);
            AudioSynth.Normalize(buf, 0.95f);
            AudioSynth.FadeEdges(buf, 0.02f);
            return AudioSynth.ToClip("PROC_SFX_FireworkBurst", buf);
        }

        static AudioClip BuildTaikoHit()
        {
            var buf = AudioSynth.Buffer(0.8f);
            var rng = URandom.CreateFromIndex(1717u);
            AddTaiko(buf, 0, 1f, ref rng);
            AudioSynth.Normalize(buf, 0.92f);
            AudioSynth.FadeEdges(buf, 0.02f);
            return AudioSynth.ToClip("PROC_SFX_TaikoHit", buf);
        }

        static AudioClip BuildSizzle()
        {
            var buf = AudioSynth.Buffer(2.0f);
            var rng = URandom.CreateFromIndex(8080u);
            for (int i = 0; i < buf.Length; i++) buf[i] = rng.NextFloat(-1f, 1f);
            AudioSynth.HighPass(buf, 2200f);
            AudioSynth.LowPass(buf, 8000f);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)AudioSynth.SampleRate;
                buf[i] *= 0.65f + 0.35f * Mathf.Sin(t * 3.3f * 6.2831853f + Mathf.Sin(t * 1.1f));
            }
            // 時々はじける
            for (int i = 0; i < 24; i++)
                AudioSynth.AddNoise(buf, AudioSynth.Samples(rng.NextFloat(0f, 1.95f)), 0.015f, 0.5f,
                                    Adsr.Percussive(0.013f), 9000f, 3000f, ref rng);
            AudioSynth.Normalize(buf, 0.5f);
            AudioSynth.SmoothLoopSeam(buf, 0.15f);
            return AudioSynth.ToClip("PROC_SFX_Sizzle", buf);
        }

        static AudioClip BuildShaving()
        {
            var buf = AudioSynth.Buffer(2.0f);
            var rng = URandom.CreateFromIndex(6161u);
            for (int i = 0; i < buf.Length; i++) buf[i] = rng.NextFloat(-1f, 1f);
            AudioSynth.BandPass(buf, 1800f, 1.6f);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)AudioSynth.SampleRate;
                // 氷の塊が回転するリズム
                buf[i] *= 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 7.5f * Mathf.PI));
            }
            AudioSynth.AddTone(buf, 0, 690f, 1.95f, 0.05f,
                               new Adsr(0.2f, 0.3f, 0.7f, 0.2f), WaveKind.Triangle, null, seed: 70u);
            AudioSynth.Normalize(buf, 0.5f);
            AudioSynth.SmoothLoopSeam(buf, 0.15f);
            return AudioSynth.ToClip("PROC_SFX_Shaving", buf);
        }

        static AudioClip BuildWater()
        {
            var buf = AudioSynth.Buffer(1.2f);
            var rng = URandom.CreateFromIndex(3030u);
            // ぽちゃぽちゃという水滴
            for (int i = 0; i < 9; i++)
            {
                float t = rng.NextFloat(0f, 1.0f);
                float f = rng.NextFloat(520f, 1350f);
                AudioSynth.AddTone(buf, AudioSynth.Samples(t), f, 0.10f, rng.NextFloat(0.2f, 0.42f),
                                   Adsr.Percussive(0.09f), WaveKind.Sine, null,
                                   bendSemitones: rng.NextFloat(6f, 14f), seed: rng.NextUInt(1u, 9999u));
            }
            // 水面の擦れ
            AudioSynth.AddNoise(buf, 0, 1.0f, 0.06f, new Adsr(0.1f, 0.3f, 0.6f, 0.2f),
                                4000f, 800f, ref rng);
            AudioSynth.Normalize(buf, 0.6f);
            AudioSynth.FadeEdges(buf);
            return AudioSynth.ToClip("PROC_SFX_Water", buf);
        }

        static AudioClip BuildPop()
        {
            var buf = AudioSynth.Buffer(0.3f);
            var rng = URandom.CreateFromIndex(1212u);
            AudioSynth.AddTone(buf, 0, 320f, 0.09f, 0.6f, Adsr.Percussive(0.08f),
                               WaveKind.Sine, null, bendSemitones: 16f, seed: 80u);
            AudioSynth.AddNoise(buf, 0, 0.02f, 0.25f, Adsr.Percussive(0.018f), 6000f, 1200f, ref rng);
            AudioSynth.Normalize(buf, 0.75f);
            AudioSynth.FadeEdges(buf);
            return AudioSynth.ToClip("PROC_SFX_Pop", buf);
        }

        static AudioClip BuildWhirr()
        {
            var buf = AudioSynth.Buffer(2.0f);
            var rng = URandom.CreateFromIndex(5151u);
            AudioSynth.AddTone(buf, 0, 118f, 1.95f, 0.22f, new Adsr(0.15f, 0.2f, 0.9f, 0.15f),
                               WaveKind.Saw, new[] { 0.2f, 0.1f, 0.05f },
                               vibratoHz: 3.1f, vibratoCents: 18f, seed: 90u);
            AudioSynth.AddNoise(buf, 0, 1.95f, 0.09f, new Adsr(0.15f, 0.2f, 0.9f, 0.15f),
                                2600f, 400f, ref rng);
            AudioSynth.LowPass(buf, 3000f);
            AudioSynth.Normalize(buf, 0.5f);
            AudioSynth.SmoothLoopSeam(buf, 0.15f);
            return AudioSynth.ToClip("PROC_SFX_Whirr", buf);
        }

        static AudioClip BuildError()
        {
            var buf = AudioSynth.Buffer(0.55f);
            AudioSynth.AddTone(buf, 0, 233f, 0.16f, 0.28f, Adsr.Percussive(0.15f),
                               WaveKind.Square, null, seed: 95u);
            AudioSynth.AddTone(buf, AudioSynth.Samples(0.15f), 165f, 0.3f, 0.28f,
                               Adsr.Percussive(0.28f), WaveKind.Square, null, seed: 96u);
            AudioSynth.LowPass(buf, 2200f);
            AudioSynth.Normalize(buf, 0.6f);
            AudioSynth.FadeEdges(buf);
            return AudioSynth.ToClip("PROC_SFX_Error", buf);
        }

        static AudioClip BuildClick()
        {
            var buf = AudioSynth.Buffer(0.09f);
            var rng = URandom.CreateFromIndex(999u);
            AudioSynth.AddNoise(buf, 0, 0.012f, 0.5f, Adsr.Percussive(0.011f), 7000f, 1600f, ref rng);
            AudioSynth.AddTone(buf, 0, 1900f, 0.045f, 0.18f, Adsr.Percussive(0.04f),
                               WaveKind.Sine, null, seed: 97u);
            AudioSynth.Normalize(buf, 0.5f);
            AudioSynth.FadeEdges(buf, 0.004f);
            return AudioSynth.ToClip("PROC_SFX_Click", buf);
        }
    }
}
