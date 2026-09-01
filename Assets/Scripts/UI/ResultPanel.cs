using System;
using System.Collections.Generic;
using Matsuri.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// 祭りの結果画面 (§36)。
    /// 売上 → 来場者 → 平均満足度 → 最高同時来場者 → 人気No.1 を1つずつ順に出し、
    /// 最後にスコアとランキング順位を出す。カウントアップ演出つき。
    /// </summary>
    public sealed class ResultPanel : VisualElement
    {
        sealed class Row
        {
            public VisualElement Root;
            public Label Value;
        }

        readonly Label _titleLabel;
        readonly Label _subtitleLabel;
        readonly VisualElement _rowsContainer;
        readonly Label _scoreValue;
        readonly Label _rankLabel;
        readonly VisualElement _scoreBlock;
        readonly VisualElement _buttonRow;

        readonly List<Row> _rows = new List<Row>();
        readonly List<IVisualElementScheduledItem> _scheduled = new List<IVisualElementScheduledItem>();

        /// <summary>「もう一度」。</summary>
        public event Action RetryRequested;

        /// <summary>「コードを直す」。</summary>
        public event Action EditCodeRequested;

        public bool IsShown => style.display == DisplayStyle.Flex;

        public ResultPanel()
        {
            AddToClassList("matsuri-result");
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.bottom = 0f;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.88f);
            style.display = DisplayStyle.None;

            var card = new VisualElement();
            card.AddToClassList("matsuri-result__card");
            card.style.width = 560f;
            card.style.maxWidth = Length.Percent(92f);
            card.style.backgroundColor = MatsuriUiTheme.Hex("#0E1220FA");
            MatsuriUiTheme.SetRadius(card, 16f);
            MatsuriUiTheme.SetBorder(card, 1f, MatsuriUiTheme.Hex("#2E3650"));
            MatsuriUiTheme.SetPadding(card, 30f, 34f, 26f, 34f);
            Add(card);

            _titleLabel = new Label("祭り、おわり");
            _titleLabel.style.fontSize = 26f;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = MatsuriUiTheme.TextPrimary;
            MatsuriUiTheme.ApplyUiFont(_titleLabel);
            card.Add(_titleLabel);

            _subtitleLabel = new Label();
            _subtitleLabel.style.fontSize = 12f;
            _subtitleLabel.style.color = MatsuriUiTheme.TextSecondary;
            _subtitleLabel.style.marginTop = 4f;
            _subtitleLabel.style.marginBottom = 20f;
            MatsuriUiTheme.ApplyUiFont(_subtitleLabel);
            card.Add(_subtitleLabel);

            _rowsContainer = new VisualElement();
            card.Add(_rowsContainer);

            for (int i = 0; i < 5; i++) _rows.Add(CreateRow(_rowsContainer));

            // ── スコア ──
            _scoreBlock = new VisualElement();
            _scoreBlock.style.marginTop = 22f;
            MatsuriUiTheme.SetPadding(_scoreBlock, 16f, 18f, 16f, 18f);
            MatsuriUiTheme.SetRadius(_scoreBlock, 10f);
            _scoreBlock.style.backgroundColor = MatsuriUiTheme.Hex("#161122");
            MatsuriUiTheme.SetBorder(_scoreBlock, 1f, MatsuriUiTheme.Hex("#4A3320"));
            _scoreBlock.style.alignItems = Align.Center;
            _scoreBlock.style.display = DisplayStyle.None;
            card.Add(_scoreBlock);

            var scoreCaption = new Label("スコア");
            scoreCaption.style.fontSize = MatsuriUiTheme.CaptionFontSize;
            scoreCaption.style.letterSpacing = 4f;
            scoreCaption.style.color = MatsuriUiTheme.TextSecondary;
            MatsuriUiTheme.ApplyUiFont(scoreCaption);
            _scoreBlock.Add(scoreCaption);

            _scoreValue = new Label("0");
            _scoreValue.style.fontSize = 42f;
            _scoreValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scoreValue.style.color = MatsuriUiTheme.AccentYellow;
            MatsuriUiTheme.ApplyUiFont(_scoreValue);
            _scoreBlock.Add(_scoreValue);

            _rankLabel = new Label();
            _rankLabel.style.fontSize = 13f;
            _rankLabel.style.color = MatsuriUiTheme.AccentWarm;
            _rankLabel.style.marginTop = 4f;
            MatsuriUiTheme.ApplyUiFont(_rankLabel);
            _scoreBlock.Add(_rankLabel);

            // ── ボタン ──
            _buttonRow = new VisualElement();
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.justifyContent = Justify.Center;
            _buttonRow.style.marginTop = 24f;
            _buttonRow.style.display = DisplayStyle.None;
            card.Add(_buttonRow);

            _buttonRow.Add(MatsuriUiTheme.CreateButton("もう一度", () => RetryRequested?.Invoke(), true));
            _buttonRow.Add(MatsuriUiTheme.CreateButton("コードを直す", () =>
            {
                Hide();
                EditCodeRequested?.Invoke();
            }));
        }

        static Row CreateRow(VisualElement parent)
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.alignItems = Align.Center;
            MatsuriUiTheme.SetPadding(root, 9f, 2f, 9f, 2f);
            root.style.borderBottomWidth = 1f;
            root.style.borderBottomColor = MatsuriUiTheme.Hex("#1C2334");
            root.style.display = DisplayStyle.None;
            parent.Add(root);

            var caption = new Label();
            caption.name = "caption";
            caption.style.fontSize = 13f;
            caption.style.color = MatsuriUiTheme.TextSecondary;
            MatsuriUiTheme.ApplyUiFont(caption);
            root.Add(caption);

            var value = new Label();
            value.name = "value";
            value.style.fontSize = 21f;
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            value.style.color = MatsuriUiTheme.TextPrimary;
            MatsuriUiTheme.ApplyUiFont(value);
            root.Add(value);

            return new Row { Root = root, Value = value };
        }

        /// <summary>結果を表示する。</summary>
        public void Show(FestivalResult result)
        {
            CancelScheduled();

            style.display = DisplayStyle.Flex;
            BringToFront();

            if (result == null)
            {
                _titleLabel.text = "祭り、おわり";
                _subtitleLabel.text = string.Empty;
                return;
            }

            string name = string.IsNullOrEmpty(result.FestivalName) ? "夏祭り" : result.FestivalName;
            _titleLabel.text = name + "、おわり";
            _subtitleLabel.text = BuildSubtitle(result);

            float satisfaction = result.AverageSatisfaction;
            if (satisfaction <= 1.0001f) satisfaction *= 100f;

            Prepare(0, "売上", MatsuriUiTheme.AccentYellow);
            Prepare(1, "来場者", MatsuriUiTheme.AccentBlue);
            Prepare(2, "平均満足度", MatsuriUiTheme.AccentGreen);
            Prepare(3, "最高同時来場者", MatsuriUiTheme.TextPrimary);
            Prepare(4, "人気No.1", MatsuriUiTheme.AccentWarm);

            _scoreBlock.style.display = DisplayStyle.None;
            _buttonRow.style.display = DisplayStyle.None;
            _rankLabel.text = string.Empty;

            RevealNumber(0, 0, result.Revenue, 380, v => MatsuriUiTheme.FormatYen((long)v));
            RevealNumber(1, 900, result.VisitorCount, 380, v => MatsuriUiTheme.FormatCount((long)v) + " 人");
            RevealNumber(2, 1800, satisfaction, 380, v => v.ToString("0.0") + " %");
            RevealNumber(3, 2700, result.PeakConcurrent, 380, v => MatsuriUiTheme.FormatCount((long)v) + " 人");

            Schedule(3600, () =>
            {
                var row = _rows[4];
                row.Root.style.display = DisplayStyle.Flex;
                string top = string.IsNullOrEmpty(result.TopStallName) ? "—" : result.TopStallName;
                row.Value.text = result.TopStallRevenue > 0
                    ? $"{top}  ({MatsuriUiTheme.FormatYen(result.TopStallRevenue)})"
                    : top;
                FadeIn(row.Root);
            });

            Schedule(4500, () =>
            {
                _scoreBlock.style.display = DisplayStyle.Flex;
                FadeIn(_scoreBlock);
                MatsuriUiTheme.Tween(this, 0f, result.TotalScore, 900,
                    v => _scoreValue.text = MatsuriUiTheme.FormatCount((long)v));
                _rankLabel.text = BuildRankText(result);
            });

            Schedule(5600, () =>
            {
                _buttonRow.style.display = DisplayStyle.Flex;
                FadeIn(_buttonRow);
            });
        }

        public void Hide()
        {
            CancelScheduled();
            style.display = DisplayStyle.None;
        }

        // ── 内部 ───────────────────────────────────────────────

        static string BuildSubtitle(FestivalResult r)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(r.ModeName)) parts.Add(r.ModeName);
            if (r.StallKindsUsed > 0) parts.Add($"屋台の種類 {r.StallKindsUsed}");
            if (!string.IsNullOrEmpty(r.CreatedDate)) parts.Add(r.CreatedDate);
            return string.Join("  ・  ", parts);
        }

        void Prepare(int index, string caption, Color color)
        {
            var row = _rows[index];
            row.Root.style.display = DisplayStyle.None;
            row.Root.style.opacity = 0f;
            row.Value.style.color = color;
            row.Value.text = "—";
            var captionLabel = row.Root.Q<Label>("caption");
            if (captionLabel != null) captionLabel.text = caption;
        }

        void RevealNumber(int index, int delayMs, float target, int durationMs, Func<float, string> format)
        {
            Schedule(delayMs, () =>
            {
                var row = _rows[index];
                row.Root.style.display = DisplayStyle.Flex;
                FadeIn(row.Root);
                MatsuriUiTheme.Tween(this, 0f, target, durationMs, v => row.Value.text = format(v));
            });
        }

        void FadeIn(VisualElement e)
        {
            e.style.opacity = 0f;
            MatsuriUiTheme.Tween(this, 0f, 1f, 260, v => e.style.opacity = v);
        }

        void Schedule(int delayMs, Action action)
        {
            var item = schedule.Execute(action);
            item.ExecuteLater(delayMs);
            _scheduled.Add(item);
        }

        void CancelScheduled()
        {
            for (int i = 0; i < _scheduled.Count; i++) _scheduled[i]?.Pause();
            _scheduled.Clear();
        }

        /// <summary>ローカルランキング (§37) から順位を求める。失敗しても結果表示は続ける。</summary>
        static string BuildRankText(FestivalResult result)
        {
            try
            {
                var backend = new LocalJsonRanking();
                var top = backend.GetTop(100);
                if (top == null || top.Count == 0) return "初めての記録";

                int rank = 1;
                for (int i = 0; i < top.Count; i++)
                {
                    var r = top[i];
                    if (r != null && r.TotalScore > result.TotalScore) rank++;
                }
                return $"ランキング {rank} 位  /  {Mathf.Max(rank, top.Count)} 件中";
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
