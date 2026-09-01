using System;
using System.Collections;
using Matsuri.Core;
using Matsuri.Script.Commands;
using Matsuri.Stalls;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 仕様書 §36 / §80。祭りの一生（開始 → 開催中 → 終了 → 結果 → リセット）と、
    /// 開催中に評価されるトリガールール (§14 〜 §17)。
    /// </summary>
    public sealed partial class FestivalManager
    {
        /// <summary>ルールを評価する間隔（実時間・秒）。毎フレームやる必要はない (§57)。</summary>
        const float RuleEvaluationInterval = 0.2f;

        /// <summary>祭り終了後、客が帰りきるのを待つ最大時間（実時間・秒）。</summary>
        const float MaxCloseWaitSeconds = 25f;

        // ────────────────────────────────────────────────────
        // 開始 (§80)
        // ────────────────────────────────────────────────────

        /// <summary>祭りを開始する。§80「灯りが一斉につく」瞬間。</summary>
        public void StartFestival()
        {
            if (Game != null && Game.Phase == GamePhase.Running) return;

            if (_built.Count == 0)
            {
                ReportRuntimeMessage("まだ何も建っていません。先にコードを実行してください。",
                    Matsuri.Script.DiagnosticSeverity.Warning);
                return;
            }

            Subscribe();
            _closing = false;
            _ruleTimer = 0f;

            if (_plan != null) _plan.ResetRules();

            if (Game != null) Game.SetPhase(GamePhase.Running);

            var time = Clock;
            if (time != null)
            {
                time.ResetClock();
                time.StartClock();
            }

            PrepareVisitorGates();

            var audio = Game != null ? Game.Audio : null;
            if (audio != null)
            {
                audio.StartAmbience();
                audio.PlayBgm();
            }

            // 建った物すべての灯りが一斉につく (§80)。
            for (int i = 0; i < _built.Count; i++)
                if (_built[i] != null) _built[i].OnFestivalStart();

            var cameras = Game != null ? Game.Cameras : null;
            if (cameras != null) cameras.PlayFestivalOpening();

            MatsuriLog.Always("祭りを開催しました。");
        }

        /// <summary>入り口・出口が建てられていなければ、会場の南端を使う。</summary>
        void PrepareVisitorGates()
        {
            var visitors = VisitorsManager;
            if (visitors == null) return;

            if (visitors.EntrancePosition == Vector3.zero) visitors.EntrancePosition = _placement.SouthGate;
            if (visitors.ExitPosition == Vector3.zero) visitors.ExitPosition = visitors.EntrancePosition;

            visitors.BeginArrivals();
        }

        // ────────────────────────────────────────────────────
        // 終了 (§36)
        // ────────────────────────────────────────────────────

        void OnClockFinished() => EndFestival();

        /// <summary>22:00。店じまいして、客が帰りきったら結果画面へ。</summary>
        public void EndFestival()
        {
            if (_closing) return;
            if (Game != null && Game.Phase != GamePhase.Running) return;

            _closing = true;

            var time = Clock;
            if (time != null) time.StopClock();

            var visitors = VisitorsManager;
            if (visitors != null)
            {
                visitors.StopArrivals();
                visitors.SendEveryoneHome();
            }

            var events = Game != null ? Game.Events : null;
            if (events != null) events.StopAll();

            for (int i = 0; i < _built.Count; i++)
                if (_built[i] != null) _built[i].OnFestivalEnd();

            MatsuriLog.Always("祭りが終わりました。お客さんが帰っていきます…");

            if (_closeRoutine != null) StopCoroutine(_closeRoutine);
            _closeRoutine = StartCoroutine(CloseFestivalRoutine());
        }

        IEnumerator CloseFestivalRoutine()
        {
            float waited = 0f;
            var visitors = VisitorsManager;

            while (waited < MaxCloseWaitSeconds)
            {
                if (visitors == null || visitors.CurrentVisitors <= 0) break;
                waited += UnityEngine.Time.deltaTime;
                yield return null;
            }

            var audio = Game != null ? Game.Audio : null;
            if (audio != null)
            {
                audio.StopBgm();
                audio.StopAmbience();
            }

            var result = BuildResult();

            if (Game != null) Game.SetPhase(GamePhase.Finished);

            var ui = Game != null ? Game.UI : null;
            if (ui != null) ui.ShowResult(result);

            _closeRoutine = null;
        }

        // ────────────────────────────────────────────────────
        // リセット
        // ────────────────────────────────────────────────────

        /// <summary>建てた物をすべて消して最初の状態に戻す。RUN のたびに呼ばれる。</summary>
        public void ResetAll()
        {
            StopAllCoroutines();
            _closeRoutine = null;
            _pendingBuilds = 0;
            _staggerIndex = 0;
            _closing = false;
            _ruleTimer = 0f;

            var stalls = StallsManager;
            for (int i = 0; i < _built.Count; i++)
            {
                var obj = _built[i];
                if (obj == null) continue;

                if (stalls != null && obj is Stall stall) stalls.Unregister(stall);
                Destroy(obj.gameObject);
            }
            _built.Clear();

            // 建設途中の残骸（リング・光の柱など）も消す。
            var root = BuiltRoot;
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);

            _placement.Clear();

            var events = Game != null ? Game.Events : null;
            if (events != null) events.StopAll();

            var visitors = VisitorsManager;
            if (visitors != null) visitors.ResetAll();

            var time = Clock;
            if (time != null) time.ResetClock();

            ResetEconomy();

            _plan = null;

            if (Game != null && Game.Phase != GamePhase.Editing) Game.SetPhase(GamePhase.Editing);

            NavigationService.Instance?.RequestRebake();
        }

        void ResetEconomy()
        {
            var economy = Economy;
            if (economy == null) return;

            if (Balance != null) economy.Initialize(Balance, Game != null ? Game.Mode : GameMode.Free);
            else economy.ResetAll();
        }

        // ────────────────────────────────────────────────────
        // トリガールール (§14 〜 §17)
        // ────────────────────────────────────────────────────

        void EvaluateRules(float dt)
        {
            if (_plan == null || _plan.Rules == null || _plan.Rules.Count == 0) return;

            _ruleTimer -= dt;
            if (_ruleTimer > 0f) return;
            _ruleTimer += RuleEvaluationInterval;

            var rules = _plan.Rules;
            for (int i = 0; i < rules.Count; i++) EvaluateRule(rules[i]);
        }

        void EvaluateRule(TriggerRule rule)
        {
            if (rule == null) return;

            bool satisfied = rule.Condition == null || rule.Condition.Evaluate(this);

            if (satisfied && (!rule.Fired || !rule.Once))
            {
                if (rule.Once) rule.Fired = true;

                MatsuriLog.Info($"ルール成立: {rule.Describe()}");

                // ルールで増える物も、間を置いて1つずつ建つようにする (§39)。
                _staggerIndex = 0;

                var body = rule.Body;
                if (body != null)
                {
                    for (int i = 0; i < body.Count; i++) ExecuteCommand(body[i]);
                }
            }

            // 入れ子のルールは、親の条件が成立してから評価対象に入る。
            bool parentActive = satisfied || (rule.Once && rule.Fired);
            if (!parentActive) return;

            var nested = rule.NestedRules;
            if (nested == null) return;

            for (int i = 0; i < nested.Count; i++) EvaluateRule(nested[i]);
        }
    }
}
