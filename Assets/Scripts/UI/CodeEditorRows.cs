using System;
using System.Collections.Generic;
using Matsuri.Script;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// 行要素の生成・更新と、ガター／ハイライト／入力の 3 層同期。
    /// （§66 に従い CodeEditorElement を責務ごとに分割した partial の一部）
    ///
    /// ここが「行番号がコード行とずれない」ことの担保箇所である。
    /// ・行の高さは実測した 1 つの値を、ガター行とコード行の両方に同じだけ与える
    /// ・両者は同じ親 (_stack) の中で同じ上余白から積み上がる
    /// ・したがって n 行目の Y 座標は構造的に一致する
    /// </summary>
    public sealed partial class CodeEditorElement
    {
        // ── 再構築 ─────────────────────────────────────────────

        void RebuildAll(string text)
        {
            _sourceCache = Normalize(text);
            SplitHighlightedLines(_sourceCache, _lineTexts);

            _lineCount = _lineTexts.Count;
            if (_lineCount < 1) _lineCount = 1;

            EnsureRowCount(_lineCount);
            ApplyRowTexts();
            RefreshLineStates();
            UpdateContentSize();
        }

        /// <summary>
        /// ハイライト済みのリッチテキストを行ごとに切り分ける。
        /// MatsuriSyntaxHighlighter は文字と改行を一切変えず、色タグも行をまたがないため、
        /// 単純に '\n' で割れば元の行と 1 対 1 に対応する。
        /// </summary>
        void SplitHighlightedLines(string source, List<string> into)
        {
            into.Clear();
            string rich = MatsuriSyntaxHighlighter.ToRichText(source, _diagnostics);
            if (string.IsNullOrEmpty(rich))
            {
                into.Add(string.Empty);
                return;
            }

            int start = 0;
            for (int i = 0; i < rich.Length; i++)
            {
                if (rich[i] != '\n') continue;
                into.Add(rich.Substring(start, i - start));
                start = i + 1;
            }
            into.Add(rich.Substring(start));
        }

        void ApplyRowTexts()
        {
            for (int i = 0; i < _lineCount && i < _lineRows.Count; i++)
            {
                // IME 変換中はハイライト層の文字だけ消して入力層を見せる
                _lineRows[i].text = _composing ? string.Empty
                    : (i < _lineTexts.Count ? _lineTexts[i] : string.Empty);
            }
        }

        // ── 行要素 ─────────────────────────────────────────────

        void EnsureRowCount(int count)
        {
            if (count < 1) count = 1;

            while (_lineRows.Count < count)
            {
                int number = _lineRows.Count + 1;
                _lineRows.Add(CreateCodeRow());
                _gutterRows.Add(CreateGutterRow(number));
            }

            for (int i = 0; i < _lineRows.Count; i++)
            {
                var display = i < count ? DisplayStyle.Flex : DisplayStyle.None;
                _lineRows[i].style.display = display;
                _gutterRows[i].style.display = display;
            }
        }

        Label CreateCodeRow()
        {
            var row = new Label(string.Empty);
            row.AddToClassList("matsuri-editor__line");
            row.pickingMode = PickingMode.Ignore;
            row.enableRichText = true;
            CodeEditorLayout.ApplyTextLayer(row, MatsuriUiTheme.CodeFontSize);
            // 背景（現在行・エラー行の帯）はガター右端から始め、文字はその内側から。
            // 右の余白も行自身に持たせることで、帯が行の右端まで途切れずに伸びる。
            row.style.paddingLeft = TextLeft;
            row.style.paddingRight = CodeEditorLayout.TextRight;
            row.style.color = MatsuriUiTheme.Hex(MatsuriUiTheme.SynDefault);
            row.style.flexGrow = 0f;
            row.style.flexShrink = 0f;
            row.style.overflow = Overflow.Hidden;
            row.style.height = _lineHeight;
            _codeColumn.Add(row);
            return row;
        }

        VisualElement CreateGutterRow(int number)
        {
            var g = new VisualElement();
            g.AddToClassList("matsuri-editor__gutter-row");
            g.pickingMode = PickingMode.Ignore;
            g.style.flexDirection = FlexDirection.Row;
            g.style.alignItems = Align.Stretch;
            g.style.flexGrow = 0f;
            g.style.flexShrink = 0f;
            g.style.overflow = Overflow.Hidden;
            g.style.height = _lineHeight;

            // エラー行の ● (§42)
            var marker = new Label(" ") { name = "marker" };
            marker.enableRichText = false;
            CodeEditorLayout.ApplyTextLayer(marker, MatsuriUiTheme.CodeFontSize - 4f);
            marker.style.width = CodeEditorLayout.MarkerWidth;
            marker.style.flexGrow = 0f;
            marker.style.flexShrink = 0f;
            marker.style.unityTextAlign = TextAnchor.MiddleCenter;
            marker.style.color = Color.clear;
            g.Add(marker);

            // 行番号。コード行と同じフォント・同じサイズ・同じ上揃えにする。
            // ここだけ小さくすると、字の上端がコード行とずれて「合っていない」ように見える。
            var num = new Label(number.ToString()) { name = "number" };
            num.enableRichText = false;
            CodeEditorLayout.ApplyTextLayer(num, MatsuriUiTheme.CodeFontSize);
            num.style.flexGrow = 1f;
            num.style.flexShrink = 1f;
            num.style.unityTextAlign = TextAnchor.UpperRight;
            num.style.paddingRight = CodeEditorLayout.NumberRight;
            num.style.color = MatsuriUiTheme.TextMuted;
            g.Add(num);

            _gutterContent.Add(g);
            return g;
        }

        // ── 行の状態（現在行 / エラー行） ───────────────────────

        void RefreshLineStates()
        {
            for (int i = 0; i < _lineCount && i < _lineRows.Count; i++)
            {
                int lineNumber = i + 1;
                bool isError = _errorLines.Contains(lineNumber);
                bool isWarning = !isError && _warningLines.Contains(lineNumber);
                bool isCurrent = i == _caretLine;

                Color bg = Color.clear;
                if (isError) bg = MatsuriUiTheme.ErrorLineBg;
                else if (isWarning) bg = MatsuriUiTheme.WarningLineBg;
                if (isCurrent)
                {
                    bg = isError
                        ? MatsuriUiTheme.ErrorCurrentLineBg
                        : (isWarning ? MatsuriUiTheme.WarningCurrentLineBg : MatsuriUiTheme.CurrentLineBg);
                }
                _lineRows[i].style.backgroundColor = bg;
                _gutterRows[i].style.backgroundColor = bg;

                var marker = _gutterRows[i].Q<Label>("marker");
                var num = _gutterRows[i].Q<Label>("number");
                if (marker != null)
                {
                    if (isError)
                    {
                        marker.text = "●";
                        marker.style.color = MatsuriUiTheme.SeverityError;
                    }
                    else if (isWarning)
                    {
                        marker.text = "●";
                        marker.style.color = MatsuriUiTheme.SeverityWarning;
                    }
                    else
                    {
                        marker.text = " ";
                        marker.style.color = Color.clear;
                    }
                }

                if (num != null)
                {
                    num.text = lineNumber.ToString();
                    num.style.color = isError
                        ? MatsuriUiTheme.SeverityError
                        : (isCurrent ? MatsuriUiTheme.TextPrimary : MatsuriUiTheme.TextMuted);
                }
            }
        }

        // ── 計測とレイアウト同期 ────────────────────────────────

        /// <summary>1 行ぶんの高さを実測し、変わっていれば全行に反映する。</summary>
        void RefreshMetrics()
        {
            float h = CodeEditorLayout.MeasureLineHeight(_measure, MatsuriUiTheme.CodeFontSize);
            if (h <= CodeEditorLayout.MinLineHeight) return;
            if (Mathf.Abs(h - _lineHeight) < 0.01f) return;

            _lineHeight = h;
            for (int i = 0; i < _lineRows.Count; i++)
            {
                _lineRows[i].style.height = h;
                _gutterRows[i].style.height = h;
            }
        }

        /// <summary>コード領域の最小サイズを表示領域に合わせる（現在行の帯を端まで伸ばすため）。</summary>
        // 直前に書いた寸法。同じ値を書き直すと再レイアウトが走り、
        // それがまた GeometryChangedEvent を呼んで無限に往復することがある
        // （ウィンドウを急にリサイズしたときに「recursive layout」として現れる）。
        float _lastContentW = float.NaN;
        float _lastContentH = float.NaN;

        void UpdateContentSize()
        {
            var viewport = _scroll.contentViewport;
            if (viewport == null) return;

            float viewW = viewport.resolvedStyle.width;
            float viewH = viewport.resolvedStyle.height;

            // ぴったり同じ幅にするとスクロールバーの出現判定が揺れるので 1px 引く
            if (viewW > 2f && !Mathf.Approximately(viewW, _lastContentW))
            {
                _lastContentW = viewW;
                _stack.style.minWidth = viewW - 1f;
                _codeColumn.style.minWidth = Mathf.Max(0f, viewW - GutterWidth - 1f);
            }

            if (viewH > 2f && !Mathf.Approximately(viewH, _lastContentH))
            {
                _lastContentH = viewH;
                _stack.style.minHeight = viewH - 1f;
            }
        }

        /// <summary>
        /// ガター層はスクロールビューの内側にあるので縦は勝手に追従する。
        /// 横スクロールぶんだけ left を戻して、左端に貼り付けたままにする。
        /// </summary>
        void SyncGutterHorizontal()
        {
            float x = _scroll.scrollOffset.x;
            if (x < 0f) x = 0f;
            _gutterLayer.style.left = x;
        }

        // ── 定期処理 ────────────────────────────────────────────

        void Poll()
        {
            SyncGutterHorizontal();
            RefreshMetrics();
            CodeEditorLayout.ResetInnerScroll(_input);

            // 値変更イベントが届かない経路（外部からの差し替え等）でも表示を追従させる
            string current = _input.value ?? string.Empty;
            if (!string.Equals(current, _sourceCache, StringComparison.Ordinal))
            {
                RebuildAll(current);
            }

            int caret = GetCaret();
            if (caret != _caretIndex)
            {
                _caretIndex = caret;
                IndexToLineColumn(Text, caret, out int line, out _);
                if (line != _caretLine)
                {
                    _caretLine = line;
                    RefreshLineStates();
                }
                if (_popup.IsOpen) _popup.SetPosition(ComputeCaretPopupPosition(caret));
            }

            UpdateComposingState();
        }
    }
}
