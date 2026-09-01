using UnityEngine;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Art
{
    /// <summary>
    /// §62「金魚すくいの水槽で金魚が泳ぐ」。
    /// 水槽（親 Transform）のローカル空間の中を、目標点を決めては向きを変えながら泳ぐ。
    /// カメラから遠い水槽では更新を止める (§58)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoldfishSwimmer : MonoBehaviour
    {
        [Tooltip("遊泳速度 (m/s)。")]
        public float Speed = 0.16f;

        [Tooltip("旋回速度 (度/秒)。")]
        public float TurnSpeed = 150f;

        [Tooltip("尾びれの振れ幅（度）。")]
        public float TailAmount = 14f;

        [Tooltip("この距離より遠いと泳ぐのをやめる。")]
        public float CullDistance = 26f;

        Vector3 _center = Vector3.zero;
        Vector2 _half = new Vector2(0.5f, 0.35f);
        float _depth = 0.05f;

        Vector3 _target;
        float _bobPhase;
        float _tailPhase;
        MRandom _rng;
        bool _configured;

        static Camera s_Camera;
        float _nextVisibilityCheck;
        bool _active = true;

        /// <summary>
        /// 泳げる範囲を決める。center/half は親のローカル座標系、depth は水面下の沈み込み。
        /// </summary>
        public void Configure(Vector3 center, Vector2 halfExtents, float depth, uint seed)
        {
            _center = center;
            _half = halfExtents;
            _depth = depth;
            _rng = new MRandom(seed == 0u ? 1u : seed);
            _bobPhase = _rng.NextFloat() * 6.283f;
            _tailPhase = _rng.NextFloat() * 6.283f;
            Speed *= _rng.NextFloat(0.75f, 1.35f);
            _configured = true;
            PickTarget();
            transform.localPosition = new Vector3(
                _center.x + _rng.NextFloat(-_half.x, _half.x),
                _center.y - _rng.NextFloat(0f, _depth),
                _center.z + _rng.NextFloat(-_half.y, _half.y));
            transform.localRotation = Quaternion.Euler(0f, _rng.NextFloat(0f, 360f), 0f);
        }

        void Awake()
        {
            if (!_configured)
            {
                _rng = new MRandom((uint)(Mathf.Abs(GetInstanceID()) + 1));
                PickTarget();
            }
        }

        void PickTarget()
        {
            _target = new Vector3(
                _center.x + _rng.NextFloat(-_half.x, _half.x),
                _center.y - _rng.NextFloat(0f, _depth),
                _center.z + _rng.NextFloat(-_half.y, _half.y));
        }

        void Update()
        {
            if (Time.time >= _nextVisibilityCheck)
            {
                _nextVisibilityCheck = Time.time + 0.5f;
                _active = IsNearCamera();
            }
            if (!_active) return;

            float dt = Time.deltaTime;
            Vector3 pos = transform.localPosition;
            Vector3 to = _target - pos;
            to.y = 0f;

            if (to.sqrMagnitude < 0.0045f)
            {
                PickTarget();
                to = _target - pos;
                to.y = 0f;
            }

            if (to.sqrMagnitude > 1e-6f)
            {
                Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, want, TurnSpeed * dt);
            }

            // 前進は「体の向き」に沿って。旋回中は少し減速して魚らしくする
            float align = Vector3.Dot(transform.localRotation * Vector3.forward, to.sqrMagnitude > 1e-6f ? to.normalized : Vector3.forward);
            float speed = Speed * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(align));
            pos += (transform.localRotation * Vector3.forward) * speed * dt;

            _bobPhase += dt * 1.7f;
            float targetY = Mathf.Lerp(pos.y, _target.y, dt * 1.2f);
            pos.y = targetY + Mathf.Sin(_bobPhase) * 0.006f;

            // 水槽からはみ出さない
            pos.x = Mathf.Clamp(pos.x, _center.x - _half.x, _center.x + _half.x);
            pos.z = Mathf.Clamp(pos.z, _center.z - _half.y, _center.z + _half.y);
            pos.y = Mathf.Clamp(pos.y, _center.y - _depth, _center.y - 0.004f);
            transform.localPosition = pos;

            // 尾びれ：進行方向のまわりに体をくねらせる
            _tailPhase += dt * (6f + speed * 12f);
            float wag = Mathf.Sin(_tailPhase);
            transform.localRotation *= Quaternion.Euler(0f, wag * TailAmount * 0.35f, wag * TailAmount * 0.5f);
        }

        bool IsNearCamera()
        {
            if (s_Camera == null || !s_Camera.isActiveAndEnabled)
                s_Camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (s_Camera == null) return true;
            float d2 = (s_Camera.transform.position - transform.position).sqrMagnitude;
            return d2 <= CullDistance * CullDistance;
        }
    }
}
