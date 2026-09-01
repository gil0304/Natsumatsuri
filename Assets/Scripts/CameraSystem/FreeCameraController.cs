using UnityEngine;

namespace Matsuri.CameraSystem
{
    /// <summary>
    /// 仕様書 §38「自由視点」。完成した祭りを好きな角度から眺めるカメラ。
    /// 右ドラッグでマウスルック、WASD で移動、Shift で加速、Q/E で上下。
    /// 地面より下には行かない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FreeCameraController : MonoBehaviour
    {
        [Header("移動")]
        public float MoveSpeed = 12f;
        public float FastMultiplier = 3.6f;
        public float SlowMultiplier = 0.3f;
        public float VerticalSpeed = 8f;

        [Header("視点")]
        public float LookSensitivity = 0.14f;
        public float MinPitch = -80f;
        public float MaxPitch = 82f;

        [Header("範囲")]
        [Tooltip("地面より下に潜らないための最低高度。")]
        public float MinHeight = 0.8f;
        public float MaxHeight = 120f;
        public float HorizontalLimit = 110f;

        [Header("滑らかさ")]
        public float MoveSmoothing = 12f;
        public float LookSmoothing = 20f;

        /// <summary>このカメラが操作対象のときだけ true。</summary>
        public bool IsActive { get; set; }

        CameraManager _owner;
        Vector3 _position;
        Vector3 _velocity;
        float _yaw;
        float _pitch;
        float _targetYaw;
        float _targetPitch;

        internal void Bind(CameraManager owner)
        {
            _owner = owner;
            _position = owner != null
                ? owner.VenueCenter + new Vector3(0f, 12f, -34f)
                : new Vector3(0f, 12f, -34f);
            _targetYaw = _yaw = 0f;
            _targetPitch = _pitch = 16f;
            Apply();
        }

        /// <summary>今映っているカメラの姿勢を引き継ぐ（モード切替をなめらかにする）。</summary>
        public void SyncFrom(Transform source)
        {
            if (source == null) return;
            _position = source.position;
            var e = source.rotation.eulerAngles;
            _targetYaw = _yaw = e.y;
            float p = e.x > 180f ? e.x - 360f : e.x;
            _targetPitch = _pitch = Mathf.Clamp(p, MinPitch, MaxPitch);
            _velocity = Vector3.zero;
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
                if (LookDragHeld)
                {
                    var d = MatsuriCameraInput.MouseDelta;
                    _targetYaw += d.x * LookSensitivity;
                    _targetPitch = Mathf.Clamp(_targetPitch - d.y * LookSensitivity, MinPitch, MaxPitch);
                }

                float speed = MoveSpeed;
                if (MatsuriCameraInput.Fast) speed *= FastMultiplier;
                if (MatsuriCameraInput.Slow) speed *= SlowMultiplier;

                var move = MatsuriCameraInput.Move;
                var rot = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 wish = rot * new Vector3(move.x, 0f, move.y) * speed;
                wish.y += MatsuriCameraInput.Roll * VerticalSpeed *
                          (MatsuriCameraInput.Fast ? FastMultiplier : 1f);
                _velocity = wish;
            }
            else
            {
                _velocity = Vector3.Lerp(_velocity, Vector3.zero, 1f - Mathf.Exp(-6f * dt));
            }

            _position += _velocity * dt;
            ClampPosition();

            float k = 1f - Mathf.Exp(-LookSmoothing * dt);
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, k);
            _pitch = Mathf.Lerp(_pitch, _targetPitch, k);

            Apply();
        }

        void ClampPosition()
        {
            // 地面より下に行かない (§38)。地面が起伏していても Raycast で拾う。
            float floor = MinHeight;
            if (Physics.Raycast(new Vector3(_position.x, 200f, _position.z), Vector3.down,
                                out var hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                floor = hit.point.y + MinHeight;

            _position.y = Mathf.Clamp(_position.y, floor, MaxHeight);
            _position.x = Mathf.Clamp(_position.x, -HorizontalLimit, HorizontalLimit);
            _position.z = Mathf.Clamp(_position.z, -HorizontalLimit, HorizontalLimit);
        }

        void Apply()
        {
            transform.SetPositionAndRotation(_position, Quaternion.Euler(_pitch, _yaw, 0f));
        }

        /// <summary>指定地点を、指定距離だけ離れた斜め上から見る。</summary>
        public void LookAtFrom(Vector3 worldPos, float distance)
        {
            Vector3 dir = new Vector3(-0.35f, 0.42f, -0.84f).normalized;
            _position = worldPos + dir * Mathf.Max(3f, distance);
            ClampPosition();
            Vector3 look = (worldPos - _position).normalized;
            if (look.sqrMagnitude > 0.0001f)
            {
                var q = Quaternion.LookRotation(look, Vector3.up).eulerAngles;
                _targetYaw = _yaw = q.y;
                float p = q.x > 180f ? q.x - 360f : q.x;
                _targetPitch = _pitch = Mathf.Clamp(p, MinPitch, MaxPitch);
            }
            _velocity = Vector3.zero;
            Apply();
        }

        /// <summary>会場全体を見渡す位置に飛ぶ。</summary>
        public void FrameWholeVenue()
        {
            Vector3 center = _owner != null ? _owner.VenueCenter : Vector3.zero;
            LookAtFrom(center, 72f);
        }
    }
}
