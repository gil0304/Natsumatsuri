using Matsuri.Art;
using Matsuri.Data;
using Matsuri.Festival;
using Matsuri.Stalls;
using UnityEngine;
using UnityEngine.AI;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場者1人 (§25〜§30)。
    ///
    /// - **LLM は使わない (§25)**。判断は VisitorBrain / DestinationScorer の純粋な計算だけで行う。
    /// - 見た目は Matsuri.Art.ProceduralVisitorFactory が作った体を子に持ち、
    ///   ProceduralWalkAnimator に速度を渡して歩かせる (§79「無表情NPCが直立」を避ける)。
    /// - 近距離では NavMeshAgent、遠距離では簡易直線移動に落とす (§57 距離LOD)。
    /// - ThinkStep() は毎フレームではなく VisitorManager が分散して呼ぶ (§57)。
    ///
    /// ファイルは §66 に従って3分割してある:
    ///   VisitorAgent.cs        … 状態・API・意思決定の入口
    ///   VisitorAgentStates.cs  … 状態機械の毎フレーム更新 (§28)
    ///   VisitorLocomotion.cs   … 移動・NavMesh・LOD・見た目
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class VisitorAgent : MonoBehaviour
    {
        // ---- 調整値（体感に関わるものだけ。経営バランスの数値は BalanceConfig 側 §31） ----
        const float ArriveThreshold       = 1.2f;   // 目的地に着いたとみなす距離
        const float JoinQueueDistance     = 3.2f;   // ここまで近づいたら並ぶ
        const float EnjoyDuration         = 4.5f;   // 買った物を味わう時間
        const float RestDuration          = 9f;     // ベンチで休む時間
        const float MaxServeSeconds       = 30f;    // 接客が終わらないときの保険
        const float MaxApproachSeconds    = 50f;    // 屋台に辿り着けないときの保険
        const float EnergyDrainPerSecond  = 0.32f;
        const float HungerGrowthPerSecond = 0.22f;
        const float FunGrowthPerSecond    = 0.16f;
        const float RestRecoverPerSecond  = 6.5f;
        const float RestSatisfactionPerSecond = 0.45f;
        const float TurnSpeed             = 9f;
        const float LookUpAngle           = 38f;

        // ---- 参照 ----
        VisitorManager _manager;
        VisitorArchetype _archetype;
        VisitorArchetype _bodyArchetype;
        NavMeshAgent _agent;
        GameObject _body;
        ProceduralWalkAnimator _walk;
        Transform _head;
        Quaternion _headRestLocal = Quaternion.identity;

        // ---- 状態 ----
        VisitorState _state;
        MRandom _rng;
        Stall _targetStall;
        Stall _avoidStall;
        float _avoidTimer;
        float _lastTargetScore;
        /// <summary>いま座っているベンチの席 (§20)。盆踊り場などの滞在先は _amenity 側。</summary>
        Facility _restSpot;

        Facility _amenity;
        Facility _avoidAmenity;
        float _avoidAmenityTimer;
        Vector3 _amenitySlot;
        float _amenityStaySeconds;
        float _amenityScore;
        Vector3 _destination;
        bool _hasDestination;
        bool _simplified;
        bool _navActive;
        bool _initialized;
        float _lastThinkTime;
        float _lookUp01;
        VisitorStateKind _stateBeforeFireworks = VisitorStateKind.Browsing;

        // ================================================================
        // コントラクトの公開API
        // ================================================================

        public VisitorArchetype Archetype => _archetype;
        public VisitorStateKind State => _state.Kind;

        public float Money        { get => _state.Money;        set => _state.Money = Mathf.Max(0f, value); }
        public float Hunger       { get => _state.Hunger;       set => _state.Hunger = Mathf.Clamp(value, 0f, 100f); }
        public float Fun          { get => _state.Fun;          set => _state.Fun = Mathf.Clamp(value, 0f, 100f); }
        public float Energy       { get => _state.Energy;       set => _state.Energy = Mathf.Clamp(value, 0f, 100f); }
        public float Patience     { get => _state.Patience;     set => _state.Patience = Mathf.Max(1f, value); }
        public float Satisfaction { get => _state.Satisfaction; set => _state.Satisfaction = Mathf.Clamp(value, 0f, 100f); }

        public int VisitCount => _state.VisitCount;
        public Stall TargetStall => _targetStall;
        public Vector3 Position => transform.position;

        // ---- 個体差パラメータ (§26) ----
        public float WalkingSpeed      { get => _state.WalkingSpeed; set => _state.WalkingSpeed = Mathf.Max(0.2f, value); }
        public float PriceSensitivity  => _state.PriceSensitivity;
        public float FireworksInterest => _state.FireworksInterest;
        public int   TargetVisitCount  => _state.TargetVisitCount;
        public float LifeTime          => _state.LifeTime;
        public float QueueWaitTime     => _state.QueueWaitTime;
        public VisitorLeaveReason LeaveReason => _state.LeaveReason;

        /// <summary>この人がいま何をしているかの日本語表記 (§38 来場者視点カメラ用)。</summary>
        public string StateLabel => VisitorStateLabel.ToJapanese(_state.Kind);

        /// <summary>所属マネージャ。プールから貸し出すときに設定される。</summary>
        public VisitorManager Manager { get => _manager; set => _manager = value; }

        /// <summary>分散更新のバケット番号 (§57)。</summary>
        public int Bucket { get; set; }

        /// <summary>遠距離簡易更新モードか (§57)。</summary>
        public bool IsSimplified => _simplified;

        /// <summary>いま NavMeshAgent を実際に使っているか (§57 本数制御)。</summary>
        public bool UsesNavAgent => _navActive;

        /// <summary>この体がどのアーキタイプ用に作られたか。プールの貸し出しに使う。</summary>
        public VisitorArchetype BodyArchetype => _bodyArchetype;

        // ================================================================
        // 初期化
        // ================================================================

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();
            ConfigureNavAgent(_agent);
            _agent.enabled = false;

            if (_rng.state == 0u) _rng = new MRandom(1u);
        }

        // ================================================================
        // 出現・退場
        // ================================================================

        public void Spawn(VisitorArchetype archetype, Vector3 pos, uint seed)
        {
            EnsureInit();

            _archetype = archetype;
            _rng = new MRandom(seed == 0u ? 1u : seed);

            if (_body == null || _bodyArchetype != archetype)
                BuildBody(archetype, seed);

            _state.Roll(archetype, ref _rng);
            _state.Seed = seed;
            ApplyBodyScale(archetype);

            _targetStall = null;
            _avoidStall = null;
            _avoidTimer = 0f;
            _lastTargetScore = float.NegativeInfinity;
            _amenity = null;
            _avoidAmenity = null;
            _avoidAmenityTimer = 0f;
            _amenitySlot = pos;
            _amenityStaySeconds = 0f;
            _amenityScore = float.NegativeInfinity;
            _hasDestination = false;
            _simplified = false;
            _navActive = false;
            _lookUp01 = 0f;
            _lastThinkTime = Time.time;

            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, _rng.NextFloat(0f, 360f), 0f));
            if (_walk != null)
            {
                _walk.enabled = true;
                _walk.SetIdle(false);
                _walk.SetSpeed(0f);
            }
            if (_head != null) _head.localRotation = _headRestLocal;

            EnterState(VisitorStateKind.Entering);
            TryEnableNav();

            // 入場したら会場の中へ歩き出す。全員が同じ点に向かわないよう散らす。
            MoveTo(_manager != null ? _manager.PickEntryTarget(ref _rng) : pos + Vector3.forward * 8f);
        }

        public void Despawn()
        {
            if (_state.Kind == VisitorStateKind.Gone) return;

            LeaveCurrentQueue();
            ReleaseAmenity();
            DisableNav();

            _state.Kind = VisitorStateKind.Gone;
            _hasDestination = false;
            if (_walk != null) _walk.SetSpeed(0f);

            if (_manager != null) _manager.NotifyGone(this);
        }

        /// <summary>プールに返すときの完全リセット (§57)。</summary>
        public void ResetForPool()
        {
            LeaveCurrentQueue();
            ReleaseAmenity();
            DisableNav();

            _archetype = null;
            _targetStall = null;
            _avoidStall = null;
            _avoidTimer = 0f;
            _avoidAmenity = null;
            _avoidAmenityTimer = 0f;
            _amenityStaySeconds = 0f;
            _lastTargetScore = float.NegativeInfinity;
            _hasDestination = false;
            _destination = Vector3.zero;
            _simplified = false;
            _lookUp01 = 0f;
            _state.Clear();

            if (_walk != null) { _walk.SetSpeed(0f); _walk.SetIdle(true); _walk.enabled = false; }
            if (_head != null) _head.localRotation = _headRestLocal;
        }

        /// <summary>祭りの終了などで帰らせる (§8 22:00)。</summary>
        public void SendHome(VisitorLeaveReason reason = VisitorLeaveReason.ForcedHome)
        {
            if (_state.Kind == VisitorStateKind.Gone || _state.Kind == VisitorStateKind.Leaving) return;
            _state.LeaveReason = reason;
            LeaveCurrentQueue();
            ReleaseAmenity();
            EnterState(VisitorStateKind.Leaving);
            MoveTo(_manager != null ? _manager.ExitPosition : transform.position);
        }

        // ================================================================
        // 思考 (§57 分散更新。毎フレームは呼ばれない)
        // ================================================================

        public void ThinkStep()
        {
            if (!_initialized || _state.Kind == VisitorStateKind.Gone) return;

            float dtThink = Mathf.Clamp(Time.time - _lastThinkTime, 0f, 2f);
            _lastThinkTime = Time.time;

            if (_avoidTimer > 0f)
            {
                _avoidTimer -= dtThink;
                if (_avoidTimer <= 0f) _avoidStall = null;
            }

            if (_avoidAmenityTimer > 0f)
            {
                _avoidAmenityTimer -= dtThink;
                if (_avoidAmenityTimer <= 0f) _avoidAmenity = null;
            }

            // NavMesh は屋台が建つたびに貼り直される。近距離なら毎回つなぎ直しを試す。
            if (!_simplified && !_navActive) TryEnableNav();

            ApplyAmbientSatisfaction(dtThink);

            switch (_state.Kind)
            {
                case VisitorStateKind.Entering:
                case VisitorStateKind.Browsing:
                case VisitorStateKind.MovingToStall:
                case VisitorStateKind.MovingToAmenity:
                    ThinkAboutDestination(dtThink);
                    break;

                case VisitorStateKind.Queueing:
                    if (_targetStall == null) EnterState(VisitorStateKind.Browsing);
                    break;

                case VisitorStateKind.Leaving:
                    if (!_hasDestination && _manager != null) MoveTo(_manager.ExitPosition);
                    break;
            }
        }

        /// <summary>
        /// §29 のスコアで次の目的地を決め、帰宅の判定も行う (§28)。
        /// 屋台 (DestinationScorer) と施設 (AmenityScorer) は同じ尺度で採点されるので、
        /// 「次は屋台か、盆踊り場か、休憩所か」を1つのスコア表で比べられる (§34)。
        /// </summary>
        void ThinkAboutDestination(float dtThink)
        {
            var balance = _manager != null ? _manager.Balance : null;
            var stalls = _manager != null ? _manager.StallList : null;
            float minutes = _manager != null ? _manager.MinutesOfDay : 0f;

            // すでに施設へ歩いている途中なら、そのまま行かせる（ふらふら迷わせない）。
            if (_state.Kind == VisitorStateKind.MovingToAmenity && _amenity != null)
            {
                MoveTo(_amenitySlot);
                return;
            }

            Stall best = VisitorBrain.ChooseStall(this, stalls, balance, ref _rng, _avoidStall, out float score);
            Facility amenity = VisitorBrain.ChooseAmenity(this, balance, ref _rng, _avoidAmenity, out float amenityScore);
            bool affordable = VisitorBrain.AnyAffordable(this, stalls);

            // 屋台が無くても居場所があるなら、まだ帰る理由にはならない (§34)。
            bool hasCandidate = best != null || amenity != null;

            var reason = VisitorBrain.EvaluateGoHome(this, balance, minutes, hasCandidate, affordable);
            if (reason != VisitorLeaveReason.None)
            {
                SendHome(reason);
                return;
            }

            // 施設のほうが行きたければそちらへ (§34)。
            // 屋台に向かっている最中は、はっきり上回ったときだけ乗り換える。
            float margin = _state.Kind == VisitorStateKind.MovingToStall
                ? VisitorBrain.SwitchTargetMargin
                : VisitorBrain.AmenityPreferenceMargin;

            if (amenity != null && (best == null || amenityScore > score + margin))
            {
                if (BeginGoToAmenity(amenity, amenityScore)) return;
            }

            if (best == null)
            {
                // 行きたい屋台も居場所も無い (§34「欲しい屋台がない」で満足度が下がる)。
                Satisfaction -= VisitorBrain.NothingToDoPerSecond(balance) * dtThink;
                _targetStall = null;
                if (_state.Kind != VisitorStateKind.Browsing) EnterState(VisitorStateKind.Browsing);
                if (!_hasDestination || ReachedDestination()) Wander();
                return;
            }

            // ヒステリシス。差が小さいうちは乗り換えない（ふらふら迷って見えるのを防ぐ）。
            bool shouldSwitch =
                _targetStall == null ||
                _state.Kind != VisitorStateKind.MovingToStall ||
                (best != _targetStall && score > _lastTargetScore + VisitorBrain.SwitchTargetMargin);

            if (shouldSwitch)
            {
                _targetStall = best;
                _lastTargetScore = score;
                EnterState(VisitorStateKind.MovingToStall);
            }
            MoveTo(QueueApproachPoint(_targetStall));
        }

        /// <summary>混雑・装飾・花火から受ける満足度の増減 (§34)。</summary>
        void ApplyAmbientSatisfaction(float dt)
        {
            if (_manager == null || dt <= 0f) return;
            int crowd = _manager.GetCrowdingAt(transform.position);
            float ambience = _manager.GetAmbienceAt(transform.position);
            Satisfaction += VisitorBrain.AmbientSatisfactionPerSecond(
                this, _manager.Balance, crowd, ambience, _manager.FireworksActive) * dt;
        }

        // ================================================================
        // 外から呼ばれるイベント
        // ================================================================

        /// <summary>屋台が接客を完了したときに呼ぶ (§30 / §32 / §34)。</summary>
        public void OnServed(Stall stall, int price)
        {
            if (stall == null) return;
            var balance = _manager != null ? _manager.Balance : null;
            var data = stall.Data;

            _state.Money = Mathf.Max(0f, _state.Money - price);

            if (data != null)
            {
                if (data.Category == StallCategory.Food)
                {
                    Hunger -= data.HungerRelief;
                    Fun    += data.FunRelief * 0.3f;          // 食べ歩きも少し楽しい
                }
                else
                {
                    Fun    -= data.FunRelief;
                    Hunger += data.HungerRelief * 0.1f;
                }
                Energy -= data.EnergyCost;
                Satisfaction += VisitorBrain.ServeSatisfaction(this, stall, price, balance, _state.QueueWaitTime);
            }

            _state.VisitCount++;
            _state.QueueWaitTime = 0f;
            _state.ServeTimer = 0f;

            // 同じ屋台を続けて買わない。祭りが「1軒だけ大行列」にならないようにする。
            _avoidStall = stall;
            _avoidTimer = 45f;
            _targetStall = null;
            _lastTargetScore = float.NegativeInfinity;

            EnterState(VisitorStateKind.Enjoying);
        }

        /// <summary>花火が上がった (§22 / §34)。空を見上げ、満足度が上がる。</summary>
        public void OnFireworks(float burst)
        {
            if (_state.Kind == VisitorStateKind.Gone) return;

            float interest = Mathf.Clamp01(_state.FireworksInterest * 0.01f);
            Satisfaction += burst * interest;

            // 首は誰でも上がる。行列や接客の途中でも列は離れない。
            _state.LookUpTimer = Mathf.Max(_state.LookUpTimer, 2.2f + _rng.NextFloat(0f, 2.5f));

            bool canStop =
                _state.Kind == VisitorStateKind.Browsing ||
                _state.Kind == VisitorStateKind.MovingToStall ||
                _state.Kind == VisitorStateKind.Entering;

            // 興味の薄い人は足を止めない。全員が同じ動きをすると嘘くさい (§79)。
            if (canStop && interest > 0.35f)
            {
                _stateBeforeFireworks = VisitorStateKind.Browsing;
                EnterState(VisitorStateKind.WatchingFireworks);
                _state.FireworksTimer = 3f + _rng.NextFloat(0f, 3.5f) * interest;
                StopMoving();
            }
        }

        // ================================================================
        // 後始末
        // ================================================================

        void LeaveCurrentQueue()
        {
            if (_targetStall == null) return;
            _targetStall.LeaveQueue(this);
            _targetStall = null;
        }

        void ReleaseRestSpot()
        {
            if (_restSpot == null) return;
            _restSpot.Release();
            _restSpot = null;
        }

        internal static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
