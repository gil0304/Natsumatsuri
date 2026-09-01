using Matsuri.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.CameraSystem
{
    /// <summary>
    /// 画面右下に、いまのカメラモードと操作を小さく出す (§38 / §64)。
    ///
    /// UIManager の UIDocument には触らず、自前のパネルを1枚だけ持つ。
    /// すべての要素を PickingMode.Ignore にしてあるので、
    /// 左ドラッグでのパン判定 (MatsuriCameraInput.PointerOverUi) を邪魔しない。
    ///
    /// 数秒で薄くなり、カメラを触ると元の濃さに戻る。教材の吹き出しにはしない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraHintOverlay : MonoBehaviour
    {
        [Header("表示")]
        [Tooltip("はっきり見えている秒数。")]
        public float VisibleSeconds = 4.5f;

        [Tooltip("薄くなるのにかける秒数。")]
        public float FadeSeconds = 1.2f;

        [Tooltip("薄くなったときの不透明度。0 にすると完全に消える。")]
        public float DimOpacity = 0.22f;

        PanelSettings _panelSettings;
        UIDocument _document;
        VisualElement _card;
        Label _title;
        Label _keys;
        float _timer;
        CameraMode _mode = CameraMode.Build;

        void Awake()
        {
            BuildPanel();
            ShowFor(_mode);
        }

        void OnDestroy()
        {
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        void BuildPanel()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "CameraHintPanelSettings";
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.match = 0.5f;
            // UIManager (100) より奥に置く。操作ヒントが本体 UI を覆わないように。
            _panelSettings.sortingOrder = 90f;
            _panelSettings.clearDepthStencil = true;
            _panelSettings.clearColor = false;

            var theme = FindTheme();
            if (theme != null) _panelSettings.themeStyleSheet = theme;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;

            var root = _document.rootVisualElement;
            if (root == null) return;

            root.pickingMode = PickingMode.Ignore;
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.FlexEnd;
            root.style.alignItems = Align.FlexEnd;
            root.style.paddingRight = 18f;
            root.style.paddingBottom = 14f;

            _card = new VisualElement { pickingMode = PickingMode.Ignore };
            _card.style.flexDirection = FlexDirection.Column;
            _card.style.alignItems = Align.FlexEnd;
            _card.style.paddingLeft = 12f;
            _card.style.paddingRight = 12f;
            _card.style.paddingTop = 7f;
            _card.style.paddingBottom = 7f;
            SetBorderRadius(_card, 6f);
            _card.style.backgroundColor = new Color(0.04f, 0.05f, 0.09f, 0.5f);
            root.Add(_card);

            _title = MakeLabel(11f, MatsuriUiTheme.AccentWarm);
            _title.style.letterSpacing = 2f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _card.Add(_title);

            _keys = MakeLabel(12f, MatsuriUiTheme.TextSecondary);
            _keys.style.marginTop = 2f;
            _card.Add(_keys);
        }

        static ThemeStyleSheet FindTheme()
        {
            var theme = Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme");
            if (theme == null) theme = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            if (theme == null)
            {
                var found = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>();
                if (found != null && found.Length > 0) theme = found[0];
            }
            return theme;
        }

        static Label MakeLabel(float size, Color color)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            MatsuriUiTheme.ApplyUiFont(label);
            return label;
        }

        static void SetBorderRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        void Update()
        {
            if (_card == null) return;

            if (MatsuriCameraInput.AnyCameraActivity) _timer = 0f;
            else _timer += Time.unscaledDeltaTime;

            float opacity = 1f;
            if (_timer > VisibleSeconds)
            {
                float k = FadeSeconds <= 0.001f
                    ? 1f
                    : Mathf.Clamp01((_timer - VisibleSeconds) / FadeSeconds);
                opacity = Mathf.Lerp(1f, Mathf.Clamp01(DimOpacity), k);
            }
            _card.style.opacity = opacity;
        }

        /// <summary>指定モードの操作を表示し、濃さを元に戻す。</summary>
        public void ShowFor(CameraMode mode)
        {
            _mode = mode;
            _timer = 0f;
            if (_title == null || _keys == null) return;
            _title.text = TitleOf(mode);
            _keys.text = KeysOf(mode);
            if (_card != null) _card.style.opacity = 1f;
        }

        /// <summary>いまのモードのまま、もう一度はっきり見せる。</summary>
        public void Nudge() => ShowFor(_mode);

        static string TitleOf(CameraMode mode) => mode switch
        {
            CameraMode.Build => "BUILD CAMERA",
            CameraMode.Free => "FREE CAMERA",
            _ => "VISITOR CAMERA"
        };

        static string KeysOf(CameraMode mode) => mode switch
        {
            // トラックパッドとキーボードだけで完結する操作を出す。
            // 中ボタンや右ドラッグは案内しない（トラックパッドに無い／やりにくいため）。
            CameraMode.Build =>
                "WASD 移動 / Q・E 回転 / R・F 俯角 / Z・X 拡大縮小 / Space 全体\n" +
                "2本指スクロール↕ 拡大縮小・↔ 回転 / ドラッグ 移動 / Option+ドラッグ 回転 / C 切替",
            CameraMode.Free =>
                "WASD 移動 / Q・E 上下 / Shift 加速 / ドラッグ 視点 / C 切替",
            _ =>
                "WASD 歩く / Shift 走る / ドラッグ 見回す / C 切替"
        };
    }
}
