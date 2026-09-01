using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using UiTextElement = UnityEngine.UIElements.TextElement;
using MeasureMode = UnityEngine.UIElements.VisualElement.MeasureMode;

namespace Matsuri.UI
{
    /// <summary>
    /// コードエディターの重ね合わせ 3 層（行番号ガター / ハイライト / 入力）を
    /// 1px もずらさずに揃えるための計測とスタイル適用 (§10 / §66)。
    ///
    /// ズレの原因は毎回この 4 つに集約される:
    ///   1. フォント・フォントサイズが層ごとに違う
    ///   2. 行の高さ（送り）を推測値で持っている
    ///   3. パディング・マージンが層ごとに違う（特に TextField の内部要素 unity-text-input）
    ///   4. 折り返しが起きて 1 行が 2 行分の高さになる
    /// このクラスは 1・3・4 を「全層に同じ値を強制する」ことで潰し、
    /// 2 を「推測をやめて実測する」ことで潰す。
    /// </summary>
    public static class CodeEditorLayout
    {
        /// <summary>行番号ガターの幅。コード行はこの右から始まる。</summary>
        public const float GutterWidth = MatsuriUiTheme.GutterWidth;

        /// <summary>ガター右端からコード文字の開始位置までの余白。</summary>
        public const float TextLeft = 14f;

        /// <summary>コード領域の上端から 1 行目までの余白。3 層すべてで同じ値を使う。</summary>
        public const float TextTop = 10f;

        /// <summary>最終行の右／下に置く余白（横スクロールの遊び）。</summary>
        public const float TextRight  = 48f;
        public const float TextBottom = 36f;

        /// <summary>ガター内の ● 印の幅。</summary>
        public const float MarkerWidth = 16f;

        /// <summary>行番号の右余白。</summary>
        public const float NumberRight = 10f;

        /// <summary>これ以下の行高は「計測できなかった」とみなす。</summary>
        public const float MinLineHeight = 4f;

        /// <summary>フォント情報すら取れないときの最終手段の行高比。</summary>
        public const float FallbackLineHeightRatio = 1.32f;

        // ── スタイル適用 ───────────────────────────────────────

        /// <summary>
        /// 「コードの 1 行を描く要素」として必要なスタイルを全部入れる。
        /// 3 層のどの要素にもこれを通すことで、フォント・サイズ・字間・
        /// 揃え・折り返しが必ず一致する。
        /// </summary>
        public static void ApplyTextLayer(VisualElement e, float fontSize)
        {
            if (e == null) return;

            // 余白・枠線・角丸をすべて 0 に落とす（既定テーマの装飾を消す）
            MatsuriUiTheme.StripBox(e);

            // 日本語フォント（Matsuri.Core.MatsuriFontProvider 供給）を明示指定する。
            // ここを層ごとに変えると字幅も行高も変わり、必ずズレる。
            MatsuriUiTheme.ApplyCodeFont(e);

            e.style.fontSize = fontSize;
            e.style.unityFontStyleAndWeight = FontStyle.Normal;
            e.style.unityTextAlign = TextAnchor.UpperLeft;
            e.style.letterSpacing = 0f;
            e.style.wordSpacing = 0f;

            // 折り返しが起きると 1 行が 2 行分の高さになり、行番号との対応が崩れる。
            // 横にはみ出す分は外側の ScrollView の横スクロールで逃がす。
            e.style.whiteSpace = WhiteSpace.NoWrap;
        }

        /// <summary>
        /// TextField 本体と、その内部要素（unity-text-input とその中の UiTextElement）に
        /// 同じフォント・サイズ・余白・折り返し設定を流し込む。
        /// 内部要素が既定値のままだと、上に重ねたハイライト層と必ずずれる。
        /// </summary>
        public static void ApplyToTextField(TextField field, float fontSize, List<UiTextElement> collected)
        {
            if (field == null) return;

            collected?.Clear();

            // ラベル（BaseField の左のラベル）は使わない。残っていると文字の開始位置がずれる。
            if (field.labelElement != null)
            {
                field.labelElement.style.display = DisplayStyle.None;
                field.labelElement.style.width = 0f;
                field.labelElement.style.minWidth = 0f;
            }

            var all = field.Query<VisualElement>().ToList();
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                ApplyTextLayer(e, fontSize);
                e.style.backgroundColor = Color.clear;
                if (e is UiTextElement te) collected?.Add(te);
            }

