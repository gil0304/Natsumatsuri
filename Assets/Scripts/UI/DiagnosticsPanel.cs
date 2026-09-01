using System;
using System.Collections.Generic;
using Matsuri.Script;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// エラー表示パネル (§41 / §42)。
    /// 「12行目 / 『場所』が設定されていません。/ 例: …」を日本語で並べる。
    /// クリックするとそのエラー行にキャレットが飛ぶ。
    /// エラーが無いときは「問題なし」を控えめに出すだけにする。
    /// </summary>
    public sealed class DiagnosticsPanel : VisualElement
    {
        readonly Label _titleLabel;
        readonly Label _countBadge;
        readonly ScrollView _list;
        readonly Label _emptyLabel;

        /// <summary>行がクリックされた（1始まりの行番号）。</summary>
        public event Action<int> LineSelected;

        public DiagnosticsPanel()
        {
            AddToClassList("matsuri-diagnostics");
            style.flexDirection = FlexDirection.Column;
            style.backgroundColor = MatsuriUiTheme.BgPanel;
            MatsuriUiTheme.SetRadius(this, MatsuriUiTheme.RadiusLarge);
            MatsuriUiTheme.SetBorder(this, 1f, MatsuriUiTheme.Border);
            style.overflow = Overflow.Hidden;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 32f;
            header.style.backgroundColor = MatsuriUiTheme.Hex("#0A0D16");
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = MatsuriUiTheme.Border;
            MatsuriUiTheme.SetPadding(header, 0f, 12f, 0f, 14f);
            Add(header);

            _titleLabel = new Label("PROBLEMS");
            _titleLabel.style.fontSize = MatsuriUiTheme.TitleFontSize;
            _titleLabel.style.color = MatsuriUiTheme.TextSecondary;
            _titleLabel.style.letterSpacing = 3f;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            MatsuriUiTheme.ApplyUiFont(_titleLabel);
            header.Add(_titleLabel);

            _countBadge = new Label("0");
            _countBadge.style.fontSize = 10f;
            _countBadge.style.marginLeft = 10f;
            _countBadge.style.color = MatsuriUiTheme.TextMuted;
            MatsuriUiTheme.SetPadding(_countBadge, 1f, 7f, 1f, 7f);
            MatsuriUiTheme.SetRadius(_countBadge, 8f);
            _countBadge.style.backgroundColor = MatsuriUiTheme.Hex("#1B2133");
            MatsuriUiTheme.ApplyUiFont(_countBadge);
            header.Add(_countBadge);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1f;
            Add(_list);

            _emptyLabel = new Label("問題なし");
            _emptyLabel.style.fontSize = 12f;
            _emptyLabel.style.color = MatsuriUiTheme.TextMuted;
            MatsuriUiTheme.SetPadding(_emptyLabel, 12f, 14f, 12f, 16f);
            MatsuriUiTheme.ApplyUiFont(_emptyLabel);
            _list.Add(_emptyLabel);
        }

        /// <summary>診断を表示する。</summary>
        public void SetDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
        {
            _list.Clear();

            int errors = 0, warnings = 0;
            if (diagnostics != null)
            {
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    var d = diagnostics[i];
                    if (d == null) continue;
                    if (d.Severity == DiagnosticSeverity.Error) errors++;
                    else if (d.Severity == DiagnosticSeverity.Warning) warnings++;
                }
            }

            int total = diagnostics?.Count ?? 0;
            _countBadge.text = errors > 0 ? errors + " 件のエラー"
                : (warnings > 0 ? warnings + " 件の注意" : "0");
            _countBadge.style.color = errors > 0 ? MatsuriUiTheme.SeverityError
                : (warnings > 0 ? MatsuriUiTheme.SeverityWarning : MatsuriUiTheme.TextMuted);
            _countBadge.style.backgroundColor = errors > 0
                ? MatsuriUiTheme.Hex("#2A1414")
                : MatsuriUiTheme.Hex("#1B2133");

            if (total == 0)
            {
                _list.Add(_emptyLabel);
                return;
            }

            for (int i = 0; i < diagnostics.Count; i++)
            {
                var d = diagnostics[i];
                if (d == null) continue;
                _list.Add(CreateRow(d));
            }
        }

        VisualElement CreateRow(Diagnostic d)
        {
            Color color = SeverityColor(d.Severity);

            var row = new VisualElement();
            row.AddToClassList("matsuri-diagnostics__row");
            row.style.flexDirection = FlexDirection.Row;
            MatsuriUiTheme.SetPadding(row, 9f, 14f, 9f, 12f);
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = MatsuriUiTheme.Hex("#1A2032");

            var bar = new VisualElement();
            bar.style.width = 3f;
            bar.style.flexShrink = 0f;
            bar.style.backgroundColor = color;
            MatsuriUiTheme.SetRadius(bar, 2f);
            bar.style.marginRight = 10f;
            row.Add(bar);

            var content = new VisualElement();
            content.style.flexGrow = 1f;
            row.Add(content);

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 3f;
            content.Add(head);

            var lineLabel = new Label(d.Line + "行目");
            lineLabel.style.fontSize = 11f;
            lineLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            lineLabel.style.color = color;
            lineLabel.style.marginRight = 8f;
            MatsuriUiTheme.ApplyUiFont(lineLabel);
            head.Add(lineLabel);

            var kindLabel = new Label(SeverityName(d.Severity));
            kindLabel.style.fontSize = 10f;
            kindLabel.style.color = MatsuriUiTheme.TextMuted;
            MatsuriUiTheme.ApplyUiFont(kindLabel);
            head.Add(kindLabel);

            var message = new Label(d.Message);
            message.style.fontSize = 13f;
            message.style.color = MatsuriUiTheme.TextPrimary;
            message.style.whiteSpace = WhiteSpace.Normal;
            MatsuriUiTheme.ApplyUiFont(message);
            content.Add(message);

            if (d.Suggestions != null && d.Suggestions.Count > 0)
            {
                var sb = new System.Text.StringBuilder("もしかして: ");
                for (int i = 0; i < d.Suggestions.Count; i++)
                {
                    if (i > 0) sb.Append(" / ");
                    sb.Append(d.Suggestions[i]);
                }
                var suggest = new Label(sb.ToString());
                suggest.style.fontSize = 12f;
                suggest.style.color = MatsuriUiTheme.AccentYellow;
                suggest.style.marginTop = 4f;
                suggest.style.whiteSpace = WhiteSpace.Normal;
                MatsuriUiTheme.ApplyUiFont(suggest);
                content.Add(suggest);
            }

            if (!string.IsNullOrEmpty(d.Example))
            {
                var example = new Label(d.Example);
                example.enableRichText = false;
                example.style.fontSize = 12f;
                example.style.color = MatsuriUiTheme.Hex("#9FB2CC");
                example.style.whiteSpace = WhiteSpace.NoWrap;
                example.style.marginTop = 6f;
                example.style.backgroundColor = MatsuriUiTheme.Hex("#0A0D16");
                MatsuriUiTheme.SetPadding(example, 6f, 10f, 6f, 10f);
                MatsuriUiTheme.SetRadius(example, 4f);
                MatsuriUiTheme.SetBorder(example, 1f, MatsuriUiTheme.Hex("#1F2739"));
                MatsuriUiTheme.ApplyCodeFont(example);
                content.Add(example);
            }

            int line = d.Line;
            row.RegisterCallback<PointerDownEvent>(_ => LineSelected?.Invoke(line));
            row.RegisterCallback<PointerEnterEvent>(_ => row.style.backgroundColor = MatsuriUiTheme.Hex("#161C2B"));
            row.RegisterCallback<PointerLeaveEvent>(_ => row.style.backgroundColor = Color.clear);

            return row;
        }

        static Color SeverityColor(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Error: return MatsuriUiTheme.SeverityError;
                case DiagnosticSeverity.Warning: return MatsuriUiTheme.SeverityWarning;
                default: return MatsuriUiTheme.SeverityInfo;
            }
        }

        static string SeverityName(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Error: return "エラー";
                case DiagnosticSeverity.Warning: return "注意";
                default: return "情報";
            }
        }
    }
}
