using System;
using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Save;
using Matsuri.Script;
using Matsuri.TimeSystem;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// 画面全体の組み立てと更新 (§9 / §64)。
    /// UIDocument と PanelSettings をコードから作り、VisualElement を C# で組む。
    /// USS (Assets/UI/Styles/Resources/matsuri.uss) は StyleSheet として読み込むが、
    /// 読めなかった場合でも成立するよう主要スタイルは C# 側でも指定してある。
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        [Header("フォント (§4 日本語表示)")]
        [Tooltip("差し込まれなければ Resources の NotoSansJP を使う。")]
        public FontAsset JapaneseFontAsset;

        [Tooltip("コード用フォント。未指定なら日本語フォントを使う。")]
        public FontAsset CodeFontAsset;

        [Header("スタイル")]
        [Tooltip("Assets/UI/Styles/Resources/matsuri.uss。未指定なら Resources から探す。")]
        public StyleSheet MatsuriStyleSheet;

        [Tooltip("PanelSettings のテーマ。未指定なら Resources から探す。")]
        public ThemeStyleSheet Theme;

        // ── 要素 ───────────────────────────────────────────────
        PanelSettings _panelSettings;
        UIDocument _document;
        VisualElement _root;
        HudPanel _hud;
        CodeEditorElement _editor;
        DiagnosticsPanel _diagnostics;
        ResultPanel _result;
        TutorialOverlay _tutorial;
        VisualElement _toastLayer;
        Button _runButton, _startButton, _resetButton, _cameraButton;

        GameManager _game;
        bool _initialized;
        bool _bound;
        float _lastExternalHudTime = -99f;

        // ── コントラクトのイベント ─────────────────────────────
        public event Action<string> RunRequested;
        public event Action StartRequested;
        public event Action ResetRequested;
        public event Action CameraCycleRequested;

        /// <summary>コードエディター本体。カタログの差し込みなどに使う。</summary>
        public CodeEditorElement Editor => _editor;

        void Start()
        {
            if (!_initialized) Initialize();
            TryBindGame();
        }

        void OnDestroy()
        {
            Unbind();
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        // ── 構築 ───────────────────────────────────────────────

        /// <summary>UI を構築する。二重呼び出しは無視する。</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // フォントは「差し込み > 共有プロバイダ > Resources の NotoSansJP > 既定」の順で決める
            if (JapaneseFontAsset != null) MatsuriUiTheme.JapaneseFont = JapaneseFontAsset;
            else if (MatsuriFontProvider.JapaneseFontAsset != null)
                MatsuriUiTheme.JapaneseFont = MatsuriFontProvider.JapaneseFontAsset;

            if (CodeFontAsset != null) MatsuriUiTheme.CodeFontAsset = CodeFontAsset;

            CreateDocument();
            BuildLayout();
            LoadStyleSheet();

            SetSource(GetStarterSource());
            _tutorial.Begin();
            ApplyPhase(GamePhase.Editing);
        }

        void CreateDocument()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "MatsuriPanelSettings";
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = 100f;
            _panelSettings.clearDepthStencil = true;
            _panelSettings.clearColor = false;

            // テーマが見つからなくても警告は出さない。理由は
            // MatsuriUiTheme.ResolveRuntimeTheme() のコメントを参照。
            var theme = MatsuriUiTheme.ResolveRuntimeTheme(Theme);
            if (theme != null) _panelSettings.themeStyleSheet = theme;

            var go = new GameObject("MatsuriUIDocument");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            _document = go.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            go.SetActive(true);

            _root = _document.rootVisualElement;
            if (_root == null)
            {
                MatsuriLog.Error("UIDocument.rootVisualElement を生成できなかった。UI を表示できない。");
                _root = new VisualElement();
            }
        }

        void LoadStyleSheet()
        {
            var sheet = MatsuriUiTheme.ResolveStyleSheet(MatsuriStyleSheet);
            if (sheet != null) _root.styleSheets.Add(sheet);
            // 見つからない場合も警告は出さない。matsuri.uss は補助レイヤであり、
            // 色・余白・フォントは MatsuriUiTheme が C# 側で必ず指定しているため。
        }

        void BuildLayout()
        {
            _root.AddToClassList("matsuri-root");
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.left = 0f;
            _root.style.right = 0f;
            _root.style.top = 0f;
            _root.style.bottom = 0f;
            _root.style.backgroundColor = Color.clear;
            MatsuriUiTheme.ApplyUiFont(_root);

            // ── 上部バー ──
            _hud = new HudPanel();
            _root.Add(_hud);

            // ── 左: コード ──
            var left = new VisualElement();
            left.style.position = Position.Absolute;
            left.style.left = 18f;
            left.style.top = MatsuriUiTheme.TopBarHeight + 16f;
            left.style.bottom = MatsuriUiTheme.BottomBarHeight + 16f;
            left.style.width = MatsuriUiTheme.LeftPanelWidth;
            left.style.flexDirection = FlexDirection.Column;
            _root.Add(left);

            _editor = new CodeEditorElement();
            _editor.style.flexGrow = 1f;
            _editor.TextChanged += OnEditorTextChanged;
            _editor.RunRequested += InvokeRun;
            left.Add(_editor);

            _diagnostics = new DiagnosticsPanel();
            _diagnostics.style.height = 172f;
            _diagnostics.style.flexShrink = 0f;
            _diagnostics.style.marginTop = 12f;
            _diagnostics.LineSelected += line => _editor.FocusLine(line);
            left.Add(_diagnostics);

            // ── 右: 3D の見出しだけ置く（3D は背景に描かれる） ──
            var stageTag = new Label("3D FESTIVAL");
            stageTag.pickingMode = PickingMode.Ignore;
            stageTag.style.position = Position.Absolute;
            stageTag.style.right = 24f;
            stageTag.style.top = MatsuriUiTheme.TopBarHeight + 16f;
            stageTag.style.fontSize = MatsuriUiTheme.TitleFontSize;
            stageTag.style.letterSpacing = 4f;
            stageTag.style.color = new Color(1f, 1f, 1f, 0.24f);
            MatsuriUiTheme.ApplyUiFont(stageTag);
            _root.Add(stageTag);

            // ── 下部バー ──
            _root.Add(BuildBottomBar());

            // ── トースト ──
            _toastLayer = new VisualElement();
            _toastLayer.pickingMode = PickingMode.Ignore;
            _toastLayer.style.position = Position.Absolute;
            _toastLayer.style.left = 0f;
            _toastLayer.style.right = 0f;
            _toastLayer.style.top = MatsuriUiTheme.TopBarHeight + 18f;
            _toastLayer.style.alignItems = Align.Center;
            _root.Add(_toastLayer);

            // ── チュートリアル ──
            _tutorial = new TutorialOverlay();
            _tutorial.style.left = MatsuriUiTheme.LeftPanelWidth + 42f;
            _tutorial.style.bottom = MatsuriUiTheme.BottomBarHeight + 18f;
            _root.Add(_tutorial);

            // ── 結果 ──
            _result = new ResultPanel();
            _result.RetryRequested += () =>
            {
                _result.Hide();
                InvokeReset();
            };
            _result.EditCodeRequested += () => _editor.FocusEditor();
            _root.Add(_result);
        }

        VisualElement BuildBottomBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("matsuri-bottombar");
            bar.style.position = Position.Absolute;
            bar.style.left = 0f;
            bar.style.right = 0f;
            bar.style.bottom = 0f;
            bar.style.height = MatsuriUiTheme.BottomBarHeight;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.backgroundColor = MatsuriUiTheme.Hex("#080B14E6");
            bar.style.borderTopWidth = 1f;
            bar.style.borderTopColor = MatsuriUiTheme.Border;
            MatsuriUiTheme.SetPadding(bar, 0f, 22f, 0f, 12f);

            _runButton = MatsuriUiTheme.CreateButton("▶  実行 (RUN)", InvokeRun, true);
            _startButton = MatsuriUiTheme.CreateButton("祭りを開催", InvokeStart);
            _resetButton = MatsuriUiTheme.CreateButton("リセット", InvokeReset);
            _cameraButton = MatsuriUiTheme.CreateButton("カメラ切替", InvokeCameraCycle);

            bar.Add(_runButton);
            bar.Add(_startButton);
            bar.Add(_resetButton);
            bar.Add(_cameraButton);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            bar.Add(spacer);

            var hint = new Label("Ctrl / ⌘ + Enter で実行  ・  Tab でインデント  ・  Ctrl + Space で補完");
            hint.pickingMode = PickingMode.Ignore;
            hint.style.fontSize = 11f;
            hint.style.color = MatsuriUiTheme.TextMuted;
            MatsuriUiTheme.ApplyUiFont(hint);
            bar.Add(hint);

            return bar;
        }

        // ── コントラクトAPI ────────────────────────────────────

        /// <summary>コンパイル結果を表示する (§42)。</summary>
        public void ShowDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
        {
            if (!_initialized) Initialize();
            _editor.SetDiagnostics(diagnostics);
            _diagnostics.SetDiagnostics(diagnostics);

        }

        public void SetSource(string code)
        {
            if (!_initialized) Initialize();
            _editor.Text = code ?? string.Empty;
        }

        public string GetSource() => _editor != null ? _editor.Text : string.Empty;

        public void UpdateHud(long budget, long revenue, int visitors, FestivalClock clock)
        {
            // 外部（FestivalManager など）が HUD を駆動している間は、
            // UIManager 側の自前ポーリングを止めて数値が競合しないようにする。
            _lastExternalHudTime = UnityEngine.Time.unscaledTime;
            ApplyHud(budget, revenue, visitors, clock);
        }

        void ApplyHud(long budget, long revenue, int visitors, FestivalClock clock)
        {
            if (_hud == null) return;
            _hud.UpdateValues(budget, revenue, visitors, clock);
        }

        /// <summary>結果画面 (§36)。</summary>
        public void ShowResult(FestivalResult result)
        {
            if (!_initialized) Initialize();
            _result.Show(result);
            _tutorial.NotifyResultShown();
        }

        /// <summary>画面上部に短いメッセージを出す。</summary>
        public void ShowToast(string message, DiagnosticSeverity severity = DiagnosticSeverity.Info)
        {
            if (string.IsNullOrEmpty(message) || _toastLayer == null) return;

            Color accent = severity == DiagnosticSeverity.Error ? MatsuriUiTheme.SeverityError
                : (severity == DiagnosticSeverity.Warning ? MatsuriUiTheme.SeverityWarning : MatsuriUiTheme.SeverityInfo);

            var toast = new Label(message);
            toast.pickingMode = PickingMode.Ignore;
            toast.style.fontSize = 13f;
            toast.style.color = MatsuriUiTheme.TextPrimary;
            toast.style.backgroundColor = MatsuriUiTheme.Hex("#141826F5");
            toast.style.marginBottom = 8f;
            toast.style.whiteSpace = WhiteSpace.Normal;
            toast.style.maxWidth = 560f;
            MatsuriUiTheme.SetPadding(toast, 10f, 18f, 10f, 16f);
            MatsuriUiTheme.SetRadius(toast, 8f);
            MatsuriUiTheme.SetBorder(toast, 1f, accent);
            toast.style.borderLeftWidth = 3f;
            MatsuriUiTheme.ApplyUiFont(toast);
            _toastLayer.Add(toast);

            if (_toastLayer.childCount > 4) _toastLayer.RemoveAt(0);

            MatsuriUiTheme.Tween(_toastLayer, 0f, 1f, 2600, t =>
            {
                toast.style.opacity = t < 0.78f ? 1f : Mathf.InverseLerp(1f, 0.78f, t);
                if (t >= 1f) toast.RemoveFromHierarchy();
            });
        }

        /// <summary>チュートリアルの一文を差し替える (§45)。</summary>
        public void ShowTutorial(string message)
        {
            if (!_initialized) Initialize();
            _tutorial.ShowCustom(message);
        }

        // ── ボタン ────────────────────────────────────────────

        void InvokeRun()
        {
            string source = GetSource();
            _tutorial.NotifyRun(source);
            if (RunRequested != null) RunRequested.Invoke(source);
            else _game?.RunCode(source);
        }

        void InvokeStart()
        {
            if (StartRequested != null) StartRequested.Invoke();
            else _game?.StartFestival();
        }

        void InvokeReset()
        {
            _result.Hide();
            _hud.ResetValues();
            if (ResetRequested != null) ResetRequested.Invoke();
            else _game?.ResetFestival();
        }

        void InvokeCameraCycle()
        {
            if (CameraCycleRequested != null) CameraCycleRequested.Invoke();
            else _game?.Cameras?.CycleMode();
        }

        void OnEditorTextChanged(string source)
        {
            _tutorial.NotifyCodeChanged(source);
        }

        // ── GameManager との接続 ───────────────────────────────

        void TryBindGame()
        {
            if (_bound) return;
            var gm = GameManager.Instance;
            if (gm == null) gm = FindFirstObjectByType<GameManager>();
            if (gm == null) return;

            _game = gm;
            _bound = true;
            gm.PhaseChanged += ApplyPhase;
            if (gm.Script != null) gm.Script.Compiled += OnCompiled;
            if (gm.Catalog != null) _editor.Catalog = gm.Catalog;
            ApplyPhase(gm.Phase);
        }

        void Unbind()
        {
            if (!_bound || _game == null) return;
            _game.PhaseChanged -= ApplyPhase;
            if (_game.Script != null) _game.Script.Compiled -= OnCompiled;
            _bound = false;
        }

        void OnCompiled(Matsuri.Script.Commands.FestivalPlan plan)
        {
            if (plan == null) return;
            ShowDiagnostics(plan.Diagnostics);
            if (!plan.HasErrors)
            {
                _editor.SetTitle(plan.FestivalName);
                ShowToast($"「{(string.IsNullOrEmpty(plan.FestivalName) ? "夏祭り" : plan.FestivalName)}」を建てた。" +
                          $"見積り {MatsuriUiTheme.FormatYen(plan.EstimatedCost)}");
            }
        }

        void ApplyPhase(GamePhase phase)
        {
            bool editing = phase == GamePhase.Editing;
            bool finished = phase == GamePhase.Finished;

            MatsuriUiTheme.SetButtonEnabled(_runButton, editing || finished, true);
            MatsuriUiTheme.SetButtonEnabled(_startButton, editing);
            MatsuriUiTheme.SetButtonEnabled(_resetButton, phase != GamePhase.Building);
            MatsuriUiTheme.SetButtonEnabled(_cameraButton, true);

            switch (phase)
            {
                case GamePhase.Editing:
                    _hud.SetPhaseLabel("準備中");
                    break;
                case GamePhase.Building:
                    _hud.SetPhaseLabel("建設中");
                    break;
                case GamePhase.Running:
                    _hud.SetPhaseLabel("開催中");
                    _tutorial.NotifyFestivalStarted();
                    break;
                case GamePhase.Finished:
                    _hud.SetPhaseLabel("終了");
                    break;
            }
        }

        void Update()
        {
            if (!_bound) TryBindGame();
            if (!_bound || _game == null) return;

            if (_editor != null && _editor.Catalog == null && _game.Catalog != null)
                _editor.Catalog = _game.Catalog;

            // 外部から UpdateHud が呼ばれているならそちらを優先する
            if (UnityEngine.Time.unscaledTime - _lastExternalHudTime < 0.5f) return;

            long budget = _game.Economy != null ? _game.Economy.Budget : 0L;
            long revenue = _game.Economy != null ? _game.Economy.Revenue : 0L;
            int visitors = _game.Visitors != null ? _game.Visitors.CurrentVisitors : 0;
            FestivalClock clock = _game.Time != null ? _game.Time.Clock : FestivalClock.AtStart;

            ApplyHud(budget, revenue, visitors, clock);
        }

        static string GetStarterSource()
        {
            try
            {
                string starter = MatsuriSamples.Starter;
                if (!string.IsNullOrEmpty(starter)) return starter;
            }
            catch (Exception)
            {
                // サンプルがまだ用意されていない場合は最小の雛形を出す
            }
            return "祭り \"夏祭り\" {\n    屋台 たこ焼き {\n        場所 0, 0\n        値段 500\n    }\n}\n";
        }
    }
}
