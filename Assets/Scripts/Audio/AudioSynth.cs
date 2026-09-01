using System;
using UnityEngine;
using URandom = Unity.Mathematics.Random;

namespace Matsuri.Audio
{
    /// <summary>波形の種類 (§24)。</summary>
    public enum WaveKind
    {
        /// <summary>正弦波。篠笛や口笛のような柔らかい音。</summary>
        Sine,
        /// <summary>のこぎり波。倍音が多くブザーやモーター音向き。</summary>
        Saw,
        /// <summary>矩形波。硬い電子音。</summary>
        Square,
        /// <summary>三角波。矩形波より柔らかい。</summary>
        Triangle,
        /// <summary>ホワイトノイズ。打楽器や環境音の素。</summary>
        Noise
    }

    /// <summary>
    /// 仕様書 §24 / §63。音源ファイルを一切持たないため、音はすべて C# で合成する。
    /// ここは合成の「部品」だけを提供する低レベル層。
    /// 実際の祭囃子や効果音の組み立ては <see cref="ProceduralAudioLibrary"/> が行う。
    /// </summary>
    public static class AudioSynth
    {
        /// <summary>サンプリング周波数。</summary>
        public const int SampleRate = 44100;

        const float Tau = 6.28318530718f;

        // ── バッファ ───────────────────────────────────────────

