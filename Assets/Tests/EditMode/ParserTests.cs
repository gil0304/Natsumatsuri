using System.Collections.Generic;
using System.Linq;
using Matsuri.Script;
using Matsuri.Script.Ast;
using Matsuri.Script.Lexing;
using Matsuri.Script.Parsing;
using NUnit.Framework;

namespace Matsuri.Tests
{
    /// <summary>構文解析のテスト。正常系・閉じ忘れ・プロパティ欠落・ネスト。</summary>
    public class ParserTests
    {
        static FestivalProgram Parse(string source, out List<Diagnostic> diagnostics)
        {
            diagnostics = new List<Diagnostic>();
            var tokens = Lexer.Tokenize(source, diagnostics);
            return Parser.Parse(tokens, diagnostics);
        }

        static FestivalProgram Parse(string source) => Parse(source, out _);

        [Test]
        public void 屋台をひとつ読む()
        {
            var program = Parse("屋台 \"たこ焼き\" { 場所 5, 10 }", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(0));
            Assert.That(program.Body.Count, Is.EqualTo(1));

            var stall = program.Body[0] as StallNode;
            Assert.That(stall, Is.Not.Null);
            Assert.That(stall.Name, Is.EqualTo("たこ焼き"));
            Assert.That(stall.Properties.Count, Is.EqualTo(1));
            Assert.That(stall.Properties[0].Keyword, Is.EqualTo("場所"));
            Assert.That(stall.Properties[0].Numbers, Is.EqualTo(new List<double> { 5.0, 10.0 }));
        }

        [Test]
        public void 値段と向きも読む()
        {
            var program = Parse("屋台 \"たこ焼き\" {\n  場所 5, 10\n  値段 500\n  向き 90\n}", out var diagnostics);
            var stall = (StallNode)program.Body[0];
            Assert.That(diagnostics.Count, Is.EqualTo(0));
            Assert.That(stall.Properties.Count, Is.EqualTo(3));
            Assert.That(stall.Properties[1].Keyword, Is.EqualTo("値段"));
            Assert.That(stall.Properties[1].Number0, Is.EqualTo(500.0));
            Assert.That(stall.Properties[2].Keyword, Is.EqualTo("向き"));
        }

        [Test]
        public void 祭りブロックの名前を取り出して中身を展開する()
        {
            var program = Parse("祭り \"夏の宴\" {\n  屋台 \"たこ焼き\" { 場所 1, 1 }\n  装飾 \"提灯\" { 場所 2, 2 }\n}");
            Assert.That(program.Name, Is.EqualTo("夏の宴"));
            Assert.That(program.Body.Count, Is.EqualTo(2));
            Assert.That(program.Body[0], Is.TypeOf<StallNode>());
            Assert.That(program.Body[1], Is.TypeOf<DecorationNode>());
        }

        [Test]
        public void 祭りで囲まなくても読める()
        {
            var program = Parse("屋台 \"たこ焼き\" { 場所 1, 1 }\n装飾 \"提灯\" { 場所 2, 2 }\n設備 \"ベンチ\" { 場所 3, 3 }");
            Assert.That(program.Body.Count, Is.EqualTo(3));
            Assert.That(program.Body[2], Is.TypeOf<FacilityNode>());
        }

        [Test]
        public void 英語で書いても同じ形になる()
        {
            var program = Parse("stall \"takoyaki\" {\n  position 5, 10\n  price 500\n}");
            var stall = program.Body[0] as StallNode;
            Assert.That(stall, Is.Not.Null);
            Assert.That(stall.Name, Is.EqualTo("takoyaki"));
            Assert.That(stall.Properties[0].Keyword, Is.EqualTo("場所"));
            Assert.That(stall.Properties[1].Keyword, Is.EqualTo("値段"));
        }

        [Test]
        public void 時間ブロックを読む()
        {
            var program = Parse("時間 19:00 {\n  盆踊り\n}", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(0));

            var time = program.Body[0] as TimeNode;
            Assert.That(time, Is.Not.Null);
            Assert.That(time.MinutesOfDay, Is.EqualTo(19 * 60));
            Assert.That(time.Body.Count, Is.EqualTo(1));
            Assert.That(((EventNode)time.Body[0]).Name, Is.EqualTo("盆踊り"));
        }

