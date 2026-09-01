using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Matsuri.Script;
using Matsuri.Script.Completion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// キー入力・編集操作・コード補完 (§43)。
    /// （§66 に従い CodeEditorElement を責務ごとに分割した partial の一部）
    /// </summary>
    public sealed partial class CodeEditorElement
    {
        // ── キー入力 ────────────────────────────────────────────

        void OnKeyDown(KeyDownEvent evt)
        {
            if (!IsNavigationKey(evt.keyCode))
            {
                _lastKeyTime = UnityEngine.Time.unscaledTime;
            }

            bool isReturn = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter
                            || evt.character == '\n' || evt.character == '\r';
            bool isTab = evt.keyCode == KeyCode.Tab || evt.character == '\t';
            bool cmd = evt.ctrlKey || evt.commandKey;

            if (isReturn && cmd)
            {
                if (GuardFrame(ref _runFrame)) RunRequested?.Invoke();
                Consume(evt);
                return;
            }

            if (_popup.IsOpen)
            {
                if (evt.keyCode == KeyCode.UpArrow) { _popup.Move(-1); Consume(evt); return; }
                if (evt.keyCode == KeyCode.DownArrow) { _popup.Move(1); Consume(evt); return; }
                if (evt.keyCode == KeyCode.Escape) { _popup.Close(); Consume(evt); return; }
                if (isReturn || isTab)
                {
                    if (GuardFrame(ref _acceptFrame)) _popup.AcceptCurrent();
                    Consume(evt);
                    return;
                }
            }

            if (evt.keyCode == KeyCode.Space && cmd)
            {
                UpdateCompletion(true);
                Consume(evt);
                return;
            }

            if (isTab)
            {
                if (GuardFrame(ref _tabFrame))
                {
                    if (evt.shiftKey) Outdent();
                    else InsertIndent();
                }
                Consume(evt);
                return;
            }

            if (isReturn)
            {
                if (GuardFrame(ref _returnFrame)) InsertNewLineWithIndent();
                Consume(evt);
                return;
            }

            if (evt.keyCode == KeyCode.Escape) _popup.Close();
        }

        static void Consume(EventBase evt)
        {
            evt.StopImmediatePropagation();
            evt.PreventDefault();
        }

        static bool GuardFrame(ref int field)
        {
            int f = UnityEngine.Time.frameCount;
            if (field == f) return false;
            field = f;
            return true;
        }

        static bool IsNavigationKey(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.Home:
                case KeyCode.End:
                case KeyCode.PageUp:
                case KeyCode.PageDown:
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                case KeyCode.Escape:
                    return true;
                default:
                    return false;
            }
        }

        // ── 編集操作 ────────────────────────────────────────────

        void InsertIndent()
        {
            GetSelection(out int start, out int end);
            ReplaceRange(start, end - start, new string(' ', MatsuriUiTheme.IndentSize), MatsuriUiTheme.IndentSize);
        }

        void Outdent()
        {
            string text = Text;
            GetSelection(out int start, out _);
            int lineStart = LineStartIndex(text, LineOf(text, start));
            int remove = 0;
            while (remove < MatsuriUiTheme.IndentSize
                   && start - remove - 1 >= lineStart
                   && text[start - remove - 1] == ' ')
            {
                remove++;
            }
            if (remove == 0) return;
            ReplaceRange(start - remove, remove, string.Empty, 0);
        }

        void InsertNewLineWithIndent()
        {
            string text = Text;
            GetSelection(out int start, out int end);
            int lineStart = LineStartIndex(text, LineOf(text, start));

            int indent = 0;
            while (lineStart + indent < text.Length && lineStart + indent < start && text[lineStart + indent] == ' ') indent++;

            string beforeCaret = text.Substring(lineStart, Mathf.Max(0, start - lineStart)).TrimEnd();
            bool opensBlock = beforeCaret.EndsWith("{", StringComparison.Ordinal);
            if (opensBlock) indent += MatsuriUiTheme.IndentSize;

            char next = end < text.Length ? text[end] : '\0';
            if (opensBlock && next == '}')
            {
                int closeIndent = Mathf.Max(0, indent - MatsuriUiTheme.IndentSize);
                string insert = "\n" + new string(' ', indent) + "\n" + new string(' ', closeIndent);
                ReplaceRange(start, end - start, insert, 1 + indent);
            }
            else
            {
                string insert = "\n" + new string(' ', indent);
                ReplaceRange(start, end - start, insert, insert.Length);
            }
        }

        void ReplaceRange(int start, int length, string insert, int caretOffset)
        {
            string text = Text;
            start = Mathf.Clamp(start, 0, text.Length);
            length = Mathf.Clamp(length, 0, text.Length - start);
            insert ??= string.Empty;

            string next = text.Substring(0, start) + insert + text.Substring(start + length);
            _input.value = next;

            int caret = Mathf.Clamp(start + caretOffset, 0, next.Length);
            SetCaret(caret);
            IndexToLineColumn(next, caret, out int line, out _);
            EnsureLineVisible(line);
        }

        // ── 補完 (§43) ─────────────────────────────────────────

        void UpdateCompletion(bool force)
        {
            if (Catalog == null) { _popup.Close(); return; }

            string text = Text;
            int caret = GetCaret();
            if (caret < 0 || caret > text.Length) { _popup.Close(); return; }
            if (!force && !ShouldSuggest(text, caret)) { _popup.Close(); return; }

            List<CompletionItem> items;
            try
            {
                items = CompletionProvider.GetCompletions(text, caret, Catalog);
            }
            catch (Exception)
            {
                _popup.Close();
                return;
            }

            if (items == null || items.Count == 0) { _popup.Close(); return; }
            _popup.Open(items, ComputeCaretPopupPosition(caret));
        }

        static bool ShouldSuggest(string text, int caret)
        {
            if (caret <= 0) return false;
            char prev = text[caret - 1];
            if (IsWordChar(prev)) return true;
            if (prev != ' ' && prev != '　') return false;

            // 「屋台 」「装飾 」の直後は名前の候補を出す
            int i = caret - 1;
            while (i > 0 && (text[i - 1] == ' ' || text[i - 1] == '　')) i--;
            int wordEnd = i;
            while (i > 0 && IsWordChar(text[i - 1])) i--;
            if (wordEnd <= i) return false;
            string word = text.Substring(i, wordEnd - i);
            return word == "屋台" || word == "装飾" || word == "設備" || word == "イベント"
                   || word == "stall" || word == "decoration" || word == "facility" || word == "event";
        }

        void OnCompletionAccepted(CompletionItem item)
        {
            string text = Text;
            int caret = GetCaret();
            if (caret < 0 || caret > text.Length) return;

            int start = caret;
            while (start > 0 && IsWordChar(text[start - 1])) start--;

            string insert = item.InsertText;
            if (string.IsNullOrEmpty(insert)) insert = item.Label;
            if (string.IsNullOrEmpty(insert)) return;

            // ひな形が複数行なら現在行のインデントに合わせる
            if (insert.IndexOf('\n') >= 0)
            {
                int lineStart = LineStartIndex(text, LineOf(text, start));
                int indent = 0;
                while (lineStart + indent < text.Length && text[lineStart + indent] == ' ') indent++;
                if (indent > 0)
                {
                    string pad = new string(' ', indent);
                    var sb = new StringBuilder();
                    var lines = insert.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i > 0) sb.Append('\n').Append(pad);
                        sb.Append(lines[i]);
                    }
                    insert = sb.ToString();
                }
            }

            ReplaceRange(start, caret - start, insert, insert.Length);
            _input.Focus();
        }

        Vector2 ComputeCaretPopupPosition(int caret)
        {
            string text = Text;
            IndexToLineColumn(text, caret, out int line, out int column);
            int lineStart = LineStartIndex(text, line);
            int prefixLength = Mathf.Clamp(column, 0, Mathf.Max(0, text.Length - lineStart));
            string prefix = text.Substring(lineStart, prefixLength);

            float x = MeasureWidth(prefix);
            float y = (line + 1) * LineHeight + 4f;

            // コード文字の原点はガターの右 + 左余白。ここを間違えると候補窓が行とずれる。
            Vector2 world = _stack.LocalToWorld(new Vector2(GutterWidth + TextLeft + x, TextTop + y));
            Vector2 local = this.WorldToLocal(world);

            float w = resolvedStyle.width;
            float h = resolvedStyle.height;
            if (w > 0f) local.x = Mathf.Clamp(local.x, 4f, Mathf.Max(4f, w - 348f));
            if (h > 0f && local.y > h - 130f) local.y = Mathf.Max(4f, local.y - LineHeight - 260f);
            if (local.y < 4f) local.y = 4f;
            return local;
        }

        float MeasureWidth(string s)
            => CodeEditorLayout.MeasureWidth(_measure, s, MatsuriUiTheme.CodeFontSize);

        /// <summary>1 行ぶんの高さ。実測値は CodeEditorRows 側が維持している。</summary>
        float LineHeight
            => _lineHeight > CodeEditorLayout.MinLineHeight
                ? _lineHeight
                : CodeEditorLayout.FallbackLineHeight(MatsuriUiTheme.CodeFontSize);

        void EnsureLineVisible(int line)
        {
            float lh = LineHeight;
            float top = TextTop + line * lh;
            float bottom = top + lh;
            float viewH = _scroll.contentViewport.resolvedStyle.height;
            if (viewH <= 0f) return;

            var offset = _scroll.scrollOffset;
            if (top < offset.y) offset.y = Mathf.Max(0f, top - lh);
            else if (bottom > offset.y + viewH) offset.y = bottom - viewH + lh;
            _scroll.scrollOffset = offset;
        }
    }
}
