using System.Collections.Generic;
using Matsuri.Script.Lexing;

namespace Matsuri.Script.Parsing
{
    /// <summary>
    /// 構文解析中の読み取り位置と診断の入れ物。
    /// 「エラーが出ても止まらない」ための回復操作もここに置く (§41)。
    /// </summary>
    internal sealed class ParseContext
    {
        readonly List<Token> _tokens;
        int _pos;

        public readonly List<Diagnostic> Diagnostics;

        /// <summary>1回の解析で出すエラーの上限。壊れたコードでUIを埋め尽くさないため。</summary>
        public const int MaxErrors = 30;

        int _errorCount;

        public ParseContext(List<Token> tokens, List<Diagnostic> diagnostics)
        {
            _tokens = tokens ?? new List<Token>();
            if (_tokens.Count == 0) _tokens.Add(Token.EndOfFile(1, 1));
            Diagnostics = diagnostics ?? new List<Diagnostic>();
        }

        public Token Current => _tokens[_pos < _tokens.Count ? _pos : _tokens.Count - 1];

        public Token Peek(int offset)
        {
            int p = _pos + offset;
            if (p < 0) p = 0;
            if (p >= _tokens.Count) p = _tokens.Count - 1;
            return _tokens[p];
        }

        public bool IsAtEnd => Current.Type == TokenType.EndOfFile;

        public Token Advance()
        {
            var t = Current;
            if (_pos < _tokens.Count - 1) _pos++;
            return t;
        }

        public bool Check(TokenType type) => Current.Type == type;

        public bool Match(TokenType type)
        {
            if (!Check(type)) return false;
            Advance();
            return true;
        }

        /// <summary>改行を読み飛ばす。</summary>
        public void SkipNewlines()
        {
            while (Current.Type == TokenType.Newline) Advance();
        }

        /// <summary>改行と、字句解析で落とせなかった不明トークンを読み飛ばす。</summary>
        public void SkipNewlinesAndUnknown()
        {
            while (Current.Type == TokenType.Newline || Current.Type == TokenType.Unknown) Advance();
        }

        /// <summary>
        /// 改行をまたいで「{」が続いているかを先読みする。
        /// 「屋台 "たこ焼き"」の次の行に「{」を書くスタイルも受け入れるため。
        /// </summary>
        public bool CheckBlockStart()
        {
            int look = 0;
            while (Peek(look).Type == TokenType.Newline) look++;
            return Peek(look).Type == TokenType.LBrace;
        }

        /// <summary>CheckBlockStart が真のとき、改行を飛ばして「{」を消費する。</summary>
        public Token ConsumeBlockStart()
        {
            SkipNewlines();
            return Advance();   // LBrace
        }

        /// <summary>エラーから立て直す。次の行、またはブロックの終わりまで読み飛ばす。</summary>
        public void SkipToNextStatement()
        {
            while (!IsAtEnd)
            {
                if (Current.Type == TokenType.Newline) { Advance(); return; }
                if (Current.Type == TokenType.RBrace) return;
                Advance();
            }
        }

        public void Error(Token at, string message, string example = null, IReadOnlyList<string> suggestions = null)
        {
            if (_errorCount >= MaxErrors) return;
            _errorCount++;
            Diagnostics.Add(Diagnostic.Error(at.Line, at.Column, at.Length <= 0 ? 1 : at.Length,
                message, example, suggestions));
        }

        public void Warn(Token at, string message, string example = null)
        {
            Diagnostics.Add(Diagnostic.Warning(at.Line, at.Column, at.Length <= 0 ? 1 : at.Length, message, example));
        }
    }
}
