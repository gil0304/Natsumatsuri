using UnityEngine;

namespace Matsuri.CameraSystem
{
    /// <summary>
    /// 仕様書 §38「建設ビュー」。斜め上から見下ろす街づくりゲーム風のカメラ。
    ///
    ///   右ドラッグ    … 周回（左右に回す／俯角を変える）
    ///   中ドラッグ    … 平行移動
    ///   左ドラッグ    … 平行移動（UI の上で押し始めたときは UI に譲る）
    ///   ホイール      … 拡大縮小
    ///   WASD / 矢印   … 平行移動
    ///   Q / E         … 左右に回す
    ///   R / F         … 俯角を変える
    ///
    /// 俯角は手動が基本。しばらく触らないでいると、そのズーム距離に見合った
    /// 既定の俯角へゆっくり戻る（寄ると横から、引くと見下ろす画になる）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildCameraController : MonoBehaviour
    {
        [Header("注視点")]
        [Tooltip("見ている地面上の点。パンで動く。")]
        public Vector3 Pivot = Vector3.zero;

        [Tooltip("会場からどれだけ外に出られるか。会場は概ね -60〜60 (§18)。")]
        public float PanLimit = 78f;

        [Header("ズーム")]
        public float Distance = 34f;
        public float MinDistance = 9f;
        public float MaxDistance = 96f;
        public float ZoomSpeed = 6f;

        [Header("俯角")]
        [Tooltip("これより水平にはならない。")]
        public float MinPitch = 12f;
        [Tooltip("これより真上からにはならない。")]
        public float MaxPitch = 85f;
        [Tooltip("最も寄ったときの既定の俯角。低い＝屋台を横から見る。")]
        public float PitchAtNear = 24f;
        [Tooltip("最も引いたときの既定の俯角。高い＝真上から見下ろす。")]
        public float PitchAtFar = 62f;
        [Tooltip("手動で俯角を触ってから、既定へ戻り始めるまでの秒数。")]
        public float PitchHoldSeconds = 3f;
        [Tooltip("既定の俯角へ戻る速さ。0 にすると戻らない。")]
        public float PitchReturnSpeed = 0.45f;

        [Header("回転")]
        public float Yaw = 20f;
        public float Pitch = 34f;
        public float KeyRotateSpeed = 90f;
        public float KeyPitchSpeed = 55f;
        public float DragRotateSpeed = 0.24f;
        public float DragPitchSpeed = 0.18f;

        [Header("パン")]
        public float KeyPanSpeed = 26f;

        [Tooltip("Z / X キーでの拡大縮小の速さ（1秒あたり）。トラックパッド環境ではこれが主役になる。")]
        public float KeyZoomSpeed = 9f;

        [Tooltip("2本指スクロール（左右）で回る速さ。")]
        public float ScrollRotateSpeed = 3.2f;

        [Tooltip("2本指スクロールの左右を「回転」に使う。切ると左右は何もしない。")]
        public bool HorizontalScrollRotates = true;
        [Tooltip("マウス1ピクセルあたりの移動量。距離に比例して効きが変わる。")]
        public float DragPanSpeed = 0.0018f;

        [Header("追従の滑らかさ")]
        public float PositionSmoothing = 10f;
        public float RotationSmoothing = 12f;

        /// <summary>このカメラが操作対象のときだけ true。</summary>
        public bool IsActive { get; set; }

        CameraManager _owner;
        float _targetDistance;
        Vector3 _targetPivot;
        float _targetYaw;
        float _targetPitch;
        float _pitchTouchedAt = -999f;
        bool _leftHeldLastFrame;
        bool _leftDragPans;

        internal void Bind(CameraManager owner)
        {
            _owner = owner;
            _targetDistance = Distance;
            _targetPivot = Pivot;
            _targetYaw = Yaw;
            _targetPitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
            ApplyImmediate();
        }

        void LateUpdate()
        {
            if (IsActive) ReadInput();
            else _leftHeldLastFrame = MatsuriCameraInput.LeftHeld;

            RelaxPitchTowardDefault();

            float k = 1f - Mathf.Exp(-PositionSmoothing * Time.unscaledDeltaTime);
            float kr = 1f - Mathf.Exp(-RotationSmoothing * Time.unscaledDeltaTime);

            Pivot = Vector3.Lerp(Pivot, _targetPivot, k);
            Distance = Mathf.Lerp(Distance, _targetDistance, k);
            Yaw = Mathf.LerpAngle(Yaw, _targetYaw, kr);
            Pitch = Mathf.Lerp(Pitch, _targetPitch, kr);

            Place();
        }

        void ReadInput()
        {
            if (!MatsuriCameraInput.Available)
            {
                _leftHeldLastFrame = false;
                return;
            }

            if (MatsuriCameraInput.FrameVenuePressed) FrameWholeVenue();

            ReadZoom();
            ReadRotation();
            ReadPan();

            _targetPivot.x = Mathf.Clamp(_targetPivot.x, -PanLimit, PanLimit);
            _targetPivot.z = Mathf.Clamp(_targetPivot.z, -PanLimit, PanLimit);
            _targetPivot.y = 0f;
        }

        void ReadZoom()
        {
            float dt = Time.unscaledDeltaTime;

            // ① キーボード（Z / X、+ / -、PageUp / PageDown）
            // トラックパッドにはホイールが無いので、キーだけでも寄り引きできること。
            float zoomKey = MatsuriCameraInput.ZoomKey;
            if (Mathf.Abs(zoomKey) > 0.001f)
                ApplyZoom(zoomKey * KeyZoomSpeed * dt);

            // ② トラックパッドの2本指スクロール（上下）／マウスホイール
            float scroll = MatsuriCameraInput.NormalizeScroll(MatsuriCameraInput.ScrollDelta.y);
            if (Mathf.Abs(scroll) > 0.0001f)
                ApplyZoom(scroll * ZoomSpeed);
        }

        /// <summary>引いているときほど1目盛りの効きを大きくする（対数的なズーム感）。</summary>
        void ApplyZoom(float amount)
        {
            float step = amount * (0.35f + _targetDistance * 0.035f);
            _targetDistance = Mathf.Clamp(_targetDistance - step, MinDistance, MaxDistance);
        }

        void ReadRotation()
        {
            float dt = Time.unscaledDeltaTime;

            float roll = MatsuriCameraInput.Roll;
            if (Mathf.Abs(roll) > 0.001f)
                _targetYaw += roll * KeyRotateSpeed * dt;

            float pitchKey = MatsuriCameraInput.PitchKey;
            if (Mathf.Abs(pitchKey) > 0.001f)
                SetTargetPitch(_targetPitch + pitchKey * KeyPitchSpeed * dt);

            // トラックパッドの2本指スクロール（左右）で水平に回す。
            // 縦スクロールが拡大縮小なので、横は回転に充てるのが指の動きとして自然。
            if (HorizontalScrollRotates)
            {
                float sideways = MatsuriCameraInput.NormalizeScroll(MatsuriCameraInput.ScrollDelta.x);
                if (Mathf.Abs(sideways) > 0.0001f)
                    _targetYaw += sideways * ScrollRotateSpeed;
            }

            // Option(Alt) を押しながらドラッグすると周回。
            // トラックパッドには中ボタンも無く右ドラッグもやりにくいので、
            // 「指1本のドラッグ＝移動」「Option＋ドラッグ＝回転」で切り替える。
            bool orbitDrag = (MatsuriCameraInput.AltHeld && _leftDragPans) || MatsuriCameraInput.RightHeld;
            if (!orbitDrag) return;

            var d = MatsuriCameraInput.MouseDelta;
            if (Mathf.Abs(d.x) > 0.0001f) _targetYaw += d.x * DragRotateSpeed;
            if (Mathf.Abs(d.y) > 0.0001f) SetTargetPitch(_targetPitch - d.y * DragPitchSpeed);
        }

        void ReadPan()
        {
            Vector3 forward = Quaternion.Euler(0f, _targetYaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, _targetYaw, 0f) * Vector3.right;

            var move = MatsuriCameraInput.Move;
            if (move.sqrMagnitude > 0.0001f)
            {
                // 引いているときほど速く動く。
                float speed = KeyPanSpeed * (0.35f + _targetDistance / MaxDistance) *
                              (MatsuriCameraInput.Fast ? 2.4f : 1f);
                _targetPivot += (right * move.x + forward * move.y) * speed * Time.unscaledDeltaTime;
            }

            bool leftHeld = MatsuriCameraInput.LeftHeld;
            if (leftHeld && !_leftHeldLastFrame)
            {
                // 押し始めが UI の上なら、そのドラッグは UI のものとして扱う。
                _leftDragPans = !MatsuriCameraInput.PointerOverUi;
            }
            else if (!leftHeld)
            {
                _leftDragPans = false;
            }
            _leftHeldLastFrame = leftHeld;

            // Option を押している間のドラッグは回転なので、移動には使わない。
            bool dragPan = MatsuriCameraInput.MiddleHeld ||
                           (leftHeld && _leftDragPans && !MatsuriCameraInput.AltHeld);
            if (!dragPan) return;

            var d = MatsuriCameraInput.MouseDelta;
            if (d.sqrMagnitude < 0.0001f) return;

            // 地面をつかんで引っぱる感じ。引いているほど1ピクセルの移動量が大きい。
            float pixels = DragPanSpeed * Mathf.Max(1f, _targetDistance);
            _targetPivot -= (right * d.x + forward * d.y) * pixels;
        }

        void SetTargetPitch(float value)
        {
            _targetPitch = Mathf.Clamp(value, MinPitch, MaxPitch);
            _pitchTouchedAt = Time.unscaledTime;
        }

        /// <summary>手を離してしばらく経ったら、距離なりの既定の俯角へゆっくり戻す。</summary>
        void RelaxPitchTowardDefault()
        {
            if (PitchReturnSpeed <= 0.0001f) return;
            if (Time.unscaledTime - _pitchTouchedAt < PitchHoldSeconds) return;

            float k = 1f - Mathf.Exp(-PitchReturnSpeed * Time.unscaledDeltaTime);
            _targetPitch = Mathf.Clamp(
                Mathf.Lerp(_targetPitch, DefaultPitchFor(_targetDistance), k), MinPitch, MaxPitch);
        }

        /// <summary>ズーム距離に見合った既定の俯角 (§38)。</summary>
        public float DefaultPitchFor(float distance)
        {
            float t = Mathf.InverseLerp(MinDistance, MaxDistance, distance);
            return Mathf.Clamp(
                Mathf.Lerp(PitchAtNear, PitchAtFar, Mathf.SmoothStep(0f, 1f, t)), MinPitch, MaxPitch);
        }

        /// <summary>いまの俯角。</summary>
        public float CurrentPitch => Mathf.Clamp(Pitch, MinPitch, MaxPitch);

        void Place()
        {
            var rot = Quaternion.Euler(CurrentPitch, Yaw, 0f);
            Vector3 pos = Pivot - rot * Vector3.forward * Distance;
            // 地面にめり込まない。
            if (pos.y < 1.5f) pos.y = 1.5f;
            transform.SetPositionAndRotation(pos, rot);
        }

        void ApplyImmediate()
        {
            Pivot = _targetPivot;
            Distance = _targetDistance;
            Yaw = _targetYaw;
            Pitch = _targetPitch;
            Place();
        }

        /// <summary>指定地点に寄る (§42 のエラー箇所ジャンプなど)。</summary>
        public void FocusOn(Vector3 worldPos, float distance)
        {
            _targetPivot = new Vector3(
                Mathf.Clamp(worldPos.x, -PanLimit, PanLimit), 0f,
                Mathf.Clamp(worldPos.z, -PanLimit, PanLimit));
            _targetDistance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        }

        /// <summary>会場全体が入るところまで引く。</summary>
        public void FrameWholeVenue()
        {
            _targetPivot = _owner != null ? _owner.VenueCenter : Vector3.zero;
            _targetPivot.y = 0f;
            _targetDistance = MaxDistance * 0.85f;
        }

        /// <summary>向きを直接決める。開催演出などから使う。</summary>
        public void SetOrientation(float yaw, float pitch)
        {
            _targetYaw = yaw;
            SetTargetPitch(pitch);
        }
    }
}
