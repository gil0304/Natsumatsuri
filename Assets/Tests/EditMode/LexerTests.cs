using System.Collections.Generic;
using System.Linq;
using Matsuri.Script;
using Matsuri.Script.Lexing;
using NUnit.Framework;

namespace Matsuri.Tests
{
    /// <summary>字句解析のテスト。日本語・全角記号・時刻・コメント・文字列。</summary>
    public class LexerTests
    {
        static List<Token> Lex(string source, out List<Diagnostic> diagnostics)
        {
            diagnostics = new List<Diagnostic>();
            return Lexer.Tokenize(source, diagnostics);
        }

        static List<Token> Lex(string source) => Lex(source, out _);

        static List<Token> Meaningful(string source)
            => Lex(source).Where(t => t.Type != TokenType.Newline && t.Type != TokenType.EndOfFile).ToList();

        [Test]
        public void 日本語の語はひとつのトークンになる()
        {
            var tokens = Meaningful("屋台");
            Assert.That(tokens.Count, Is.EqualTo(1));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Identifier));
            Assert.That(tokens[0].Text, Is.EqualTo("屋台"));
        }

        [Test]
        public void カタカナと長音符もひとつの語になる()
        {
            var tokens = Meaningful("スーパーボールすくい");
            Assert.That(tokens.Count, Is.EqualTo(1));
            Assert.That(tokens[0].Text, Is.EqualTo("スーパーボールすくい"));
        }

        [Test]
        public void 文字列を読み取る()
        {
            var tokens = Meaningful("屋台 \"たこ焼き\"");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.String));
            Assert.That(tokens[1].Text, Is.EqualTo("たこ焼き"));
        }

        [Test]
        public void 全角の波括弧とカンマを半角と同じに扱う()
        {
            var tokens = Meaningful("屋台 \"たこ焼き\" ｛ 場所 5，10 ｝");
            Assert.That(tokens.Any(t => t.Type == TokenType.LBrace), Is.True);
            Assert.That(tokens.Any(t => t.Type == TokenType.RBrace), Is.True);
            Assert.That(tokens.Count(t => t.Type == TokenType.Comma), Is.EqualTo(1));
        }

        [Test]
        public void 全角の数字を読み取る()
        {
            var tokens = Meaningful("値段 ５００");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Number));
            Assert.That(tokens[1].Number, Is.EqualTo(500.0));
        }

        [Test]
        public void 全角クォートの文字列を読み取る()
        {
            var tokens = Meaningful("屋台 “たこ焼き”");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.String));
            Assert.That(tokens[1].Text, Is.EqualTo("たこ焼き"));
        }

        [Test]
        public void カギ括弧の文字列を読み取る()
        {
            var tokens = Meaningful("屋台 「たこ焼き」");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.String));
            Assert.That(tokens[1].Text, Is.EqualTo("たこ焼き"));
        }

        [Test]
        public void 時刻はTimeトークンになる()
        {
            var tokens = Meaningful("時間 19:00");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Time));
            Assert.That(tokens[1].Number, Is.EqualTo(19 * 60));
            Assert.That(tokens[1].Text, Is.EqualTo("19:00"));
        }

        [Test]
        public void 全角コロンの時刻も読める()
        {
            var tokens = Meaningful("時間 20：30");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Time));
            Assert.That(tokens[1].Number, Is.EqualTo(20 * 60 + 30));
        }

        [Test]
        public void 数値とコロンだけならTimeにしない()
        {
            var tokens = Meaningful("5 : 10");
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Number));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Colon));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Number));
        }

        [Test]
        public void スラッシュ二本のコメントは無視される()
        {
            var tokens = Meaningful("屋台 // これはメモ\n装飾");
            Assert.That(tokens.Count, Is.EqualTo(2));
            Assert.That(tokens[1].Text, Is.EqualTo("装飾"));
        }

        [Test]
        public void シャープのコメントは無視される()
        {
            var tokens = Meaningful("# 全部メモ\n屋台");
            Assert.That(tokens.Count, Is.EqualTo(1));
            Assert.That(tokens[0].Text, Is.EqualTo("屋台"));
        }

        [Test]
        public void 改行はトークンになる()
        {
            var tokens = Lex("屋台\n装飾");
            Assert.That(tokens.Count(t => t.Type == TokenType.Newline), Is.EqualTo(1));
        }

        [Test]
        public void マイナスの数を読み取る()
        {
            var tokens = Meaningful("場所 -20, -5");
            Assert.That(tokens[1].Number, Is.EqualTo(-20.0));
            Assert.That(tokens[3].Number, Is.EqualTo(-5.0));
        }

        [Test]
        public void 小数を読み取る()
        {
            var tokens = Meaningful("場所 2.5, 3");
            Assert.That(tokens[1].Number, Is.EqualTo(2.5));
        }

        [Test]
        public void ドットは屋台名と指標を分ける()
        {
            var tokens = Meaningful("たこ焼き.待ち人数");
            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Dot));
            Assert.That(tokens[2].Text, Is.EqualTo("待ち人数"));
        }

        [Test]
        public void かつとまたはは専用のトークンになる()
        {
            var tokens = Meaningful("A かつ B または C");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.And));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.Or));
        }

        [Test]
        public void 英語のandとorも受け取る()
        {
            var tokens = Meaningful("a and b or c");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.And));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.Or));
        }

        [Test]
        public void 比較演算子を読み取る()
        {
            var tokens = Meaningful("> >= < <= == !=");
            Assert.That(tokens.Select(t => t.Type).ToArray(), Is.EqualTo(new[]
            {
                TokenType.Greater, TokenType.GreaterEqual, TokenType.Less,
                TokenType.LessEqual, TokenType.EqualEqual, TokenType.NotEqual
            }));
        }

        [Test]
        public void 全角の比較記号も読み取る()
        {
            var tokens = Meaningful("来場者数 ＞ 500");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Greater));
        }

        [Test]
        public void 全角の以上記号を読み取る()
        {
            var tokens = Meaningful("来場者数 ≧ 500");
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.GreaterEqual));
        }

        [Test]
        public void くっついて書かれたキーワードを切り離す()
        {
            var tokens = Meaningful("もし来場者数 > 500");
            Assert.That(tokens[0].Text, Is.EqualTo("もし"));
            Assert.That(tokens[1].Text, Is.EqualTo("来場者数"));
        }

        [Test]
        public void 閉じ忘れた文字列は日本語で知らせる()
        {
            Lex("屋台 \"たこ焼き", out var diagnostics);
            Assert.That(diagnostics.Count, Is.GreaterThan(0));
            Assert.That(diagnostics[0].Message, Does.Contain("閉じられていません"));
        }

        [Test]
        public void 読めない文字はUnknownと診断になる()
        {
            var tokens = Lex("屋台 ★", out var diagnostics);
            Assert.That(tokens.Any(t => t.Type == TokenType.Unknown), Is.True);
            Assert.That(diagnostics.Count, Is.EqualTo(1));
            Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostics[0].Message, Does.Contain("★"));
        }

        [Test]
        public void 行番号と列番号が正しい()
        {
            var tokens = Lex("屋台\n    場所 5");
            var basho = tokens.First(t => t.Text == "場所");
            Assert.That(basho.Line, Is.EqualTo(2));
            Assert.That(basho.Column, Is.EqualTo(5));
        }

        [Test]
        public void 最後は必ずEndOfFileで終わる()
        {
            var tokens = Lex("屋台");
            Assert.That(tokens[tokens.Count - 1].Type, Is.EqualTo(TokenType.EndOfFile));
        }

        [Test]
        public void 空文字列でも落ちない()
        {
            var tokens = Lex("");
            Assert.That(tokens.Count, Is.EqualTo(1));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.EndOfFile));
        }

        [Test]
        public void 分が六十以上の時刻は知らせる()
        {
            Lex("時間 19:70 { }", out var diagnostics);
            Assert.That(diagnostics.Any(d => d.Message.Contains("時刻")), Is.True);
        }
    }
}
