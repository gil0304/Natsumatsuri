using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// UI 全体の見た目の定数 (§64)。
    /// 「和 × モダンゲームUI」。背景は黒〜濃紺、アクセントは提灯の赤と祭りの黄。
    /// 教育ソフト風・ブロックUI風にはしない (§64 / §79)。
    /// USS (Assets/UI/Styles/matsuri.uss) が読めなかった場合でも成立するよう、
    /// 主要な値はここから C# で直接指定する。
    /// </summary>
    public static class MatsuriUiTheme
    {
        // ── 背景 ───────────────────────────────────────────────
        public static readonly Color BgDeep       = Hex("#0B0E1A");
        public static readonly Color BgPanel      = Hex("#10131FEB");
        public static readonly Color BgPanelSolid = Hex("#141826");
        public static readonly Color BgEditor     = Hex("#0E1119F2");
        public static readonly Color BgGutter     = Hex("#0A0D15F2");
        public static readonly Color BgRaised     = Hex("#1B2133");
        public static readonly Color Border       = Hex("#232A3D");
        public static readonly Color BorderBright = Hex("#39435E");

        // ── 文字 ───────────────────────────────────────────────
        public static readonly Color TextPrimary   = Hex("#E6EAF5");
        public static readonly Color TextSecondary = Hex("#8A93AC");
        public static readonly Color TextMuted     = Hex("#5A6280");

        // ── アクセント（提灯の赤 / 祭りの黄） ─────────────────
        public static readonly Color AccentRed    = Hex("#E23B2E");
        public static readonly Color AccentYellow = Hex("#F5C24A");
        public static readonly Color AccentWarm   = Hex("#FFD08A");
        public static readonly Color AccentBlue   = Hex("#5CA9FF");
        public static readonly Color AccentGreen  = Hex("#6FE0A0");

        // ── 診断 (§42) ─────────────────────────────────────────
        public static readonly Color SeverityError   = Hex("#FF5D52");
        public static readonly Color SeverityWarning = Hex("#F5C24A");
        public static readonly Color SeverityInfo    = Hex("#5CA9FF");

        public static readonly Color ErrorLineBg   = new Color(1f, 0.30f, 0.26f, 0.11f);
        public static readonly Color WarningLineBg = new Color(0.96f, 0.76f, 0.29f, 0.07f);
        public static readonly Color CurrentLineBg = new Color(1f, 1f, 1f, 0.055f);

        /// <summary>キャレット行かつエラー行。現在行の強調とエラーの赤を両立させる。</summary>
        public static readonly Color ErrorCurrentLineBg   = new Color(1f, 0.30f, 0.26f, 0.18f);
        public static readonly Color WarningCurrentLineBg = new Color(0.96f, 0.76f, 0.29f, 0.12f);

        // ── シンタックスハイライト (§10) ────────────────────────
        public const string SynKeyword  = "#FF8A4C";  // 祭り/屋台/装飾/設備/イベント/もし/時間 …
        public const string SynProperty = "#6FD3F2";  // 場所/値段/向き/名前
        public const string SynString   = "#B8E062";  // 文字列
        public const string SynNumber   = "#C39BF5";  // 数値
        public const string SynTime     = "#F5C24A";  // 時刻
        public const string SynComment  = "#5F6880";  // コメント
        public const string SynMetric   = "#5CA9FF";  // 来場者数/売上/…
        public const string SynDefault  = "#D7DCE8";  // それ以外
        public const string SynError    = "#FF5D52";  // エラー範囲

        // ── 寸法 ───────────────────────────────────────────────
        public const float CodeFontSize    = 15f;
        public const float UiFontSize      = 14f;
        public const float CaptionFontSize = 11f;
        public const float ValueFontSize   = 24f;
        public const float TitleFontSize   = 12f;

        public const float Radius      = 6f;
        public const float RadiusLarge = 12f;
        /// <summary>
        /// 行番号ガターの幅。行番号はコード本文と同じフォントサイズで描くので、
        /// 4 桁 + ● 印 + 右余白が収まる幅を確保する。
        /// </summary>
        public const float GutterWidth = 64f;
        public const float TopBarHeight    = 66f;
        public const float BottomBarHeight = 76f;
        public const float LeftPanelWidth  = 560f;
        public const int   IndentSize      = 4;

        /// <summary>
        /// 日本語フォント。UIManager.Initialize() で差し込む。
        /// 差し込まれなければ Resources の NotoSansJP、それも無ければ既定フォント。
        /// </summary>
        public static FontAsset JapaneseFont;

        /// <summary>コード表示用フォント。未指定なら JapaneseFont を使う。</summary>
        public static FontAsset CodeFontAsset;

        static Font _legacyFont;
        static bool _legacyFontSearched;

        /// <summary>Resources から日本語フォント (Assets/UI/Fonts/Resources/NotoSansJP.ttf) を探す。</summary>
        public static Font LegacyJapaneseFont
        {
            get
            {
                if (_legacyFontSearched) return _legacyFont;
                _legacyFontSearched = true;
                _legacyFont = Matsuri.Core.MatsuriFontProvider.JapaneseFont;
                if (_legacyFont == null) _legacyFont = Resources.Load<Font>("NotoSansJP");
                if (_legacyFont == null) _legacyFont = Resources.Load<Font>("Fonts/NotoSansJP");
                return _legacyFont;
            }
        }

        /// <summary>UI 用フォントを適用する。</summary>
        public static void ApplyUiFont(VisualElement e)
        {
            if (e == null) return;
            if (JapaneseFont != null)
            {
                e.style.unityFontDefinition = new StyleFontDefinition(JapaneseFont);
                return;
            }
            var f = LegacyJapaneseFont;
            if (f != null) e.style.unityFont = new StyleFont(f);
        }

        /// <summary>コード用フォントを適用する。等幅フォントが無ければ日本語フォントで代用する。</summary>
        public static void ApplyCodeFont(VisualElement e)
        {
            if (e == null) return;
            if (CodeFontAsset != null)
            {
                e.style.unityFontDefinition = new StyleFontDefinition(CodeFontAsset);
                return;
            }
            ApplyUiFont(e);
        }

        // ── ヘルパ ─────────────────────────────────────────────

        public static Color Hex(string html)
        {
            if (ColorUtility.TryParseHtmlString(html, out var c)) return c;
            return Color.magenta;
        }

        public static string ToHex(Color c)
            => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>¥1,482,500 形式。</summary>
        public static string FormatYen(long value)
        {
            bool minus = value < 0;
            long abs = minus ? -value : value;
            string body = abs.ToString("N0", CultureInfo.InvariantCulture);
            return minus ? "-¥" + body : "¥" + body;
        }

        /// <summary>1,234 形式。</summary>
        public static string FormatCount(long value)
            => value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>四辺すべてに同じ値を設定する。</summary>
        public static void SetPadding(VisualElement e, float v)
            => SetPadding(e, v, v, v, v);

        public static void SetPadding(VisualElement e, float top, float right, float bottom, float left)
        {
            e.style.paddingTop = top;
            e.style.paddingRight = right;
            e.style.paddingBottom = bottom;
            e.style.paddingLeft = left;
        }

        public static void SetMargin(VisualElement e, float top, float right, float bottom, float left)
        {
            e.style.marginTop = top;
            e.style.marginRight = right;
            e.style.marginBottom = bottom;
            e.style.marginLeft = left;
        }

        public static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        public static void SetBorder(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderTopColor = color;
            e.style.borderRightColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
        }

        /// <summary>四辺の余白・枠線をすべて 0 にする（TextField の既定装飾を消す用）。</summary>
        public static void StripBox(VisualElement e)
        {
            SetPadding(e, 0f);
            SetMargin(e, 0f, 0f, 0f, 0f);
            SetBorder(e, 0f, Color.clear);
            SetRadius(e, 0f);
        }

        /// <summary>見出し付きのパネル枠を作る。</summary>
        public static VisualElement CreatePanel(string title, out Label titleLabel)
        {
            var panel = new VisualElement();
            panel.AddToClassList("matsuri-panel");
            panel.style.backgroundColor = BgPanel;
            SetRadius(panel, RadiusLarge);
            SetBorder(panel, 1f, Border);
            panel.style.overflow = Overflow.Hidden;

            titleLabel = new Label(title);
            titleLabel.AddToClassList("matsuri-panel__title");
            titleLabel.style.fontSize = TitleFontSize;
            titleLabel.style.color = TextSecondary;
            titleLabel.style.letterSpacing = 3f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetPadding(titleLabel, 10f, 14f, 10f, 14f);
            titleLabel.style.borderBottomWidth = 1f;
            titleLabel.style.borderBottomColor = Border;
            titleLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            ApplyUiFont(titleLabel);
            panel.Add(titleLabel);
            return panel;
        }

        /// <summary>下部バーのボタン。primary なら提灯の赤。</summary>
        public static Button CreateButton(string text, Action onClick, bool primary = false)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.AddToClassList("matsuri-button");
            if (primary) b.AddToClassList("matsuri-button--primary");
            b.style.fontSize = UiFontSize;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.height = 42f;
            b.style.minWidth = 116f;
            SetPadding(b, 0f, 20f, 0f, 20f);
            SetMargin(b, 0f, 6f, 0f, 6f);
            SetRadius(b, Radius);
            SetBorder(b, 1f, primary ? Hex("#F2634F") : BorderBright);
            b.style.backgroundColor = primary ? AccentRed : BgRaised;
            b.style.color = primary ? Hex("#FFF3EC") : TextPrimary;
            ApplyUiFont(b);

            // インラインスタイルは USS の :hover より優先されるため、ホバーは C# 側で表現する。
            Color normal = primary ? AccentRed : BgRaised;
            Color hover = primary ? Hex("#F2503E") : Hex("#242C42");
            b.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (b.enabledSelf) b.style.backgroundColor = hover;
            });
            b.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (b.enabledSelf) b.style.backgroundColor = normal;
            });
            return b;
        }

        /// <summary>ボタンの活性を切り替える（見た目も含む）。</summary>
        public static void SetButtonEnabled(Button b, bool enabled, bool primary = false)
        {
            if (b == null) return;
            b.SetEnabled(enabled);
            b.style.opacity = enabled ? 1f : 0.38f;
            b.style.backgroundColor = enabled
                ? (primary ? AccentRed : BgRaised)
                : Hex("#161B29");
        }

        /// <summary>簡易トゥイーン。パネルに載っていない場合は即座に終値を適用する。</summary>
        public static void Tween(VisualElement target, float from, float to, int durationMs, Action<float> apply)
        {
            if (apply == null) return;
            if (target == null || target.panel == null || durationMs <= 0)
            {
                apply(to);
                return;
            }
            target.experimental.animation.Start(from, to, durationMs, (_, v) => apply(v));
        }

        // ── UI Toolkit のアセット解決 ──────────────────────────

        /// <summary>
        /// matsuri.uss を StyleSheet として取得する。
        /// 実体は Assets/UI/Styles/Resources/matsuri.uss に置いてあるので
        /// Resources.Load&lt;StyleSheet&gt;("matsuri") で必ず取れる。
        /// </summary>
        public static StyleSheet ResolveStyleSheet(StyleSheet preferred)
        {
            if (preferred != null) return preferred;
            var sheet = Resources.Load<StyleSheet>("matsuri");
            if (sheet == null) sheet = Resources.Load<StyleSheet>("Styles/matsuri");
            return sheet;
        }

        /// <summary>
        /// PanelSettings に割り当てる ThemeStyleSheet を探す。
        ///
        /// 見つからなくても警告は出さない。理由:
        ///   MATSURI.exe の UI は色・余白・フォントをすべて C# (このクラス) と
        ///   matsuri.uss で明示指定しており、既定テーマが無くても見た目は変わらない。
        ///   ここで毎回警告を出すと、直しようのない雑音がログを埋めるだけになる。
        /// 呼び出し側は null を受け取ったらそのまま既定スタイルで動かしてよい。
        /// </summary>
        public static ThemeStyleSheet ResolveRuntimeTheme(ThemeStyleSheet preferred)
        {
            if (preferred != null) return preferred;

            var theme = Resources.Load<ThemeStyleSheet>("unity-runtime-theme");
            if (theme == null) theme = Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme");
            if (theme == null) theme = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            if (theme == null)
            {
                var found = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>();
                if (found != null && found.Length > 0) theme = found[0];
            }
            return theme;
        }
    }
}