        [Test]
        public void 時間を省略した時刻ブロックも読む()
        {
            var program = Parse("20:00 {\n  花火 \"大玉\"\n}", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(0));

            var time = program.Body[0] as TimeNode;
            Assert.That(time, Is.Not.Null);
            Assert.That(time.MinutesOfDay, Is.EqualTo(20 * 60));

            var ev = (EventNode)time.Body[0];
            Assert.That(ev.Name, Is.EqualTo("花火"));
            Assert.That(ev.Argument, Is.EqualTo("大玉"));
        }

        [Test]
        public void 単独の花火と盆踊りと太鼓を読む()
        {
            var program = Parse("花火 \"大玉\"\n盆踊り\n太鼓");
            Assert.That(program.Body.Count, Is.EqualTo(3));
            Assert.That(((EventNode)program.Body[0]).Name, Is.EqualTo("花火"));
            Assert.That(((EventNode)program.Body[1]).Name, Is.EqualTo("盆踊り"));
            Assert.That(((EventNode)program.Body[2]).Name, Is.EqualTo("太鼓"));
        }

        [Test]
        public void もしの条件と中身を読む()
        {
            var program = Parse("もし 来場者数 > 500 {\n  屋台 \"焼きそば\" { 場所 20, 10 }\n}", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(0));

            var ifNode = program.Body[0] as IfNode;
            Assert.That(ifNode, Is.Not.Null);

            var comparison = ifNode.Condition as ComparisonNode;
            Assert.That(comparison, Is.Not.Null);
            Assert.That(comparison.LeftMetric, Is.EqualTo("来場者数"));
            Assert.That(comparison.LeftTarget, Is.Null);
            Assert.That(comparison.Op, Is.EqualTo(">"));
            Assert.That(comparison.Right, Is.EqualTo(500.0));
            Assert.That(ifNode.Body.Count, Is.EqualTo(1));
        }

        [Test]
        public void 屋台の指標を読む()
        {
            var program = Parse("もし たこ焼き.待ち人数 > 20 {\n  屋台 \"たこ焼き\" { 場所 20, 10 }\n}");
            var comparison = (ComparisonNode)((IfNode)program.Body[0]).Condition;
            Assert.That(comparison.LeftTarget, Is.EqualTo("たこ焼き"));
            Assert.That(comparison.LeftMetric, Is.EqualTo("待ち人数"));
        }

        [Test]
        public void かつはまたはより強く結びつく()
        {
            var program = Parse("もし 来場者数 > 1 または 売上 > 2 かつ 満足度 > 3 { 太鼓 }");
            var root = (LogicalNode)((IfNode)program.Body[0]).Condition;
            Assert.That(root.IsAnd, Is.False);
            Assert.That(root.Left, Is.TypeOf<ComparisonNode>());
            Assert.That(root.Right, Is.TypeOf<LogicalNode>());
            Assert.That(((LogicalNode)root.Right).IsAnd, Is.True);
        }

        [Test]
        public void もしの中の時間もネストできる()
        {
            var program = Parse("もし 来場者数 > 100 {\n  時間 20:00 {\n    花火 \"大玉\"\n  }\n}", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(0));

            var ifNode = (IfNode)program.Body[0];
            Assert.That(ifNode.Body[0], Is.TypeOf<TimeNode>());
            Assert.That(((TimeNode)ifNode.Body[0]).Body[0], Is.TypeOf<EventNode>());
        }

        [Test]
        public void 時間の中のもしもネストできる()
        {
            var program = Parse("時間 20:00 {\n  もし 売上 > 100 {\n    花火 \"大玉\"\n  }\n}", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(0));

            var time = (TimeNode)program.Body[0];
            Assert.That(time.Body[0], Is.TypeOf<IfNode>());
        }

        [Test]
        public void 閉じ忘れの波括弧を知らせる()
        {
            Parse("屋台 \"たこ焼き\" {\n  場所 5, 5\n", out var diagnostics);
            Assert.That(diagnostics.Count, Is.GreaterThan(0));
            Assert.That(diagnostics[0].Message, Does.Contain("閉じられていません"));
        }

