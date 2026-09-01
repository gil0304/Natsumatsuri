using System;
using System.Collections.Generic;
using Matsuri.Script;
using Matsuri.Script.Completion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// Matsuri Script のコードエディター (§10 / §42 / §43)。
    ///
    /// 構造（重ね合わせ 3 層）:
    ///   1. ガター層  … 行番号と ● エラー印。1 行 = 1 要素。
    ///   2. ハイライト層 … シンタックスハイライト。こちらも 1 行 = 1 要素。
    ///   3. 入力層 (multiline TextField, 文字色は透明)
    ///      … 実際の編集はここ。日本語 IME をそのまま使うために必須。
    ///
    /// 行番号のズレ対策:
    ///   ガター層とハイライト層は「同じ親 (_stack) の中の、同じ高さの行要素の縦積み」に
    ///   してある。つまり 2 層の縦位置は座標計算ではなく構造で一致している。
    ///   ガター層はスクロールビューの内側に置いてあるので、縦スクロールでは
    ///   コード行と完全に同じだけ動く。横スクロールのぶんだけ left を戻して
    ///   左端に貼り付けている。
    ///   行の高さは推測せず CodeEditorLayout.MeasureLineHeight() で実測する。
    ///
    /// ブロックUIにはしない。実際のコードをそのまま書く (§64)。
    /// </summary>
    public sealed partial class CodeEditorElement : VisualElement
    {
        const float GutterWidth = CodeEditorLayout.GutterWidth;
        const float TextLeft    = CodeEditorLayout.TextLeft;
        const float TextTop     = CodeEditorLayout.TextTop;

        // ── 要素 ───────────────────────────────────────────────
        readonly Label _titleLabel;
        readonly Label _hintLabel;
        readonly VisualElement _gutterBack;
        readonly ScrollView _scroll;
        readonly VisualElement _stack;
        readonly VisualElement _codeColumn;
        readonly VisualElement _gutterLayer;
        readonly VisualElement _gutterContent;
        readonly Label _measure;
        readonly TextField _input;
        readonly CompletionPopup _popup;

        readonly List<Label> _lineRows = new List<Label>();
        readonly List<VisualElement> _gutterRows = new List<VisualElement>();
        readonly List<TextElement> _inputTextElements = new List<TextElement>();
        readonly List<string> _lineTexts = new List<string>();

        // ── 状態 ───────────────────────────────────────────────
        IReadOnlyList<Diagnostic> _diagnostics = Array.Empty<Diagnostic>();
        readonly HashSet<int> _errorLines = new HashSet<int>();
        readonly HashSet<int> _warningLines = new HashSet<int>();

        string _sourceCache = string.Empty;
        int _lineCount = 1;
        int _caretIndex = -1;
        int _caretLine;
        float _lineHeight;
        bool _composing;
        float _lastKeyTime;
        float _lastValueTime;
        int _runFrame, _tabFrame, _returnFrame, _acceptFrame;

        /// <summary>補完に使うカタログ。null なら補完は出ない。</summary>
        public IMatsuriCatalog Catalog { get; set; }

        /// <summary>編集中のソース。</summary>
        public string Text
        {
            get => _input.value ?? string.Empty;
            set
            {
                string normalized = Normalize(value);
                if (normalized == (_input.value ?? string.Empty)) return;
                _input.value = normalized;
                // 値変更イベントが届かない環境でも表示を必ず追従させる
                RebuildAll(normalized);
            }
        }

        /// <summary>テキストが変化した。</summary>
        public event Action<string> TextChanged;

        /// <summary>Ctrl / Cmd + Enter が押された。</summary>
        public event Action RunRequested;

        public CodeEditorElement()
        {
            AddToClassList("matsuri-editor");
            style.flexDirection = FlexDirection.Column;
            style.backgroundColor = MatsuriUiTheme.BgEditor;
            MatsuriUiTheme.SetRadius(this, MatsuriUiTheme.RadiusLarge);
            MatsuriUiTheme.SetBorder(this, 1f, MatsuriUiTheme.Border);
            style.overflow = Overflow.Hidden;

            // ── ヘッダ ──
            var header = new VisualElement();
            header.AddToClassList("matsuri-editor__header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 34f;
            header.style.flexShrink = 0f;
            header.style.backgroundColor = MatsuriUiTheme.Hex("#0A0D16");
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = MatsuriUiTheme.Border;
            MatsuriUiTheme.SetPadding(header, 0f, 12f, 0f, 14f);
            Add(header);

            var dot = new VisualElement();
            dot.style.width = 8f;
            dot.style.height = 8f;
            dot.style.backgroundColor = MatsuriUiTheme.AccentRed;
            MatsuriUiTheme.SetRadius(dot, 4f);
            dot.style.marginRight = 9f;
            header.Add(dot);

            _titleLabel = new Label("CODE EDITOR");
            _titleLabel.style.fontSize = MatsuriUiTheme.TitleFontSize;
            _titleLabel.style.color = MatsuriUiTheme.TextSecondary;
            _titleLabel.style.letterSpacing = 3f;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            MatsuriUiTheme.ApplyUiFont(_titleLabel);
            header.Add(_titleLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            header.Add(spacer);

            _hintLabel = new Label("Ctrl / ⌘ + Enter で実行");
            _hintLabel.style.fontSize = 10f;
            _hintLabel.style.color = MatsuriUiTheme.TextMuted;
            MatsuriUiTheme.ApplyUiFont(_hintLabel);
            header.Add(_hintLabel);

            // ── 本体 ──
            var body = new VisualElement();
            body.style.position = Position.Relative;
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1f;
            body.style.overflow = Overflow.Hidden;
            Add(body);

            // ガターの下地。行が画面より少ないときも左端の帯を途切れさせないための背景。
            _gutterBack = new VisualElement();
            _gutterBack.AddToClassList("matsuri-editor__gutter");
            _gutterBack.pickingMode = PickingMode.Ignore;
            _gutterBack.style.position = Position.Absolute;
            _gutterBack.style.left = 0f;
            _gutterBack.style.top = 0f;
            _gutterBack.style.bottom = 0f;
            _gutterBack.style.width = GutterWidth;
            _gutterBack.style.backgroundColor = MatsuriUiTheme.BgGutter;
            _gutterBack.style.borderRightWidth = 1f;
            _gutterBack.style.borderRightColor = MatsuriUiTheme.Border;
            body.Add(_gutterBack);

            _scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scroll.style.flexGrow = 1f;
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            body.Add(_scroll);

            // _stack がコード領域の座標原点。ガター行もコード行もこの中に置く。
            _stack = new VisualElement();
            _stack.style.position = Position.Relative;
            _stack.style.flexDirection = FlexDirection.Column;
            _stack.style.flexGrow = 0f;
            _stack.style.flexShrink = 0f;
            MatsuriUiTheme.StripBox(_stack);
            _scroll.Add(_stack);

            // ── ハイライト層（1 行 = 1 要素の縦積み） ──
            _codeColumn = new VisualElement();
            _codeColumn.pickingMode = PickingMode.Ignore;
            _codeColumn.style.flexDirection = FlexDirection.Column;
            _codeColumn.style.alignItems = Align.Stretch;
            _codeColumn.style.marginLeft = GutterWidth;
            _codeColumn.style.marginTop = TextTop;
            _codeColumn.style.paddingBottom = CodeEditorLayout.TextBottom;
            _stack.Add(_codeColumn);

            // ── 入力層（実際の編集。文字色は透明） ──
            _input = new TextField { multiline = true };
            _input.AddToClassList("matsuri-editor__input");
            _input.style.position = Position.Absolute;
            _input.style.left = GutterWidth + TextLeft;
            _input.style.top = TextTop;
            _input.style.right = 0f;
            _input.style.bottom = 0f;
            _stack.Add(_input);

            // ── ガター層（コード行と同じ親・同じ行高。縦位置は構造で一致する） ──
            _gutterLayer = new VisualElement();
            _gutterLayer.AddToClassList("matsuri-editor__gutter");
            _gutterLayer.pickingMode = PickingMode.Ignore;
            _gutterLayer.style.position = Position.Absolute;
            _gutterLayer.style.left = 0f;
            _gutterLayer.style.top = 0f;
            _gutterLayer.style.bottom = 0f;
            _gutterLayer.style.width = GutterWidth;
            _gutterLayer.style.overflow = Overflow.Hidden;
            _gutterLayer.style.backgroundColor = MatsuriUiTheme.BgGutter;
            _gutterLayer.style.borderRightWidth = 1f;
            _gutterLayer.style.borderRightColor = MatsuriUiTheme.Border;
            _stack.Add(_gutterLayer);

            _gutterContent = new VisualElement();
            _gutterContent.pickingMode = PickingMode.Ignore;
            _gutterContent.style.flexDirection = FlexDirection.Column;
            _gutterContent.style.alignItems = Align.Stretch;
            _gutterContent.style.marginTop = TextTop;   // コード列と同じ上余白
            _gutterLayer.Add(_gutterContent);

            // ── 計測用（表示しない） ──
            _measure = new Label(string.Empty);
            _measure.pickingMode = PickingMode.Ignore;
            _measure.enableRichText = false;
            _measure.style.position = Position.Absolute;
            _measure.style.left = 0f;
            _measure.style.top = 0f;
            _measure.style.visibility = Visibility.Hidden;
            CodeEditorLayout.ApplyTextLayer(_measure, MatsuriUiTheme.CodeFontSize);
            _stack.Add(_measure);

            _popup = new CompletionPopup();
            _popup.Accepted += OnCompletionAccepted;
            Add(_popup);

            ConfigureInputChrome();

            _input.RegisterValueChangedCallback(evt => OnValueChanged(evt.newValue));
            _input.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _input.RegisterCallback<FocusOutEvent>(_ => { SetComposing(false); _popup.Close(); });

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ConfigureInputChrome();
                RefreshMetrics();
                SyncNow();
            });
            _scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateContentSize());
            _scroll.horizontalScroller.valueChanged += _ => SyncGutterHorizontal();
            _scroll.verticalScroller.valueChanged += _ => SyncGutterHorizontal();

            schedule.Execute(Poll).Every(50);

            _lineHeight = CodeEditorLayout.FallbackLineHeight(MatsuriUiTheme.CodeFontSize);
            RebuildAll(string.Empty);
        }

        // ── TextField の既定装飾を消し、3 層と同じ文字組にする ──

        void ConfigureInputChrome()
        {
            CodeEditorLayout.ApplyToTextField(_input, MatsuriUiTheme.CodeFontSize, _inputTextElements);
            CodeEditorLayout.TryDisableInnerScroller(_input);
            SetInputTextColor(Color.clear);
            CaretAccess.TrySetCursorColor(_input, MatsuriUiTheme.AccentYellow);
        }

        void SetInputTextColor(Color c)
        {
            _input.style.color = c;
            for (int i = 0; i < _inputTextElements.Count; i++) _inputTextElements[i].style.color = c;
            var inner = _input.Q(TextField.textInputUssName);
            if (inner != null) inner.style.color = c;
        }

        // ── 公開API ────────────────────────────────────────────

        /// <summary>コンパイル結果の診断を反映する (§42)。エラー行を赤くする。</summary>
        public void SetDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
        {
            _diagnostics = diagnostics ?? (IReadOnlyList<Diagnostic>)Array.Empty<Diagnostic>();
            _errorLines.Clear();
            _warningLines.Clear();
            for (int i = 0; i < _diagnostics.Count; i++)
            {
                var d = _diagnostics[i];
                if (d == null) continue;
                if (d.Severity == DiagnosticSeverity.Error) _errorLines.Add(d.Line);
                else if (d.Severity == DiagnosticSeverity.Warning) _warningLines.Add(d.Line);
            }
            RebuildAll(Text);
        }

        /// <summary>指定行の先頭にキャレットを飛ばす（診断パネルのクリック用）。</summary>
        public void FocusLine(int line)
        {
            string text = Text;
            int index = LineStartIndex(text, Mathf.Max(0, line - 1));
            // 行頭のインデントを飛ばす
            while (index < text.Length && (text[index] == ' ' || text[index] == '\t')) index++;
            _input.Focus();
            SetCaret(index);
            EnsureLineVisible(Mathf.Max(0, line - 1));
        }

        /// <summary>エディターに入力フォーカスを与える。</summary>
        public void FocusEditor() => _input.Focus();

        /// <summary>ヘッダに表示する祭りの名前。</summary>
        public void SetTitle(string title)
            => _titleLabel.text = string.IsNullOrEmpty(title) ? "CODE EDITOR" : "CODE EDITOR — " + title;

        /// <summary>表示中の行数。</summary>
        public int LineCount => _lineCount;

        /// <summary>キャレットのある行（0 始まり）。</summary>
        public int CaretLine => _caretLine;

        /// <summary>実測した 1 行ぶんの高さ (px)。</summary>
        public float MeasuredLineHeight => _lineHeight;

        /// <summary>縦スクロール量。</summary>
        public float ScrollY
        {
            get => _scroll.scrollOffset.y;
            set
            {
                var o = _scroll.scrollOffset;
                o.y = value;
                _scroll.scrollOffset = o;
                SyncGutterHorizontal();
            }
        }

        /// <summary>横スクロール量。</summary>
        public float ScrollX
        {
            get => _scroll.scrollOffset.x;
            set
            {
                var o = _scroll.scrollOffset;
                o.x = value;
                _scroll.scrollOffset = o;
                SyncGutterHorizontal();
            }
        }

        /// <summary>行番号（0 始まり）に対応するガターの行要素。範囲外なら null。UI 検証用。</summary>
        public VisualElement GetGutterRowElement(int lineIndex)
            => (lineIndex >= 0 && lineIndex < _lineCount && lineIndex < _gutterRows.Count)
                ? _gutterRows[lineIndex] : null;

        /// <summary>行番号（0 始まり）に対応するコード行要素。範囲外なら null。UI 検証用。</summary>
        public VisualElement GetCodeRowElement(int lineIndex)
            => (lineIndex >= 0 && lineIndex < _lineCount && lineIndex < _lineRows.Count)
                ? _lineRows[lineIndex] : null;

        /// <summary>行の再構築・計測・スクロール同期をその場で行う。UI 検証や外部からの即時反映用。</summary>
        public void SyncNow()
        {
            RefreshMetrics();
            RebuildAll(_input.value ?? string.Empty);
            UpdateContentSize();
            SyncGutterHorizontal();
        }

        // ── テキスト変化 ────────────────────────────────────────

        void OnValueChanged(string newValue)
        {
            _lastValueTime = UnityEngine.Time.unscaledTime;
            SetComposing(false);

            RebuildAll(newValue ?? string.Empty);
            TextChanged?.Invoke(newValue ?? string.Empty);
            UpdateCompletion(false);
        }

        // ── IME ────────────────────────────────────────────────

        void UpdateComposingState()
        {
            bool composing = _lastKeyTime > _lastValueTime + 0.0005f
                             && (UnityEngine.Time.unscaledTime - _lastKeyTime) < 5f;
            SetComposing(composing);
        }

        /// <summary>
        /// IME変換中は重ね合わせが破綻する（入力層が透明なので変換中の文字が見えない）。
        /// キー入力があったのに値が変わらない状態＝変換中とみなし、
        /// その間だけハイライト層の文字を消して入力層を素の色で見せる。
        /// 行の高さと背景は保ったままなので、行番号との対応は崩れない。
        /// </summary>
        void SetComposing(bool composing)
        {
            if (_composing == composing) return;
            _composing = composing;
            ApplyRowTexts();
            SetInputTextColor(composing ? MatsuriUiTheme.TextPrimary : Color.clear);
        }
    }
}
