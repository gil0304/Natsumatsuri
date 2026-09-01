using System.Linq;
using Matsuri.Script.Completion;
using NUnit.Framework;

namespace Matsuri.Tests
{
    /// <summary>コード補完のテスト (§43)。</summary>
    public class CompletionTests
    {
        static System.Collections.Generic.List<CompletionItem> At(string source)
            => CompletionProvider.GetCompletions(source, source.Length, new FakeCatalog());

        static bool Has(System.Collections.Generic.List<CompletionItem> items, string label)
            => items.Any(i => i.Label == label);

        [Test]
        public void 屋の一文字で屋台が候補に出る()
        {
            var items = At("屋");
            Assert.That(Has(items, "屋台"), Is.True);
        }

        [Test]
        public void 何も打っていなければ命令が全部出る()
        {
            var items = At("");
            Assert.That(Has(items, "屋台"), Is.True);
            Assert.That(Has(items, "装飾"), Is.True);
            Assert.That(Has(items, "設備"), Is.True);
            Assert.That(Has(items, "もし"), Is.True);
            Assert.That(Has(items, "時間"), Is.True);
        }

        [Test]
        public void ひな形が候補に出る()
        {
            var items = At("");
            var snippet = items.FirstOrDefault(i => i.Kind == CompletionKind.Snippet);
            Assert.That(snippet.Label, Is.Not.Empty);
            Assert.That(items.Any(i => i.Kind == CompletionKind.Snippet && i.InsertText.Contains("場所")), Is.True);
        }

        [Test]
        public void 屋台のクォートの直後は屋台名が全部出る()
        {
            var items = At("屋台 \"");
            Assert.That(items.Count, Is.EqualTo(11));
            Assert.That(Has(items, "たこ焼き"), Is.True);
            Assert.That(Has(items, "スーパーボールすくい"), Is.True);
            Assert.That(items.All(i => i.Kind == CompletionKind.StallName), Is.True);
        }

        [Test]
        public void 装飾のクォートの直後は装飾名が出る()
        {
            var items = At("装飾 \"");
            Assert.That(items.Count, Is.EqualTo(7));
            Assert.That(Has(items, "提灯"), Is.True);
            Assert.That(Has(items, "たこ焼き"), Is.False);
        }

        [Test]
        public void 設備のクォートの直後は設備名が出る()
        {
            var items = At("設備 \"");
            Assert.That(items.Count, Is.EqualTo(6));
            Assert.That(Has(items, "ベンチ"), Is.True);
        }

        [Test]
        public void イベントのクォートの直後はイベント名が出る()
        {
            var items = At("イベント \"");
            Assert.That(items.Count, Is.EqualTo(3));
            Assert.That(Has(items, "花火"), Is.True);
        }

        [Test]
        public void 花火のクォートの直後は種類が出る()
        {
            var items = At("花火 \"");
            Assert.That(Has(items, "大玉"), Is.True);
            Assert.That(Has(items, "菊"), Is.True);
        }

        [Test]
        public void 打ちかけの名前で絞り込まれる()
        {
            var items = At("屋台 \"たこ");
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Label, Is.EqualTo("たこ焼き"));
        }

        [Test]
        public void ブロックの中では設定が出る()
        {
            var items = At("屋台 \"たこ焼き\" {\n    ");
            Assert.That(Has(items, "場所"), Is.True);
            Assert.That(Has(items, "値段"), Is.True);
            Assert.That(Has(items, "向き"), Is.True);
        }

        [Test]
        public void 装飾のブロックには値段を出さない()
        {
            var items = At("装飾 \"提灯\" {\n    ");
            Assert.That(Has(items, "場所"), Is.True);
            Assert.That(Has(items, "値段"), Is.False);
        }

        [Test]
        public void もしの後には指標が出る()
        {
            var items = At("もし ");
            Assert.That(Has(items, "来場者数"), Is.True);
            Assert.That(Has(items, "売上"), Is.True);
            Assert.That(Has(items, "満足度"), Is.True);
            Assert.That(Has(items, "たこ焼き.待ち人数"), Is.True);
        }

        [Test]
        public void 屋台名とドットの後には屋台の指標が出る()
        {
            var items = At("もし たこ焼き.");
            Assert.That(Has(items, "待ち人数"), Is.True);
            Assert.That(Has(items, "軒数"), Is.True);
            Assert.That(Has(items, "来場者数"), Is.False);
        }

        [Test]
        public void コメントの中では何も出さない()
        {
            var items = At("// 屋台をここに");
            Assert.That(items.Count, Is.EqualTo(0));
        }

        [Test]
        public void もしのブロックの中では命令が出る()
        {
            var items = At("もし 来場者数 > 500 {\n    ");
            Assert.That(Has(items, "屋台"), Is.True);
            Assert.That(Has(items, "場所"), Is.False);
        }

        [Test]
        public void 候補には説明がつく()
        {
            var items = At("屋台 \"");
            var takoyaki = items.First(i => i.Label == "たこ焼き");
            Assert.That(takoyaki.Detail, Is.Not.Null);
            Assert.That(takoyaki.Detail, Does.Contain("45,000円"));
        }

        [Test]
        public void カーソル位置が範囲外でも落ちない()
        {
            Assert.That(CompletionProvider.GetCompletions("屋台", 999, new FakeCatalog()), Is.Not.Null);
            Assert.That(CompletionProvider.GetCompletions(null, 0, new FakeCatalog()), Is.Not.Null);
            Assert.That(CompletionProvider.GetCompletions("屋台", -5, null), Is.Not.Null);
        }
    }
}
