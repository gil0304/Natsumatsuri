using Matsuri.Art;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Stalls;
using UnityEngine;
using UnityEngine.AI;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Visitors
{
    /// <summary>
    /// VisitorAgent の移動・NavMesh・LOD・見た目 (§57 / §79)。
    ///
    /// 近距離: NavMeshAgent で屋台や他の客を避けて歩く。
    /// 遠距離 / NavMesh 未ベイク: 簡易直線移動に落とし、歩行アニメの計算そのものを止める。
    /// どちらの場合も向きは自前で滑らかに回すので、切り替わっても見た目が破綻しない。
    /// </summary>
    public sealed partial class VisitorAgent
    {
        // ================================================================
        // 見た目 (§79)
        // ================================================================

        /// <summary>NavMeshAgent の共通設定。プールが非アクティブなまま作るときにも使う。</summary>
        public static void ConfigureNavAgent(NavMeshAgent agent)
        {
            if (agent == null) return;
            agent.radius = 0.28f;
            agent.height = 1.65f;
            agent.baseOffset = 0f;
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;
            agent.autoRepath = true;
            agent.updateRotation = false;   // 向きは自前で回す（簡易移動と見た目を揃えるため）
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }

        /// <summary>
        /// 体を作る (§79)。ProceduralVisitorFactory が体格・服色・髪色をばらけさせる。
        /// プールの事前生成時に1回だけ呼ばれ、以後は使い回す（生成コストを祭りの最中に持ち込まない）。
        /// </summary>
        public void BuildBody(VisitorArchetype archetype, uint seed)
        {
            EnsureInit();
            if (archetype == null) return;

            if (_body != null)
            {
                if (Application.isPlaying) Destroy(_body); else DestroyImmediate(_body);
                _body = null;
            }

            var rng = new MRandom(seed == 0u ? 1u : seed);
            _body = ProceduralVisitorFactory.Build(archetype, ref rng, transform);
            _bodyArchetype = archetype;
            _walk = null;
            _head = null;

            if (_body == null) return;

            _body.transform.localPosition = Vector3.zero;
            _body.transform.localRotation = Quaternion.identity;

            _walk = _body.GetComponentInChildren<ProceduralWalkAnimator>(true);
            if (_walk == null) _walk = _body.AddComponent<ProceduralWalkAnimator>();

            _head = FindDeep(_body.transform, "Head");
            if (_head != null) _headRestLocal = _head.localRotation;
        }

        /// <summary>体格の個体差。体は使い回すが、出現のたびに背丈を少し変える (§79)。</summary>
        void ApplyBodyScale(VisitorArchetype archetype)
        {
            if (_body == null || archetype == null) return;
            float mid = Mathf.Max(0.5f, (archetype.BodyHeight.Min + archetype.BodyHeight.Max) * 0.5f);
            float scale = Mathf.Clamp(_state.BodyHeight / mid, 0.82f, 1.18f);
            _body.transform.localScale = Vector3.one * scale;
            if (_agent != null) _agent.height = Mathf.Max(0.8f, 1.65f * scale);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>花火を見上げる首の動き (§22)。</summary>
        void UpdateLookUp(float dt)
        {
            if (_head == null) return;

            bool wantsUp = _state.LookUpTimer > 0f || _state.Kind == VisitorStateKind.WatchingFireworks;
            if (_state.LookUpTimer > 0f) _state.LookUpTimer -= dt;

            _lookUp01 = Mathf.MoveTowards(_lookUp01, wantsUp ? 1f : 0f, dt * 2.2f);
            _head.localRotation = _lookUp01 <= 0.001f
                ? _headRestLocal
                : _headRestLocal * Quaternion.Euler(-LookUpAngle * _lookUp01, 0f, 0f);
        }

        // ================================================================
        // LOD (§57)
        // ================================================================

        /// <summary>
        /// 遠距離の簡易更新に切り替える。
        /// NavMeshAgent を切り、歩行アニメのコンポーネントごと無効化して計算を止める。
        /// </summary>
        public void SetSimplified(bool simplified)
        {
            if (_simplified == simplified) return;
            _simplified = simplified;

            if (simplified)
            {
                DisableNav();
                if (_walk != null) { _walk.SetSpeed(0f); _walk.enabled = false; }
            }
            else
            {
                if (_walk != null && _state.Kind != VisitorStateKind.Gone) _walk.enabled = true;
                TryEnableNav();
            }
        }

        /// <summary>NavMeshAgent を有効化する。本数の上限と NavMesh の有無を必ず確認する (§57)。</summary>
        bool TryEnableNav()
        {
            if (_agent == null || _simplified) return false;
            if (_navActive) return true;
            if (_state.Kind == VisitorStateKind.Gone) return false;

            // 同時に動かす NavMeshAgent の本数そのものを制御する。
            if (_manager != null && !_manager.TryAcquireNavAgentSlot()) return false;

            // まだベイクされていない / 足元に NavMesh が無いなら、簡易移動のまま続行する。
            if (!NavigationService.TrySample(transform.position, out Vector3 onMesh, 2.5f))
            {
                if (_manager != null) _manager.ReleaseNavAgentSlot();
                return false;
            }

            _agent.enabled = true;
            if (!_agent.isOnNavMesh) _agent.Warp(onMesh);
            if (!_agent.isOnNavMesh)
            {
                _agent.enabled = false;
                if (_manager != null) _manager.ReleaseNavAgentSlot();
                return false;
            }

            _agent.speed = WalkingSpeed;
            _agent.isStopped = false;
            _agent.avoidancePriority = 30 + (int)(_rng.NextUInt() % 40u);
            _navActive = true;
            if (_hasDestination) SetNavDestination(_destination);
            return true;
        }

        void DisableNav()
        {
            if (!_navActive) return;
            _navActive = false;

            if (_agent != null && _agent.enabled)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                _agent.enabled = false;
            }
            if (_manager != null) _manager.ReleaseNavAgentSlot();
        }

        // ================================================================
        // 移動
        // ================================================================

        void MoveTo(Vector3 world)
        {
            _destination = world;
            _hasDestination = true;
            if (_navActive) SetNavDestination(world);
        }

        void SetNavDestination(Vector3 world)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            if (NavigationService.TrySample(world, out Vector3 onMesh, 4f)) world = onMesh;
            _agent.SetDestination(world);
        }

        void StopMoving()
        {
            _hasDestination = false;
            if (_navActive && _agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.isStopped = true;
            SetAnimSpeed(0f);
        }

        void MoveUpdate(float dt)
        {
            if (!_hasDestination) { SetAnimSpeed(0f); return; }

            if (_navActive)
            {
                if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                {
                    // 再ベイクで足元の NavMesh が消えた等。簡易移動に落として続行する。
                    DisableNav();
                }
                else
                {
                    _agent.isStopped = false;
                    _agent.speed = WalkingSpeed;
                    Vector3 v = _agent.velocity;
                    v.y = 0f;
                    if (v.sqrMagnitude > 0.0025f) FaceDirection(v, dt);
                    SetAnimSpeed(v.magnitude);
                    return;
                }
            }

            // --- 簡易直線移動（遠距離・NavMesh 未ベイク時） ---
            Vector3 pos = transform.position;
            Vector3 to = _destination - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.05f) { SetAnimSpeed(0f); return; }

            Vector3 dir = to / dist;
            float step = Mathf.Min(WalkingSpeed * dt, dist);
            Vector3 next = pos + dir * step;
            next.y = pos.y;
            transform.position = next;
            FaceDirection(dir, dt);
            SetAnimSpeed(WalkingSpeed);
        }

        bool ReachedDestination()
        {
            if (!_hasDestination) return true;
            return FlatDistance(transform.position, _destination) <= ArriveThreshold;
        }

        /// <summary>目的地が無いときの散歩 (§79 直立静止させない)。</summary>
        void Wander()
        {
            Vector3 center = _manager != null ? _manager.WanderCenter : Vector3.zero;
            float radius = _manager != null ? _manager.WanderRadius : 20f;
            MoveTo(VisitorBrain.WanderTarget(transform.position, center, radius, ref _rng));
        }

        /// <summary>行列の最後尾のあたり。ここまで来たら並ぶ (§30)。</summary>
        Vector3 QueueApproachPoint(Stall stall)
        {
            if (stall == null) return transform.position;
            var data = stall.Data;
            int maxQueue = data != null ? Mathf.Max(1, data.MaxQueueLength) : 8;
            int index = Mathf.Clamp(stall.QueueLength, 0, maxQueue - 1);
            Vector3 slot = stall.GetQueueSlotPosition(index);
            return slot.sqrMagnitude < 0.0001f ? stall.transform.position : slot;
        }

        void FaceTowards(Vector3 worldPoint, float dt)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) FaceDirection(dir, dt);
        }

        void FaceDirection(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-TurnSpeed * dt));
        }

        void SetAnimSpeed(float speed)
        {
            if (_walk == null || !_walk.enabled) return;
            _walk.SetSpeed(speed);
            _walk.SetIdle(speed < 0.05f);
        }
    }
}
