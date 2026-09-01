using System;
using Matsuri.Core;
using Matsuri.Save;
using Matsuri.Script.Commands;
using Matsuri.Stalls;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 仕様書 §14 〜 §17 / §35 / §36。
    /// <see cref="IFestivalMetrics"/> の実装と、結果画面用のデータ組み立て。
    ///
    /// 値そのものは各マネージャが持っている。ここは「集めて渡す」だけにして、
    /// 集計ロジックを一箇所にまとめる (§66)。
    /// </summary>
    public sealed partial class FestivalManager
    {
        // ────────────────────────────────────────────────────
        // IFestivalMetrics
        // ────────────────────────────────────────────────────

        /// <summary>累計来場者数 (§14 「もし 来場者数 > 500」が読む値)。</summary>
        public int VisitorCount
        {
            get
            {
                var v = VisitorsManager;
                return v != null ? v.TotalVisitors : 0;
            }
        }

        /// <summary>いま会場にいる人数。</summary>
        public int CurrentVisitorCount
        {
            get
            {
                var v = VisitorsManager;
                return v != null ? v.CurrentVisitors : 0;
            }
        }

        /// <summary>売上 (§16)。</summary>
        public long Revenue
        {
            get
            {
                var e = Economy;
                return e != null ? e.Revenue : 0L;
            }
        }

        /// <summary>残り予算 (§31)。</summary>
        public long Budget
        {
            get
            {
                var e = Economy;
                return e != null ? e.Budget : 0L;
            }
        }

        /// <summary>平均満足度 0〜1 (§34)。</summary>
        public float AverageSatisfaction
        {
            get
            {
                var v = VisitorsManager;
                return v != null ? NormalizeSatisfaction(v.AverageSatisfaction) : 0f;
            }
        }

        /// <summary>ゲーム内時刻を「その日の分」で表す。17:00 = 1020。</summary>
        public int MinutesOfDay
        {
            get
            {
                var t = Clock;
                return t != null ? Mathf.RoundToInt(t.Clock.MinutesOfDay) : 17 * 60;
            }
        }

        public int GetQueueLength(string stallId)
        {
            var s = StallsManager;
            return s != null && !string.IsNullOrEmpty(stallId) ? s.GetQueueLength(stallId) : 0;
        }

        public long GetStallRevenue(string stallId)
        {
            var s = StallsManager;
            if (s != null && !string.IsNullOrEmpty(stallId)) return s.GetRevenue(stallId);

            var e = Economy;
            return e != null ? e.GetStallRevenue(stallId) : 0L;
        }

        public int GetStallCount(string stallId)
        {
            var s = StallsManager;
            return s != null && !string.IsNullOrEmpty(stallId) ? s.GetCount(stallId) : 0;
        }

        /// <summary>
        /// 満足度の尺度を 0〜1 に揃える。
        /// NPC 側は 0〜100 で持っているので、100 尺度で来ても正しく扱えるようにする。
        /// </summary>
        static float NormalizeSatisfaction(float raw)
        {
            if (float.IsNaN(raw)) return 0f;
            float value = raw > 1.5f ? raw / 100f : raw;
            return Mathf.Clamp01(value);
        }

        // ────────────────────────────────────────────────────
        // 会場の魅力 (§33) — 来場者数の計算に使う
        // ────────────────────────────────────────────────────

        /// <summary>
        /// 祭りの魅力。屋台の数と種類、装飾の存在から決まる。
        /// VisitorManager が来場ペースに掛けて使う。
        /// </summary>
        public float Attraction
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _built.Count; i++)
                {
                    var obj = _built[i];
                    if (obj == null) continue;

                    switch (obj.Kind)
                    {
                        case FestivalObjectKind.Stall: total += 3f; break;
                        case FestivalObjectKind.Facility: total += 0.5f; break;
                        case FestivalObjectKind.Decoration:
                            total += obj is Decoration d ? d.AttractionValue : 1f;
                            break;
                    }
                }

                var stalls = StallsManager;
                if (stalls != null) total += stalls.DistinctStallKinds * 2.5f;

                return total;
            }
        }

        // ────────────────────────────────────────────────────
        // 結果 (§35 / §36)
        // ────────────────────────────────────────────────────

        /// <summary>結果画面に出すデータを組み立て、スコア計算とランキング登録まで行う。</summary>
        public FestivalResult BuildResult()
        {
            var visitors = VisitorsManager;
            var stalls = StallsManager;
            var economy = Economy;

            Stall top = FindTopStall();

            var result = new FestivalResult
            {
                FestivalName = _plan != null && !string.IsNullOrEmpty(_plan.FestivalName) ? _plan.FestivalName : "MY MATSURI",
                Revenue = economy != null ? economy.Revenue : 0L,
                VisitorCount = visitors != null ? visitors.TotalVisitors : 0,
                AverageSatisfaction = AverageSatisfaction,
                PeakConcurrent = visitors != null ? visitors.PeakVisitors : 0,
                StallKindsUsed = stalls != null ? stalls.DistinctStallKinds : 0,
                TopStallName = top != null && top.Data != null ? top.Data.DisplayName : "—",
                TopStallRevenue = top != null && top.Data != null ? GetStallRevenue(top.Data.Id) : 0L,
                CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                SourceCode = Game != null && Game.Script != null ? Game.Script.CurrentSource : string.Empty,
                ModeName = ModeLabel(Game != null ? Game.Mode : GameMode.Free)
            };

            try
            {
                result.TotalScore = ScoreRules.CalculateTotal(result, Balance);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"スコアの計算に失敗しました: {e.Message}");
                result.TotalScore = result.Revenue;
            }

            try
            {
                var backend = RankingService.Current;
                if (backend != null) backend.Submit(result);
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"ランキングへの登録に失敗しました: {e.Message}");
            }

            MatsuriLog.Always(
                $"結果: 売上 ¥{result.Revenue:N0} / 来場 {result.VisitorCount}人 / " +
                $"平均満足度 {result.AverageSatisfaction * 100f:0}% / スコア {result.TotalScore:N0}");

            return result;
        }

        /// <summary>売上が一番大きい屋台。売上が無ければ人気度で決める (§36 の「人気No.1」)。</summary>
        Stall FindTopStall()
        {
            var stalls = StallsManager;
            if (stalls == null) return null;

            var list = stalls.Stalls;
            if (list == null || list.Count == 0) return null;

            Stall best = null;
            long bestRevenue = -1;

            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null) continue;

                if (s.Revenue > bestRevenue)
                {
                    bestRevenue = s.Revenue;
                    best = s;
                }
            }

            if (best != null && bestRevenue > 0) return best;
            return stalls.MostPopular != null ? stalls.MostPopular : best;
        }

        static string ModeLabel(GameMode mode) => mode switch
        {
            GameMode.Challenge => "CHALLENGE MODE",
            GameMode.Battle => "BATTLE MODE",
            _ => "FREE MODE"
        };
    }
}
