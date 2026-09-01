using System;
using System.Text;
using UnityEngine;

namespace Matsuri.Core
{
    /// <summary>
    /// 仕様書 §56 の「NPC 300人で 60fps、最終目標 1000人」を、勘ではなく数字で確認するための計測器。
    ///
    /// 設計方針:
    ///  - **計測そのものが重くならないこと**。毎フレーム呼ばれるので、
    ///    List への追加も文字列生成も行わない。フレーム時間はヒストグラムに数えるだけ。
    ///  - **GC を出さないこと** (§57)。バッファはすべて静的に確保済みで、
    ///    計測中に new は一切起きない。文字列を作るのは EndSample の後の Format だけ。
    ///  - 静的クラスなので、シーンに何も置かなくても、どこからでも計測を開始できる。
    ///
    /// 使い方:
    /// <code>
    ///   MatsuriPerformance.BeginSample();
    ///   // …毎フレーム MatsuriPerformance.Tick(Time.unscaledDeltaTime, 人数);
    ///   var report = MatsuriPerformance.EndSample();
    ///   Debug.Log(MatsuriPerformance.Format(report));
    /// </code>
    /// </summary>
    public static class MatsuriPerformance
    {
        /// <summary>計測結果のログにつく接頭辞。バッチ実行の結果はこれで grep する。</summary>
        public const string Prefix = "[MATSURI-PERF]";

        // フレーム時間のヒストグラム。0.25ms 刻みで 0〜256ms、最後の1つは溢れ用。
        // 全フレームを配列に貯めると 1000人×数分で数十万要素になるので、
        // 分布だけを数える方式にして固定メモリに収める。P95 の分解能は 0.25ms。
        const int BinCount = 1024;
        const float BinMilliseconds = 0.25f;
        const float MaxBinnedMilliseconds = BinCount * BinMilliseconds;

        static readonly int[] s_Bins = new int[BinCount + 1];
        static readonly StringBuilder s_Text = new StringBuilder(256);

        static bool s_Sampling;
        static int s_WarmupLeft;
        static int s_Frames;
        static double s_TotalMilliseconds;
        static float s_MinMilliseconds;
        static float s_MaxMilliseconds;
        static int s_PeakVisitors;
        static long s_PeakManagedBytes;
        static int s_MemoryCountdown;
        static PerfReport s_Last;

        /// <summary>管理メモリを見に行く間隔（フレーム）。毎フレーム見るほどの精度は要らない。</summary>
        const int MemorySampleInterval = 15;

        /// <summary>計測結果 (§56)。</summary>
        public struct PerfReport
        {
            /// <summary>計測したフレーム数。</summary>
            public int Frames;

            /// <summary>平均フレームレート。</summary>
            public float AverageFps;

            /// <summary>最悪フレームのフレームレート。カクつきの大きさを表す。</summary>
            public float MinFps;

            /// <summary>フレーム時間の95パーセンタイル（ミリ秒）。平均より体感に近い。</summary>
            public float P95FrameMs;

            /// <summary>最悪のフレーム時間（ミリ秒）。</summary>
            public float MaxFrameMs;

            /// <summary>計測中の最大同時来場者数。</summary>
            public int PeakVisitors;

            /// <summary>計測中の管理メモリのピーク（バイト）。</summary>
            public long PeakManagedMemoryBytes;
        }

        /// <summary>いま計測中か。</summary>
        public static bool IsSampling => s_Sampling;

        /// <summary>直近の EndSample の結果。</summary>
        public static PerfReport LastReport => s_Last;

        // ================================================================
        // 計測
        // ================================================================

        /// <summary>計測を開始する。前の計測結果は破棄される。</summary>
        public static void BeginSample() => BeginSample(1);

        /// <summary>
        /// 計測を開始する。
        /// </summary>
        /// <param name="warmupFrames">
        /// 最初の数フレームを捨てる。計測開始のフレームは
        /// シーン構築やシェーダのコンパイルを含んでいて代表値にならないため。
        /// </param>
        public static void BeginSample(int warmupFrames)
        {
            Array.Clear(s_Bins, 0, s_Bins.Length);
            s_Sampling = true;
            s_WarmupLeft = Mathf.Max(0, warmupFrames);
            s_Frames = 0;
            s_TotalMilliseconds = 0.0;
            s_MinMilliseconds = float.MaxValue;
            s_MaxMilliseconds = 0f;
            s_PeakVisitors = 0;
            s_PeakManagedBytes = 0;
            s_MemoryCountdown = 0;
        }

