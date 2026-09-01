using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// チュートリアル (§45)。長い説明は出さない。今やることを一行だけ出す。
    /// 段階: ①RUNしてみよう ②屋台を増やそう ③値段を変えよう ④祭りを開催しよう ⑤結果を見よう。
    /// 条件を満たしたら自動で次へ進む。いつでもスキップできる。
    /// </summary>
    public sealed class TutorialOverlay : VisualElement
    {
        sealed class Step
        {
            public string Title;
            public string Hint;
        }

        static readonly List<Step> Steps = new List<Step>
        {
            new Step { Title = "たこ焼き屋を作ってみよう", Hint = "RUN を押すと、書いたとおりに屋台が建つ。" },
            new Step { Title = "屋台を増やそう",           Hint = "「屋台 かき氷」をもう一行足して、また RUN。" },
            new Step { Title = "値段を変えよう",           Hint = "屋台の中に「値段 400」と書いてみる。" },
            new Step { Title = "祭りを開催しよう",         Hint = "「祭りを開催」を押すと 17:00 から人が来る。" },
            new Step { Title = "結果を見よう",             Hint = "22:00 まで待つと、売上と満足度が出る。" }
        };

        readonly VisualElement _card;
        readonly VisualElement _dots = new VisualElement();
        readonly Label _stepLabel;
        readonly Label _titleLabel;
        readonly Label _hintLabel;
        readonly List<VisualElement> _dotElements = new List<VisualElement>();

        int _index;
        bool _finished;
        bool _custom;

        /// <summary>スキップされた。</summary>
        public event Action Skipped;

        public bool IsActive => !_finished && style.display == DisplayStyle.Flex;

        public TutorialOverlay()
        {
            AddToClassList("matsuri-tutorial");
            style.position = Position.Absolute;
            style.left = 0f;
            style.bottom = 0f;
            pickingMode = PickingMode.Ignore;

            _card = new VisualElement();
            _card.AddToClassList("matsuri-tutorial__card");
            _card.style.width = 380f;
            _card.style.backgroundColor = MatsuriUiTheme.Hex("#141826F7");
            MatsuriUiTheme.SetRadius(_card, 12f);
            MatsuriUiTheme.SetBorder(_card, 1f, MatsuriUiTheme.Hex("#4A3320"));
            MatsuriUiTheme.SetPadding(_card, 14f, 16f, 14f, 18f);
            _card.style.borderLeftWidth = 3f;
            _card.style.borderLeftColor = MatsuriUiTheme.AccentYellow;
            Add(_card);

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 7f;
            _card.Add(head);

            _stepLabel = new Label();
            _stepLabel.style.fontSize = 10f;
            _stepLabel.style.letterSpacing = 2f;
            _stepLabel.style.color = MatsuriUiTheme.AccentYellow;
            MatsuriUiTheme.ApplyUiFont(_stepLabel);
            head.Add(_stepLabel);

            _dots.style.flexDirection = FlexDirection.Row;
            _dots.style.alignItems = Align.Center;
            _dots.style.marginLeft = 10f;
            head.Add(_dots);
            for (int i = 0; i < Steps.Count; i++)
            {
                var dot = new VisualElement();
                dot.style.width = 6f;
                dot.style.height = 6f;
                dot.style.marginRight = 5f;
                MatsuriUiTheme.SetRadius(dot, 3f);
                dot.style.backgroundColor = MatsuriUiTheme.Hex("#39435E");
                _dots.Add(dot);
                _dotElements.Add(dot);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            head.Add(spacer);

            var skip = new Label("スキップ");
            skip.style.fontSize = 10f;
            skip.style.color = MatsuriUiTheme.TextMuted;
            skip.pickingMode = PickingMode.Position;
            MatsuriUiTheme.ApplyUiFont(skip);
            skip.RegisterCallback<PointerDownEvent>(_ =>
            {
                Finish();
                Skipped?.Invoke();
            });
            skip.RegisterCallback<PointerEnterEvent>(_ => skip.style.color = MatsuriUiTheme.TextPrimary);
            skip.RegisterCallback<PointerLeaveEvent>(_ => skip.style.color = MatsuriUiTheme.TextMuted);
            head.Add(skip);

            _titleLabel = new Label();
            _titleLabel.style.fontSize = 16f;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = MatsuriUiTheme.TextPrimary;
            _titleLabel.style.whiteSpace = WhiteSpace.Normal;
            MatsuriUiTheme.ApplyUiFont(_titleLabel);
            _card.Add(_titleLabel);

            _hintLabel = new Label();
            _hintLabel.style.fontSize = 12f;
            _hintLabel.style.color = MatsuriUiTheme.TextSecondary;
            _hintLabel.style.marginTop = 4f;
            _hintLabel.style.whiteSpace = WhiteSpace.Normal;
            MatsuriUiTheme.ApplyUiFont(_hintLabel);
            _card.Add(_hintLabel);

            // カード自体はクリックを受ける（スキップ用）
            _card.pickingMode = PickingMode.Position;

            style.display = DisplayStyle.None;
        }

        /// <summary>最初の段階から始める。</summary>
        public void Begin()
        {
            _finished = false;
            _custom = false;
            _index = 0;
            style.display = DisplayStyle.Flex;
            Refresh();
        }

        /// <summary>任意の一文を出す（UIManager.ShowTutorial 用）。</summary>
        public void ShowCustom(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _custom = true;
            style.display = DisplayStyle.Flex;
            _stepLabel.text = "HINT";
            _titleLabel.text = message;
            _hintLabel.text = string.Empty;
            for (int i = 0; i < _dotElements.Count; i++)
                _dotElements[i].style.backgroundColor = MatsuriUiTheme.Hex("#39435E");
            Flash();
        }

        public void Finish()
        {
            _finished = true;
            style.display = DisplayStyle.None;
        }

        // ── 進行条件 ────────────────────────────────────────────

        /// <summary>RUN が押された。</summary>
        public void NotifyRun(string source)
        {
            if (!IsActive) return;
            if (_index == 0) Advance();
            else NotifyCodeChanged(source);
        }

        /// <summary>コードが変わった。屋台の数や「値段」の有無で段階を進める。</summary>
        public void NotifyCodeChanged(string source)
        {
            if (!IsActive || _custom) return;
            source ??= string.Empty;

            if (_index == 1 && CountOccurrences(source, "屋台") >= 2) Advance();
            else if (_index == 2 && (source.Contains("値段") || source.Contains("価格"))) Advance();
        }

        /// <summary>祭りが開催された。</summary>
        public void NotifyFestivalStarted()
        {
            if (!IsActive) return;
            if (_index < 3) _index = 3;
            if (_index == 3) Advance();
        }

        /// <summary>結果画面が出た。</summary>
        public void NotifyResultShown()
        {
            if (!IsActive) return;
            Finish();
        }

        // ── 内部 ───────────────────────────────────────────────

        void Advance()
        {
            _index++;
            if (_index >= Steps.Count)
            {
                Finish();
                return;
            }
            Refresh();
            Flash();
        }

        void Refresh()
        {
            _custom = false;
            if (_index < 0 || _index >= Steps.Count) return;
            var step = Steps[_index];
            _stepLabel.text = $"STEP {_index + 1} / {Steps.Count}";
            _titleLabel.text = step.Title;
            _hintLabel.text = step.Hint;

            for (int i = 0; i < _dotElements.Count; i++)
            {
                _dotElements[i].style.backgroundColor = i <= _index
                    ? MatsuriUiTheme.AccentYellow
                    : MatsuriUiTheme.Hex("#39435E");
            }
        }

        void Flash()
        {
            MatsuriUiTheme.Tween(this, 0f, 1f, 420, t =>
            {
                _card.style.borderLeftColor = Color.Lerp(
                    MatsuriUiTheme.AccentRed, MatsuriUiTheme.AccentYellow, t);
                _card.style.opacity = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(t * 2.2f));
            });
        }

        static int CountOccurrences(string source, string needle)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(needle)) return 0;
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}
