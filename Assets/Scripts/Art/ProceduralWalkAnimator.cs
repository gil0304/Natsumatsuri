using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// §79「無表情NPCが直立している」を避けるための手続きアニメ。
    /// Animator も AnimationClip も使わず、Transform を直接動かす。
    /// Humanoid のクリップが手に入ったら、このコンポーネントを外すだけで差し替えられる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralWalkAnimator : MonoBehaviour
    {
        [Header("歩行")]
        [Tooltip("1歩の長さ (m)。歩調の計算に使う。")]
        public float StrideLength = 0.62f;

        [Tooltip("脚の振り幅（度）。")]
        public float LegSwing = 32f;

        [Tooltip("腕の振り幅（度）。")]
        public float ArmSwing = 24f;

        [Tooltip("胴の上下動 (m)。")]
        public float Bounce = 0.035f;

        [Tooltip("胴の左右ロール（度）。")]
        public float Roll = 3.5f;

        [Header("待機")]
        [Tooltip("重心移動の幅 (m)。")]
        public float IdleShift = 0.018f;

        [Tooltip("呼吸の深さ。")]
        public float Breath = 0.012f;

        [Header("負荷")]
        [Tooltip("この距離より遠いと更新を止める (§58)。")]
        public float CullDistance = 70f;

        Transform _head, _body, _armL, _armR, _legL, _legR;
        Vector3 _headPos, _bodyPos, _armLPos, _armRPos, _legLPos, _legRPos;
        Quaternion _headRot, _bodyRot, _armLRot, _armRRot, _legLRot, _legRRot;

        float _phase;
        float _speed;
        bool _idle = true;
        bool _lookUp;
        float _lookBlend;
        float _personal;

        // 時々の首振り
        float _nextGlance;
        float _glanceTarget;
        float _glanceCurrent;

        static Camera s_Camera;
        float _nextVisibilityCheck;
        bool _active = true;
        bool _bound;

        void Awake()
        {
            _personal = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * 0.6180339f, 1f);
            _phase = _personal * Mathf.PI * 2f;
            Bind();
        }

        void Start() => Bind();

        /// <summary>体のパーツを名前で探して基本姿勢を覚える。ファクトリ側の子名に依存する。</summary>
        public void Bind()
        {
            if (_bound) return;
            _head = Find("Head");
            _body = Find("Body");
            _armL = Find("ArmL");
            _armR = Find("ArmR");
            _legL = Find("LegL");
            _legR = Find("LegR");
            if (_head == null && _body == null && _legL == null) return;   // まだ組み立て途中
            _bound = true;

            Capture(_head, out _headPos, out _headRot);
            Capture(_body, out _bodyPos, out _bodyRot);
            Capture(_armL, out _armLPos, out _armLRot);
            Capture(_armR, out _armRPos, out _armRRot);
            Capture(_legL, out _legLPos, out _legLRot);
            Capture(_legR, out _legRPos, out _legRRot);
            _nextGlance = Time.time + 2f + _personal * 4f;
        }

        static void Capture(Transform t, out Vector3 pos, out Quaternion rot)
        {
            if (t == null) { pos = Vector3.zero; rot = Quaternion.identity; return; }
            pos = t.localPosition;
            rot = t.localRotation;
        }

        Transform Find(string n)
        {
            var t = transform.Find(n);
            if (t != null) return t;
            // 階層が深い場合に備えて全探索する
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == n) return all[i];
            return null;
        }

        // ------------------------------------------------------------------ 外部API

        /// <summary>移動速度 (m/s)。歩調と振り幅がこれで変わる。</summary>
        public void SetSpeed(float metersPerSecond)
        {
            _speed = Mathf.Max(0f, metersPerSecond);
            if (_speed > 0.08f) _idle = false;
        }

        /// <summary>立ち止まっているか。</summary>
        public void SetIdle(bool idle)
        {
            _idle = idle;
            if (idle) _speed = 0f;
        }

        /// <summary>花火を見上げる (§26 / §61)。</summary>
        public void LookUp(bool up) => _lookUp = up;

        // ------------------------------------------------------------------ 更新

        void OnDisable() => ResetPose();

        void OnEnable()
        {
            _nextVisibilityCheck = 0f;
            _active = true;
        }

        /// <summary>更新を止めるときは必ず基本姿勢に戻す。中途半端な姿勢で固まらせない。</summary>
        public void ResetPose()
        {
            if (!_bound) return;
            Apply(_head, _headPos, _headRot);
            Apply(_body, _bodyPos, _bodyRot);
            Apply(_armL, _armLPos, _armLRot);
            Apply(_armR, _armRPos, _armRRot);
            Apply(_legL, _legLPos, _legLRot);
            Apply(_legR, _legRPos, _legRRot);
        }

        static void Apply(Transform t, Vector3 pos, Quaternion rot)
        {
            if (t == null) return;
            t.localPosition = pos;
            t.localRotation = rot;
        }

        void Update()
        {
            if (!_bound) { Bind(); if (!_bound) return; }

            if (Time.time >= _nextVisibilityCheck)
            {
                _nextVisibilityCheck = Time.time + 0.5f;
                bool near = IsNearCamera();
                if (_active && !near) ResetPose();
                _active = near;
            }
            if (!_active) return;

            float dt = Time.deltaTime;
            _lookBlend = Mathf.MoveTowards(_lookBlend, _lookUp ? 1f : 0f, dt * 2.6f);

            bool walking = !_idle && _speed > 0.08f;
            if (walking) UpdateWalk(dt);
            else UpdateIdle(dt);

            UpdateHead(dt, walking);
        }

        void UpdateWalk(float dt)
        {
            float stride = Mathf.Max(0.2f, StrideLength);
            // 1歩あたり半周期。速いほど歩調が上がる
            float cyclesPerSecond = _speed / (stride * 2f);
            _phase += dt * cyclesPerSecond * Mathf.PI * 2f;

            float s = Mathf.Sin(_phase);
            float gait = Mathf.Clamp01(_speed / 1.5f);
            float leg = LegSwing * gait;
            float arm = ArmSwing * gait;

            Apply(_legL, _legLPos, _legLRot * Quaternion.Euler(s * leg, 0f, 0f));
            Apply(_legR, _legRPos, _legRRot * Quaternion.Euler(-s * leg, 0f, 0f));
            // 腕は脚と逆位相。肩を少し開いて自然にする
            Apply(_armL, _armLPos, _armLRot * Quaternion.Euler(-s * arm, 0f, s * arm * 0.15f));
            Apply(_armR, _armRPos, _armRRot * Quaternion.Euler(s * arm, 0f, -s * arm * 0.15f));

            if (_body != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(_phase)) * Bounce * gait;
                _body.localPosition = _bodyPos + new Vector3(0f, bounce, 0f);
                _body.localRotation = _bodyRot * Quaternion.Euler(gait * 2.0f, 0f, -s * Roll * gait);
            }
        }

        void UpdateIdle(float dt)
        {
            _phase += dt * 1.1f;
            float t = _phase + _personal * 6.28f;
            float shift = Mathf.Sin(t * 0.55f);
            float breath = Mathf.Sin(t * 1.35f);

            if (_body != null)
            {
                _body.localPosition = _bodyPos + new Vector3(shift * IdleShift, breath * Breath, 0f);
                _body.localRotation = _bodyRot * Quaternion.Euler(breath * 0.9f, 0f, -shift * 1.6f);
            }
            // 腕はほぼ下がったまま、わずかに揺れる
            Apply(_armL, _armLPos, _armLRot * Quaternion.Euler(breath * 2.4f, 0f, shift * 1.4f));
            Apply(_armR, _armRPos, _armRRot * Quaternion.Euler(-breath * 2.4f, 0f, -shift * 1.4f));
            // 体重を乗せている脚が伸び、反対がゆるむ
            Apply(_legL, _legLPos, _legLRot * Quaternion.Euler(shift * 2.2f, 0f, 0f));
            Apply(_legR, _legRPos, _legRRot * Quaternion.Euler(-shift * 2.2f, 0f, 0f));
        }

        void UpdateHead(float dt, bool walking)
        {
            if (_head == null) return;

            if (Time.time >= _nextGlance)
            {
                _nextGlance = Time.time + Random.Range(2.5f, 6.5f);
                _glanceTarget = Random.Range(-38f, 38f);
            }
            _glanceCurrent = Mathf.MoveTowards(_glanceCurrent, _glanceTarget, dt * 55f);

            float bob = walking ? Mathf.Sin(_phase * 2f) * 1.6f : Mathf.Sin(_phase * 1.35f) * 0.9f;
            float pitch = Mathf.Lerp(bob, -42f, _lookBlend);
            float yaw = _glanceCurrent * (1f - _lookBlend);

            _head.localPosition = _headPos;
            _head.localRotation = _headRot * Quaternion.Euler(pitch, yaw, 0f);
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
