using Matsuri.Data;
using Matsuri.Script;
using NUnit.Framework;

namespace Matsuri.Tests
{
    /// <summary>
    /// 表記ゆれ吸収のテスト。
    /// ゲーム側の <see cref="NameUtility"/> と、言語処理系側の <see cref="ScriptText"/> は
    /// 同じ規則で動かなければならない（アセンブリが分かれているので実装は2つある）。
    /// </summary>
    public class NameUtilityTests
    {
        [Test]
        public void カタカナはひらがなに寄せられる()
        {
            Assert.That(NameUtility.Normalize("タコヤキ"), Is.EqualTo("たこやき"));
            Assert.That(ScriptText.Normalize("タコヤキ"), Is.EqualTo("たこやき"));
        }

        [Test]
        public void 全角英数は半角小文字になる()
        {
            Assert.That(NameUtility.Normalize("ＴＡＫＯ１"), Is.EqualTo("tako1"));
            Assert.That(ScriptText.Normalize("ＴＡＫＯ１"), Is.EqualTo("tako1"));
        }

        [Test]
        public void 空白とアンダースコアとハイフンは消える()
        {
            Assert.That(NameUtility.Normalize("bon_odori"), Is.EqualTo("bonodori"));
            Assert.That(ScriptText.Normalize("bon odori"), Is.EqualTo("bonodori"));
            Assert.That(ScriptText.Normalize("yoyo-tsuri"), Is.EqualTo("yoyotsuri"));
        }

        [Test]
        public void 全角スペースも消える()
        {
            Assert.That(NameUtility.Normalize("たこ　焼き"), Is.EqualTo("たこ焼き"));
            Assert.That(ScriptText.Normalize("たこ　焼き"), Is.EqualTo("たこ焼き"));
        }

        [Test]
        public void 表記ゆれを同じとみなす()
        {
            Assert.That(NameUtility.Equals("タコヤキ", "たこやき"), Is.True);
            Assert.That(ScriptText.NameEquals("タコヤキ", "たこやき"), Is.True);
            Assert.That(ScriptText.NameEquals("たこ焼き", "焼きそば"), Is.False);
        }

        [Test]
        public void 同じ文字列の距離はゼロ()
        {
            Assert.That(NameUtility.Distance("たこ焼き", "たこ焼き"), Is.EqualTo(0));
            Assert.That(ScriptText.Distance("たこ焼き", "タコ焼き"), Is.EqualTo(0));
        }

        [Test]
        public void 一文字違いの距離は一()
        {
            Assert.That(NameUtility.Distance("たこ焼き", "たこ焼く"), Is.EqualTo(1));
            Assert.That(ScriptText.Distance("来場者数", "来場社数"), Is.EqualTo(1));
        }

        [Test]
        public void 空文字列との距離は長さになる()
        {
            Assert.That(ScriptText.Distance("", "たこ焼き"), Is.EqualTo(4));
            Assert.That(ScriptText.Distance("たこ焼き", ""), Is.EqualTo(4));
        }

        [Test]
        public void 前方一致と部分一致が使える()
        {
            Assert.That(NameUtility.StartsWith("たこ焼き", "たこ"), Is.True);
            Assert.That(ScriptText.StartsWith("たこ焼き", "タコ"), Is.True);
            Assert.That(ScriptText.Contains("スーパーボールすくい", "ぼーる"), Is.True);
            Assert.That(ScriptText.StartsWith("焼きそば", "たこ"), Is.False);
        }

        [Test]
        public void 金額は三桁区切りになる()
        {
            Assert.That(ScriptText.Yen(1250000), Is.EqualTo("1,250,000"));
            Assert.That(ScriptText.Yen(500), Is.EqualTo("500"));
            Assert.That(ScriptText.Yen(0), Is.EqualTo("0"));
        }

        [Test]
        public void 時刻の文字列化ができる()
        {
            Assert.That(ScriptText.ClockText(19 * 60), Is.EqualTo("19:00"));
            Assert.That(ScriptText.ClockText(20 * 60 + 30), Is.EqualTo("20:30"));
            Assert.That(ScriptText.ClockText(17 * 60 + 5), Is.EqualTo("17:05"));
        }

        [Test]
        public void 全角記号は半角に読み替えられる()
        {
            Assert.That(ScriptText.NormalizePunctuation('｛'), Is.EqualTo('{'));
            Assert.That(ScriptText.NormalizePunctuation('＞'), Is.EqualTo('>'));
            Assert.That(ScriptText.NormalizePunctuation('，'), Is.EqualTo(','));
            Assert.That(ScriptText.NormalizePunctuation('５'), Is.EqualTo('5'));
            Assert.That(ScriptText.NormalizePunctuation('た'), Is.EqualTo('た'));
        }

        [Test]
        public void 日本語の文字だと分かる()
        {
            Assert.That(ScriptText.IsJapanese('た'), Is.True);
            Assert.That(ScriptText.IsJapanese('ボ'), Is.True);
            Assert.That(ScriptText.IsJapanese('焼'), Is.True);
            Assert.That(ScriptText.IsJapanese('ー'), Is.True);
            Assert.That(ScriptText.IsJapanese('a'), Is.False);
            Assert.That(ScriptText.IsJapanese('5'), Is.False);
        }
    }
}
