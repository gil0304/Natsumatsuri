using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Matsuri.CameraSystem
{
    /// <summary>
    /// カメラ操作の入力読み取り (§38)。
    ///
    /// ここが一手に引き受けているのは次の3つ。
    ///   1. Input System が無効な環境でもコンパイルが通るようにする
    ///   2. コードエディター (UI Toolkit の TextField) に文字を打っている間、
    ///      キーボードのカメラ操作を止める。止めないと WASD がそのまま
    ///      コードに打ち込まれ、プレイヤーからは「カメラが動かない」ように見える
    ///   3. マウスカーソルが UI の上にあるかどうかを判定する
    ///      （左ドラッグでのパンが、UI のクリックを奪わないようにするため）
    ///
    /// マウス操作は文字入力中でも止めない。文章を打ちながら会場を見回せた方が良いため。
    /// </summary>
    public static class MatsuriCameraInput
    {
        /// <summary>入力デバイスが1つでもあるか。</summary>
        public static bool Available
        {
#if ENABLE_INPUT_SYSTEM
            get => Keyboard.current != null || Mouse.current != null;
#else
            get => false;
#endif
        }

        // ── 文字入力中かどうか ─────────────────────────────────

        /// <summary>
        /// 判定を外から強制する。null なら実際のフォーカス状態を見る。
        /// テストや、独自の入力欄を持つ画面から差し込むために公開している。
        /// </summary>
        public static bool? TextInputFocusOverride { get; set; }

        static int _focusFrame = -1;
        static bool _focusCached;

        /// <summary>
        /// いま UI Toolkit のテキスト入力欄がキーボードフォーカスを持っているか。
        /// true の間、キーボードによるカメラ操作は無効になる。
        /// </summary>
        public static bool TextInputHasFocus
        {
            get
            {
                if (TextInputFocusOverride.HasValue) return TextInputFocusOverride.Value;

                int frame = Time.frameCount;
                if (frame != _focusFrame)
                {
                    _focusFrame = frame;
                    _focusCached = DetectTextInputFocus();
                }
                return _focusCached;
            }
        }

        /// <summary>キーボードのカメラ操作が効く状態か。</summary>
        public static bool KeyboardEnabled => !TextInputHasFocus;

        static bool DetectTextInputFocus()
        {
            var documents = Documents;
            for (int i = 0; i < documents.Count; i++)
            {
                var root = documents[i] != null ? documents[i].rootVisualElement : null;
                if (root == null) continue;

                var panel = root.panel;
                var controller = panel != null ? panel.focusController : null;
                var focused = controller != null ? controller.focusedElement : null;
                if (focused != null && IsTextInput(focused)) return true;
            }
            return false;
        }

        /// <summary>
        /// フォーカスされている要素が「文字を打ち込む欄」かどうか。
        /// TextField の実際のフォーカス先は内側の入力要素なので、親をたどって調べる。
        /// Button も TextElement の派生なので、型名だけで判断せず
        /// テキスト入力欄の系統 (TextField / TextInputBaseField) かどうかを見る。
        /// </summary>
        static bool IsTextInput(Focusable focused)
        {
            var element = focused as VisualElement;
            while (element != null)
            {
                if (element.ClassListContains("unity-text-input") ||
                    element.ClassListContains("unity-base-text-field"))
                    return true;

                for (var type = element.GetType(); type != null; type = type.BaseType)
                {
                    string name = type.Name;
                    if (name.StartsWith("TextField") ||
                        name.StartsWith("TextInputBaseField") ||
                        name.StartsWith("TextValueField"))
                        return true;
                    if (type == typeof(VisualElement)) break;
                }
                element = element.parent;
            }
            return false;
        }

        // ── カーソルが UI の上にあるか ─────────────────────────

        /// <summary>マウスカーソルが UI パネルの要素の上にあるか。</summary>
        public static bool PointerOverUi => IsPointerOverUi(MousePosition);

        /// <summary>指定したスクリーン座標 (左下原点) が UI に拾われるか。</summary>
        public static bool IsPointerOverUi(Vector2 screenPosition)
        {
            var documents = Documents;
            for (int i = 0; i < documents.Count; i++)
            {
                var root = documents[i] != null ? documents[i].rootVisualElement : null;
                if (root == null) continue;

                var panel = root.panel;
                if (panel == null) continue;

                // UI Toolkit の座標は左上原点、スクリーン座標は左下原点。
                var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
                var local = RuntimePanelUtils.ScreenToPanel(panel, flipped);
                var picked = panel.Pick(local);

                // ルート自体しか当たらないなら、実質「何もない場所」。
                if (picked != null && picked != root) return true;
            }
            return false;
        }

        /// <summary>
        /// 文字入力欄のフォーカスを外す。3D の会場をクリックしたときに呼ぶ。
        /// これをやらないと、一度コードを書いた後ずっと WASD がエディターに
        /// 吸われたままになり、カメラが動かないように見える。
        /// </summary>
        public static void ReleaseTextInputFocus()
        {
            var documents = Documents;
            for (int i = 0; i < documents.Count; i++)
            {
                var root = documents[i] != null ? documents[i].rootVisualElement : null;
                var panel = root != null ? root.panel : null;
                var controller = panel != null ? panel.focusController : null;
                var focused = controller != null ? controller.focusedElement : null;
                if (focused != null && IsTextInput(focused)) focused.Blur();
            }
            _focusFrame = -1;   // 判定のキャッシュを捨てる
        }

        // ── UIDocument のキャッシュ ────────────────────────────

        static readonly List<UIDocument> _documents = new List<UIDocument>();
        static float _documentsRefreshedAt = -999f;
        const float DocumentCacheSeconds = 0.5f;

        static List<UIDocument> Documents
        {
            get
            {
                float now = Time.unscaledTime;
                bool stale = now - _documentsRefreshedAt > DocumentCacheSeconds ||
                             _documentsRefreshedAt > now;
                if (!stale)
                {
                    for (int i = 0; i < _documents.Count; i++)
                    {
                        if (_documents[i] == null) { stale = true; break; }
                    }
                }

                if (stale)
                {
                    _documents.Clear();
                    var found = Object.FindObjectsByType<UIDocument>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    for (int i = 0; i < found.Length; i++) _documents.Add(found[i]);
                    _documentsRefreshedAt = now;
                }
                return _documents;
            }
        }

        // ── キーボード ─────────────────────────────────────────

        /// <summary>WASD / 矢印キー。x=左右, y=前後。文字入力中は常に 0。</summary>
        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null || TextInputHasFocus) return Vector2.zero;
                float x = 0f, y = 0f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
                return new Vector2(x, y);
#else
                return Vector2.zero;
#endif
            }
        }

        /// <summary>Q / E。-1 = 左回転・下降、+1 = 右回転・上昇。文字入力中は 0。</summary>
        public static float Roll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null || TextInputHasFocus) return 0f;
                float v = 0f;
                if (k.qKey.isPressed) v -= 1f;
                if (k.eKey.isPressed) v += 1f;
                return v;
