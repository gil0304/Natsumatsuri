using UnityEngine;
using URandom = Unity.Mathematics.Random;
using Adsr = Matsuri.Audio.AudioSynth.Adsr;

namespace Matsuri.Audio
{
    /// <summary>
    /// 仕様書 §24 / §63。祭囃子と環境音（ざわめき・セミ・虫の音）の合成部。
    /// 効果音は ProceduralAudioLibrary.cs 側にある（同じクラスを分割している）。
    /// どれも数十秒のループ素材で、AudioSource.loop で鳴らし続ける前提。
    /// </summary>
    public static partial class ProceduralAudioLibrary
    {
        // ── 祭囃子 (§24) ───────────────────────────────────────

        static AudioClip BuildFestivalBgm()
        {
            const float Bpm = 100f;
            float eighth = 30f / Bpm;                 // 8分音符 = 0.3秒
            const int TotalEighths = 80;              // 24秒
            float length = TotalEighths * eighth;

            var buf = AudioSynth.Buffer(length);
            var rng = URandom.CreateFromIndex(20240817u);

            // 篠笛のメロディ。(音階index, 8分音符いくつ分)。-1 は休符。
            int[,] melody =
            {
                {0,2},{2,2},{3,2},{2,1},{1,1},{0,4},{-1,4},
                {4,2},{3,1},{2,1},{3,2},{4,2},{5,4},{-1,4},
                {5,2},{4,2},{3,2},{2,2},{1,2},{0,2},{-1,4},
                {0,1},{1,1},{2,2},{3,2},{2,2},{0,4},{-1,4},
                {0,2},{2,2},{3,2},{2,1},{1,1},{0,4},{-1,4}
            };

            // 篠笛は基音＋倍音。息のゆらぎ（ビブラート）を必ず入れる。
            float[] fluteHarmonics = { 0.42f, 0.22f, 0.11f, 0.05f };
            int cursor = 0;
            for (int i = 0; i < melody.GetLength(0); i++)
            {
                int deg = melody[i, 0];
                int len = melody[i, 1];
                if (deg >= 0)
                {
                    float dur = len * eighth * 0.92f;
                    int off = AudioSynth.Samples(cursor * eighth);
                    AudioSynth.AddTone(buf, off, Note(deg), dur, 0.22f, Adsr.Flute,
                                       WaveKind.Sine, fluteHarmonics,
                                       vibratoHz: 5.2f, vibratoCents: 28f, seed: (uint)(i + 7));
                    // 息の音を薄く重ねる
                    var breath = URandom.CreateFromIndex((uint)(i * 31 + 3));
                    AudioSynth.AddNoise(buf, off, dur, 0.020f,
                                        new Adsr(0.05f, 0.2f, 0.5f, 0.1f),
                                        Note(deg) * 3.4f, Note(deg) * 1.6f, ref breath);
                }
                cursor += len;
            }

            // 太鼓。4拍子の1拍目と3拍目、4小節ごとに「ドコドン」の締め。
            float beat = eighth * 2f;
            int beats = TotalEighths / 2;
            for (int b = 0; b < beats; b++)
            {
                int inBar = b % 4;
                bool strong = inBar == 0;
                if (inBar == 0 || inBar == 2)
                    AddTaiko(buf, AudioSynth.Samples(b * beat), strong ? 0.85f : 0.55f, ref rng);

                if (b % 16 == 15)
                {
                    AddTaiko(buf, AudioSynth.Samples(b * beat + eighth * 0.5f), 0.45f, ref rng);
                    AddTaiko(buf, AudioSynth.Samples(b * beat + eighth * 1.0f), 0.5f, ref rng);
                    AddTaiko(buf, AudioSynth.Samples(b * beat + eighth * 1.5f), 0.75f, ref rng);
                }
            }

            // 鉦（チャンチキ）。裏拍で鳴らす。
            for (int e = 1; e < TotalEighths; e += 2)
                AddKane(buf, AudioSynth.Samples(e * eighth), e % 4 == 1 ? 0.16f : 0.11f);

            AudioSynth.SoftClip(buf);
            AudioSynth.Normalize(buf, 0.82f);
            AudioSynth.SmoothLoopSeam(buf, 0.4f);
            return AudioSynth.ToClip("PROC_FestivalBgm", buf);
        }