        [Test]
        public void 波括弧が多すぎる場合も知らせる()
        {
            Parse("屋台 \"たこ焼き\" { 場所 5, 5 }\n}", out var diagnostics);
            Assert.That(diagnostics.Any(d => d.Message.Contains("多い")), Is.True);
        }

        [Test]
        public void 場所の数字が足りないと知らせる()
        {
            Parse("屋台 \"たこ焼き\" { 場所 5 }", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(1));
            Assert.That(diagnostics[0].Message, Does.Contain("2つの数字"));
            Assert.That(diagnostics[0].Example, Is.Not.Null);
        }

        [Test]
        public void エラーがあっても後の行を読み続ける()
        {
            var program = Parse("屋台 \"たこ焼き\" { 場所 }\n屋台 \"焼きそば\" { 場所 }\n屋台 \"かき氷\" { 場所 1, 1 }",
                out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(2), "エラーは2件まとめて返る");
            Assert.That(program.Body.Count, Is.EqualTo(3), "3つとも構文木には残る");
        }

        [Test]
        public void 知らない命令はもしかして候補を出す()
        {
            Parse("屋号 \"たこ焼き\" { 場所 1, 1 }", out var diagnostics);
            Assert.That(diagnostics.Count, Is.GreaterThan(0));
            Assert.That(diagnostics[0].Message, Does.Contain("という命令はありません"));
            Assert.That(diagnostics[0].Suggestions.Contains("屋台"), Is.True);
        }

        [Test]
        public void 知らない設定はもしかして候補を出す()
        {
            Parse("屋台 \"たこ焼き\" {\n  場書 1, 1\n}", out var diagnostics);
            Assert.That(diagnostics.Any(d => d.Message.Contains("という設定はありません")), Is.True);
            Assert.That(diagnostics[0].Suggestions.Count, Is.GreaterThan(0));
        }

        [Test]
        public void 名前がないと知らせる()
        {
            Parse("屋台 { 場所 1, 1 }", out var diagnostics);
            Assert.That(diagnostics.Count, Is.GreaterThan(0));
            Assert.That(diagnostics[0].Message, Does.Contain("名前がありません"));
        }

        [Test]
        public void 条件の比較記号がないと知らせる()
        {
            Parse("もし 来場者数 500 { 太鼓 }", out var diagnostics);
            Assert.That(diagnostics.Count, Is.GreaterThan(0));
            Assert.That(diagnostics[0].Message, Does.Contain("くらべる記号"));
        }

        [Test]
        public void 全角で書かれたコードも同じ構文木になる()
        {
            var program = Parse("屋台 “たこ焼き” ｛ 場所 ５，１０ ｝", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(0));

            var stall = (StallNode)program.Body[0];
            Assert.That(stall.Name, Is.EqualTo("たこ焼き"));
            Assert.That(stall.Properties[0].Numbers[0], Is.EqualTo(5.0));
            Assert.That(stall.Properties[0].Numbers[1], Is.EqualTo(10.0));
        }

        [Test]
        public void 波括弧を次の行に書いてもよい()
        {
            var program = Parse("屋台 \"たこ焼き\"\n{\n  場所 5, 5\n}", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(0));
            Assert.That(((StallNode)program.Body[0]).Properties.Count, Is.EqualTo(1));
        }

        [Test]
        public void 行番号がエラーに入る()
        {
            Parse("屋台 \"たこ焼き\" { 場所 1, 1 }\n\n屋台 \"焼きそば\" { 場所 }", out var diagnostics);
            Assert.That(diagnostics.Count, Is.EqualTo(1));
            Assert.That(diagnostics[0].Line, Is.EqualTo(3));
        }

        [Test]
        public void 空のコードでも落ちない()
        {
            var program = Parse("", out var diagnostics);
            Assert.That(program.Body.Count, Is.EqualTo(0));
            Assert.That(diagnostics.Count, Is.EqualTo(0));
        }

        [Test]
        public void コメントだけのコードでも落ちない()
        {
            var program = Parse("// なにも書いていない\n# ここもメモ", out var diagnostics);
            Assert.That(program.Body.Count, Is.EqualTo(0));
            Assert.That(diagnostics.Count, Is.EqualTo(0));
        }
    }
}