#else
                return 0f;
#endif
            }
        }

        /// <summary>R / F。+1 = 見下ろす角度を強める、-1 = 弱める。文字入力中は 0。</summary>
        public static float PitchKey
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null || TextInputHasFocus) return 0f;
                float v = 0f;
                if (k.rKey.isPressed) v += 1f;
                if (k.fKey.isPressed) v -= 1f;
                return v;
#else
                return 0f;
#endif
            }
        }

        /// <summary>
        /// Z / X（および +/- と PageUp/PageDown）で拡大縮小する。
        /// トラックパッドにはホイールが無いので、キーボードだけでも寄り引きできる必要がある。
        /// +1 = 寄る、-1 = 引く。文字入力中は 0。
        /// </summary>
        public static float ZoomKey
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null || TextInputHasFocus) return 0f;
                float v = 0f;
                if (k.zKey.isPressed || k.equalsKey.isPressed ||
                    k.numpadPlusKey.isPressed || k.pageUpKey.isPressed) v += 1f;
                if (k.xKey.isPressed || k.minusKey.isPressed ||
                    k.numpadMinusKey.isPressed || k.pageDownKey.isPressed) v -= 1f;
                return v;
#else
                return 0f;
#endif
            }
        }

        /// <summary>
        /// 会場全体を映し直す（Space / Home）。迷子になったときの戻り道。
        /// </summary>
        public static bool FrameVenuePressed
        {
#if ENABLE_INPUT_SYSTEM
            get
            {
                var k = Keyboard.current;
                if (k == null || TextInputHasFocus) return false;
                return k.spaceKey.wasPressedThisFrame || k.homeKey.wasPressedThisFrame;
            }
#else
            get => false;
#endif
        }

        /// <summary>
        /// Option / Alt。トラックパッドでは「1本指ドラッグ＝移動」「Option+ドラッグ＝回転」に使う。
        /// 中ボタンも右ドラッグも無い環境で、回転と移動を指1本で切り替えるための修飾キー。
        /// </summary>
        public static bool AltHeld
        {
#if ENABLE_INPUT_SYSTEM
            get => Keyboard.current != null &&
                   (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);
#else
            get => false;
#endif
        }

        /// <summary>Shift（加速）。文字入力中は false。</summary>
        public static bool Fast
        {
#if ENABLE_INPUT_SYSTEM
            get => Keyboard.current != null && !TextInputHasFocus &&
                   Keyboard.current.leftShiftKey.isPressed;
#else
            get => false;
#endif
        }

        /// <summary>Ctrl（減速）。文字入力中は false。</summary>
        public static bool Slow
        {
#if ENABLE_INPUT_SYSTEM
            get => Keyboard.current != null && !TextInputHasFocus &&
                   Keyboard.current.leftCtrlKey.isPressed;
#else
            get => false;
#endif
        }

        // ── カメラ切替キー (C) ─────────────────────────────────

        static int _cycleFrame = -1;
        static bool _cyclePrev;
        static bool _cycleEdge;

        /// <summary>
        /// この 1 フレームで C キーが押されたか。
        /// Input System の更新回数ではなくフレーム単位で立ち上がりを取るので、
        /// テストのように手動で入力を流し込む場合でも二重発火しない。
        /// </summary>
        public static bool CycleModePressed
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != _cycleFrame)
                {
                    _cycleFrame = frame;
                    bool now = false;
#if ENABLE_INPUT_SYSTEM
                    var k = Keyboard.current;
                    now = k != null && k.cKey.isPressed;
#endif
                    _cycleEdge = now && !_cyclePrev && !TextInputHasFocus;
                    _cyclePrev = now;
                }
                return _cycleEdge;
            }
        }

        // ── マウス（文字入力中でも有効） ───────────────────────

        public static Vector2 MouseDelta
        {
#if ENABLE_INPUT_SYSTEM
            get => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
            get => Vector2.zero;
#endif
        }

        public static Vector3 MousePosition
        {
#if ENABLE_INPUT_SYSTEM
            get => Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
            get => Vector3.zero;
#endif
        }

        /// <summary>
        /// スクロールの生の値。トラックパッドの2本指スクロールは縦横の両方が来る。
        /// マウスホイールは1ノッチ 120 前後の飛び飛びの値、
        /// トラックパッドは 1〜数十の連続した値で来るため、
        /// 使う側は <see cref="NormalizeScroll"/> を通すこと。
        /// </summary>
        public static Vector2 ScrollDelta
        {
#if ENABLE_INPUT_SYSTEM
            get => Mouse.current != null ? Mouse.current.scroll.ReadValue() : Vector2.zero;
#else
            get => Vector2.zero;
#endif
        }

        public static float Scroll => ScrollDelta.y;

        /// <summary>
        /// ホイールとトラックパッドの目盛りの違いを吸収する。
        ///
        /// マウスホイールは 1ノッチ 120 前後の大きな値で飛んでくるので、1回を ±1 として扱う。
        /// トラックパッドの2本指スクロールは小さな値が毎フレーム連続で来るので、
        /// そのまま割って滑らかな連続量にする。
        /// どちらでも「1操作あたりの効き」が揃うようにするのが目的。
        /// </summary>
        public static float NormalizeScroll(float raw)
        {
            if (Mathf.Abs(raw) < 0.0001f) return 0f;

            // ホイールらしい大きな飛び値
            if (Mathf.Abs(raw) >= WheelNotchThreshold)
                return Mathf.Clamp(raw / WheelNotch, -1.5f, 1.5f);

            // トラックパッドの連続値
            return Mathf.Clamp(raw / TrackpadScrollUnit, -1.5f, 1.5f);
        }

        /// <summary>マウスホイール1ノッチぶんの値。</summary>
        public const float WheelNotch = 120f;

        /// <summary>これ以上ならホイール、未満ならトラックパッドと判定する。</summary>
        public const float WheelNotchThreshold = 40f;

        /// <summary>トラックパッドのスクロール量を ±1 に正規化する割り数。</summary>
        public const float TrackpadScrollUnit = 9f;

        public static bool LeftHeld
        {
#if ENABLE_INPUT_SYSTEM
            get => Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            get => false;
#endif
        }

        public static bool MiddleHeld
        {
#if ENABLE_INPUT_SYSTEM
            get => Mouse.current != null && Mouse.current.middleButton.isPressed;
#else
            get => false;
#endif
        }

        public static bool RightHeld
        {
#if ENABLE_INPUT_SYSTEM
            get => Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
            get => false;
#endif
        }

        static int _clickFrame = -1;
        static bool _clickPrev;
        static bool _clickEdge;

        /// <summary>この 1 フレームでマウスのいずれかのボタンが押し下げられたか。</summary>
        public static bool AnyMouseButtonPressedThisFrame
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != _clickFrame)
                {
                    _clickFrame = frame;
                    bool now = LeftHeld || MiddleHeld || RightHeld;
                    _clickEdge = now && !_clickPrev;
                    _clickPrev = now;
                }
                return _clickEdge;
            }
        }

        /// <summary>いまカメラを触っているか。操作ヒントの再表示に使う。</summary>
        public static bool AnyCameraActivity
        {
            get
            {
                if (!Available) return false;
                if (LeftHeld || MiddleHeld || RightHeld) return true;
                if (Mathf.Abs(Scroll) > 0.01f) return true;
                if (Move.sqrMagnitude > 0.0001f) return true;
                if (Mathf.Abs(Roll) > 0.001f) return true;
                if (Mathf.Abs(PitchKey) > 0.001f) return true;
                return false;
            }
        }

        /// <summary>
        /// フレームをまたぐ一時状態を捨てる。
        /// シーンを作り直すテストなどから呼ぶ。
        /// </summary>
        public static void ResetTransientState()
        {
            TextInputFocusOverride = null;
            _focusFrame = -1;
            _focusCached = false;
            _cycleFrame = -1;
            _cyclePrev = false;
            _cycleEdge = false;
            _clickFrame = -1;
            _clickPrev = false;
            _clickEdge = false;
            _documents.Clear();
            _documentsRefreshedAt = -999f;
        }
    }
}
