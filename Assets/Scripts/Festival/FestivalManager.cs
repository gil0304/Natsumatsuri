using System;
using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Economy;
using Matsuri.Script;
using Matsuri.Script.Commands;
using Matsuri.Stalls;
using Matsuri.TimeSystem;
using Matsuri.Visitors;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 仕様書 §39 / §50 / §51 / §53。本作の心臓。
    ///
    /// Matsuri Script が生んだ <see cref="FestivalPlan"/> を受け取り、
    ///   ・即時コマンドを建設演出付きで順に実行する
    ///   ・トリガールールを保持し、開催中に評価する
    ///   ・祭りの開始と終了を取り仕切る
    /// 言語処理系は GameObject を一切知らない。ここが唯一の接続点 (§53)。
    ///
    /// 責務が多いので3つに分けている (§66):
    ///   FestivalManager.cs          … RUN と建設演出の管理、毎フレームの進行
    ///   FestivalManager.Lifecycle.cs… 開始 / 終了 / リセット / ルール評価
    ///   FestivalManager.Commands.cs … IFestivalCommandSink の実装
    ///   FestivalManager.Metrics.cs  … IFestivalMetrics の実装と結果集計
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed partial class FestivalManager : MonoBehaviour, IFestivalCommandSink, IFestivalMetrics
    {
        [Tooltip("建てた物をぶら下げる親。未設定なら実行時に作る。")]
        [SerializeField] Transform _builtRoot;

        [Tooltip("バランス設定。未設定なら GameManager から取得する。")]
        public BalanceConfig Balance;

        /// <summary>いま会場に建っている物。RUN のたびに作り直される。</summary>
        readonly List<FestivalObject> _built = new List<FestivalObject>(64);

        readonly FestivalPlacement _placement = new FestivalPlacement();

        FestivalPlan _plan;
        int _pendingBuilds;
        int _staggerIndex;
        float _ruleTimer;
        bool _closing;
        bool _subscribed;
        Coroutine _closeRoutine;

        /// <summary>建てた物の親 Transform。</summary>
        public Transform BuiltRoot
        {
            get
            {
                if (_builtRoot == null)
                {
                    var existing = GameObject.Find("Built");
                    _builtRoot = existing != null ? existing.transform : new GameObject("Built").transform;
                }
                return _builtRoot;
            }
        }

        /// <summary>いま会場に建っている物すべて。</summary>
        public IReadOnlyList<FestivalObject> BuiltObjects => _built;

        /// <summary>いま実行中のプラン。</summary>
        public FestivalPlan CurrentPlan => _plan;

        /// <summary>建設演出が完了した瞬間の物。UI のログ表示などが受ける。</summary>
        public event Action<FestivalObject> ObjectBuilt;

        /// <summary>建設中の物が残っているか。</summary>
        public bool IsBuilding => _pendingBuilds > 0;

        /// <summary>配置計算。入り口位置の既定値などを外からも参照できるようにする。</summary>
        public FestivalPlacement Placement => _placement;

        // ── 各マネージャへの近道 ────────────────────────────
        static GameManager Game => GameManager.Instance;
        EconomyManager Economy => Game != null ? Game.Economy : null;
        StallManager StallsManager => Game != null ? Game.Stalls : null;
        VisitorManager VisitorsManager => Game != null ? Game.Visitors : null;
        TimeManager Clock => Game != null ? Game.Time : null;
        MatsuriCatalog Catalog => Game != null ? Game.Catalog : null;

        void Awake()
        {
            if (Balance == null && Game != null) Balance = Game.Balance;
            ConfigurePlacement();
        }

        void Start() => Subscribe();

        void OnDestroy() => Unsubscribe();

        void ConfigurePlacement()
        {
            GroundBounds bounds = Catalog != null ? Catalog.Bounds : GroundBounds.Default;
            _placement.Configure(bounds);
        }

        void Subscribe()
        {
            if (_subscribed) return;

            var time = Clock;
            if (time == null) return;

            time.MinuteTicked += OnMinuteTicked;
            time.Finished += OnClockFinished;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;

            var time = Clock;
            if (time != null)
            {
                time.MinuteTicked -= OnMinuteTicked;
                time.Finished -= OnClockFinished;
            }
            _subscribed = false;
        }

        // ────────────────────────────────────────────────────
        // RUN — プランの適用 (§39 / §40)
        // ────────────────────────────────────────────────────

        /// <summary>
        /// RUN。書かれたコードのとおりに祭りを建て直す。
        /// 毎回すべて作り直すことで「コードが唯一の真実」になる (§51)。
        /// </summary>
        public void ApplyPlan(FestivalPlan plan)
        {
            if (plan == null)
            {
                ReportRuntimeMessage("実行できるコードがありません。", DiagnosticSeverity.Warning);
                return;
            }

            if (plan.HasErrors)
            {
                ReportRuntimeMessage("コードにエラーがあるので、祭りは作られませんでした。", DiagnosticSeverity.Error);
                return;
            }

            ResetAll();

            _plan = plan;
            _plan.ResetRules();

            ConfigurePlacement();

            if (Game != null) Game.SetPhase(GamePhase.Building);

            _staggerIndex = 0;

            var commands = plan.ImmediateCommands;
            if (commands != null)
            {
                for (int i = 0; i < commands.Count; i++) ExecuteCommand(commands[i]);
            }

            MatsuriLog.Always($"「{plan.FestivalName}」を建設中… ({_built.Count}件)");

            if (_pendingBuilds <= 0) OnAllBuildsComplete();
        }

        /// <summary>コマンドを1つ実行する。1つ失敗しても残りは続ける。</summary>
        void ExecuteCommand(IFestivalCommand cmd)
        {
            if (cmd == null) return;

            try
            {
                cmd.Execute(this);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"{cmd.SourceLine}行目の実行に失敗しました: {e}");
                ReportRuntimeMessage(
                    $"{cmd.SourceLine}行目「{cmd.Describe()}」を実行できませんでした。",
                    DiagnosticSeverity.Error);
            }
        }

        // ────────────────────────────────────────────────────
        // 建設演出の管理 (§39)
        // ────────────────────────────────────────────────────

        /// <summary>建てた物を登録し、順番をずらして建設演出を始める。</summary>
        void RegisterAndAnimate(FestivalObject obj, GameObject visual)
        {
            if (obj == null || visual == null) return;

            _built.Add(obj);

            float duration = Balance != null ? Balance.BuildRiseDuration : 0.85f;
            float stagger = Balance != null ? Balance.BuildStagger : 0.12f;
            float delay = _staggerIndex * stagger;
            _staggerIndex++;

            _pendingBuilds++;

            StartCoroutine(BuildAnimation.Play(visual, duration, delay, () => OnOneBuildComplete(obj)));
        }

        void OnOneBuildComplete(FestivalObject obj)
        {
            _pendingBuilds--;

            if (obj != null)
            {
                obj.OnBuilt();

                // 建ち終わってから道をくり抜く。演出中にやると、
                // せり上がっている途中の高さでくり抜かれてしまう。
                //
                // ただし「人が中に入る施設」は掘ってはいけない。
                // 盆踊り場・休憩所・神社の立ち位置は建物の内側にあるので、
                // ここを掘ると NavMesh が消え、客が入口の手前で永久に立ち往生する。
                if (ShouldCarveNavMesh(obj))
                    NavigationService.AddCarvingObstacle(obj.gameObject);

                // 開催中に建った物は、その場で営業を始める（§14 のルールで増築したとき）。
                if (Game != null && Game.Phase == GamePhase.Running) obj.OnFestivalStart();

                ObjectBuilt?.Invoke(obj);
            }

            if (_pendingBuilds <= 0) OnAllBuildsComplete();
        }

        /// <summary>
        /// その物が NavMesh をくり抜くべきか。
        /// 屋台や物置きは通れない障害物にするが、
        /// 客が立ち入る施設 (§34 の居場所) は通れるままにしておく。
        /// </summary>
        static bool ShouldCarveNavMesh(FestivalObject obj)
        {
            if (obj == null) return false;
            if (obj.Kind == FestivalObjectKind.Decoration) return false;

            // 滞在できる施設＝中に立ち位置がある＝掘ってはいけない
            if (obj is Facility facility && facility.IsPlaceToStay) return false;

            return true;
        }

        void OnAllBuildsComplete()
        {
            _pendingBuilds = 0;
            _staggerIndex = 0;

            // 屋台が建ったので、NPC が歩ける道を作り直す (§28)。
            NavigationService.Instance?.RequestRebake();

            if (Game != null && Game.Phase == GamePhase.Building)
            {
                Game.SetPhase(GamePhase.Editing);
                ReportRuntimeMessage($"祭りの準備ができました。（{_built.Count}件）「祭りを開催」で始まります。");
            }
        }

        // ────────────────────────────────────────────────────
        // 毎フレームの進行
        // ────────────────────────────────────────────────────

        void Update()
        {
            if (Game == null) return;
            if (Game.Phase != GamePhase.Running) return;

            float dt = UnityEngine.Time.deltaTime;
            var time = Clock;
            FestivalClock clock = time != null ? time.Clock : FestivalClock.AtStart;

            TickBuiltObjects(dt, clock);

            var stalls = StallsManager;
            if (stalls != null) stalls.TickAll(dt, clock);

            var visitors = VisitorsManager;
            if (visitors != null) visitors.TickVisitors(dt, clock);

            EvaluateRules(dt);
            UpdateAudioIntensity();
        }

        /// <summary>屋台は StallManager がまとめて回すので、ここでは扱わない (§57 更新の集中管理)。</summary>
        void TickBuiltObjects(float dt, FestivalClock clock)
        {
            for (int i = 0; i < _built.Count; i++)
            {
                var obj = _built[i];
                if (obj == null) continue;
                if (obj.Kind == FestivalObjectKind.Stall) continue;
                obj.TickFestival(dt, clock);
            }
        }

        /// <summary>人が増えるほど賑やかにする (§24)。</summary>
        void UpdateAudioIntensity()
        {
            var audio = Game != null ? Game.Audio : null;
            var visitors = VisitorsManager;
            if (audio == null || visitors == null || Balance == null) return;

            float t = Mathf.Clamp01(visitors.CurrentVisitors / Mathf.Max(1f, Balance.MaxConcurrentVisitors * 0.6f));
            audio.SetFestivalIntensity(t);
        }

        /// <summary>時計が1分進んだ。§8 の見た目変化を建った物すべてに配る。</summary>
        void OnMinuteTicked(FestivalClock clock)
        {
            var time = Clock;
            float night = time != null ? time.NightAmount : 0f;

            for (int i = 0; i < _built.Count; i++)
            {
                var obj = _built[i];
                if (obj == null) continue;
                obj.OnTimeOfDayChanged(clock, night);
            }

            var ui = Game != null ? Game.UI : null;
            if (ui != null) ui.UpdateHud(Budget, Revenue, CurrentVisitorCount, clock);
        }
    }
}
