using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>揺れ方の種類 (§63)。</summary>
    public enum SwayMode
    {
        /// <summary>Transform の回転で揺らす。提灯・のぼりの竿・木。</summary>
        Rotate,
        /// <summary>メッシュ頂点を波打たせる。暖簾・のぼりの布。</summary>
        Cloth,
        /// <summary>テクスチャUVを流す。水面 (§62)。</summary>
        WaterScroll
    }

    /// <summary>
    /// §63「風で揺れる」。提灯・暖簾・のぼり・木を、風の位相をずらしながら揺らす。
    /// カメラから遠い物は自動で更新を止める (§58)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwayAnimator : MonoBehaviour
    {
        [Tooltip("揺れ幅。Rotate は度、Cloth はメートル、WaterScroll は 1 秒あたりの UV 移動量。")]
        public float Amount = 5f;

        [Tooltip("揺れの速さ。")]
        public float Speed = 1f;

        public SwayMode Mode = SwayMode.Rotate;

        [Tooltip("Rotate のときの主な回転軸（ローカル）。")]
        public Vector3 Axis = Vector3.forward;

        [Tooltip("この距離より遠いカメラからは更新しない。")]
        public float CullDistance = 55f;

        [Tooltip("Cloth のとき、上端をどれだけ固定するか（0=全部動く）。")]
        public float Anchor = 0.15f;

        float _phase;
        Quaternion _baseRotation;
        Transform _tr;

        // Cloth 用
        Mesh _clothMesh;
        Vector3[] _clothBase;
        Vector3[] _clothWork;
        float _clothHeight = 1f;
        float _clothWidth = 1f;

        // WaterScroll 用
        Renderer _renderer;
        Vector2 _scrollTiling = new Vector2(3f, 3f);
        Vector2 _scroll;

        static Camera s_Camera;
        float _nextVisibilityCheck;
        bool _active = true;

        bool _setupDone;

        void Awake()
        {
            _tr = transform;
            _phase = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * 0.6180339f, 1f) * Mathf.PI * 2f;
        }

        void Start() => EnsureSetup();

        /// <summary>
        /// 基本姿勢の取得とモード別の準備。生成直後に姿勢を決める作りなので、
        /// Awake ではなく「組み立てが終わった後」に一度だけ走らせる。
        /// </summary>
        public void EnsureSetup()
        {
            if (_setupDone) return;
            _setupDone = true;
            if (_tr == null) _tr = transform;
            _baseRotation = _tr.localRotation;
            if (Mode == SwayMode.Cloth) SetupCloth();
            if (Mode == SwayMode.WaterScroll) _renderer = GetComponent<Renderer>();
        }

        void OnEnable()
        {
            _nextVisibilityCheck = 0f;
            _active = true;
        }

        void OnDisable()
        {
            // 止まったときに変な姿勢で固まらないよう、基本姿勢に戻す
            if (Mode == SwayMode.Rotate && _tr != null && _setupDone) _tr.localRotation = _baseRotation;
            if (Mode == SwayMode.Cloth && _clothMesh != null && _clothBase != null)
                _clothMesh.SetVertices(_clothBase);
        }

        void OnDestroy()
        {
            if (_clothMesh == null) return;
            if (Application.isPlaying) Destroy(_clothMesh);
            else DestroyImmediate(_clothMesh);
        }

        void SetupCloth()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Mode = SwayMode.Rotate; return; }

            // 共有メッシュを壊さないよう、この布だけの複製を持つ
            _clothMesh = Instantiate(mf.sharedMesh);
            _clothMesh.name = mf.sharedMesh.name + "_Sway";
            _clothMesh.MarkDynamic();
            mf.sharedMesh = _clothMesh;

            _clothBase = _clothMesh.vertices;
            _clothWork = new Vector3[_clothBase.Length];
            float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < _clothBase.Length; i++)
            {
                var v = _clothBase[i];
                if (v.y < minY) minY = v.y;
                if (v.y > maxY) maxY = v.y;
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
            }
            _clothHeight = Mathf.Max(0.01f, maxY - minY);
            _clothWidth = Mathf.Max(0.01f, maxX - minX);
        }

        /// <summary>水面のタイリングを指定する。UV を流すときの基準になる。</summary>
        public void SetWaterTiling(Vector2 tiling) => _scrollTiling = tiling;

        void Update()
        {
            if (!_setupDone) EnsureSetup();
            if (Time.time >= _nextVisibilityCheck)
            {
                _nextVisibilityCheck = Time.time + 0.5f;
                _active = IsNearCamera();
            }
            if (!_active) return;

            float t = Time.time * Mathf.Max(0.01f, Speed) + _phase;

            switch (Mode)
            {
                case SwayMode.Rotate: UpdateRotate(t); break;
                case SwayMode.Cloth: UpdateCloth(t); break;
                case SwayMode.WaterScroll: UpdateWater(); break;
            }
        }

        void UpdateRotate(float t)
        {
            // 主軸の揺れに、直交方向の小さな揺れを足して単調さを消す
            float a = Mathf.Sin(t) * Amount;
            float b = Mathf.Sin(t * 0.63f + 1.7f) * Amount * 0.35f;
            Vector3 axis = Axis.sqrMagnitude < 1e-6f ? Vector3.forward : Axis.normalized;
            Vector3 other = Vector3.Cross(axis, Vector3.up);
            if (other.sqrMagnitude < 1e-4f) other = Vector3.right;
            _tr.localRotation = _baseRotation
                                * Quaternion.AngleAxis(a, axis)
                                * Quaternion.AngleAxis(b, other.normalized);
        }

        void UpdateCloth(float t)
        {
            if (_clothMesh == null || _clothBase == null) return;
            float amp = Amount;
            float invH = 1f / _clothHeight;
            float invW = 1f / _clothWidth;

            for (int i = 0; i < _clothBase.Length; i++)
            {
                Vector3 v = _clothBase[i];
                // 上端(y=0)からの垂れ具合。下ほど大きく動く
                float drop = Mathf.Clamp01(-v.y * invH);
                float w = Mathf.Max(0f, drop - Anchor) / Mathf.Max(0.0001f, 1f - Anchor);
                w = w * w;
                float u = v.x * invW;
                float wave = Mathf.Sin(t * 2.1f + u * 5.5f) * 0.7f + Mathf.Sin(t * 1.3f + u * 2.1f) * 0.3f;
                v.z += wave * amp * w;
                v.x += Mathf.Sin(t * 0.9f + u * 1.3f) * amp * 0.25f * w;
                v.y += -Mathf.Abs(wave) * amp * 0.12f * w;
                _clothWork[i] = v;
            }
            _clothMesh.SetVertices(_clothWork);
            _clothMesh.RecalculateBounds();
        }

        void UpdateWater()
        {
            if (_renderer == null) return;
            _scroll.x = Mathf.Repeat(_scroll.x + Time.deltaTime * Amount * Speed, 1f);
            _scroll.y = Mathf.Repeat(_scroll.y + Time.deltaTime * Amount * Speed * 0.63f, 1f);
            ArtParts.SetTextureOffset(_renderer, _scrollTiling, _scroll);
        }

        bool IsNearCamera()
        {
            if (s_Camera == null || !s_Camera.isActiveAndEnabled)
                s_Camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (s_Camera == null) return true;
            float d2 = (s_Camera.transform.position - _tr.position).sqrMagnitude;
            return d2 <= CullDistance * CullDistance;
        }

        /// <summary>ファクトリから一括で設定するための入り口。</summary>
        public static SwayAnimator Attach(GameObject go, SwayMode mode, float amount, float speed)
        {
            var s = go.AddComponent<SwayAnimator>();
            s.Mode = mode;
            s.Amount = amount;
            s.Speed = speed;
            return s;   // 準備は Start（＝組み立て完了後）で走る
        }
    }
}
