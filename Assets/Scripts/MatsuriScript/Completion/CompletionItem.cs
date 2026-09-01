namespace Matsuri.Script.Completion
{
    /// <summary>補完候補の種類 (§43)。UI はこれでアイコンと色を変える。</summary>
    public enum CompletionKind
    {
        Keyword,
        StallName,
        DecorationName,
        FacilityName,
        EventName,
        Property,
        Metric,
        Snippet
    }

    /// <summary>補完候補1件。</summary>
    public readonly struct CompletionItem
    {
        /// <summary>候補リストに出す文字。</summary>
        public readonly string Label;

        /// <summary>実際に挿入される文字。改行を含むひな形もある。</summary>
        public readonly string InsertText;

        public readonly CompletionKind Kind;

        /// <summary>右側に薄く出す説明。「500円 / 建設 80,000円」など。</summary>
        public readonly string Detail;

        public CompletionItem(string label, string insertText, CompletionKind kind, string detail = null)
        {
            Label = label ?? string.Empty;
            InsertText = insertText ?? Label ?? string.Empty;
            Kind = kind;
            Detail = detail;
        }

        public override string ToString() => $"{Label} ({Kind})";
    }
}
