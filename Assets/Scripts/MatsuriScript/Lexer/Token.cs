namespace Matsuri.Script.Lexing
{
    /// <summary>Matsuri Script の字句の種類 (§51)。</summary>
    public enum TokenType
    {
        /// <summary>「屋台」「たこ焼き」「visitors」など。日本語の連なりは1トークン。</summary>
        Identifier,

        /// <summary>"たこ焼き" / 「たこ焼き」 の中身。</summary>
        String,

        /// <summary>500 / -20 / 3.5</summary>
        Number,

        /// <summary>19:00 のような時刻。Number には「その日の分」(19*60=1140) が入る。</summary>
        Time,

        LBrace,
        RBrace,
        Comma,
        Colon,

        Greater,
        GreaterEqual,
        Less,
        LessEqual,
        EqualEqual,
        NotEqual,

        Dot,

        /// <summary>かつ / and</summary>
        And,

        /// <summary>または / or</summary>
        Or,

        /// <summary>改行。文の区切りとして意味を持つ。</summary>
        Newline,

        EndOfFile,

        /// <summary>読めなかった文字。Diagnostic が必ず一緒に出る。</summary>
        Unknown
    }

    /// <summary>字句1個。エラー表示のために行・列・長さを必ず持つ (§41)。</summary>
    public readonly struct Token
    {
        public readonly TokenType Type;

        /// <summary>ソースに書かれていた文字列。String の場合はクォートを除いた中身。</summary>
        public readonly string Text;

        /// <summary>Number / Time のときの数値。</summary>
        public readonly double Number;

        /// <summary>1始まりの行番号。</summary>
        public readonly int Line;

        /// <summary>1始まりの列番号。</summary>
        public readonly int Column;

        /// <summary>ソース上での文字数（下線を引く長さ）。</summary>
        public readonly int Length;

        public Token(TokenType type, string text, double number, int line, int column, int length)
        {
            Type = type;
            Text = text ?? string.Empty;
            Number = number;
            Line = line < 1 ? 1 : line;
            Column = column < 1 ? 1 : column;
            Length = length < 0 ? 0 : length;
        }

        public static Token Simple(TokenType type, string text, int line, int column)
            => new Token(type, text, 0.0, line, column, text?.Length ?? 0);

        public static Token EndOfFile(int line, int column)
            => new Token(TokenType.EndOfFile, "", 0.0, line, column, 0);

        public bool Is(TokenType type) => Type == type;

        /// <summary>比較演算子かどうか。</summary>
        public bool IsCompareOperator
            => Type == TokenType.Greater || Type == TokenType.GreaterEqual
            || Type == TokenType.Less || Type == TokenType.LessEqual
            || Type == TokenType.EqualEqual || Type == TokenType.NotEqual;

        /// <summary>エラーメッセージに埋め込むための、人間に読める字句の説明。</summary>
        public string DisplayText => Type switch
        {
            TokenType.Newline   => "改行",
            TokenType.EndOfFile => "コードの終わり",
            TokenType.String    => "\"" + Text + "\"",
            _ => string.IsNullOrEmpty(Text) ? "?" : Text
        };

        public override string ToString() => $"{Type}('{Text}') L{Line}:{Column}";
    }
}
