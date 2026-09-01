using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Festival;
using Matsuri.Stalls;
using Matsuri.TimeSystem;
using UnityEngine;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場者全体の管理 (§25 / §56 / §57)。
    ///
    /// 目標は NPC 300人で安定 60fps、最終目標 1000人 (§56)。そのため §57 の最適化を全部入れる:
    ///  - Object Pooling        … VisitorPool が事前生成し、生成コストをフレームに分散する
    ///  - 更新の分散            … 全NPCをバケットに分け、1フレームに1バケットだけ思考させる。
    ///                            バケット数は人数に応じて自動で増える（1バケット30人が目安）
    ///  - 距離による処理簡略化  … 遠い人は簡易移動＋アニメ停止＋低頻度更新
    ///  - NavMeshAgent 数の制御 … VisitorLodController が「カメラに近い上位N体」に枠を配る
    ///  - 描画のLOD             … 近/中/遠の3段。中で影とアニメを切り、遠で Renderer ごと切る
    ///
    /// 会場の状態（屋台・装飾・ベンチ・混雑度）の集計は VisitorWorldCache、
    /// 描画と NavMeshAgent の配分は VisitorLodController に分けてある (§66)。
    ///
    /// **毎フレームの処理では GC を出さない**。foreach による列挙子の確保も、
    /// LINQ も、文字列生成も行わない。作業用の List はすべて使い回す。
    ///
    /// ファイルは §66 に従って3分割してある:
    ///   VisitorManager.cs          … 状態・公開API・初期化・毎フレームの更新
    ///   VisitorManager.Arrivals.cs … 来場ペースと出現 (§8)
    ///   VisitorManager.Budget.cs   … 更新の分散と NavMeshAgent の配分 (§57)
    /// </summary>
    public sealed partial class VisitorManager : MonoBehaviour
    {
        [Tooltip("BalanceConfig.MaxActiveNavAgents が無いときに使う NavMeshAgent の本数上限 (§57)。")]
        [SerializeField] int _maxNavAgents = 220;

        [Tooltip("1フレームに出現させてよい人数の上限。到着ラッシュでフレームを飛ばさないため。")]
        [SerializeField] int _maxSpawnPerFrame = 8;

        [Tooltip("この距離を超えた来場者は Renderer ごと無効化する (§57)。")]
        [SerializeField] float _farCullDistance = 95f;

        readonly List<VisitorAgent> _active = new List<VisitorAgent>(1024);
        readonly List<VisitorAgent> _gone = new List<VisitorAgent>(64);
        readonly VisitorWorldCache _world = new VisitorWorldCache();
        readonly VisitorLodController _lod = new VisitorLodController();

        MatsuriCatalog _catalog;
        BalanceConfig _balance;
        VisitorPool _pool;
        Transform _root;

        MRandom _rng = new MRandom(0x5EED1234u);
        float[] _bucketAccum = new float[12];
        int _baseBuckets = 12;
        int _bucketCursor;
        int _bucketAssign;

        bool _arriving;
        float _arrivalAccumulator;
        int _navAgentsInUse;

        float _cacheTimer = 999f;
        float _densityTimer = 999f;
        float _statsTimer = 999f;
        float _lodBudgetTimer = 999f;
        float _bucketTimer = 999f;

        Vector3 _lastCameraPosition;
        bool _hasCameraPosition;

        int _total;
        int _peak;
        float _averageSatisfaction;

        Vector3 _entrance = new Vector3(-8f, 0f, -57f);
        Vector3 _exit = new Vector3(8f, 0f, -57f);

        // ================================================================
        // コントラクトの公開API
        // ================================================================

        /// <summary>累計来場者数 (§35)。</summary>
        public int TotalVisitors => _total;

        /// <summary>いま会場に居る人数。</summary>
        public int CurrentVisitors => _active.Count;

        /// <summary>最高同時来場者数 (§35)。</summary>
        public int PeakVisitors => _peak;

        /// <summary>会場に居る人の平均満足度 0-100 (§34 / §35)。</summary>
        public float AverageSatisfaction => _averageSatisfaction;

        public IReadOnlyList<VisitorAgent> Active => _active;

        /// <summary>入り口。入り口設備が建っていればそこ、無ければ会場の南端。</summary>
        public Vector3 EntrancePosition { get => _entrance; set => _entrance = value; }

        /// <summary>出口。出口設備が建っていればそこ、無ければ会場の南端。</summary>
        public Vector3 ExitPosition { get => _exit; set => _exit = value; }

        // ---- NPC 側から参照される情報 ----
        public BalanceConfig Balance => _balance;
        public IReadOnlyList<Stall> StallList => _world.Stalls;
        public float MinutesOfDay { get; private set; } = FestivalClock.StartMinutes;
        public bool FireworksActive { get; private set; }
        public Vector3 WanderCenter => _world.WanderCenter;
        public float WanderRadius => _world.WanderRadius;
        public int NavAgentsInUse => _navAgentsInUse;
        public bool IsArriving => _arriving;

        // ================================================================
        // 初期化
        // ================================================================

        public void Initialize(MatsuriCatalog catalog, BalanceConfig balance)
        {
            _catalog = catalog;
            _balance = balance;

            _baseBuckets = balance != null ? Mathf.Clamp(balance.VisitorThinkBuckets, 1, MaxThinkBuckets) : 12;
            _bucketAccum = new float[_baseBuckets];
            _bucketCursor = 0;
            _bucketAssign = 0;

            if (_root == null)
            {
                var rootGo = new GameObject("VisitorPool");
                rootGo.transform.SetParent(transform, false);
                _root = rootGo.transform;
            }

            if (_pool == null) _pool = gameObject.AddComponent<VisitorPool>();

            _lod.Clear();
            ConfigurePoolAndLod();

            ApplyDefaultGates();
            RefreshWorld();

            MatsuriLog.Info($"来場者マネージャを初期化しました（上限 {Capacity()}人 / " +
                            $"思考バケット {_baseBuckets} / NavMeshAgent {MaxNavAgents}本）。");
        }

        /// <summary>
        /// BalanceConfig.MaxConcurrentVisitors を変えたあとに呼ぶ。
        /// プールの目標数と LOD の予算を新しい人数に合わせ直す (§56 の 300人↔1000人の切り替え)。
        /// </summary>
        public void ReconfigureCapacity()
        {
            if (_pool == null) return;
            ConfigurePoolAndLod();
            MatsuriLog.Info($"来場者の上限を {Capacity()}人 に変更しました。");
        }

        int Capacity() => _balance != null ? Mathf.Max(1, _balance.MaxConcurrentVisitors) : 300;

        void ConfigurePoolAndLod()
        {
            int capacity = Capacity();

            // 事前生成の速さは目標人数に合わせる。1000人を6体/フレームだと約170フレームかかる。
            int perFrame = Mathf.Clamp(capacity / 120, 4, 24);

            _pool.Configure(_catalog, _root, capacity, perFrame);
            _pool.BeginPrewarm();

            float simplify = _balance != null ? Mathf.Max(5f, _balance.VisitorSimplifyDistance) : 45f;
            _lod.Configure(simplify, _farCullDistance, MaxNavAgents);
        }

        /// <summary>入り口・出口の既定位置を会場の南端に置く (§28)。</summary>
        void ApplyDefaultGates()
        {
            var bounds = _catalog != null ? _catalog.Bounds : Matsuri.Script.GroundBounds.Default;
            float centerX = (bounds.MinX + bounds.MaxX) * 0.5f;
            // 祭りは実時間で数分しかない (§7)。会場の端から歩かせると
            // 屋台に着く前に祭りが終わってしまうので、門は南寄りの中間に置く。
            // プレイヤーが「入り口」を建てればそちらが優先される。
            float southZ = bounds.MinZ * 0.55f;

            _entrance = new Vector3(centerX - 8f, 0f, southZ);
            _exit = new Vector3(centerX + 8f, 0f, southZ);

            _world.SetDefaultWander(
                new Vector3(centerX, 0f, (bounds.MinZ + bounds.MaxZ) * 0.5f),
                Mathf.Max(12f, (bounds.MaxZ - bounds.MinZ) * 0.25f));
        }

        // ================================================================
        // 来場・退場
        // ================================================================

        public void BeginArrivals()
        {
            _arriving = true;
            _arrivalAccumulator = 0f;
        }

        public void StopArrivals() => _arriving = false;

        /// <summary>全員を出口へ向かわせる (§8 22:00)。</summary>
        public void SendEveryoneHome()
        {
            _arriving = false;
            for (int i = 0; i < _active.Count; i++)
            {
                var v = _active[i];
                if (v != null) v.SendHome(VisitorLeaveReason.ForcedHome);
            }
        }

        public void ResetAll()
        {
            _arriving = false;
            _arrivalAccumulator = 0f;
            RecycleGone();

            for (int i = 0; i < _active.Count; i++)
            {
                var v = _active[i];
                if (v == null) continue;
                // Manager は Return() の中で外す。先に外すと NavMeshAgent の枠が返らなくなる。
                _lod.Release(v);
                if (_pool != null) _pool.Return(v);
            }
            _active.Clear();
            _gone.Clear();
            _world.Clear();
            ApplyDefaultGates();

            _total = 0;
            _peak = 0;
            _averageSatisfaction = 0f;
            _navAgentsInUse = 0;
            _bucketAssign = 0;
            _hasCameraPosition = false;
            MinutesOfDay = _balance != null ? _balance.StartMinutes : FestivalClock.StartMinutes;
        }

        // ================================================================
        // 毎フレーム更新
        // ================================================================

        /// <summary>
        /// 性能計測 (§56)。祭りが動いていない間も含めて毎フレーム数える。
        /// 計測していないときは MatsuriPerformance 側が即座に戻るので、負荷は無視できる。
        /// </summary>
        void Update()
        {
            MatsuriPerformance.Tick(UnityEngine.Time.unscaledDeltaTime, _active.Count);
        }

        public void TickVisitors(float dt, FestivalClock clock)
        {
            if (dt <= 0f) return;
            MinutesOfDay = clock.MinutesOfDay;

            _cacheTimer += dt;
            if (_cacheTimer >= 1f) { _cacheTimer = 0f; RefreshWorld(); }

            _densityTimer += dt;
            if (_densityTimer >= 0.4f) { _densityTimer = 0f; _world.RebuildDensity(_active); }

            _bucketTimer += dt;
            if (_bucketTimer >= 1f) { _bucketTimer = 0f; EnsureBucketCapacity(); }

            UpdateArrivals(dt, clock);
            UpdateAgents(dt);
            RecycleGone();

            _statsTimer += dt;
            if (_statsTimer >= 0.5f) { _statsTimer = 0f; RecomputeStats(); }
        }

        /// <summary>§57 の中核。思考の分散と距離LODをここでまとめて行う。</summary>
        void UpdateAgents(float dt)
        {
            int buckets = _bucketAccum.Length;
            _bucketCursor = (_bucketCursor + 1) % buckets;
            for (int i = 0; i < buckets; i++) _bucketAccum[i] += dt;

            // このバケットが前回処理されてからの経過時間。遠距離NPCはこれをまとめて進める。
            float bucketDt = _bucketAccum[_bucketCursor];
            _bucketAccum[_bucketCursor] = 0f;

            Vector3 camPos = CameraPosition();
            UpdateLodBudget(dt, camPos);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var v = _active[i];
                if (v == null) { _active.RemoveAt(i); continue; }

                bool inBucket = v.Bucket == _bucketCursor;

                if (inBucket)
                {
                    // LOD 判定も分散する。毎フレーム全員の距離を測ったら最適化の意味がない。
                    // 距離はここで1回だけ測り、簡易化の判定と描画LODの両方に使う。
                    float distance = VisitorLodController.FlatDistance(v.Position, camPos);
                    bool simplify = _lod.ShouldSimplify(v.IsSimplified, distance);
                    v.SetSimplified(simplify);
                    _lod.Apply(v, distance, simplify);
                    v.ThinkStep();
                }

                if (v.IsSimplified)
                {
                    // 遠くの人は自分の番のときだけ、まとめた時間ぶん動かす。
                    if (inBucket) v.TickAgent(bucketDt);
                }
                else
                {
                    v.TickAgent(dt);
                }

                if (v.State == VisitorStateKind.Gone)
                {
                    if (!_gone.Contains(v)) _gone.Add(v);
                    _active.RemoveAt(i);
                }
            }
        }

        void RecycleGone()
        {
            if (_gone.Count == 0) return;
            for (int i = 0; i < _gone.Count; i++)
            {
                var v = _gone[i];
                if (v == null) continue;
                _lod.Release(v);
                if (_pool != null) _pool.Return(v);
            }
            _gone.Clear();
        }

        // ================================================================
        // 会場情報（NPC から引かれる）
        // ================================================================

        void RefreshWorld()
        {
            _world.Refresh();

            // 入り口／出口の設備が建っていればそちらを優先する (§20)。
            if (_world.HasEntrance) _entrance = _world.EntrancePosition;
            if (_world.HasExit) _exit = _world.ExitPosition;
            else if (_world.HasEntrance) _exit = _world.EntrancePosition;

            var events = GameManager.Instance != null ? GameManager.Instance.Events : null;
            FireworksActive = events != null && events.IsFireworksActive;
        }

        public int GetCrowdingAt(Vector3 position) => _world.GetCrowdingAt(position);

        public float GetAmbienceAt(Vector3 position) => _world.GetAmbienceAt(position);

        public bool TryFindRestSpot(VisitorAgent visitor, out Facility facility)
        {
            facility = null;
            if (visitor == null) return false;
            return _world.TryFindRestSpot(visitor.Position, out facility);
        }

        // ================================================================
        // NavMeshAgent の本数制御 (§57)
        // ================================================================

        public bool TryAcquireNavAgentSlot()
        {
            if (_navAgentsInUse >= MaxNavAgents) return false;
            _navAgentsInUse++;
            return true;
        }

        public void ReleaseNavAgentSlot()
        {
            if (_navAgentsInUse > 0) _navAgentsInUse--;
        }

        // ================================================================
        // その他
        // ================================================================

        /// <summary>
        /// NPC 自身から「帰りました」と通知される。
        /// ここでリストを触ると走査中の添字がずれて別人を消してしまうので、
        /// 実際の回収は UpdateAgents のスイープに任せる。
        /// </summary>
        public void NotifyGone(VisitorAgent agent)
        {
            if (agent == null) return;
            // 何もしない。State == Gone を見て UpdateAgents が回収する。
        }

        /// <summary>花火が上がった (§22)。全員に伝えて空を見上げさせる。</summary>
        public void BroadcastFireworks(float burst)
        {
            FireworksActive = true;
            for (int i = 0; i < _active.Count; i++)
            {
                var v = _active[i];
                if (v != null) v.OnFireworks(burst);
            }
        }

        void RecomputeStats()
        {
            if (_active.Count == 0) { _averageSatisfaction = 0f; return; }

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < _active.Count; i++)
            {
                var v = _active[i];
                if (v == null) continue;
                sum += v.Satisfaction;
                count++;
            }
            _averageSatisfaction = count > 0 ? sum / count : 0f;
        }

        Vector3 CameraPosition()
        {
            var cameras = GameManager.Instance != null ? GameManager.Instance.Cameras : null;
            if (cameras != null && cameras.MainCamera != null) return cameras.MainCamera.transform.position;

            var main = UnityEngine.Camera.main;
            if (main != null) return main.transform.position;
            return _world.WanderCenter;
        }
    }
}