        /// <summary>太鼓の一打。低い胴の共鳴＋皮の打撃ノイズ。</summary>
        static void AddTaiko(float[] buf, int offset, float amp, ref URandom rng)
        {
            // 胴鳴り：ピッチが少し下がる低音
            AudioSynth.AddTone(buf, offset, 82f, 0.45f, amp * 0.9f,
                               new Adsr(0.002f, 0.42f, 0f, 0.05f), WaveKind.Sine,
                               null, bendSemitones: -5f, seed: rng.NextUInt(1u, 99999u));
            AudioSynth.AddTone(buf, offset, 128f, 0.22f, amp * 0.35f,
                               Adsr.Percussive(0.2f), WaveKind.Sine,
                               null, bendSemitones: -7f, seed: rng.NextUInt(1u, 99999u));
            // 皮の打撃
            AudioSynth.AddNoise(buf, offset, 0.09f, amp * 0.5f,
                                Adsr.Percussive(0.08f), 2600f, 220f, ref rng);
        }

        /// <summary>鉦。非整数倍音の金属音。</summary>
        static void AddKane(float[] buf, int offset, float amp)
        {
            float[] partials = { 2180f, 3140f, 4370f, 5810f };
            float[] gains = { 1f, 0.62f, 0.38f, 0.2f };
            for (int i = 0; i < partials.Length; i++)
                AudioSynth.AddTone(buf, offset, partials[i], 0.26f, amp * gains[i],
                                   Adsr.Percussive(0.24f), WaveKind.Sine, null, seed: (uint)(i + 91));
        }

        // ── ざわめき (§63) ─────────────────────────────────────

        static AudioClip BuildCrowd()
        {
            const float Length = 20f;
            var buf = AudioSynth.Buffer(Length);
            var rng = URandom.CreateFromIndex(553311u);

            // 土台：ピンクノイズを人声帯域に寄せ、ゆっくり揺らす
            var bed = AudioSynth.Buffer(Length);
            var pink = new AudioSynth.PinkNoise();
            for (int i = 0; i < bed.Length; i++) bed[i] = pink.Next(ref rng);
            AudioSynth.BandPass(bed, 520f, 0.9f);
            for (int i = 0; i < bed.Length; i++)
            {
                float t = i / (float)AudioSynth.SampleRate;
                float lfo = 0.72f
                          + 0.16f * Mathf.Sin(t * 0.11f * 6.2831853f)
                          + 0.12f * Mathf.Sin(t * 0.29f * 6.2831853f + 1.7f);
                bed[i] *= lfo;
            }
            AudioSynth.MixInto(buf, bed, 0, 1.0f);

            // 個々の話し声の塊。フォルマントっぽい帯域で短く鳴らす。
            for (int v = 0; v < 90; v++)
            {
                float start = rng.NextFloat(0f, Length - 1.4f);
                float dur = rng.NextFloat(0.28f, 1.1f);
                float center = rng.NextFloat(260f, 1150f);
                var blob = AudioSynth.Buffer(dur + 0.2f);
                for (int i = 0; i < blob.Length; i++) blob[i] = rng.NextFloat(-1f, 1f);
                AudioSynth.BandPass(blob, center, 5.5f);
                // 抑揚
                for (int i = 0; i < blob.Length; i++)
                {
                    float t = i / (float)AudioSynth.SampleRate;
                    float e = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                    blob[i] *= e * (0.7f + 0.3f * Mathf.Sin(t * rng.NextFloat(4f, 11f) * 6.2831853f));
                }
                AudioSynth.MixInto(buf, blob, AudioSynth.Samples(start), rng.NextFloat(0.10f, 0.32f));
            }

            // 遠くの笑い声を数回
            for (int l = 0; l < 6; l++)
            {
                float start = rng.NextFloat(0f, Length - 1f);
                var laugh = AudioSynth.Buffer(0.75f);
                for (int i = 0; i < laugh.Length; i++)
                {
                    float t = i / (float)AudioSynth.SampleRate;
                    float am = Mathf.Max(0f, Mathf.Sin(t * 9f * 6.2831853f));
                    laugh[i] = rng.NextFloat(-1f, 1f) * am * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.75f));
                }
                AudioSynth.BandPass(laugh, rng.NextFloat(420f, 780f), 4f);
                AudioSynth.MixInto(buf, laugh, AudioSynth.Samples(start), 0.28f);
            }

