using System.Collections.Generic;
using Matsuri.TimeSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// 画面上部のバー (§9)。予算 / 売上 / 来場者 / 時刻 と、祭りの進行バー (17:00→22:00)。
    /// 金額は ¥1,482,500 形式。数値は変化したときだけ書き換える。
    /// 売上が増えた瞬間は数字がポップし、増加分が浮かんで消える。
    /// </summary>
    public sealed class HudPanel : VisualElement
    {
        readonly Label _budgetValue;
        readonly Label _revenueValue;
        readonly Label _visitorValue;
        readonly Label _clockValue;
        readonly VisualElement _progressFill;
        readonly Label _progressLabel;
        readonly Label _phaseLabel;
        readonly VisualElement _revenueBlock;

        readonly List<Label> _floaters = new List<Label>();

        long _budget = long.MinValue;
        long _revenue = long.MinValue;
        int _visitors = int.MinValue;
        int _clockMinutes = int.MinValue;

        public HudPanel()
        {
            AddToClassList("matsuri-hud");
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.height = MatsuriUiTheme.TopBarHeight;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.backgroundColor = MatsuriUiTheme.Hex("#080B14E6");
            style.borderBottomWidth = 1f;
            style.borderBottomColor = MatsuriUiTheme.Border;
            MatsuriUiTheme.SetPadding(this, 0f, 22f, 0f, 22f);

            // ── ブランド ──
            var brand = new VisualElement();
            brand.style.flexDirection = FlexDirection.Row;
            brand.style.alignItems = Align.Center;
            brand.style.marginRight = 28f;
            Add(brand);

            var lantern = new VisualElement();
            lantern.style.width = 10f;
            lantern.style.height = 14f;
            lantern.style.backgroundColor = MatsuriUiTheme.AccentRed;
            MatsuriUiTheme.SetRadius(lantern, 5f);
            lantern.style.marginRight = 10f;
            brand.Add(lantern);

            var brandLabel = new Label("MATSURI.exe");
            brandLabel.style.fontSize = 16f;
            brandLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            brandLabel.style.color = MatsuriUiTheme.TextPrimary;
            brandLabel.style.letterSpacing = 1f;
            MatsuriUiTheme.ApplyUiFont(brandLabel);
            brand.Add(brandLabel);

            _phaseLabel = new Label("準備中");
            _phaseLabel.style.fontSize = 10f;
            _phaseLabel.style.color = MatsuriUiTheme.AccentYellow;
            _phaseLabel.style.marginLeft = 12f;
            MatsuriUiTheme.SetPadding(_phaseLabel, 2f, 8f, 2f, 8f);
            MatsuriUiTheme.SetRadius(_phaseLabel, 3f);
            MatsuriUiTheme.SetBorder(_phaseLabel, 1f, MatsuriUiTheme.Hex("#4A3E1E"));
            _phaseLabel.style.backgroundColor = MatsuriUiTheme.Hex("#211B0E");
            MatsuriUiTheme.ApplyUiFont(_phaseLabel);
            brand.Add(_phaseLabel);

            // ── 指標 ──
            _budgetValue = AddStat("予算", MatsuriUiTheme.TextPrimary, out _);
            _revenueValue = AddStat("売上", MatsuriUiTheme.AccentYellow, out _revenueBlock);
            _visitorValue = AddStat("来場者", MatsuriUiTheme.AccentBlue, out _);
            _clockValue = AddStat("時刻", MatsuriUiTheme.AccentWarm, out _);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            Add(spacer);

            // ── 進行バー ──
            var progressBlock = new VisualElement();
            progressBlock.style.width = 240f;
            progressBlock.style.flexShrink = 0f;
            Add(progressBlock);

            _progressLabel = new Label("17:00 — 22:00");
            _progressLabel.style.fontSize = MatsuriUiTheme.CaptionFontSize;
            _progressLabel.style.color = MatsuriUiTheme.TextSecondary;
            _progressLabel.style.marginBottom = 6f;
            MatsuriUiTheme.ApplyUiFont(_progressLabel);
            progressBlock.Add(_progressLabel);

            var track = new VisualElement();
            track.style.height = 5f;
            track.style.backgroundColor = MatsuriUiTheme.Hex("#1B2133");
            MatsuriUiTheme.SetRadius(track, 3f);
            track.style.overflow = Overflow.Hidden;
            progressBlock.Add(track);

            _progressFill = new VisualElement();
            _progressFill.style.height = 5f;
            _progressFill.style.width = Length.Percent(0f);
            _progressFill.style.backgroundColor = MatsuriUiTheme.AccentRed;
            MatsuriUiTheme.SetRadius(_progressFill, 3f);
            track.Add(_progressFill);

            ResetValues();
        }

        Label AddStat(string caption, Color valueColor, out VisualElement block)
        {
            block = new VisualElement();
            block.AddToClassList("matsuri-hud__stat");
            block.style.flexDirection = FlexDirection.Column;
            block.style.justifyContent = Justify.Center;
            block.style.minWidth = 132f;
            block.style.marginRight = 26f;
            block.style.borderLeftWidth = 2f;
            block.style.borderLeftColor = MatsuriUiTheme.Hex("#232A3D");
            block.style.paddingLeft = 12f;

            var captionLabel = new Label(caption);
            captionLabel.style.fontSize = MatsuriUiTheme.CaptionFontSize;
            captionLabel.style.color = MatsuriUiTheme.TextSecondary;
            captionLabel.style.letterSpacing = 2f;
            captionLabel.style.marginBottom = 2f;
            MatsuriUiTheme.ApplyUiFont(captionLabel);
            block.Add(captionLabel);

            var valueLabel = new Label("—");
            valueLabel.style.fontSize = MatsuriUiTheme.ValueFontSize;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = valueColor;
            MatsuriUiTheme.ApplyUiFont(valueLabel);
            block.Add(valueLabel);

            Add(block);
            return valueLabel;
        }

        /// <summary>「準備中」「建設中」「開催中」「終了」などの短いラベル。</summary>
        public void SetPhaseLabel(string text)
        {
            if (_phaseLabel.text == text) return;
            _phaseLabel.text = text;
        }

        /// <summary>数値を反映する。変化していない項目は書き換えない。</summary>
        public void UpdateValues(long budget, long revenue, int visitors, FestivalClock clock)
        {
            if (budget != _budget)
            {
                bool decreased = budget < _budget && _budget != long.MinValue;
                _budget = budget;
                _budgetValue.text = MatsuriUiTheme.FormatYen(budget);
                _budgetValue.style.color = budget <= 0 ? MatsuriUiTheme.SeverityError : MatsuriUiTheme.TextPrimary;
                if (decreased) Pop(_budgetValue);
            }

            if (revenue != _revenue)
            {
                long delta = (_revenue == long.MinValue) ? 0 : revenue - _revenue;
                _revenue = revenue;
                _revenueValue.text = MatsuriUiTheme.FormatYen(revenue);
                if (delta > 0)
                {
                    Pop(_revenueValue);
                    SpawnFloater("+" + MatsuriUiTheme.FormatYen(delta));
                }
            }

            if (visitors != _visitors)
            {
                _visitors = visitors;
                _visitorValue.text = MatsuriUiTheme.FormatCount(visitors) + " 人";
            }

            int minutes = Mathf.RoundToInt(clock.MinutesOfDay);
            if (minutes != _clockMinutes)
            {
                _clockMinutes = minutes;
                _clockValue.text = clock.ToString();
                float t = Mathf.Clamp01(clock.Normalized);
                _progressFill.style.width = Length.Percent(t * 100f);
                _progressFill.style.backgroundColor = Color.Lerp(
                    MatsuriUiTheme.AccentRed, MatsuriUiTheme.AccentYellow, t);
                _progressLabel.text = t >= 1f
                    ? "17:00 — 22:00  (終了)"
                    : $"17:00 — 22:00  ({Mathf.RoundToInt(t * 100f)}%)";
            }
        }

        public void ResetValues()
        {
            _budget = long.MinValue;
            _revenue = long.MinValue;
            _visitors = int.MinValue;
            _clockMinutes = int.MinValue;
            _budgetValue.text = "—";
            _revenueValue.text = MatsuriUiTheme.FormatYen(0);
            _visitorValue.text = "0 人";
            _clockValue.text = FestivalClock.AtStart.ToString();
            _progressFill.style.width = Length.Percent(0f);
            _progressLabel.text = "17:00 — 22:00";
            for (int i = 0; i < _floaters.Count; i++) _floaters[i].RemoveFromHierarchy();
            _floaters.Clear();
        }

        void Pop(Label label)
        {
            float baseSize = MatsuriUiTheme.ValueFontSize;
            MatsuriUiTheme.Tween(this, baseSize * 1.22f, baseSize, 320,
                v => label.style.fontSize = v);
        }

        /// <summary>増加分が売上の上に浮かんで消える演出。</summary>
        void SpawnFloater(string text)
        {
            if (_floaters.Count > 6) return;

            var floater = new Label(text);
            floater.pickingMode = PickingMode.Ignore;
            floater.style.position = Position.Absolute;
            floater.style.fontSize = 15f;
            floater.style.unityFontStyleAndWeight = FontStyle.Bold;
            floater.style.color = MatsuriUiTheme.AccentGreen;
            MatsuriUiTheme.ApplyUiFont(floater);
            Add(floater);
            _floaters.Add(floater);

            float startX = 24f;
            float startY = 6f;
            var block = _revenueBlock;
            if (block != null && block.resolvedStyle.width > 0f)
            {
                Vector2 local = this.WorldToLocal(block.worldBound.position);
                startX = local.x + 14f;
                startY = local.y + 2f;
            }
            floater.style.left = startX;
            floater.style.top = startY;

            MatsuriUiTheme.Tween(this, 0f, 1f, 900, t =>
            {
                floater.style.top = startY - t * 26f;
                floater.style.opacity = t < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, t);
                if (t >= 1f)
                {
                    floater.RemoveFromHierarchy();
                    _floaters.Remove(floater);
                }
            });
        }
    }
}
