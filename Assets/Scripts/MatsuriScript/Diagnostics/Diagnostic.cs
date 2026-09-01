using System.Collections.Generic;

namespace Matsuri.Script
{
    /// <summary>診断の重大度。</summary>
    public enum DiagnosticSeverity
    {
        Error,
        Warning,
        Info
    }

    /// <summary>
    /// 仕様書 §41 / §42 に基づく、プレイヤー向けの分かりやすいエラー情報。
    /// 「SyntaxError token 402」のような機械的メッセージは作らない。
    /// 必ず「何行目の」「何が」「どう直せばよいか」を日本語で持つ。
    /// </summary>
    public sealed class Diagnostic
    {
        /// <summary>1始まりの行番号。</summary>
        public int Line { get; }

        /// <summary>1始まりの列番号。</summary>
        public int Column { get; }

        /// <summary>下線を引く長さ（文字数）。0以下なら行全体。</summary>
        public int Length { get; }

        public DiagnosticSeverity Severity { get; }

        /// <summary>「『場所』が設定されていません。」のような一文。</summary>
        public string Message { get; }

        /// <summary>直し方を示す短いコード例。無い場合は null。</summary>
        public string Example { get; }

        /// <summary>「もしかして: たこ焼き」のような候補。無い場合は空。</summary>
        public IReadOnlyList<string> Suggestions { get; }

        public Diagnostic(
            DiagnosticSeverity severity,
            int line,
            int column,
            int length,
            string message,
            string example = null,
            IReadOnlyList<string> suggestions = null)
        {
            Severity = severity;
            Line = line < 1 ? 1 : line;
            Column = column < 1 ? 1 : column;
            Length = length;
            Message = message ?? string.Empty;
            Example = example;
            Suggestions = suggestions ?? System.Array.Empty<string>();
        }

        public static Diagnostic Error(int line, int column, int length, string message,
            string example = null, IReadOnlyList<string> suggestions = null)
            => new Diagnostic(DiagnosticSeverity.Error, line, column, length, message, example, suggestions);

        public static Diagnostic Warning(int line, int column, int length, string message,
            string example = null, IReadOnlyList<string> suggestions = null)
            => new Diagnostic(DiagnosticSeverity.Warning, line, column, length, message, example, suggestions);

        public static Diagnostic Info(int line, int column, int length, string message,
            string example = null)
            => new Diagnostic(DiagnosticSeverity.Info, line, column, length, message, example);

        /// <summary>UI のエラーパネル表示用の整形済みテキスト。</summary>
        public string ToDisplayString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Line).Append("行目\n").Append(Message);
            if (Suggestions.Count > 0)
            {
                sb.Append("\nもしかして: ");
                for (int i = 0; i < Suggestions.Count; i++)
                {
                    if (i > 0) sb.Append(" / ");
                    sb.Append(Suggestions[i]);
                }
            }
            if (!string.IsNullOrEmpty(Example))
            {
                sb.Append("\n例:\n").Append(Example);
            }
            return sb.ToString();
        }

        public override string ToString() => $"[{Severity}] L{Line}:{Column} {Message}";
    }
}
