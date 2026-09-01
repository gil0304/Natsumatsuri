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
    /// キャレット操作とテキスト計算。UI Toolkit のキャレットAPIはリフレクションで解決する。
    /// （§66 に従い CodeEditorElement を責務ごとに分割した partial の一部）
    /// </summary>
    public sealed partial class CodeEditorElement
    {
        // ── キャレット ─────────────────────────────────────────

        int GetCaret()
        {
            int fallback = (_input.value ?? string.Empty).Length;
            return CaretAccess.GetCursorIndex(_input, fallback);
        }

        void GetSelection(out int start, out int end)
        {
            int cursor = GetCaret();
            int select = CaretAccess.GetSelectIndex(_input, cursor);
            start = Mathf.Min(cursor, select);
            end = Mathf.Max(cursor, select);
        }

        void SetCaret(int index)
        {
            CaretAccess.SetCursorIndex(_input, index);
            _caretIndex = index;
            IndexToLineColumn(Text, index, out int line, out _);
            _caretLine = line;
            RefreshLineStates();
            // 値変更直後はレイアウトが確定していないので次フレームでも設定する
            schedule.Execute(() => CaretAccess.SetCursorIndex(_input, index)).ExecuteLater(0);
        }

        // ── テキストユーティリティ ─────────────────────────────

        static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int n = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
            return n;
        }

        static int LineOf(string text, int index)
        {
            IndexToLineColumn(text, index, out int line, out _);
            return line;
        }

        static void IndexToLineColumn(string text, int index, out int line, out int column)
        {
            line = 0;
            column = 0;
            if (string.IsNullOrEmpty(text)) return;
            if (index < 0) index = 0;
            if (index > text.Length) index = text.Length;
            int lastBreak = -1;
            for (int i = 0; i < index; i++)
            {
                if (text[i] == '\n') { line++; lastBreak = i; }
            }
            column = index - lastBreak - 1;
        }

        static int LineStartIndex(string text, int line)
        {
            if (string.IsNullOrEmpty(text) || line <= 0) return 0;
            int current = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;
                current++;
                if (current == line) return i + 1;
            }
            return text.Length;
        }

        static bool IsWordChar(char c)
        {
            if (c == '_') return true;
            return char.IsLetterOrDigit(c);
        }

        // ── TextField のキャレット操作（リフレクション） ────────
        //
        // UI Toolkit のキャレットAPIはバージョンにより公開経路が異なるため、
        // コンパイル時依存を持たずに解決する。取得できない場合も編集自体は動く。

        static class CaretAccess
        {
            static bool _initialized;
            static PropertyInfo _selectionProp;
            static PropertyInfo _selCursorIndex;
            static PropertyInfo _selSelectIndex;
            static PropertyInfo _selCursorColor;
            static PropertyInfo _fieldCursorIndex;
            static PropertyInfo _fieldSelectIndex;

            static void Init(TextField field)
            {
                if (_initialized) return;
                _initialized = true;
                const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                try
                {
                    var type = field.GetType();
                    _selectionProp = type.GetProperty("textSelection", Flags);
                    if (_selectionProp != null)
                    {
                        var st = _selectionProp.PropertyType;
                        _selCursorIndex = st.GetProperty("cursorIndex");
                        _selSelectIndex = st.GetProperty("selectIndex");
                        _selCursorColor = st.GetProperty("cursorColor");
                    }
                    _fieldCursorIndex = type.GetProperty("cursorIndex", Flags);
                    _fieldSelectIndex = type.GetProperty("selectIndex", Flags);
                }
                catch (Exception)
                {
                    _selectionProp = null;
                }
            }

            static object GetSelection(TextField field)
            {
                if (_selectionProp == null) return null;
                try { return _selectionProp.GetValue(field); }
                catch (Exception) { return null; }
            }

            public static int GetCursorIndex(TextField field, int fallback)
            {
                Init(field);
                try
                {
                    var sel = GetSelection(field);
                    if (sel != null && _selCursorIndex != null) return (int)_selCursorIndex.GetValue(sel);
                    if (_fieldCursorIndex != null) return (int)_fieldCursorIndex.GetValue(field);
                }
                catch (Exception)
                {
                    // 取得できない環境では末尾扱い
                }
                return fallback;
            }

            public static int GetSelectIndex(TextField field, int fallback)
            {
                Init(field);
                try
                {
                    var sel = GetSelection(field);
                    if (sel != null && _selSelectIndex != null) return (int)_selSelectIndex.GetValue(sel);
                    if (_fieldSelectIndex != null) return (int)_fieldSelectIndex.GetValue(field);
                }
                catch (Exception)
                {
                    // 取得できない環境では選択なし扱い
                }
                return fallback;
            }

            public static void SetCursorIndex(TextField field, int index)
            {
                Init(field);
                if (index < 0) index = 0;
                try
                {
                    var sel = GetSelection(field);
                    if (sel != null && _selCursorIndex != null && _selSelectIndex != null)
                    {
                        _selSelectIndex.SetValue(sel, index);
                        _selCursorIndex.SetValue(sel, index);
                        return;
                    }
                    if (_fieldSelectIndex != null) _fieldSelectIndex.SetValue(field, index);
                    if (_fieldCursorIndex != null) _fieldCursorIndex.SetValue(field, index);
                }
                catch (Exception)
                {
                    // 設定できない環境では何もしない
                }
            }

            public static void TrySetCursorColor(TextField field, Color color)
            {
                Init(field);
                try
                {
                    var sel = GetSelection(field);
                    if (sel != null && _selCursorColor != null) _selCursorColor.SetValue(sel, color);
                }
                catch (Exception)
                {
                    // キャレット色を変えられなくても致命的ではない
                }
            }
        }
    }
}
