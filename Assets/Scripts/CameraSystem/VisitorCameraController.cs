using Matsuri.Visitors;
using UnityEngine;

namespace Matsuri.CameraSystem
{
    /// <summary>
    /// 仕様書 §38「来場者視点」。NPC と同じ目線の高さ (1.5m) で祭りを見る。
    /// 特定の来場者を追従するモードと、自分で会場を歩き回るモードを持つ。
    ///
    ///   右ドラッグ … 見回す
    ///   WASD       … 歩く（追従中に歩き出すと、その人からは離れる）
    ///   Shift      … 走る
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisitorCameraController : MonoBehaviour
    {
        [Header("目線")]
        [Tooltip("来場者と同じ目線の高さ (§38)。")]
        public float EyeHeight = 1.5f;

        [Tooltip("追従時、頭のどれだけ後ろに置くか。0 にすると完全な一人称。")]
        public float ShoulderOffset = 0.45f;

        [Header("歩き回るとき")]
        public float WalkSpeed = 3.4f;
        public float RunMultiplier = 2.6f;
        public float LookSensitivity = 0.14f;
        public float MinPitch = -70f;
        public float MaxPitch = 70f;
        public float HorizontalLimit = 78f;

        [Header("滑らかさ")]
        public float FollowSmoothing = 9f;
        public float LookSmoothing = 14f;
        [Tooltip("歩行に合わせた上下の揺れ。0 にすると揺れない。")]
        public float HeadBobAmount = 0.035f;
        public float HeadBobSpeed = 8.5f;

        /// <summary>このカメラが操作対象のときだけ true。</summary>
        public bool IsActive { get; set; }

        /// <summary>追いかけている来場者。null なら自分で歩き回るモード。</summary>
        public VisitorAgent Target { get; private set; }

        CameraManager _owner;
        Vector3 _position;
        float _yaw, _pitch, _targetYaw, _targetPitch;
        float _bobPhase;

        internal void Bind(CameraManager owner)
        {
            _owner = owner;
            _position = (_owner != null ? _owner.EntrancePosition : Vector3.zero) + Vector3.up * EyeHeight;
            _targetYaw = _yaw = 0f;
            _targetPitch = _pitch = 0f;
            Apply();
        }

        /// <summary>今映っているカメラの向きを引き継ぐ。</summary>
        public void SyncFrom(Transform source)
        {
            if (source == null) return;
            var e = source.rotation.eulerAngles;
            _targetYaw = _yaw = e.y;
            float p = e.x > 180f ? e.x - 360f : e.x;
            _targetPitch = _pitch = Mathf.Clamp(p, MinPitch, MaxPitch);
        }

        /// <summary>追いかける来場者を決める。null を渡すと自分で歩き回るモードに戻る。</summary>
        public void SetTarget(VisitorAgent v)
        {
            Target = v;
            if (v != null)
            {
                // 追従開始時に飛び移る。
                _position = v.transform.position + Vector3.up * EyeHeight;
                _yaw = _targetYaw = v.transform.eulerAngles.y;
            }
        }

        /// <summary>指定地点まで視点をワープさせる（歩き回るモード）。</summary>
        public void WalkTo(Vector3 worldPos)
        {
            Target = null;
            _position = new Vector3(worldPos.x, GroundHeight(worldPos) + EyeHeight, worldPos.z - 4f);
            Vector3 look = (worldPos - _position);
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                _targetYaw = _yaw = Quaternion.LookRotation(look.normalized, Vector3.up).eulerAngles.y;
            Apply();
        }

        /// <summary>
        /// 見回しのドラッグが押されているか。
        /// トラックパッドには中ボタンが無く右ドラッグもやりにくいので、
        /// **指1本のドラッグ**で見回せるようにしてある（UI の上で押し始めたときは除く）。
        /// マウスの右ドラッグも従来どおり使える。
        /// </summary>
        static bool LookDragHeld =>
            MatsuriCameraInput.RightHeld ||
            (MatsuriCameraInput.LeftHeld && !MatsuriCameraInput.PointerOverUi);

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            if (IsActive && MatsuriCameraInput.Available)
            {
                // 追従中でも見回しはできる。
                if (LookDragHeld)
                {
                    var d = MatsuriCameraInput.MouseDelta;
                    _targetYaw += d.x * LookSensitivity;
                    _targetPitch = Mathf.Clamp(_targetPitch - d.y * LookSensitivity, MinPitch, MaxPitch);
                }

                var move = MatsuriCameraInput.Move;
                if (move.sqrMagnitude > 0.0001f)
                {
                    // 誰かに付いて回っている最中でも、自分で歩き出したらその人からは離れる。
                    if (Target != null)
                    {
                        Target = null;
                        _position = transform.position;
                    }

                    float speed = WalkSpeed * (MatsuriCameraInput.Fast ? RunMultiplier : 1f);
                    Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                    Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
                    _position += (fwd * move.y + right * move.x) * speed * dt;
                    _bobPhase += dt * HeadBobSpeed * Mathf.Clamp01(move.magnitude);
                }
            }

            if (Target != null && Target.isActiveAndEnabled)
            {
                var tt = Target.transform;
                Vector3 desired = tt.position + Vector3.up * EyeHeight - tt.forward * ShoulderOffset;
                float k = 1f - Mathf.Exp(-FollowSmoothing * dt);
                _position = Vector3.Lerp(_position, desired, k);

                // 見回していないときは、その人が向いている方を見る。
                if (!LookDragHeld)
                    _targetYaw = Mathf.LerpAngle(_targetYaw, tt.eulerAngles.y, k);

                _bobPhase += dt * HeadBobSpeed;
            }
            else
            {
                if (Target != null) Target = null;   // 帰ってしまった人は離す
                _position.x = Mathf.Clamp(_position.x, -HorizontalLimit, HorizontalLimit);
                _position.z = Mathf.Clamp(_position.z, -HorizontalLimit, HorizontalLimit);
                _position.y = GroundHeight(_position) + EyeHeight;
            }

            float kl = 1f - Mathf.Exp(-LookSmoothing * dt);
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, kl);
            _pitch = Mathf.Lerp(_pitch, _targetPitch, kl);

            Apply();
        }

        float GroundHeight(Vector3 at)
        {
            if (Physics.Raycast(new Vector3(at.x, 60f, at.z), Vector3.down,
                                out var hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }

        void Apply()
        {
            Vector3 p = _position;
            if (HeadBobAmount > 0f) p.y += Mathf.Sin(_bobPhase) * HeadBobAmount;
            transform.SetPositionAndRotation(p, Quaternion.Euler(_pitch, _yaw, 0f));
        }
    }
}
