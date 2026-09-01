using UnityEngine;

namespace Matsuri.Visitors
{
    /// <summary>
    /// VisitorAgent の状態機械 (§28)。
    /// Entering → Browsing → MovingToStall → Queueing → BeingServed → Enjoying
    ///          → (Resting / WatchingFireworks) → Leaving → Gone
    ///
    /// TickAgent() は毎フレーム呼ばれる（遠距離のNPCだけはバケットの番にまとめて呼ばれる §57）。
    /// </summary>
    public sealed partial class VisitorAgent
    {
        public void TickAgent(float dt)
        {
            if (!_initialized || dt <= 0f) return;
            if (_state.Kind == VisitorStateKind.Gone) return;

            _state.StateTime += dt;
            _state.LifeTime += dt;

            switch (_state.Kind)
            {
                case VisitorStateKind.Entering:          TickEntering(dt); break;
                case VisitorStateKind.Browsing:          TickBrowsing(dt); break;
                case VisitorStateKind.MovingToStall:     TickMovingToStall(dt); break;
                case VisitorStateKind.Queueing:          TickQueueing(dt); break;
                case VisitorStateKind.BeingServed:       TickBeingServed(dt); break;
                case VisitorStateKind.Enjoying:          TickEnjoying(dt); break;

                // 居場所へ向かう／滞在する (§34)。
                case VisitorStateKind.MovingToAmenity:   TickMovingToAmenity(dt); break;
                case VisitorStateKind.Dancing:
                case VisitorStateKind.Praying:           TickAmenityStay(dt); break;

                // 休むのは2通りある。
                //   ベンチ (_restSpot)      … 設備をその場で使うだけの簡単なもの
                //   休憩所 (_amenity)       … 立ち位置を確保して滞在する居場所
                case VisitorStateKind.Resting:
                    if (CurrentAmenity != null) TickAmenityStay(dt);
                    else TickResting(dt);
                    break;

                case VisitorStateKind.WatchingFireworks: TickWatchingFireworks(dt); break;
                case VisitorStateKind.Leaving:           TickLeaving(dt); break;
            }

            DecayNeeds(dt);
            UpdateLookUp(dt);
            _state.ClampAll();
        }

        // ------------------------------------------------------------------

        void TickEntering(float dt)
        {
            MoveUpdate(dt);
            if (ReachedDestination() || _state.StateTime > 14f) EnterState(VisitorStateKind.Browsing);
        }

        void TickBrowsing(float dt)
        {
            MoveUpdate(dt);
            if (!_hasDestination || ReachedDestination()) Wander();
        }

        void TickMovingToStall(float dt)
        {
            if (_targetStall == null || !_targetStall.IsOpen)
            {
                _targetStall = null;
                EnterState(VisitorStateKind.Browsing);
                return;
            }

            MoveUpdate(dt);

            Vector3 approach = QueueApproachPoint(_targetStall);
            if (FlatDistance(transform.position, approach) < JoinQueueDistance)
            {
                if (_targetStall.CanAcceptQueue && _targetStall.TryJoinQueue(this))
                {
                    _state.QueueWaitTime = 0f;
                    EnterState(VisitorStateKind.Queueing);
                }
                else
                {
                    // 満員で並べなかった (§34 満足度が少し下がる)。
                    Satisfaction -= 2f;
                    _avoidStall = _targetStall;
                    _avoidTimer = 20f;
                    _targetStall = null;
                    EnterState(VisitorStateKind.Browsing);
                }
                return;
            }

            // どうしても辿り着けない（NavMesh が塞がっている等）ときの保険。
            if (_state.StateTime > MaxApproachSeconds)
            {
                _avoidStall = _targetStall;
                _avoidTimer = 20f;
                _targetStall = null;
                EnterState(VisitorStateKind.Browsing);
            }
        }

        /// <summary>行列 (§30)。自分の順番の位置まで詰めて待つ。</summary>
        void TickQueueing(float dt)
        {
            if (_targetStall == null)
            {
                EnterState(VisitorStateKind.Browsing);
                return;
            }

            if (_targetStall.IsBeingServed(this))
            {
                EnterState(VisitorStateKind.BeingServed);
                return;
            }

            int index = _targetStall.GetQueueIndex(this);
            if (index < 0)
            {
                // 何らかの理由で列から外れていた。
                _targetStall = null;
                EnterState(VisitorStateKind.Browsing);
                return;
            }

            MoveTo(_targetStall.GetQueueSlotPosition(index));
            MoveUpdate(dt);

            _state.QueueWaitTime += dt;

            var balance = _manager != null ? _manager.Balance : null;
            if (balance != null) Satisfaction -= balance.SatisfactionPerWaitSecond * dt;

            // 我慢の限界 (§34)。諦めると満足度が大きく下がる。
            if (_state.QueueWaitTime > VisitorBrain.QueuePatienceSeconds(this, _targetStall))
            {
                _targetStall.LeaveQueue(this);
                if (balance != null) Satisfaction -= balance.SatisfactionOnGiveUp;
                _avoidStall = _targetStall;
                _avoidTimer = 35f;
                _targetStall = null;
                _state.QueueWaitTime = 0f;
                EnterState(VisitorStateKind.Browsing);
            }
        }

