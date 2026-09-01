using System.Collections;
using Matsuri.CameraSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Matsuri.Tests
{
    /// <summary>
    /// カメラ操作 (§38) が「本当に入力で動くか」を、仮想の
    /// キーボード／マウスから実際に入力を流し込んで確かめる。
    ///
    /// 「拡大縮小しかできない」という不具合の再発防止が目的なので、
    /// 移動・回転・俯角・モード切替を一通り座標で検証する。
    ///
    /// 待機は実時間ではなくフレーム数で行う。カメラは unscaledDeltaTime で
    /// 動くため、バッチモードの速いフレームでも回数さえ回れば必ず動く。
    /// </summary>
    public sealed class CameraControlTests : InputTestFixture
    {
        const int SettleFrames = 20;

        // カメラは Time.unscaledDeltaTime で動き、目標値へ指数的に近づく。
        // バッチモードは1フレームが 1ms 未満になることがあり、
        // 「Nフレーム待つ」では実時間がほとんど進まず、カメラが動く前に検証してしまう。
        // そのため待ちはすべて**実時間**で行う。

        /// <summary>キーを押し続ける時間（秒）。平滑化が追いつくだけの長さを取る。</summary>
        const float DriveSeconds = 1.2f;

        /// <summary>マウスドラッグを続ける時間（秒）。</summary>
        const float DragSeconds = 0.8f;

        /// <summary>ホイールを刻む回数。スクロールは離散的なので回数で数える。</summary>
        const int ScrollTicks = 12;

        CameraManager _cameras;
        GameObject _rig;
        GameObject _mainCamera;
        Keyboard _keyboard;
        Mouse _mouse;

        // InputTestFixture が [SetUp]/[TearDown] を持つため、
        // シーンの構築は各テストの本体から呼ぶ（順序の取り合いを避ける）。
        public override void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            if (_rig != null) UnityEngine.Object.DestroyImmediate(_rig);
            if (_mainCamera != null) UnityEngine.Object.DestroyImmediate(_mainCamera);
            _rig = null;
            _mainCamera = null;
            _cameras = null;
            _keyboard = null;
            _mouse = null;

            MatsuriCameraInput.ResetTransientState();
            base.TearDown();
        }

        // ── シーン構築 ─────────────────────────────────────────

        IEnumerator SetUpScene(bool withHint = false)
        {
            _mainCamera = new GameObject("Test Main Camera");
            _mainCamera.tag = "MainCamera";
            _mainCamera.AddComponent<Camera>();

            // Awake の前に設定を差し込みたいので、非アクティブで足してから起こす。
            _rig = new GameObject("Test CameraManager");
            _rig.SetActive(false);
            _cameras = _rig.AddComponent<CameraManager>();
            _cameras.ShowHint = withHint;
            _rig.SetActive(true);

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();

            yield return null;
            yield return null;

            Assert.IsNotNull(_cameras.Build, "建設カメラのコントローラーが作られていない。");
            Assert.AreEqual(CameraMode.Build, _cameras.Mode, "初期モードが建設ビューでない。");

            _cameras.Build.FocusOn(Vector3.zero, 34f);
            yield return Frames(SettleFrames);
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        /// <summary>実時間で待つ。フレームの長さに左右されない。</summary>
        static IEnumerator Seconds(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// マウスの移動量は「その更新で動いた分」なので、毎フレーム与え直す。
        /// </summary>
        /// <summary>
        /// ドラッグの<b>合計</b>移動量を指定して、その量を duration 秒かけて配る。
        /// カメラ側は「1フレームで動いた量」を積算するので、
        /// フレームの速さに関係なく合計が同じになるこの形でないと、
        /// 環境によって回り方が何十倍も変わってしまう。
        /// </summary>
        IEnumerator DriveMouse(Vector2 totalDelta, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                float dt = UnityEngine.Time.unscaledDeltaTime;
                float share = Mathf.Clamp01(dt / Mathf.Max(0.0001f, seconds));
                Set(_mouse.delta, totalDelta * share);
                t += dt;
                yield return null;
            }
            Set(_mouse.delta, Vector2.zero);
            // 平滑化が目標値へ追いつくのを待つ。
            yield return Seconds(0.6f);
        }

        /// <summary>
        /// スクロールを縦横まとめて流す。
        /// トラックパッドの2本指スクロールは小さな値が連続で来るので、
        /// 1ティックずつ与えては 0 に戻す。
        /// </summary>
        IEnumerator DriveScrollVector(Vector2 amountPerTick, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                Set(_mouse.scroll, amountPerTick);
                yield return null;
                Set(_mouse.scroll, Vector2.zero);
                yield return null;
            }
            yield return Seconds(0.8f);
        }

        IEnumerator DriveScroll(float amountPerTick, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                Set(_mouse.scroll, new Vector2(0f, amountPerTick));
                yield return null;
                Set(_mouse.scroll, Vector2.zero);
                yield return null;
            }
            // 平滑化が目標値に追いつくまで待つ。
            yield return Seconds(0.8f);
        }

        Vector2 ScreenCenter => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // ── キーボード ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator ForwardKeyMovesPivot()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            float before = build.Pivot.z;

            Press(_keyboard.wKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.wKey);

            Assert.Greater(build.Pivot.z, before + 0.05f,
                "W キーを押しても建設カメラの注視点が前に進まなかった。");
        }

        [UnityTest]
        public IEnumerator StrafeKeyMovesPivotSideways()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            Vector3 before = build.Pivot;

            Press(_keyboard.dKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.dKey);

            Assert.Greater((build.Pivot - before).magnitude, 0.05f,
                "D キーを押しても建設カメラの注視点が横に動かなかった。");
        }

        [UnityTest]
        public IEnumerator RotateKeyChangesYaw()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            float before = build.Yaw;

            Press(_keyboard.eKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.eKey);

            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(before, build.Yaw)), 0.5f,
                "E キーを押しても建設カメラが回らなかった。");
        }

        [UnityTest]
        public IEnumerator PitchKeyChangesPitch()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            float before = build.CurrentPitch;

            Press(_keyboard.rKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.rKey);

            Assert.Greater(Mathf.Abs(build.CurrentPitch - before), 0.5f,
                "R キーを押しても建設カメラの俯角が変わらなかった。");
            Assert.GreaterOrEqual(build.CurrentPitch, build.MinPitch - 0.01f, "俯角が下限を割った。");
            Assert.LessOrEqual(build.CurrentPitch, build.MaxPitch + 0.01f, "俯角が上限を超えた。");
        }

        // ── マウス ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator OptionDragOrbitsYawAndPitch()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            float yawBefore = build.Yaw;
            float pitchBefore = build.CurrentPitch;

            // トラックパッドには中ボタンが無いので、Option + 1本指ドラッグで周回する。
            Set(_mouse.position, ScreenCenter);
            Press(_keyboard.leftAltKey);
            Press(_mouse.leftButton);
            yield return null;                       // 押し始めの判定を通す
            yield return DriveMouse(new Vector2(320f, 200f), DragSeconds);
            Release(_mouse.leftButton);
            Release(_keyboard.leftAltKey);

            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yawBefore, build.Yaw)), 1f,
                "Option+ドラッグでカメラが左右に回らなかった。");
            Assert.Greater(Mathf.Abs(build.CurrentPitch - pitchBefore), 1f,
                "Option+ドラッグで俯角が変わらなかった。");
        }

        [UnityTest]
        public IEnumerator TwoFingerSideScrollRotates()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            float yawBefore = build.Yaw;

            // トラックパッドの2本指スクロール（左右）。連続した小さい値で来る。
            yield return DriveScrollVector(new Vector2(9f, 0f), 20);

            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yawBefore, build.Yaw)), 1f,
                "2本指スクロール（左右）でカメラが回らなかった。");
        }

        [UnityTest]
        public IEnumerator LeftDragPansPivotWhenNotOverUi()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            Vector3 before = build.Pivot;

            Set(_mouse.position, ScreenCenter);
            Press(_mouse.leftButton);
            yield return null;                       // 押し始めの判定を通す
            yield return DriveMouse(new Vector2(280f, 180f), DragSeconds);
            Release(_mouse.leftButton);

            Assert.Greater((build.Pivot - before).magnitude, 0.1f,
                "UI の無い場所で左ドラッグしても注視点が動かなかった。");
        }

        [UnityTest]
        public IEnumerator TwoFingerScrollChangesDistance()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            float before = build.Distance;

            // トラックパッドの2本指スクロール（上下）。ホイールの 120 と違い小さい値。
            yield return DriveScrollVector(new Vector2(0f, 9f), 20);

            Assert.Less(build.Distance, before - 0.5f,
                "2本指スクロールで寄れなかった。");
            Assert.GreaterOrEqual(build.Distance, build.MinDistance - 0.01f,
                "ズームが下限を割った。");
        }

        [UnityTest]
        public IEnumerator MouseWheelAlsoChangesDistance()
        {
            yield return SetUpScene();
            var build = _cameras.Build;
            float before = build.Distance;

            // マウスを使う人のために、ホイール（1ノッチ 120）も従来どおり効くこと。
            yield return DriveScrollVector(new Vector2(0f, 120f), 6);

            Assert.Less(build.Distance, before - 0.5f, "ホイールで寄れなかった。");
        }

        [UnityTest]
        public IEnumerator ZoomKeysChangeDistance()
        {
            yield return SetUpScene();
            var build = _cameras.Build;

            // トラックパッドにホイールが無い場合でも、キーだけで寄り引きできること。
            float before = build.Distance;
            Press(_keyboard.zKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.zKey);
            Assert.Less(build.Distance, before - 0.5f, "Z キーで寄れなかった。");

            float mid = build.Distance;
            Press(_keyboard.xKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.xKey);
            Assert.Greater(build.Distance, mid + 0.5f, "X キーで引けなかった。");
        }

        // ── 文字入力中はキーボード操作を止める ─────────────────

        [UnityTest]
        public IEnumerator KeyboardIsIgnoredWhileTypingButMouseStillWorks()
        {
            yield return SetUpScene();
            var build = _cameras.Build;

            MatsuriCameraInput.TextInputFocusOverride = true;

            Vector3 pivotBefore = build.Pivot;
            float yawBefore = build.Yaw;
            var modeBefore = _cameras.Mode;

            Press(_keyboard.wKey);
            Press(_keyboard.eKey);
            Press(_keyboard.cKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.wKey);
            Release(_keyboard.eKey);
            Release(_keyboard.cKey);

            Assert.Less((build.Pivot - pivotBefore).magnitude, 0.02f,
                "コードを打っている最中に W キーでカメラが動いてしまった。");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(yawBefore, build.Yaw)), 0.1f,
                "コードを打っている最中に E キーでカメラが回ってしまった。");
            Assert.AreEqual(modeBefore, _cameras.Mode,
                "コードを打っている最中に C キーでモードが切り替わってしまった。");

            // マウスは文字入力中でも効く。
            Set(_mouse.position, ScreenCenter);
            Press(_mouse.rightButton);
            yield return DriveMouse(new Vector2(320f, 0f), DragSeconds);
            Release(_mouse.rightButton);

            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yawBefore, build.Yaw)), 1f,
                "文字入力中でもマウスでは回せるはずが、回らなかった。");

            MatsuriCameraInput.TextInputFocusOverride = null;
        }

        // ── モード切替 ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator CycleKeySwitchesCameraMode()
        {
            yield return SetUpScene();

            Press(_keyboard.cKey);
            yield return Frames(2);
            Assert.AreEqual(CameraMode.Free, _cameras.Mode,
                "C キーで自由視点に切り替わらなかった。");

            Release(_keyboard.cKey);
            yield return Frames(2);
            Assert.AreEqual(CameraMode.Free, _cameras.Mode,
                "C キーを離しただけでモードが進んでしまった。");

            Press(_keyboard.cKey);
            yield return Frames(2);
            Assert.AreEqual(CameraMode.Visitor, _cameras.Mode,
                "2回目の C キーで来場者視点に切り替わらなかった。");
            Release(_keyboard.cKey);
        }

        [UnityTest]
        public IEnumerator HeldCycleKeyDoesNotSpinThroughModes()
        {
            yield return SetUpScene();

            Press(_keyboard.cKey);
            yield return Seconds(DriveSeconds);
            Assert.AreEqual(CameraMode.Free, _cameras.Mode,
                "C キーを押しっぱなしにしただけでモードが回り続けた。");
            Release(_keyboard.cKey);
        }

        [UnityTest]
        public IEnumerator FreeCameraMovesWithKeyboard()
        {
            yield return SetUpScene();
            _cameras.SetMode(CameraMode.Free);
            yield return Frames(2);

            var free = _cameras.Free;
            Vector3 before = free.transform.position;

            Press(_keyboard.wKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.wKey);

            Assert.Greater((free.transform.position - before).magnitude, 0.05f,
                "自由視点で W キーを押してもカメラが動かなかった。");
        }

        [UnityTest]
        public IEnumerator VisitorCameraMovesWithKeyboard()
        {
            yield return SetUpScene();
            _cameras.SetMode(CameraMode.Visitor);
            yield return Frames(2);

            var visitor = _cameras.VisitorView;
            Vector3 before = visitor.transform.position;

            Press(_keyboard.wKey);
            yield return Seconds(DriveSeconds);
            Release(_keyboard.wKey);

            Vector3 moved = visitor.transform.position - before;
            moved.y = 0f;
            Assert.Greater(moved.magnitude, 0.05f,
                "来場者視点で W キーを押しても歩かなかった。");
        }

        // ── 操作ヒント ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator HintOverlayIsBuilt()
        {
            // 実行環境に UI Toolkit のテーマが無いと警告が出るが、表示自体は成立する。
            LogAssert.ignoreFailingMessages = true;

            yield return SetUpScene(withHint: true);

            Assert.IsNotNull(_cameras.Hint, "操作ヒントが作られていない。");
            var document = _cameras.Hint.GetComponent<UIDocument>();
            Assert.IsNotNull(document, "操作ヒントの UIDocument が無い。");
            Assert.IsNotNull(document.rootVisualElement, "操作ヒントのルート要素が無い。");
        }
    }
}