            AudioSynth.LowPass(buf, 4200f);
            AudioSynth.HighPass(buf, 90f);
            AudioSynth.SoftClip(buf);
            AudioSynth.Normalize(buf, 0.55f);
            AudioSynth.SmoothLoopSeam(buf, 0.6f);
            return AudioSynth.ToClip("PROC_CrowdAmbience", buf);
        }

        // ── セミ・虫 (§63) ─────────────────────────────────────

        static AudioClip BuildCicada()
        {
            const float Length = 12f;
            var buf = AudioSynth.Buffer(Length);
            var rng = URandom.CreateFromIndex(778899u);

            // ヒグラシ：カナカナカナ…。3匹を少しずらして鳴かせる。
            for (int individual = 0; individual < 3; individual++)
            {
                float baseFreq = rng.NextFloat(3200f, 4300f);
                float pulseHz = rng.NextFloat(19f, 25f);
                float t = rng.NextFloat(0f, 3f);
                while (t < Length - 0.5f)
                {
                    float phrase = rng.NextFloat(2.0f, 3.2f);
                    int pulses = Mathf.Max(4, Mathf.RoundToInt(phrase * pulseHz));
                    for (int p = 0; p < pulses; p++)
                    {
                        float pt = t + p / pulseHz;
                        if (pt >= Length) break;
                        float k = p / (float)pulses;
                        // 鳴きの終わりに向かってピッチと音量が落ちる
                        float f = baseFreq * (1f - 0.28f * k);
                        float amp = 0.22f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.05f)) + 0.03f;
                        int off = AudioSynth.Samples(pt);
                        AudioSynth.AddTone(buf, off, f, 0.018f, amp, Adsr.Percussive(0.016f),
                                           WaveKind.Triangle, null, seed: rng.NextUInt(1u, 99999u));
                        AudioSynth.AddNoise(buf, off, 0.020f, amp * 0.6f, Adsr.Percussive(0.018f),
                                            f * 1.5f, f * 0.6f, ref rng);
                    }
                    t += phrase + rng.NextFloat(0.6f, 1.8f);
                }
            }

            AudioSynth.HighPass(buf, 1200f);
            AudioSynth.SoftClip(buf);
            AudioSynth.Normalize(buf, 0.42f);
            AudioSynth.SmoothLoopSeam(buf, 0.3f);
            return AudioSynth.ToClip("PROC_CicadaAmbience", buf);
        }

        static AudioClip BuildNightInsects()
        {
            const float Length = 12f;
            var buf = AudioSynth.Buffer(Length);
            var rng = URandom.CreateFromIndex(112358u);

            // 鈴虫：リーン、リーン。細かいトレモロのかかった高い音。
            for (int individual = 0; individual < 4; individual++)
            {
                float baseFreq = rng.NextFloat(4200f, 5200f);
                float tremolo = rng.NextFloat(26f, 38f);
                float t = rng.NextFloat(0f, 2.5f);
                while (t < Length - 0.4f)
                {
                    float dur = rng.NextFloat(0.28f, 0.5f);
                    var chirp = AudioSynth.Buffer(dur);
                    for (int i = 0; i < chirp.Length; i++)
                    {
                        float lt = i / (float)AudioSynth.SampleRate;
                        float am = 0.5f + 0.5f * Mathf.Sin(lt * tremolo * 6.2831853f);
                        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(lt / dur));
                        chirp[i] = Mathf.Sin(lt * baseFreq * 6.2831853f) * am * env;
                    }
                    AudioSynth.MixInto(buf, chirp, AudioSynth.Samples(t), rng.NextFloat(0.10f, 0.22f));
                    t += dur + rng.NextFloat(0.18f, 0.55f);
                }
            }

            // 遠くのクツワムシ的な地の音
            var bedRng = URandom.CreateFromIndex(4242u);
            var bed = AudioSynth.Buffer(Length);
            for (int i = 0; i < bed.Length; i++) bed[i] = bedRng.NextFloat(-1f, 1f);
            AudioSynth.BandPass(bed, 6400f, 3f);
            for (int i = 0; i < bed.Length; i++)
            {
                float t = i / (float)AudioSynth.SampleRate;
                bed[i] *= 0.5f + 0.5f * Mathf.Sin(t * 42f * 6.2831853f);
            }
            AudioSynth.MixInto(buf, bed, 0, 0.06f);

            AudioSynth.SoftClip(buf);
            AudioSynth.Normalize(buf, 0.34f);
            AudioSynth.SmoothLoopSeam(buf, 0.3f);
            return AudioSynth.ToClip("PROC_NightInsects", buf);
        }
    }
}
