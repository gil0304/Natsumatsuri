using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Matsuri.Script;
using Matsuri.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Matsuri.Tests
{
    /// <summary>
    /// コードエディターの行番号ズレの機械的な検査 (§10 / §42)。
    ///
    /// 「見た目が合っているか」を人の目に頼らずに済ませるため、
    /// n 行目のガター要素と n 行目のコード行要素の worldBound.y を突き合わせ、
    /// 差が 1px 未満であることを確かめる。
    /// 併せて、人が目視するための画面キャプチャも撮る（[Explicit] の別テスト）。
    /// </summary>
    public sealed class UiCaptureTests
    {
        const float Tolerance = 1f;

        GameObject _go;
        UIManager _ui;
        CodeEditorElement _editor;

        static string OutDir =>
            System.Environment.GetEnvironmentVariable("MATSURI_SHOT_DIR")
            ?? Application.persistentDataPath;

        /// <summary>日本語・英数字・空行・とても長い行を混ぜた 20 行。</summary>
        static readonly string[] SampleLines =
        {
            "祭り \"夏祭り\" {",
            "    # 屋台をならべる (comment 123)",
            "    屋台 たこ焼き {",
            "        場所 -6, 4",
            "        値段 500",
            "    }",
            "",
            "    屋台 かき氷 {",
            "        場所 0, 4",
            "        値段 400",
            "    }",
            "",
            "    装飾 提灯 { 場所 -3, 0 }",
            "    設備 ベンチ { 場所 3, 0 }",
            "",
            "    # とても長い行: 折り返しが起きると行番号と対応が崩れるので横スクロールで逃がす " +
            "abcdefghijklmnopqrstuvwxyz 0123456789 わたあめ りんご飴 金魚すくい ヨーヨー釣り 射的 型抜き 焼きそば",
            "    もし 来場者数 >= 100 {",
            "        イベント 花火 { 時間 20:00 }",
            "    }",
            "}"
        };

        static string Sample => string.Join("\n", SampleLines);

        static string ManyLines(int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append("    屋台 やたい").Append(i).Append(" { 場所 ").Append(i).Append(", 0 値段 300 }");
            }
            return sb.ToString();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("UiCaptureTests_UI");
            _ui = _go.AddComponent<UIManager>();
            _ui.Initialize();
            _editor = _ui.Editor;
            Assert.IsNotNull(_editor, "CodeEditorElement が作られていません。");

            // レイアウトが確定するまで数フレーム回す
            for (int i = 0; i < 4; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            _ui = null;
            _editor = null;
            yield return null;
        }

        IEnumerator SetSource(string source)
        {
            _editor.Text = source;
            _editor.SyncNow();
            for (int i = 0; i < 3; i++) yield return null;
            _editor.SyncNow();
            yield return null;
        }

        /// <summary>全行について、ガター行とコード行の縦位置・高さが一致していることを確かめる。</summary>
        void AssertAllRowsAligned(string what)
        {
            Assert.Greater(_editor.MeasuredLineHeight, 4f,
                $"{what}: 1行の高さが実測できていません ({_editor.MeasuredLineHeight})。");

            float lineHeight = _editor.MeasuredLineHeight;
            float previousY = float.NaN;

            for (int i = 0; i < _editor.LineCount; i++)
            {
                var gutter = _editor.GetGutterRowElement(i);
                var code = _editor.GetCodeRowElement(i);
                Assert.IsNotNull(gutter, $"{what}: {i + 1}行目のガター要素がありません。");
                Assert.IsNotNull(code, $"{what}: {i + 1}行目のコード行要素がありません。");

                float gy = gutter.worldBound.y;
                float cy = code.worldBound.y;
                Assert.Less(Mathf.Abs(gy - cy), Tolerance,
                    $"{what}: {i + 1}行目の行番号とコード行が {Mathf.Abs(gy - cy):0.###}px ずれています " +
                    $"(ガター y={gy:0.###} / コード y={cy:0.###})。");

                Assert.Less(Mathf.Abs(gutter.worldBound.height - code.worldBound.height), Tolerance,
                    $"{what}: {i + 1}行目の行の高さが一致しません " +
                    $"(ガター={gutter.worldBound.height:0.###} / コード={code.worldBound.height:0.###})。");

                if (!float.IsNaN(previousY))
                {
                    float step = cy - previousY;
                    Assert.Less(Mathf.Abs(step - lineHeight), Tolerance,
                        $"{what}: {i}行目→{i + 1}行目の送りが {step:0.###}px で、" +
                        $"実測の行高 {lineHeight:0.###}px と違います（折り返しが起きている可能性）。");
                }
                previousY = cy;
            }
        }

        // ── 1. 基本のズレ検査 ──────────────────────────────────

        [UnityTest]
        public IEnumerator Step1_GutterMatchesEveryCodeLine()
        {
            yield return SetSource(Sample);

            Assert.AreEqual(SampleLines.Length, _editor.LineCount,
                "行数が入力したテキストと一致しません。");
            AssertAllRowsAligned("初期表示");
        }

        // ── 2. 20行入れて10行目が揃っていること ────────────────

        [UnityTest]
        public IEnumerator Step2_TenthLineAlignedWithTwentyLines()
        {
            yield return SetSource(ManyLines(20));

            Assert.AreEqual(20, _editor.LineCount);

            var gutter = _editor.GetGutterRowElement(9);
            var code = _editor.GetCodeRowElement(9);
            Assert.IsNotNull(gutter);
            Assert.IsNotNull(code);
            Assert.Less(Mathf.Abs(gutter.worldBound.y - code.worldBound.y), Tolerance,
                $"10行目がずれています (ガター y={gutter.worldBound.y:0.###} / コード y={code.worldBound.y:0.###})。");

            AssertAllRowsAligned("20行");
        }

        // ── 3. スクロール後も揃っていること ────────────────────

        [UnityTest]
        public IEnumerator Step3_StaysAlignedAfterVerticalScroll()
        {
            yield return SetSource(ManyLines(80));
            Assert.AreEqual(80, _editor.LineCount);
            AssertAllRowsAligned("スクロール前");

            float codeBefore = _editor.GetCodeRowElement(9).worldBound.y;
            float gutterBefore = _editor.GetGutterRowElement(9).worldBound.y;

            _editor.ScrollY = 20f * _editor.MeasuredLineHeight;
            for (int i = 0; i < 3; i++) yield return null;

            float codeAfter = _editor.GetCodeRowElement(9).worldBound.y;
            float gutterAfter = _editor.GetGutterRowElement(9).worldBound.y;

            Assert.Less(codeAfter, codeBefore - 1f, "縦スクロールしていません（テストの前提が崩れています）。");
            Assert.Less(Mathf.Abs((codeBefore - codeAfter) - (gutterBefore - gutterAfter)), Tolerance,
                "縦スクロール量がガターとコードで違います。");

            AssertAllRowsAligned("縦スクロール後");
        }

        [UnityTest]
        public IEnumerator Step4_GutterDoesNotMoveOnHorizontalScroll()
        {
            yield return SetSource(Sample);

            float gutterX = _editor.GetGutterRowElement(0).worldBound.x;
            float codeX = _editor.GetCodeRowElement(0).worldBound.x;

            _editor.ScrollX = 120f;
            for (int i = 0; i < 3; i++) yield return null;

            float gutterX2 = _editor.GetGutterRowElement(0).worldBound.x;
            float codeX2 = _editor.GetCodeRowElement(0).worldBound.x;

            // バッチモード（-nographics）ではパネルに実寸が付かず、
            // 内容が溢れないため横スクロール自体が起きないことがある。
            // その場合は「横スクロールの検証」は環境的に不可能なので、
            // ガターがずれていないことだけを確かめて明示的にスキップする。
            bool actuallyScrolled = codeX2 < codeX - 1f;
            if (!actuallyScrolled)
            {
                Assert.Less(Mathf.Abs(gutterX2 - gutterX), Tolerance,
                    "横スクロールしていないのにガターが動きました。");
                AssertAllRowsAligned("横スクロールなし");
                Assert.Ignore(
                    "この環境ではコード欄に横方向の溢れが生じず、横スクロールを再現できませんでした" +
                    $"（幅 {Screen.width}x{Screen.height}）。行番号の対応は検証済みです。");
            }

            Assert.Less(Mathf.Abs(gutterX2 - gutterX), Tolerance,
                $"横スクロールでガターが {Mathf.Abs(gutterX2 - gutterX):0.###}px 動いてしまいました。");

            AssertAllRowsAligned("横スクロール後");
        }

        // ── 4. エラー行の表示 (§42) ────────────────────────────

        [UnityTest]
        public IEnumerator Step5_ErrorLineShowsGutterMarker()
        {
            yield return SetSource(Sample);

            var diagnostics = new List<Diagnostic>
            {
                Diagnostic.Error(3, 5, 2, "テスト用のエラー")
            };
            _editor.SetDiagnostics(diagnostics);
            _editor.SyncNow();
            for (int i = 0; i < 2; i++) yield return null;

            var marker = _editor.GetGutterRowElement(2).Q<Label>("marker");
            Assert.IsNotNull(marker, "ガターの ● 用ラベルがありません。");
            Assert.AreEqual("●", marker.text, "エラー行のガターに ● が出ていません。");

            var other = _editor.GetGutterRowElement(0).Q<Label>("marker");
            Assert.AreNotEqual("●", other.text, "エラーでない行に ● が出ています。");

            AssertAllRowsAligned("エラー表示後");
        }

        // ── 5. 目視用のキャプチャ ──────────────────────────────

        /// <summary>
        /// UI 込みの画面を PNG に書き出す。人が目視で確認するための材料。
        /// 通常のテスト実行を重くしないので [Explicit]（明示的に選んだときだけ走る）。
        /// </summary>
        [UnityTest]
        [Explicit]
        public IEnumerator Step6_CaptureEditorScreenshot()
        {
            yield return SetSource(Sample);

            _editor.SetDiagnostics(new List<Diagnostic>
            {
                Diagnostic.Error(4, 9, 2, "場所は数値を2つ書く"),
                Diagnostic.Warning(10, 9, 2, "値段が安すぎる")
            });
            _editor.FocusLine(9);
            for (int i = 0; i < 4; i++) yield return null;

            // ScreenCapture は WaitForEndOfFrame を必要とし、それはバッチモードでは発火しない。
            // 代わりに UI Toolkit のパネルを RenderTexture へ直接描かせて取り出す。
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            UIDocument target = null;
            foreach (var d in docs)
            {
                if (d == null || d.panelSettings == null) continue;
                if (d.rootVisualElement == null) continue;
                // いちばん手前のパネルではなく、コードエディターを含むパネルを撮る
                if (d.rootVisualElement.Q(className: "matsuri-editor") != null ||
                    d.rootVisualElement.Q<CodeEditorElement>() != null)
                {
                    target = d;
                    break;
                }
            }
            if (target == null && docs.Length > 0) target = docs[0];
            Assert.IsNotNull(target, "UIDocument が見つかりません。");

            // パネルと同じ解像度で撮る。サイズを変えると再レイアウトが走る。
            var reference = target.panelSettings.referenceResolution;
            int shotW = reference.x > 64 ? reference.x : 1920;
            int shotH = reference.y > 64 ? reference.y : 1080;
            var rt = new RenderTexture(shotW, shotH, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            var previous = target.panelSettings.targetTexture;
            target.panelSettings.targetTexture = rt;
            target.rootVisualElement.MarkDirtyRepaint();

            // パネルが RenderTexture へ描かれるまで数フレーム回す
            for (int i = 0; i < 8; i++) yield return null;

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            target.panelSettings.targetTexture = previous;

            Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, "ui_editor.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);

            Debug.Log($"[SHOT] wrote {path}");
        }
    }
}