        /// <summary>毎フレーム呼ぶ。引数は必ず「実時間の」デルタタイム。</summary>
        public static void Tick(float unscaledDeltaTime) => Tick(unscaledDeltaTime, -1);

        /// <summary>毎フレーム呼ぶ。人数も一緒に渡せる版。</summary>
        public static void Tick(float unscaledDeltaTime, int visitorCount)
        {
            if (!s_Sampling) return;

            if (visitorCount > s_PeakVisitors) s_PeakVisitors = visitorCount;

            if (s_WarmupLeft > 0) { s_WarmupLeft--; return; }
            if (unscaledDeltaTime <= 0f) return;

            float ms = unscaledDeltaTime * 1000f;
            s_Frames++;
            s_TotalMilliseconds += ms;
            if (ms < s_MinMilliseconds) s_MinMilliseconds = ms;
            if (ms > s_MaxMilliseconds) s_MaxMilliseconds = ms;

            int bin = ms >= MaxBinnedMilliseconds ? BinCount : (int)(ms / BinMilliseconds);
            if (bin < 0) bin = 0;
            s_Bins[bin]++;

            if (--s_MemoryCountdown <= 0)
            {
                s_MemoryCountdown = MemorySampleInterval;
                long managed = GC.GetTotalMemory(false);
                if (managed > s_PeakManagedBytes) s_PeakManagedBytes = managed;
            }
        }

        /// <summary>人数だけを報告する。Tick とは別の場所から人数を渡したいとき用。</summary>
        public static void ReportVisitorCount(int visitorCount)
        {
            if (!s_Sampling) return;
            if (visitorCount > s_PeakVisitors) s_PeakVisitors = visitorCount;
        }

        /// <summary>計測を終了して結果を返す。</summary>
        public static PerfReport EndSample()
        {
            s_Sampling = false;

            var report = new PerfReport
            {
                Frames = s_Frames,
                PeakVisitors = s_PeakVisitors,
                PeakManagedMemoryBytes = s_PeakManagedBytes
            };

            if (s_Frames <= 0)
            {
                s_Last = report;
                return report;
            }

            float averageMs = (float)(s_TotalMilliseconds / s_Frames);
            report.AverageFps = averageMs > 0f ? 1000f / averageMs : 0f;
            report.MaxFrameMs = s_MaxMilliseconds;
            report.MinFps = s_MaxMilliseconds > 0f ? 1000f / s_MaxMilliseconds : 0f;
            report.P95FrameMs = Percentile(0.95f);

            s_Last = report;
            return report;
        }

        /// <summary>ヒストグラムから百分位を求める。溢れ分は最悪値で代表させる。</summary>
        static float Percentile(float ratio)
        {
            if (s_Frames <= 0) return 0f;

            int want = Mathf.CeilToInt(s_Frames * Mathf.Clamp01(ratio));
            if (want < 1) want = 1;

            int accumulated = 0;
            for (int i = 0; i < BinCount; i++)
            {
                accumulated += s_Bins[i];
                if (accumulated >= want) return (i + 1) * BinMilliseconds;
            }
            return s_MaxMilliseconds;
        }

        // ================================================================
        // 出力
        // ================================================================

        /// <summary>結果を1行の文字列にする。grep しやすいよう接頭辞つき・1行で出す。</summary>
        public static string Format(in PerfReport r) => Format(in r, null);

        /// <summary>結果を1行の文字列にする。label は「1000人」などの計測条件。</summary>
        public static string Format(in PerfReport r, string label)
        {
            s_Text.Clear();
            s_Text.Append(Prefix).Append(' ');
            if (!string.IsNullOrEmpty(label)) s_Text.Append(label).Append(' ');

            s_Text.Append("frames=").Append(r.Frames)
                  .Append(" avgFps=").Append(r.AverageFps.ToString("F2"))
                  .Append(" minFps=").Append(r.MinFps.ToString("F2"))
                  .Append(" p95Ms=").Append(r.P95FrameMs.ToString("F2"))
                  .Append(" maxMs=").Append(r.MaxFrameMs.ToString("F2"))
                  .Append(" peakVisitors=").Append(r.PeakVisitors)
                  .Append(" peakManagedMB=")
                  .Append((r.PeakManagedMemoryBytes / (1024.0 * 1024.0)).ToString("F1"));

            return s_Text.ToString();
        }

        /// <summary>結果をそのままログへ流す。テストから使う。</summary>
        public static void LogReport(in PerfReport r, string label)
        {
            Debug.Log(Format(in r, label));
        }
    }
}
