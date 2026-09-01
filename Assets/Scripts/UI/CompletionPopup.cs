using System;
using System.Collections.Generic;
using Matsuri.Script.Completion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// コード補完のポップアップ (§43)。
    /// ↑↓ で選択、Tab / Enter で確定、Esc で閉じる。
    /// 見た目はコードエディター(VS Code 風)に寄せる。教育ソフト風の装飾は付けない。
    /// </summary>
    public sealed class CompletionPopup : VisualElement
    {
        const int MaxVisibleRows = 9;
        const float RowHeight = 26f;

        readonly VisualElement _rowsContainer;
        readonly Label _detailLabel;
        readonly List<CompletionItem> _items = new List<CompletionItem>();
        readonly List<VisualElement> _rows = new List<VisualElement>();

        int _selected;
        int _windowStart;

        /// <summary>確定された補完項目。</summary>
        public event Action<CompletionItem> Accepted;

        public bool IsOpen { get; private set; }

        public int Count => _items.Count;

        public CompletionPopup()
        {
            AddToClassList("matsuri-completion");
            style.position = Position.Absolute;
            style.width = 340f;
            style.backgroundColor = MatsuriUiTheme.Hex("#141A28FA");
            MatsuriUiTheme.SetRadius(this, MatsuriUiTheme.Radius);
            MatsuriUiTheme.SetBorder(this, 1f, MatsuriUiTheme.BorderBright);
            style.overflow = Overflow.Hidden;
            style.display = DisplayStyle.None;
            style.paddingTop = 4f;
            style.paddingBottom = 4f;

            _rowsContainer = new VisualElement();
            _rowsContainer.style.flexDirection = FlexDirection.Column;
            Add(_rowsContainer);

            _detailLabel = new Label();
            _detailLabel.style.fontSize = 11f;
            _detailLabel.style.color = MatsuriUiTheme.TextSecondary;
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            MatsuriUiTheme.SetPadding(_detailLabel, 6f, 10f, 6f, 10f);
            _detailLabel.style.borderTopWidth = 1f;
            _detailLabel.style.borderTopColor = MatsuriUiTheme.Border;
            _detailLabel.style.display = DisplayStyle.None;
            MatsuriUiTheme.ApplyUiFont(_detailLabel);
            Add(_detailLabel);

            for (int i = 0; i < MaxVisibleRows; i++)
            {
                _rows.Add(CreateRow(i));
                _rowsContainer.Add(_rows[i]);
            }
        }

        VisualElement CreateRow(int slot)
        {
            var row = new VisualElement();
            row.AddToClassList("matsuri-completion__row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = RowHeight;
            MatsuriUiTheme.SetPadding(row, 0f, 10f, 0f, 8f);

            var icon = new Label();
            icon.name = "icon";
            icon.style.width = 22f;
            icon.style.fontSize = 11f;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            MatsuriUiTheme.ApplyUiFont(icon);
            row.Add(icon);

            var label = new Label();
            label.name = "label";
            label.style.flexGrow = 1f;
            label.style.fontSize = 13f;
            label.style.color = MatsuriUiTheme.TextPrimary;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            MatsuriUiTheme.ApplyCodeFont(label);
            row.Add(label);

            var kind = new Label();
            kind.name = "kind";
            kind.style.fontSize = 10f;
            kind.style.color = MatsuriUiTheme.TextMuted;
            kind.style.unityTextAlign = TextAnchor.MiddleRight;
            MatsuriUiTheme.ApplyUiFont(kind);
            row.Add(kind);

            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                int index = _windowStart + slot;
                if (index >= 0 && index < _items.Count)
                {
                    _selected = index;
                    Refresh();
                    AcceptCurrent();
                }
                evt.StopPropagation();
            });

            return row;
        }

        /// <summary>候補を表示する。position は親要素のローカル座標（左上）。</summary>
        public void Open(IReadOnlyList<CompletionItem> items, Vector2 position)
        {
            if (items == null || items.Count == 0)
            {
                Close();
                return;
            }

            _items.Clear();
            for (int i = 0; i < items.Count && i < 60; i++) _items.Add(items[i]);

            _selected = 0;
            _windowStart = 0;
            IsOpen = true;
            style.display = DisplayStyle.Flex;
            SetPosition(position);
            Refresh();
            BringToFront();
        }

        /// <summary>表示位置だけを更新する。</summary>
        public void SetPosition(Vector2 position)
        {
            style.left = Mathf.Max(0f, position.x);
            style.top = Mathf.Max(0f, position.y);
        }

        public void Close()
        {
            if (!IsOpen && style.display == DisplayStyle.None) return;
            IsOpen = false;
            _items.Clear();
            style.display = DisplayStyle.None;
        }

        /// <summary>↑↓ で選択を動かす。</summary>
        public void Move(int delta)
        {
            if (!IsOpen || _items.Count == 0) return;
            _selected += delta;
            if (_selected < 0) _selected = _items.Count - 1;
            if (_selected >= _items.Count) _selected = 0;

            if (_selected < _windowStart) _windowStart = _selected;
            if (_selected >= _windowStart + MaxVisibleRows) _windowStart = _selected - MaxVisibleRows + 1;
            Refresh();
        }

        /// <summary>現在の選択を確定する。確定したら true。</summary>
        public bool AcceptCurrent()
        {
            if (!IsOpen || _selected < 0 || _selected >= _items.Count) return false;
            var item = _items[_selected];
            Close();
            Accepted?.Invoke(item);
            return true;
        }

        void Refresh()
        {
            int visible = Mathf.Min(MaxVisibleRows, _items.Count);
            for (int slot = 0; slot < _rows.Count; slot++)
            {
                var row = _rows[slot];
                int index = _windowStart + slot;
                if (slot >= visible || index >= _items.Count)
                {
                    row.style.display = DisplayStyle.None;
                    continue;
                }

                var item = _items[index];
                row.style.display = DisplayStyle.Flex;
                bool isSelected = index == _selected;
                row.style.backgroundColor = isSelected
                    ? MatsuriUiTheme.Hex("#243352")
                    : Color.clear;

                var icon = row.Q<Label>("icon");
                icon.text = KindIcon(item.Kind);
                icon.style.color = KindColor(item.Kind);

                var label = row.Q<Label>("label");
                label.text = item.Label ?? string.Empty;
                label.style.color = isSelected ? MatsuriUiTheme.TextPrimary : MatsuriUiTheme.Hex("#C3CBDE");

                var kind = row.Q<Label>("kind");
                kind.text = KindName(item.Kind);
            }

            string detail = (_selected >= 0 && _selected < _items.Count) ? _items[_selected].Detail : null;
            if (string.IsNullOrEmpty(detail))
            {
                _detailLabel.style.display = DisplayStyle.None;
            }
            else
            {
                _detailLabel.style.display = DisplayStyle.Flex;
                _detailLabel.text = detail;
            }
        }

        static string KindIcon(CompletionKind kind)
        {
            switch (kind)
            {
                case CompletionKind.Keyword: return "語";
                case CompletionKind.StallName: return "屋";
                case CompletionKind.DecorationName: return "飾";
                case CompletionKind.FacilityName: return "設";
                case CompletionKind.EventName: return "祭";
                case CompletionKind.Property: return "属";
                case CompletionKind.Metric: return "値";
                case CompletionKind.Snippet: return "型";
                default: return "・";
            }
        }

        static string KindName(CompletionKind kind)
        {
            switch (kind)
            {
                case CompletionKind.Keyword: return "キーワード";
                case CompletionKind.StallName: return "屋台";
                case CompletionKind.DecorationName: return "装飾";
                case CompletionKind.FacilityName: return "設備";
                case CompletionKind.EventName: return "イベント";
                case CompletionKind.Property: return "プロパティ";
                case CompletionKind.Metric: return "指標";
                case CompletionKind.Snippet: return "ひな形";
                default: return string.Empty;
            }
        }

        static Color KindColor(CompletionKind kind)
        {
            switch (kind)
            {
                case CompletionKind.Keyword: return MatsuriUiTheme.Hex(MatsuriUiTheme.SynKeyword);
                case CompletionKind.Property: return MatsuriUiTheme.Hex(MatsuriUiTheme.SynProperty);
                case CompletionKind.Metric: return MatsuriUiTheme.Hex(MatsuriUiTheme.SynMetric);
                case CompletionKind.EventName: return MatsuriUiTheme.AccentYellow;
                case CompletionKind.StallName: return MatsuriUiTheme.AccentWarm;
                case CompletionKind.DecorationName: return MatsuriUiTheme.AccentRed;
                case CompletionKind.FacilityName: return MatsuriUiTheme.AccentGreen;
                default: return MatsuriUiTheme.TextSecondary;
            }
        }
    }
}
