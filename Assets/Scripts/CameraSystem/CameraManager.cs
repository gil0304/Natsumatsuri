using System;
using System.Collections;
using Matsuri.Audio;
using Matsuri.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Matsuri.CameraSystem
{
    /// <summary>仕様書 §38。カメラの3モード。</summary>
    public enum CameraMode
    {
        /// <summary>建設ビュー。斜め上から見下ろす街づくりゲーム風。</summary>
        Build,
        /// <summary>自由視点。完成した祭りを好きな角度から見る。</summary>
        Free,
        /// <summary>来場者視点。NPC と同じ目線の高さで会場を歩く。</summary>
        Visitor
    }

    /// <summary>
    /// 仕様書 §38 / §80。Cinemachine 3 の仮想カメラを3本＋開催演出用1本を持ち、
    /// Priority を切り替えることでブレンドしながらモードを移動する。
    /// 各モードの操作は専用の Controller が担当する（このクラスは切替と演出だけ）。
    /// </summary>
    public sealed class CameraManager : MonoBehaviour
    {
        [Header("会場 (Bootstrap から設定してよい)")]
        [Tooltip("会場の中心。開催演出のカメラが寄っていく先。")]
        public Vector3 VenueCenter = Vector3.zero;

        [Tooltip("入り口の位置。開催演出はここの上空から始まる。")]
        public Vector3 EntrancePosition = new Vector3(0f, 0f, -52f);

        [Header("ブレンド")]
        [Tooltip("モード切替にかける秒数 (§38)。")]
        public float BlendSeconds = 1.2f;

        [Header("開催演出 (§80)")]
        public float OpeningDuration = 6.4f;
        [Tooltip("屋台の灯りが一斉に点く合図を出すまでの秒数。")]
        public float OpeningLightsCueTime = 1.5f;

        CinemachineCamera _buildCam;
        CinemachineCamera _freeCam;
        CinemachineCamera _visitorCam;
        CinemachineCamera _openingCam;
        Coroutine _opening;

        /// <summary>現在のモード。</summary>
        public CameraMode Mode { get; private set; } = CameraMode.Build;

        /// <summary>実際に描画しているカメラ（CinemachineBrain が乗っている）。</summary>
        public Camera MainCamera { get; private set; }

        public BuildCameraController Build { get; private set; }
        public FreeCameraController Free { get; private set; }
        public VisitorCameraController VisitorView { get; private set; }

        /// <summary>開催演出の再生中は true。</summary>
        public bool IsPlayingOpening { get; private set; }

        /// <summary>モードが変わったときに通知する。UI のモード表示に使う。</summary>
        public event Action<CameraMode> ModeChanged;

        /// <summary>開催演出で「屋台の灯りが一斉に点く」瞬間の合図 (§80)。</summary>
        public event Action OpeningLightsCue;

        /// <summary>画面隅の操作ヒント。</summary>
        public CameraHintOverlay Hint { get; private set; }

        [Header("操作")]
        [Tooltip("C キーでカメラモードを切り替える。文字入力中は無効。")]
        public bool AllowCycleHotkey = true;

        [Tooltip("画面右下に操作ヒントを出す。")]
        public bool ShowHint = true;

        bool _initialized;

        void Awake()
        {
            if (!_initialized) Initialize();
        }

        void Update()
        {
            // 会場をクリックしたら、コードエディターから入力の主導権を取り戻す。
            // これをしないと、一度コードを書いた後ずっと WASD がエディターに吸われ、
            // 「カメラが拡大縮小しかできない」状態に見えてしまう (§38)。
            if (MatsuriCameraInput.AnyMouseButtonPressedThisFrame &&
                !MatsuriCameraInput.PointerOverUi)
                MatsuriCameraInput.ReleaseTextInputFocus();

            // C キーでモードを回す。コードを打っている間は効かせない (§38)。
            if (!AllowCycleHotkey || IsPlayingOpening) return;
            if (MatsuriCameraInput.CycleModePressed) CycleMode();
        }

        /// <summary>カメラ一式を組み立てる。二度呼んでも安全。</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            SetupMainCamera();

            _buildCam = CreateVirtualCamera("CM_Build", 42f);
            Build = _buildCam.gameObject.AddComponent<BuildCameraController>();
            Build.Bind(this);

            _freeCam = CreateVirtualCamera("CM_Free", 58f);
            Free = _freeCam.gameObject.AddComponent<FreeCameraController>();
            Free.Bind(this);

            _visitorCam = CreateVirtualCamera("CM_Visitor", 62f);
            VisitorView = _visitorCam.gameObject.AddComponent<VisitorCameraController>();
            VisitorView.Bind(this);

            _openingCam = CreateVirtualCamera("CM_Opening", 46f);

            if (ShowHint)
            {
                var hintGo = new GameObject("CameraHint");
                hintGo.transform.SetParent(transform, false);
                Hint = hintGo.AddComponent<CameraHintOverlay>();
            }

            ApplyMode(CameraMode.Build, instant: true);
            MatsuriLog.Info("CameraManager を初期化しました。");
        }

        void SetupMainCamera()
        {
            MainCamera = Camera.main;
            if (MainCamera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                MainCamera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            MainCamera.nearClipPlane = 0.08f;
            MainCamera.farClipPlane = 800f;

            if (MainCamera.GetComponent<HDAdditionalCameraData>() == null)
                MainCamera.gameObject.AddComponent<HDAdditionalCameraData>();

            var brain = MainCamera.GetComponent<CinemachineBrain>();
            if (brain == null) brain = MainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, Mathf.Max(0.01f, BlendSeconds));
        }

        CinemachineCamera CreateVirtualCamera(string name, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<CinemachineCamera>();

            var lens = LensSettings.Default;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.08f;
            lens.FarClipPlane = 800f;
            cam.Lens = lens;

            cam.Target = new CameraTarget();
            cam.Priority = new PrioritySettings { Value = 0 };
            return cam;
        }

        // ── モード切替 (§38) ───────────────────────────────────

        public void SetMode(CameraMode m)
        {
            if (IsPlayingOpening) return;
            ApplyMode(m, instant: false);
        }

        /// <summary>Build → Free → Visitor → Build の順に切り替える。</summary>
        public void CycleMode()
        {
            var next = Mode switch
            {
                CameraMode.Build => CameraMode.Free,
                CameraMode.Free => CameraMode.Visitor,
                _ => CameraMode.Build
            };
            SetMode(next);
        }

        void ApplyMode(CameraMode m, bool instant)
        {
            bool changed = Mode != m || instant;
            Mode = m;

            SetPriority(_buildCam, m == CameraMode.Build ? 20 : 0);
            SetPriority(_freeCam, m == CameraMode.Free ? 20 : 0);
            SetPriority(_visitorCam, m == CameraMode.Visitor ? 20 : 0);
            SetPriority(_openingCam, 0);

            if (Build != null) Build.IsActive = m == CameraMode.Build;
            if (Free != null) Free.IsActive = m == CameraMode.Free;
            if (VisitorView != null) VisitorView.IsActive = m == CameraMode.Visitor;

            // 自由視点と来場者視点は「今映っている画」から始めると切替が自然になる。
            if (!instant && MainCamera != null)
            {
                if (m == CameraMode.Free) Free?.SyncFrom(MainCamera.transform);
                else if (m == CameraMode.Visitor) VisitorView?.SyncFrom(MainCamera.transform);
            }

            if (changed)
            {
                if (Hint != null) Hint.ShowFor(m);
                ModeChanged?.Invoke(m);
                MatsuriLog.Info($"カメラモード: {ModeLabel(m)}");
            }
        }

        static void SetPriority(CinemachineCamera cam, int value)
        {
            if (cam == null) return;
            cam.Priority = new PrioritySettings { Value = value };
        }

        /// <summary>UI 表示用の日本語ラベル。</summary>
        public static string ModeLabel(CameraMode m) => m switch
        {
            CameraMode.Build => "建設ビュー",
            CameraMode.Free => "自由視点",
            _ => "来場者視点"
        };

        // ── 注目 / 追従 ────────────────────────────────────────

        /// <summary>指定した場所に寄る。エラー行の屋台を見せるときなどに使う (§42)。</summary>
        public void FocusOn(Vector3 worldPos, float distance = 18f)
        {
            if (!_initialized) Initialize();
            switch (Mode)
            {
                case CameraMode.Build:
                    Build?.FocusOn(worldPos, distance);
                    break;
                case CameraMode.Free:
                    Free?.LookAtFrom(worldPos, distance);
                    break;
                default:
                    VisitorView?.WalkTo(worldPos);
                    break;
            }
        }

        /// <summary>特定の来場者を追いかける (§38 来場者視点)。</summary>
        public void FollowVisitor(Visitors.VisitorAgent v)
        {
            if (!_initialized) Initialize();
            VisitorView?.SetTarget(v);
            SetMode(CameraMode.Visitor);
        }

        // ── 開催演出 (§80) ─────────────────────────────────────

        /// <summary>
        /// 祭り開始の瞬間のカメラワーク。
        /// 入り口の上空 → ゆっくり会場へ寄る → 屋台の灯りが一斉に点くのを見せる → 建設ビューへ戻す。
        /// </summary>
        public void PlayFestivalOpening()
        {
            if (!_initialized) Initialize();
            RefreshVenue();
            if (_opening != null) StopCoroutine(_opening);
            _opening = StartCoroutine(OpeningRoutine());
        }

        /// <summary>入り口の位置を来場者管理から取り直す。Bootstrap が設定していれば何もしない。</summary>
        public void RefreshVenue()
        {
            var game = GameManager.Instance;
            if (game == null || game.Visitors == null) return;
            Vector3 entrance = game.Visitors.EntrancePosition;
            if (entrance.sqrMagnitude > 0.01f) EntrancePosition = entrance;
        }

        IEnumerator OpeningRoutine()
        {
            IsPlayingOpening = true;
            if (Build != null) Build.IsActive = false;
            if (Free != null) Free.IsActive = false;
            if (VisitorView != null) VisitorView.IsActive = false;

            Vector3 high = EntrancePosition + new Vector3(6f, 46f, -30f);
            Vector3 mid = Vector3.Lerp(EntrancePosition, VenueCenter, 0.45f) + new Vector3(2f, 18f, -18f);
            Vector3 low = VenueCenter + new Vector3(-7f, 6.2f, -19f);

            var t = _openingCam.transform;
            t.position = high;
            t.rotation = Quaternion.LookRotation((VenueCenter - high).normalized, Vector3.up);
            SetPriority(_openingCam, 60);

            float half = Mathf.Max(0.5f, OpeningDuration * 0.5f);
            bool cueFired = false;
            float elapsed = 0f;

            while (elapsed < OpeningDuration)
            {
                elapsed += Time.deltaTime;

                Vector3 pos;
                Vector3 look;
                if (elapsed <= half)
                {
                    float k = Mathf.SmoothStep(0f, 1f, elapsed / half);
                    pos = Vector3.Lerp(high, mid, k);
                    look = Vector3.Lerp(VenueCenter, VenueCenter + Vector3.up * 2.5f, k);
                }
                else
                {
                    float k = Mathf.SmoothStep(0f, 1f, (elapsed - half) / half);
                    pos = Vector3.Lerp(mid, low, k);
                    look = VenueCenter + Vector3.up * Mathf.Lerp(2.5f, 3.2f, k);
                }

                t.position = pos;
                t.rotation = Quaternion.Slerp(t.rotation,
                    Quaternion.LookRotation((look - pos).normalized, Vector3.up),
                    1f - Mathf.Exp(-6f * Time.deltaTime));

                if (!cueFired && elapsed >= OpeningLightsCueTime)
                {
                    cueFired = true;
                    OpeningLightsCue?.Invoke();
                    GameManager.Instance?.Audio?.PlaySfx(MatsuriSfx.Cheer, VenueCenter, 0.9f);
                }
                yield return null;
            }

            // 建設ビューを演出の終点あたりに置いてから戻すと、ブレンドが自然になる。
            Build?.FocusOn(VenueCenter, 26f);
            SetPriority(_openingCam, 0);
            IsPlayingOpening = false;
            ApplyMode(CameraMode.Build, instant: false);
            _opening = null;
        }
    }
}
