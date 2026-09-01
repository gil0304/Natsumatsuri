using Matsuri.Data;
using Matsuri.Festival;
using UnityEngine;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場者が「屋台で買う」以外にすること (§34)。
    /// 盆踊り場で踊る・休憩所で休む・神社で参拝する。
    ///
    /// MovingToAmenity → (Resting | Dancing | Praying) → Browsing
    ///
    /// 滞在中は Facility が持つ毎秒の増減（満足度・体力・遊びたさ）を積み、
    /// Facility.StayMinutes（ゲーム内の分）を実秒に直した時間が過ぎたら立ち位置を返す (§7)。
    /// </summary>
    public sealed partial class VisitorAgent
    {
        /// <summary>施設に辿り着けないときの保険（実時間・秒）。</summary>
        const float MaxAmenityApproachSeconds = 40f;

        /// <summary>同じ施設に居座らないよう、出たあと避ける秒数。</summary>
        const float AmenityAvoidSeconds = 30f;

        /// <summary>踊りの体の振り（度）。</summary>
        const float DanceYawSwing = 22f;

        /// <summary>座ったときに体を沈める量 (m)。</summary>
        const float SitDrop = 0.22f;

        /// <summary>お辞儀の深さ（度）。</summary>
        const float BowAngle = 34f;

        /// <summary>いま使っている施設。来場者視点カメラの表示にも使う (§38)。</summary>
        public Facility CurrentAmenity => _amenity;

        // ================================================================
        // 施設へ向かう
        // ================================================================

        /// <summary>
        /// 施設を確保して向かい始める。立ち位置が取れなければ false。
        /// 立ち位置は施設が配るので、盆踊りなら輪の一員に、神社なら賽銭箱の前に並ぶ。
        /// </summary>
        bool BeginGoToAmenity(Facility facility, float score)
        {
            if (facility == null) return false;
            if (facility == _amenity) return true;

            if (!facility.TryOccupy(this, out Vector3 slot)) return false;

            ReleaseAmenity();          // 前に押さえていた所があれば返す
            LeaveCurrentQueue();

            _amenity = facility;
            _amenitySlot = slot;
            _amenityScore = score;

            var balance = _manager != null ? _manager.Balance : null;
            _amenityStaySeconds = VisitorBrain.AmenityStaySeconds(facility, balance);

            _targetStall = null;
            _lastTargetScore = float.NegativeInfinity;

            EnterState(VisitorStateKind.MovingToAmenity);
            MoveTo(slot);
            return true;
        }

        /// <summary>施設へ歩いている間 (§28)。</summary>
        void TickMovingToAmenity(float dt)
        {
            if (_amenity == null)
            {
                EnterState(VisitorStateKind.Browsing);
                return;
            }

            MoveTo(_amenitySlot);
            MoveUpdate(dt);

            if (FlatDistance(transform.position, _amenitySlot) <= VisitorBrain.AmenityArriveDistance)
            {
                EnterState(StateForEffect(_amenity.Effect));
                return;
            }

            // どうしても辿り着けない（NavMesh が塞がっている等）ときの保険。
            if (_state.StateTime > MaxAmenityApproachSeconds)
            {
                AvoidCurrentAmenity();
                EnterState(VisitorStateKind.Browsing);
            }
        }

        /// <summary>施設の効果に対応する滞在中の状態。</summary>
        static VisitorStateKind StateForEffect(FacilityEffect effect)
        {
            switch (effect)
            {
                case FacilityEffect.Dance:   return VisitorStateKind.Dancing;
                case FacilityEffect.Worship:
                case FacilityEffect.Purify:  return VisitorStateKind.Praying;
                default:                     return VisitorStateKind.Resting;
            }
        }

        // ================================================================
        // 滞在中（休憩・踊り・参拝で共通）
        // ================================================================

        /// <summary>
        /// 施設に滞在している間 (§34)。ここが「満足度が上がる」の本体。
        /// 毎秒 SatisfactionPerSecond / EnergyPerSecond / FunPerSecond を積む。
        /// </summary>
        void TickAmenityStay(float dt)
        {
            if (_amenity == null)
            {
                ResetAmenityPose();
                EnterState(VisitorStateKind.Browsing);
                return;
            }

            // 立ち位置から離れていたら、まず歩いて戻る。
            if (FlatDistance(transform.position, _amenitySlot) > VisitorBrain.AmenityArriveDistance + 0.4f)
            {
                MoveTo(_amenitySlot);
                MoveUpdate(dt);
                if (_state.StateTime > MaxAmenityApproachSeconds)
                {
                    AvoidCurrentAmenity();
                    EnterState(VisitorStateKind.Browsing);
                }
                return;
            }

            StopMoving();
            _state.AmenityTimer += dt;
            _state.RestTimer += dt;      // 旧来の表示・デバッグ用に同じ値を持たせておく

            var balance = _manager != null ? _manager.Balance : null;
            float satisfactionScale = balance != null ? Mathf.Max(0f, balance.AmenitySatisfactionScale) : 1f;

            // 満足度の上限は 100。Satisfaction のセッターが Clamp する。
            Satisfaction += _amenity.SatisfactionPerSecond * satisfactionScale * dt;
            Energy       += _amenity.EnergyPerSecond * dt;
            Fun          += _amenity.FunPerSecond * dt;

            UpdateAmenityPose(dt);

            if (_amenityStaySeconds <= 0f || _state.AmenityTimer >= _amenityStaySeconds)
            {
                ResetAmenityPose();
                AvoidCurrentAmenity();
                EnterState(VisitorStateKind.Browsing);
            }
        }

        /// <summary>いま居る施設を返し、しばらく選ばれないようにする。</summary>
        void AvoidCurrentAmenity()
        {
            _avoidAmenity = _amenity;
            _avoidAmenityTimer = AmenityAvoidSeconds;
            ReleaseAmenity();
        }

        /// <summary>押さえていた立ち位置を施設に返す。</summary>
        void ReleaseAmenity()
        {
            if (_amenity == null) return;
            _amenity.Release(this);
            _amenity = null;
            _amenityStaySeconds = 0f;
            _state.AmenityTimer = 0f;
            ResetAmenityPose();
        }

        // ================================================================
        // 滞在中の見た目 (§79 「直立で棒立ち」を避ける)
        // ================================================================

        /// <summary>
        /// 滞在中の体の動き。ProceduralWalkAnimator は歩行専用なので、
        /// ここでは待機姿勢にしたうえで体そのものを揺らす。
        /// </summary>
        void UpdateAmenityPose(float dt)
        {
            if (_body == null) return;

            // 遠くの人は動かさない (§57)。姿勢だけ戻しておく。
            if (_simplified) { ResetAmenityPose(); return; }

            switch (_state.Kind)
            {
                case VisitorStateKind.Dancing: ApplyDancePose(dt); break;
                case VisitorStateKind.Praying: ApplyPrayPose(dt); break;
                case VisitorStateKind.Resting: ApplySitPose(); break;
                default: ResetAmenityPose(); break;
            }
        }

        /// <summary>盆踊り。やぐらの方を向いて、体を左右に振りながら手を返す。</summary>
        void ApplyDancePose(float dt)
        {
            if (_amenity != null) FaceTowards(_amenity.transform.position, dt);

            // 人ごとに位相をずらす。全員が同じ動きだと嘘くさい (§79)。
            float phase = (_state.Seed % 1000u) * 0.0062831853f;
            float t = Time.time * 2.4f + phase;

            float yaw = Mathf.Sin(t) * DanceYawSwing;
            float lean = Mathf.Sin(t * 0.5f) * 5f;
            float bob = Mathf.Abs(Mathf.Sin(t)) * 0.055f;

            _body.transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 2f) * 4f, yaw, lean);
            _body.transform.localPosition = new Vector3(Mathf.Sin(t) * 0.07f, bob, 0f);
        }

        /// <summary>参拝。二礼二拍手一礼をおおまかに再現する。</summary>
        void ApplyPrayPose(float dt)
        {
            if (_amenity != null) FaceTowards(_amenity.transform.position, dt);

            float total = Mathf.Max(0.01f, _amenityStaySeconds);
            float u = Mathf.Clamp01(_state.AmenityTimer / total);

            // 0.05-0.30: 二礼 / 0.35-0.55: 二拍手（体が小さく弾む） / 0.75-0.95: 一礼
            float bow01 = 0f;
            if (u < 0.30f) bow01 = BowCurve(u, 0.05f, 0.30f, 2);
            else if (u > 0.72f) bow01 = BowCurve(u, 0.72f, 0.96f, 1);

            float clap = (u >= 0.34f && u <= 0.58f)
                ? Mathf.Abs(Mathf.Sin((u - 0.34f) / 0.24f * Mathf.PI * 2f)) * 0.035f
                : 0f;

            _body.transform.localRotation = Quaternion.Euler(BowAngle * bow01, 0f, 0f);
            _body.transform.localPosition = new Vector3(0f, clap - bow01 * 0.05f, bow01 * 0.10f);
        }

        /// <summary>指定区間で count 回お辞儀する 0-1 のカーブ。</summary>
        static float BowCurve(float u, float from, float to, int count)
        {
            float span = Mathf.Max(0.0001f, to - from);
            float local = Mathf.Clamp01((u - from) / span);
            return Mathf.Abs(Mathf.Sin(local * Mathf.PI * count));
        }

        /// <summary>縁台に腰かける。腰を落として少し前かがみにする。</summary>
        void ApplySitPose()
        {
            _body.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            _body.transform.localPosition = new Vector3(0f, -SitDrop, 0.05f);
        }

        /// <summary>体の姿勢を立ち姿に戻す。</summary>
        void ResetAmenityPose()
        {
            if (_body == null) return;
            if (_body.transform.localPosition == Vector3.zero &&
                _body.transform.localRotation == Quaternion.identity) return;

            _body.transform.localPosition = Vector3.zero;
            _body.transform.localRotation = Quaternion.identity;
        }
    }
}