        /// <summary>接客中。実際の購入成立は Stall 側から OnServed() で通知される。</summary>
        void TickBeingServed(float dt)
        {
            if (_targetStall == null)
            {
                EnterState(VisitorStateKind.Browsing);
                return;
            }

            Transform counter = _targetStall.CustomerPosition;
            if (counter != null)
            {
                MoveTo(counter.position);
                MoveUpdate(dt);
            }
            else
            {
                StopMoving();
            }
            FaceTowards(_targetStall.transform.position, dt);

            _state.ServeTimer += dt;
            if (_state.ServeTimer > MaxServeSeconds)
            {
                // 接客が返ってこない異常系。列から抜けて次に行く。
                _targetStall.LeaveQueue(this);
                _targetStall = null;
                EnterState(VisitorStateKind.Browsing);
            }
        }

        void TickEnjoying(float dt)
        {
            StopMoving();
            _state.EnjoyTimer += dt;
            Satisfaction += 0.25f * dt;
            if (_state.EnjoyTimer >= EnjoyDuration) EnterState(VisitorStateKind.Browsing);
        }

        /// <summary>ベンチで休む (§20 / §34)。体力と満足度が回復する。</summary>
        void TickResting(float dt)
        {
            if (_restSpot == null)
            {
                EnterState(VisitorStateKind.Browsing);
                return;
            }

            if (FlatDistance(transform.position, _restSpot.transform.position) > 1.4f)
            {
                MoveTo(_restSpot.transform.position);
                MoveUpdate(dt);
                if (_state.StateTime > 30f) { ReleaseRestSpot(); EnterState(VisitorStateKind.Browsing); }
                return;
            }

            StopMoving();
            _state.RestTimer += dt;

            float strength = _restSpot.Data != null ? Mathf.Max(1f, _restSpot.Data.EffectStrength) : 20f;
            Energy += RestRecoverPerSecond * (strength / 20f) * dt;
            Satisfaction += RestSatisfactionPerSecond * dt;

            if (_state.RestTimer >= RestDuration)
            {
                ReleaseRestSpot();
                EnterState(VisitorStateKind.Browsing);
            }
        }

        /// <summary>花火を見上げて足を止めている (§22)。</summary>
        void TickWatchingFireworks(float dt)
        {
            StopMoving();
            _state.FireworksTimer -= dt;
            if (_state.FireworksTimer > 0f) return;

            var back = _stateBeforeFireworks;
            if (back == VisitorStateKind.WatchingFireworks || back == VisitorStateKind.Gone)
                back = VisitorStateKind.Browsing;
            EnterState(back);
        }

        void TickLeaving(float dt)
        {
            if (!_hasDestination && _manager != null) MoveTo(_manager.ExitPosition);
            MoveUpdate(dt);

            Vector3 exit = _manager != null ? _manager.ExitPosition : _destination;
            if (FlatDistance(transform.position, exit) < 2.5f || _state.StateTime > 90f)
                Despawn();
        }

        // ------------------------------------------------------------------

        /// <summary>時間経過で空腹・遊びたさが増え、体力が減る (§26)。</summary>
        void DecayNeeds(float dt)
        {
            _state.Energy -= EnergyDrainPerSecond * dt;
            _state.Hunger += HungerGrowthPerSecond * dt;
            _state.Fun    += FunGrowthPerSecond * dt;
        }

        void EnterState(VisitorStateKind kind)
        {
            if (_state.Kind == kind) return;
            _state.Kind = kind;
            _state.StateTime = 0f;

            switch (kind)
            {
                case VisitorStateKind.Enjoying:    _state.EnjoyTimer = 0f; break;
                case VisitorStateKind.Resting:     _state.RestTimer = 0f; break;
                case VisitorStateKind.BeingServed: _state.ServeTimer = 0f; break;
                case VisitorStateKind.Browsing:    _hasDestination = false; break;
            }
        }
    }
}