            ApplyTextLayer(field, fontSize);
            field.style.backgroundColor = Color.clear;

            var inner = field.Q(TextField.textInputUssName);
            if (inner != null)
            {
                ApplyTextLayer(inner, fontSize);
                inner.style.backgroundColor = Color.clear;
                inner.style.flexGrow = 1f;
                inner.style.flexShrink = 1f;
                inner.style.alignItems = Align.FlexStart;
                inner.style.justifyContent = Justify.FlexStart;
            }
        }

        /// <summary>
        /// 複数行 TextField が内部に持つ縦スクロールを無効化する。
        /// 内部スクロールが動くと入力層だけが独立にずれ、重ね合わせが破綻する。
        /// このプロパティは Unity のバージョンによって有無が変わるため、
        /// コンパイル時依存を持たずリフレクションで触る。
        /// </summary>
        public static void TryDisableInnerScroller(TextField field)
        {
            if (field == null) return;
            try
            {
                const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var p = field.GetType().GetProperty("verticalScrollerVisibility", Flags);
                if (p != null && p.CanWrite && p.PropertyType.IsEnum)
                {
                    p.SetValue(field, Enum.Parse(p.PropertyType, "Hidden"));
                }
            }
            catch (Exception)
            {
                // この版の TextField に該当プロパティが無いだけ。ResetInnerScroll 側で担保する。
            }
        }

        /// <summary>
        /// 内部スクロールが万一動いていたら 0 に戻す。
        /// 入力層は常に全行が収まる高さにしてあるので、本来ここは動かない。
        /// </summary>
        public static void ResetInnerScroll(TextField field)
        {
            if (field == null) return;
            var sv = field.Q<ScrollView>();
            if (sv == null) return;
            var off = sv.scrollOffset;
            if (Mathf.Abs(off.x) > 0.5f || Mathf.Abs(off.y) > 0.5f) sv.scrollOffset = Vector2.zero;
        }

        // ── 計測 ───────────────────────────────────────────────

        /// <summary>
        /// 1 行ぶんの高さ（行送り）を実測する。
        ///
        /// 「1 行だけの Label の resolvedStyle.height」は、
        /// 余白・最小高さ・レイアウト未確定などの影響を受けて当てにならない。
        /// ここでは 3 行と 2 行のテキストの高さの差を取ることで、
        /// 純粋な 1 行ぶんの送りだけを取り出す。
        /// </summary>
        public static float MeasureLineHeight(UiTextElement probe, float fontSize)
        {
            if (probe != null)
            {
                try
                {
                    float two   = probe.MeasureTextSize("A\nA",   0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).y;
                    float three = probe.MeasureTextSize("A\nA\nA", 0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).y;
                    float diff = three - two;
                    if (diff > MinLineHeight) return diff;
                }
                catch (Exception)
                {
                    // パネル未接続などで計測できない場合はフォント情報から求める
                }
            }
            return FallbackLineHeight(fontSize);
        }

        /// <summary>フォントのフェイス情報から行高を求める。実測できないときの代替。</summary>
        public static float FallbackLineHeight(float fontSize)
        {
            var asset = MatsuriUiTheme.CodeFontAsset != null
                ? MatsuriUiTheme.CodeFontAsset
                : MatsuriUiTheme.JapaneseFont;
            if (asset != null)
            {
                var face = asset.faceInfo;
                float point = face.pointSize;
                float line = face.lineHeight;
                if (point > 0.01f && line > 0.01f) return fontSize * (line / point);
            }

            var legacy = MatsuriUiTheme.LegacyJapaneseFont;
            if (legacy != null && legacy.fontSize > 0 && legacy.lineHeight > 0)
                return fontSize * ((float)legacy.lineHeight / legacy.fontSize);

            return fontSize * FallbackLineHeightRatio;
        }

        /// <summary>文字列の描画幅を測る。キャレット位置の算出に使う。</summary>
        public static float MeasureWidth(UiTextElement probe, string s, float fontSize)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            if (probe != null)
            {
                try
                {
                    var size = probe.MeasureTextSize(s, 0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined);
                    if (size.x > 0f) return size.x;
                }
                catch (Exception)
                {
                    // 計測できない環境では概算に落とす
                }
            }
            return s.Length * fontSize * 0.58f;
        }
    }
}
