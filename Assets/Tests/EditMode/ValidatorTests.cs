using System.Collections.Generic;
using System.Linq;
using Matsuri.Script;
using Matsuri.Script.Lexing;
using Matsuri.Script.Parsing;
using Matsuri.Script.Validation;
using NUnit.Framework;

namespace Matsuri.Tests
{
    /// <summary>
    /// 検証のテスト。§41 の「12行目 / 何が悪いか / どう直すか」を満たしているかを見る。
    /// </summary>
    public class ValidatorTests
    {
        static List<Diagnostic> Check(string source, FakeCatalog catalog = null)
        {
            var diagnostics = new List<Diagnostic>();
            var tokens = Lexer.Tokenize(source, diagnostics);
            var program = Parser.Parse(tokens, diagnostics);
            Validator.Validate(program, catalog ?? new FakeCatalog(), diagnostics);
            return diagnostics;
        }

        static Diagnostic FirstError(List<Diagnostic> diagnostics)
            => diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);

        static Diagnostic FirstWarning(List<Diagnostic> diagnostics)
            => diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Warning);

        [Test]
        public void 正しいコードには何も出ない()
        {
            var diagnostics = Check("屋台 \"たこ焼き\" {\n  場所 5, 10\n  値段 500\n}");
            Assert.That(diagnostics.Count, Is.EqualTo(0));
        }

        [Test]
        public void 知らない屋台名を日本語で知らせる()
        {
            var diagnostics = Check("屋台 \"たこやき焼き\" { 場所 5, 5 }");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("屋台「たこやき焼き」は見つかりません"));
        }

        [Test]
        public void 知らない屋台名にはもしかして候補がつく()
        {
            var diagnostics = Check("屋台 \"たこやき焼き\" { 場所 5, 5 }");
            var error = FirstError(diagnostics);
            Assert.That(error.Suggestions.Count, Is.GreaterThan(0));
            Assert.That(error.Suggestions.Contains("たこ焼き"), Is.True);
        }

        [Test]
        public void 場所が無いと行番号つきで知らせる()
        {
            var diagnostics = Check("// メモ\n\n屋台 \"たこ焼き\" {\n}\n");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("「場所」が設定されていません"));
            Assert.That(error.Line, Is.EqualTo(3));
        }

        [Test]
        public void 場所が無いときは直し方の例がつく()
        {
            var diagnostics = Check("屋台 \"たこ焼き\" {\n}");
            var error = FirstError(diagnostics);
            Assert.That(error.Example, Is.Not.Null);
            Assert.That(error.Example, Does.Contain("場所"));
        }

        [Test]
        public void エラー表示に行番号と例が入る()
        {
            var diagnostics = Check("屋台 \"たこ焼き\" {\n}");
            string text = FirstError(diagnostics).ToDisplayString();
            Assert.That(text, Does.Contain("行目"));
            Assert.That(text, Does.Contain("例:"));
        }

        [Test]
        public void 敷地の外に置こうとしたら知らせる()
        {
            var diagnostics = Check("屋台 \"たこ焼き\" { 場所 200, 5 }");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("会場の外です"));
            Assert.That(error.Message, Does.Contain("X:-60"));
        }

        [Test]
        public void 高すぎる値段は警告になる()
        {
            var diagnostics = Check("屋台 \"たこ焼き\" {\n  場所 5, 5\n  値段 5000\n}");
            var warning = FirstWarning(diagnostics);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Message, Does.Contain("高すぎます"));
            Assert.That(FirstError(diagnostics), Is.Null, "値段は警告であってエラーではない");
        }

        [Test]
        public void 安すぎる値段も警告になる()
        {
            var diagnostics = Check("屋台 \"たこ焼き\" {\n  場所 5, 5\n  値段 10\n}");
            Assert.That(FirstWarning(diagnostics).Message, Does.Contain("安すぎます"));
        }

        [Test]
        public void 予算超過はエラーではなく警告()
        {
            var catalog = new FakeCatalog { InitialBudget = 50000 };
            var diagnostics = Check("屋台 \"たこ焼き\" { 場所 1, 1 }\n屋台 \"焼きそば\" { 場所 5, 5 }", catalog);

            var warning = FirstWarning(diagnostics);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Message, Does.Contain("予算"));
            Assert.That(warning.Message, Does.Contain("50,000円"));
            Assert.That(FirstError(diagnostics), Is.Null);
        }

        [Test]
        public void 同じ場所に重ねて建てると警告()
        {
            var diagnostics = Check("屋台 \"たこ焼き\" { 場所 5, 5 }\n屋台 \"焼きそば\" { 場所 5, 5 }");
            var warning = FirstWarning(diagnostics);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Message, Does.Contain("すでに"));
            Assert.That(warning.Line, Is.EqualTo(2));
        }

        [Test]
        public void 祭りの時間外の時刻を知らせる()
        {
            var diagnostics = Check("時間 23:00 {\n  花火 \"大玉\"\n}");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("祭りが開いていません"));
        }

        [Test]
        public void 祭りの時間内の時刻は通る()
        {
            var diagnostics = Check("時間 19:00 {\n  盆踊り\n}");
            Assert.That(diagnostics.Count, Is.EqualTo(0));
        }

        [Test]
        public void 種類を取り違えたら正しい書き方を教える()
        {
            var diagnostics = Check("屋台 \"ベンチ\" { 場所 5, 5 }");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("設備です"));
            Assert.That(error.Message, Does.Contain("設備 \"ベンチ\""));
        }

        [Test]
        public void 装飾に値段をつけたら警告()
        {
            var diagnostics = Check("装飾 \"提灯\" {\n  場所 3, 4\n  値段 500\n}");
            var warning = FirstWarning(diagnostics);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Message, Does.Contain("「値段」はつけられません"));
        }

        [Test]
        public void 読めない指標を知らせる()
        {
            var diagnostics = Check("もし 来場社数 > 500 {\n  太鼓\n}");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("読めません"));
            Assert.That(error.Suggestions.Count, Is.GreaterThan(0));
        }

        [Test]
        public void 屋台名だけの条件は書き方を教える()
        {
            var diagnostics = Check("もし たこ焼き > 20 {\n  太鼓\n}");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("何の数か分かりません"));
            Assert.That(error.Example, Does.Contain("待ち人数"));
        }

        [Test]
        public void 条件の中の知らない屋台名を知らせる()
        {
            var diagnostics = Check("もし たこやき焼き.待ち人数 > 20 {\n  太鼓\n}");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("見つかりません"));
        }

        [Test]
        public void 屋台の知らない指標を知らせる()
        {
            var diagnostics = Check("もし たこ焼き.売れゆき > 20 {\n  太鼓\n}");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("待ち人数"));
        }

        [Test]
        public void 待ち人数の条件は通る()
        {
            var diagnostics = Check("もし たこ焼き.待ち人数 > 20 {\n  屋台 \"たこ焼き\" { 場所 20, 10 }\n}");
            Assert.That(diagnostics.Count, Is.EqualTo(0));
        }

        [Test]
        public void 満足度は百を超える値と比べたら警告()
        {
            var diagnostics = Check("もし 満足度 > 500 {\n  太鼓\n}");
            Assert.That(FirstWarning(diagnostics).Message, Does.Contain("満足度は 0〜100"));
        }

        [Test]
        public void 中身が空のもしは警告()
        {
            var diagnostics = Check("もし 来場者数 > 500 {\n}");
            Assert.That(FirstWarning(diagnostics), Is.Not.Null);
        }

        [Test]
        public void もしの中の屋台は予算に数えない()
        {
            var catalog = new FakeCatalog { InitialBudget = 50000 };
            var diagnostics = Check(
                "屋台 \"たこ焼き\" { 場所 1, 1 }\nもし 来場者数 > 100 {\n  屋台 \"焼きそば\" { 場所 5, 5 }\n}",
                catalog);
            Assert.That(diagnostics.Any(d => d.Message.Contains("予算")), Is.False);
        }

        [Test]
        public void 敷地の範囲は差し替えられる()
        {
            var catalog = new FakeCatalog { Bounds = new GroundBounds(-10f, 10f, -10f, 10f) };
            var diagnostics = Check("屋台 \"たこ焼き\" { 場所 30, 0 }", catalog);
            Assert.That(FirstError(diagnostics).Message, Does.Contain("X:-10"));
        }

        [Test]
        public void 知らないイベントを知らせる()
        {
            var diagnostics = Check("イベント \"雪まつり\"");
            var error = FirstError(diagnostics);
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("イベント「雪まつり」は見つかりません"));
        }

        [Test]
        public void 知らない花火の種類は警告()
        {
            var diagnostics = Check("花火 \"なぞ玉\"");
            var warning = FirstWarning(diagnostics);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.Message, Does.Contain("という種類はありません"));
        }

        [Test]
        public void 表記ゆれの屋台名は通る()
        {
            Assert.That(Check("屋台 \"タコヤキ\" { 場所 1, 1 }").Count, Is.EqualTo(0));
            Assert.That(Check("屋台 \"たこやき\" { 場所 1, 1 }").Count, Is.EqualTo(0));
            Assert.That(Check("屋台 \"takoyaki\" { 場所 1, 1 }").Count, Is.EqualTo(0));
        }
    }
}