        /// <summary>秒数ぶんの無音バッファを作る。</summary>
        public static float[] Buffer(float seconds)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate));
            return new float[n];
        }

        /// <summary>秒 → サンプル番号。</summary>
        public static int Samples(float seconds) => Mathf.Max(0, Mathf.RoundToInt(seconds * SampleRate));

        /// <summary>
        /// float 配列から AudioClip を作る。モノラル。
        /// loop を true にすると、書き込む前に継ぎ目をならしてつなげる。
        /// （AudioClip 自体にループ設定は無く、鳴らすときは AudioSource.loop を使う。）
        /// </summary>
        public static AudioClip ToClip(string name, float[] samples, bool loop = false)
        {
            if (samples == null || samples.Length == 0) samples = new float[SampleRate / 100];
            if (loop) SmoothLoopSeam(samples);
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ── 基本波形 ───────────────────────────────────────────

        /// <summary>phase は 0〜1 の周期位相。</summary>
        public static float Wave(WaveKind kind, float phase, ref URandom rng)
        {
            phase -= Mathf.Floor(phase);
            switch (kind)
            {
                case WaveKind.Sine: return Mathf.Sin(phase * Tau);
                case WaveKind.Saw: return phase * 2f - 1f;
                case WaveKind.Square: return phase < 0.5f ? 1f : -1f;
                case WaveKind.Triangle: return 4f * Mathf.Abs(phase - 0.5f) - 1f;
                case WaveKind.Noise: return rng.NextFloat(-1f, 1f);
                default: return 0f;
            }
        }

        // ── エンベロープ ───────────────────────────────────────

        /// <summary>ADSR エンベロープ。値はすべて「音の長さに対する秒数」。</summary>
        [Serializable]
        public struct Adsr
        {
            public float Attack;
            public float Decay;
            public float Sustain;   // 0〜1 の音量比
            public float Release;

            public Adsr(float attack, float decay, float sustain, float release)
            {
                Attack = attack; Decay = decay; Sustain = sustain; Release = release;
            }

            /// <summary>撥弦・打楽器向けの「立ち上がり即・減衰のみ」。</summary>
            public static Adsr Percussive(float decay) => new Adsr(0.002f, decay, 0f, 0.01f);

            /// <summary>笛向けの息の入るエンベロープ。</summary>
            public static Adsr Flute => new Adsr(0.06f, 0.10f, 0.75f, 0.18f);

            /// <summary>t 秒目 / 全体 duration 秒 の音量。</summary>
            public float Evaluate(float t, float duration)
            {
                if (t < 0f) return 0f;
                float releaseStart = Mathf.Max(0f, duration - Release);
                if (t >= duration + Release) return 0f;

                if (t < Attack)
                    return Attack <= 0f ? 1f : t / Attack;

                float afterAttack = t - Attack;
                float level;
                if (afterAttack < Decay)
                    level = Decay <= 0f ? Sustain : Mathf.Lerp(1f, Sustain, afterAttack / Decay);
                else
                    level = Sustain;

                if (t > releaseStart && Release > 0f)
                {
                    float k = 1f - (t - releaseStart) / Release;
                    level *= Mathf.Clamp01(k);
                }
                return level;
            }
        }

        // ── 音の追加 ───────────────────────────────────────────

        /// <summary>
        /// 楽音を1つ足す。harmonics に倍音の音量比を渡すと篠笛のような響きになる。
        /// bendSemitones を与えると発音中にピッチが滑る（花火の笛など）。
        /// </summary>
        public static void AddTone(float[] buf, int offset, float freq, float duration, float amp,
                                   Adsr env, WaveKind kind = WaveKind.Sine,
                                   float[] harmonics = null, float bendSemitones = 0f,
                                   float vibratoHz = 0f, float vibratoCents = 0f, uint seed = 1u)
        {
            if (buf == null || freq <= 0f) return;
            var rng = URandom.CreateFromIndex(seed);
            int total = Samples(duration + env.Release);
            float phase = 0f;
            float[] phases = harmonics != null ? new float[harmonics.Length] : null;

            for (int i = 0; i < total; i++)
            {
                int idx = offset + i;
                if (idx < 0) continue;
                if (idx >= buf.Length) break;

                float t = i / (float)SampleRate;
                float e = env.Evaluate(t, duration);
                if (e <= 0f) continue;

                float bend = bendSemitones == 0f ? 1f
                    : Mathf.Pow(2f, bendSemitones * (t / Mathf.Max(0.0001f, duration)) / 12f);
                float vib = vibratoCents == 0f ? 1f
                    : Mathf.Pow(2f, vibratoCents * Mathf.Sin(t * vibratoHz * Tau) / 1200f);
                float f = freq * bend * vib;

                phase += f / SampleRate;
                float v = Wave(kind, phase, ref rng);

                if (phases != null)
                {
                    for (int h = 0; h < phases.Length; h++)
                    {
                        if (harmonics[h] == 0f) continue;
                        phases[h] += f * (h + 2) / SampleRate;
                        v += Wave(kind, phases[h], ref rng) * harmonics[h];
                    }
                }

                buf[idx] += v * e * amp;
            }
        }

        /// <summary>ノイズバースト。太鼓・破裂音・調理音の素になる。</summary>
        public static void AddNoise(float[] buf, int offset, float duration, float amp, Adsr env,
                                    float lowpassHz, float highpassHz, ref URandom rng)
        {
            if (buf == null) return;
            int total = Samples(duration + env.Release);
            if (total <= 0) return;

            var tmp = new float[total];
            for (int i = 0; i < total; i++) tmp[i] = rng.NextFloat(-1f, 1f);
            if (highpassHz > 0f) HighPass(tmp, highpassHz);
            if (lowpassHz > 0f) LowPass(tmp, lowpassHz);

            for (int i = 0; i < total; i++)
            {
                int idx = offset + i;
                if (idx < 0) continue;
                if (idx >= buf.Length) break;
                float t = i / (float)SampleRate;
                buf[idx] += tmp[i] * env.Evaluate(t, duration) * amp;
            }
        }

        /// <summary>別バッファを重ねる。</summary>
        public static void MixInto(float[] dst, float[] src, int offset, float gain)
        {
            if (dst == null || src == null) return;
            for (int i = 0; i < src.Length; i++)
            {
                int idx = offset + i;
                if (idx < 0) continue;
                if (idx >= dst.Length) break;
                dst[idx] += src[i] * gain;
            }
        }

        // ── フィルタ ───────────────────────────────────────────

        /// <summary>一次ローパス。</summary>
        public static void LowPass(float[] buf, float cutoffHz)
        {
            if (buf == null || cutoffHz <= 0f) return;
            float a = 1f - Mathf.Exp(-Tau * cutoffHz / SampleRate);
            a = Mathf.Clamp01(a);
            float y = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                y += a * (buf[i] - y);
                buf[i] = y;
            }
        }

        /// <summary>一次ハイパス。</summary>
        public static void HighPass(float[] buf, float cutoffHz)
        {
            if (buf == null || cutoffHz <= 0f) return;
            float rc = 1f / (Tau * cutoffHz);
            float dt = 1f / SampleRate;
            float a = rc / (rc + dt);
            float prevIn = buf.Length > 0 ? buf[0] : 0f;
            float prevOut = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float x = buf[i];
                prevOut = a * (prevOut + x - prevIn);
                prevIn = x;
                buf[i] = prevOut;
            }
        }

        /// <summary>状態変数フィルタによるバンドパス。セミや人声の帯域づくりに使う。</summary>
        public static void BandPass(float[] buf, float centerHz, float q)
        {
            if (buf == null || centerHz <= 0f) return;
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(centerHz, SampleRate * 0.45f) / SampleRate);
            float damp = Mathf.Clamp(1f / Mathf.Max(0.5f, q), 0.02f, 1.9f);
            float low = 0f, band = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float high = buf[i] - low - damp * band;
                band += f * high;
                low += f * band;
                buf[i] = band;
            }
        }

        /// <summary>ディレイ（山びこ）。花火の残響などに使う。</summary>
        public static void Delay(float[] buf, float timeSec, float feedback, float mix)
        {
            if (buf == null) return;
            int d = Samples(timeSec);
            if (d <= 0 || d >= buf.Length) return;
            for (int i = d; i < buf.Length; i++)
                buf[i] += buf[i - d] * feedback * mix;
        }

        // ── 仕上げ ─────────────────────────────────────────────

        /// <summary>ピーク値を peak に揃える。</summary>
        public static void Normalize(float[] buf, float peak = 0.9f)
        {
            if (buf == null || buf.Length == 0) return;
            float max = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float a = Mathf.Abs(buf[i]);
                if (a > max) max = a;
            }
            if (max < 1e-6f) return;
            float g = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        /// <summary>digitalクリップを避けるためのソフトクリップ。</summary>
        public static void SoftClip(float[] buf)
        {
            if (buf == null) return;
            for (int i = 0; i < buf.Length; i++)
                buf[i] = (float)Math.Tanh(buf[i]);
        }

        /// <summary>先頭と末尾に短いフェードを入れる（ぷつっという音を消す）。</summary>
        public static void FadeEdges(float[] buf, float seconds = 0.01f)
        {
            if (buf == null) return;
            int n = Mathf.Min(Samples(seconds), buf.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                buf[i] *= k;
                buf[buf.Length - 1 - i] *= k;
            }
        }

        /// <summary>
        /// ループの継ぎ目を消す。末尾 seconds ぶんを先頭にクロスフェードで畳み込み、
        /// バッファ長は変えずに「頭と尻が繋がる」ようにする。
        /// </summary>
        public static void SmoothLoopSeam(float[] buf, float seconds = 0.35f)
        {
            if (buf == null) return;
            int n = Mathf.Min(Samples(seconds), buf.Length / 3);
            if (n <= 1) return;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;              // 0→1
                int tail = buf.Length - n + i;
                float mixed = buf[i] * k + buf[tail] * (1f - k);
                buf[i] = mixed;
            }
            // 末尾はフェードアウトさせて、頭に畳み込んだぶんの二重再生を避ける。
            for (int i = 0; i < n; i++)
            {
                float k = 1f - i / (float)n;
                buf[buf.Length - n + i] *= k;
            }
        }

        /// <summary>ピンクノイズ発生器（Paul Kellet 近似）。ざわめきや風に使う。</summary>
        public struct PinkNoise
        {
            float _b0, _b1, _b2, _b3, _b4, _b5, _b6;

            public float Next(ref URandom rng)
            {
                float white = rng.NextFloat(-1f, 1f);
                _b0 = 0.99886f * _b0 + white * 0.0555179f;
                _b1 = 0.99332f * _b1 + white * 0.0750759f;
                _b2 = 0.96900f * _b2 + white * 0.1538520f;
                _b3 = 0.86650f * _b3 + white * 0.3104856f;
                _b4 = 0.55000f * _b4 + white * 0.5329522f;
                _b5 = -0.7616f * _b5 - white * 0.0168980f;
                float pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362f;
                _b6 = white * 0.115926f;
                return pink * 0.11f;
            }
        }
    }
}
