using System;
using Matsuri.Core;
using Matsuri.Data;
using UnityEngine;

namespace Matsuri.TimeSystem
{
    /// <summary>
    /// 仕様書 §7 / §8。祭りの時計。
    /// 17:00 に始まり 22:00 に終わる。既定では現実の1秒がゲーム内の1分に相当し、
    /// 祭り全体は実時間 5分 で終わる。
    ///
    /// 時刻そのものの計算は <see cref="FestivalClock"/> が持ち、
    /// このクラスは「進める」「イベントを出す」ことだけを担当する (§66)。
    /// 22:00 に到達したときは <see cref="Finished"/> を出すだけで、
    /// 祭りの終了処理そのものは FestivalManager 側が結線する。
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class TimeManager : MonoBehaviour
    {
        [Tooltip("バランス設定。未設定なら GameManager から取得する。")]
        public BalanceConfig Balance;

        [Tooltip("早送り倍率。1 = 等倍。UI のスピードボタンから変更する。")]
        [Min(0f)]
        public float Speed = 1f;

        // ── 夜になる速さ (§8) ────────────────────────────────
        // 17:00 はまだ明るい夕方。19:30 ごろには完全に夜になっている。
        const int NightBeginMinutes = 17 * 60;        // 1020
        const int NightFullMinutes  = 19 * 60 + 30;   // 1170

        FestivalClock _clock = FestivalClock.AtStart;
        bool _running;
        bool _finishedRaised;
        int _lastWholeMinute;
        int _lastHourRaised = -1;

        /// <summary>現在のゲーム内時刻。</summary>
        public FestivalClock Clock => _clock;

        /// <summary>時計が動いているか。</summary>
        public bool IsRunning => _running;

        /// <summary>
        /// 0 = 夕方（17:00）、1 = 真夜中。§8 の「時間帯によって見た目が変わる」を駆動する値。
        /// 17:00 から 19:30 にかけて Smoothstep で 1 に到達する。
        /// </summary>
        public float NightAmount
        {
            get
            {
                float t = (_clock.MinutesOfDay - NightBeginMinutes)
                          / Mathf.Max(1f, NightFullMinutes - NightBeginMinutes);
                return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            }
        }

        /// <summary>祭りの進行度 0(17:00) 〜 1(22:00)。</summary>
        public float Progress => _clock.Normalized;

        /// <summary>ゲーム内で1分進むたび。HUD の時刻表示と §8 の見た目変化はこれを受ける。</summary>
        public event Action<FestivalClock> MinuteTicked;

        /// <summary>17, 18, 19, 20, 21, 22 時になった瞬間。</summary>
        public event Action<int> HourReached;

        /// <summary>22:00 に到達した瞬間。</summary>
        public event Action Finished;

        int StartMinutes => Balance != null ? Balance.StartMinutes : FestivalClock.StartMinutes;
        int EndMinutes   => Balance != null ? Balance.EndMinutes   : FestivalClock.EndMinutes;
        float MinutesPerRealSecond => Balance != null ? Mathf.Max(0.0001f, Balance.MinutesPerRealSecond) : 1f;

        void Awake()
        {
            if (Balance == null && GameManager.Instance != null) Balance = GameManager.Instance.Balance;
            ResetClock();
        }

        void Update()
        {
            if (!_running) return;

            float advance = UnityEngine.Time.deltaTime * MinutesPerRealSecond * Mathf.Max(0f, Speed);
            if (advance <= 0f) return;

            float end = EndMinutes;
            float next = _clock.MinutesOfDay + advance;

            bool reachedEnd = next >= end;
            if (reachedEnd) next = end;

            _clock.MinutesOfDay = next;

            RaiseMinuteEvents();

            if (reachedEnd && !_finishedRaised)
            {
                _finishedRaised = true;
                _running = false;
                MatsuriLog.Always($"祭りが終わりました（{_clock}）。");
                Finished?.Invoke();
            }
        }

        /// <summary>分・時のイベントを、飛ばした分もすべて順に出す（早送り対策）。</summary>
        void RaiseMinuteEvents()
        {
            int whole = Mathf.FloorToInt(_clock.MinutesOfDay);
            if (whole <= _lastWholeMinute) return;

            // 一気に何分も進んだ場合でも、時刻イベントを取りこぼさない。
            // ただし極端な早送りでフレームが詰まらないよう上限を設ける。
            int steps = Mathf.Min(whole - _lastWholeMinute, 120);
            int from = whole - steps;

            for (int m = from + 1; m <= whole; m++)
            {
                if (m % 60 == 0)
                {
                    int hour = (m / 60) % 24;
                    if (hour != _lastHourRaised)
                    {
                        _lastHourRaised = hour;
                        HourReached?.Invoke(hour);
                    }
                }
            }

            _lastWholeMinute = whole;
            MinuteTicked?.Invoke(_clock);
        }

        /// <summary>時計を動かし始める。祭りの開始 (§80)。</summary>
        public void StartClock()
        {
            if (_running) return;

            if (_finishedRaised) ResetClock();

            _running = true;

            // 開始時刻の「時」は開始した瞬間に通知する（17時になった）。
            int hour = _clock.Hour;
            if (hour != _lastHourRaised)
            {
                _lastHourRaised = hour;
                HourReached?.Invoke(hour);
            }

            MinuteTicked?.Invoke(_clock);
            MatsuriLog.Always($"祭りが始まりました（{_clock}）。");
        }

        /// <summary>時計を止める。結果表示の間など。</summary>
        public void StopClock() => _running = false;

        /// <summary>17:00 に巻き戻す。</summary>
        public void ResetClock()
        {
            _running = false;
            _finishedRaised = false;
            _clock = new FestivalClock(StartMinutes);
            _lastWholeMinute = Mathf.FloorToInt(_clock.MinutesOfDay);
            _lastHourRaised = -1;
        }

        /// <summary>デバッグ・テスト用。指定した実時間ぶんだけ強制的に進める。</summary>
        public void AdvanceRealSeconds(float realSeconds)
        {
            if (realSeconds <= 0f) return;
            float end = EndMinutes;
            _clock.MinutesOfDay = Mathf.Min(end, _clock.MinutesOfDay + realSeconds * MinutesPerRealSecond * Mathf.Max(0f, Speed));
            RaiseMinuteEvents();
            if (_clock.MinutesOfDay >= end && !_finishedRaised)
            {
                _finishedRaised = true;
                _running = false;
                Finished?.Invoke();
            }
        }
    }
}
