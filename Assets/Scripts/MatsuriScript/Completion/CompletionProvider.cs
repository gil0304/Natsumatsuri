using System.Collections.Generic;
using Matsuri.Script.Lexing;

namespace Matsuri.Script.Completion
{
    /// <summary>
    /// コード補完 (§43)。
    ///
    ///   「屋」        → 屋台
    ///   「屋台 "」    → カタログの屋台名がぜんぶ出る
    ///   「装飾 "」    → 装飾名、「設備 "」→ 設備名
    ///   { } の中      → 場所 / 値段 / 向き / 名前
    ///   「もし 」     → 来場者数 / 売上 / 満足度 …
    ///   「たこ焼き.」 → 待ち人数 / 売上 / 軒数
    ///   さらに、屋台まるごとのひな形（スニペット）も出す。
    ///
    /// 「思い出せなくても書ける」ことが目的なので、前方一致だけでなく部分一致でも拾う。
    /// </summary>
    public static class CompletionProvider
    {
        public static List<CompletionItem> GetCompletions(string source, int caretIndex, IMatsuriCatalog catalog)
        {
            var items = new List<CompletionItem>();
            var ctx = CompletionContext.Analyze(source, caretIndex);

            switch (ctx.Place)
            {
                case CaretPlace.Comment:
                    return items;

                case CaretPlace.InString:
                    AddStringCandidates(items, ctx, catalog);
                    break;

                case CaretPlace.StallMetric:
                    AddAll(items, MatsuriKeywords.StallMetricKeywords, CompletionKind.Metric, "屋台について見られる数");
                    break;

                case CaretPlace.Condition:
                    AddAll(items, MatsuriKeywords.MetricKeywords, CompletionKind.Metric, "祭りの様子を見る数");
                    AddStallNamesForCondition(items, catalog);
                    break;

                case CaretPlace.Property:
                    AddProperties(items, ctx, catalog);
                    break;

                default:
                    AddStatementKeywords(items);
                    AddSnippets(items, catalog);
                    break;
            }

            return Filter(items, ctx.Word);
        }

        // ── "…" の中 ─────────────────────────────────────────────
        static void AddStringCandidates(List<CompletionItem> items, CompletionContext ctx, IMatsuriCatalog catalog)
        {
            switch (ctx.StringOwner)
            {
                case KeywordKind.Stall:
                    AddCatalog(items, catalog, MatsuriEntryKind.Stall, CompletionKind.StallName);
                    return;

                case KeywordKind.Decoration:
                    AddCatalog(items, catalog, MatsuriEntryKind.Decoration, CompletionKind.DecorationName);
                    return;

                case KeywordKind.Facility:
                    AddCatalog(items, catalog, MatsuriEntryKind.Facility, CompletionKind.FacilityName);
                    return;

                case KeywordKind.Event:
                    AddCatalog(items, catalog, MatsuriEntryKind.Event, CompletionKind.EventName);
                    return;

                case KeywordKind.Fireworks:
                    for (int i = 0; i < MatsuriKeywords.FireworkKindNames.Length; i++)
                    {
                        string kindName = MatsuriKeywords.FireworkKindNames[i];
                        items.Add(new CompletionItem(kindName, kindName, CompletionKind.EventName, "花火の種類"));
                    }
                    return;

                case KeywordKind.Name:
                    return;   // 自由に付けてよい名前なので候補は出さない

                default:
                    // 「祭り "」など。名前は自由。何も出さない。
                    return;
            }
        }

        static void AddCatalog(List<CompletionItem> items, IMatsuriCatalog catalog,
            MatsuriEntryKind kind, CompletionKind completionKind)
        {
            if (catalog == null) return;
            var all = catalog.GetAll(kind);
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                var entry = all[i];
                items.Add(new CompletionItem(entry.DisplayName, entry.DisplayName, completionKind, DescribeEntry(entry)));
            }
        }

        static string DescribeEntry(CatalogEntry entry)
        {
            if (entry.Kind == MatsuriEntryKind.Stall && entry.DefaultPrice > 0)
                return $"建設 {ScriptText.Yen(entry.BuildCost)}円 / 標準の値段 {ScriptText.Yen(entry.DefaultPrice)}円";
            if (entry.BuildCost > 0)
                return $"建設 {ScriptText.Yen(entry.BuildCost)}円";
            return null;
        }

        // ── 条件 ─────────────────────────────────────────────────
        static void AddStallNamesForCondition(List<CompletionItem> items, IMatsuriCatalog catalog)
        {
            if (catalog == null) return;
            var all = catalog.GetAll(MatsuriEntryKind.Stall);
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                string name = all[i].DisplayName;
                items.Add(new CompletionItem($"{name}.待ち人数", $"{name}.待ち人数", CompletionKind.Metric,
                    "その屋台に並んでいる人数"));
            }
        }

        // ── { } の中 ─────────────────────────────────────────────
        static void AddProperties(List<CompletionItem> items, CompletionContext ctx, IMatsuriCatalog catalog)
        {
            items.Add(new CompletionItem("場所", "場所 0, 0", CompletionKind.Property, "会場のどこに置くか（X, Z）"));

            if (ctx.BlockOwner == KeywordKind.Stall)
                items.Add(new CompletionItem("値段", "値段 500", CompletionKind.Property, "1回いくらで売るか"));

            items.Add(new CompletionItem("向き", "向き 90", CompletionKind.Property, "何度まわして置くか"));
            items.Add(new CompletionItem("名前", "名前 \"\"", CompletionKind.Property, "表示される名前を変える"));
        }

        // ── 行のあたま ───────────────────────────────────────────
        static void AddStatementKeywords(List<CompletionItem> items)
        {
            items.Add(new CompletionItem("屋台", "屋台 ", CompletionKind.Keyword, "食べ物や遊びの店を建てる"));
            items.Add(new CompletionItem("装飾", "装飾 ", CompletionKind.Keyword, "提灯やのぼりで飾る"));
            items.Add(new CompletionItem("設備", "設備 ", CompletionKind.Keyword, "ベンチやゴミ箱を置く"));
            items.Add(new CompletionItem("イベント", "イベント ", CompletionKind.Keyword, "花火や盆踊りを起こす"));
            items.Add(new CompletionItem("もし", "もし ", CompletionKind.Keyword, "条件が成り立ったときだけ実行する"));
            items.Add(new CompletionItem("時間", "時間 19:00 ", CompletionKind.Keyword, "決まった時刻に実行する"));
            items.Add(new CompletionItem("花火", "花火 \"大玉\"", CompletionKind.Keyword, "花火を打ち上げる"));
            items.Add(new CompletionItem("盆踊り", "盆踊り", CompletionKind.Keyword, "やぐらで盆踊りを始める"));
            items.Add(new CompletionItem("太鼓", "太鼓", CompletionKind.Keyword, "太鼓の演奏を始める"));
            items.Add(new CompletionItem("祭り", "祭り \"夏の宴\" {\n}\n", CompletionKind.Keyword, "祭り全体に名前をつける"));
        }

        // ── ひな形 ───────────────────────────────────────────────
        static void AddSnippets(List<CompletionItem> items, IMatsuriCatalog catalog)
        {
            string stallName = FirstName(catalog, MatsuriEntryKind.Stall, "たこ焼き");
            string decorationName = FirstName(catalog, MatsuriEntryKind.Decoration, "提灯");
            string facilityName = FirstName(catalog, MatsuriEntryKind.Facility, "ベンチ");

            items.Add(new CompletionItem(
                "屋台のひな形",
                $"屋台 \"{stallName}\" {{\n    場所 0, 0\n    値段 500\n}}\n",
                CompletionKind.Snippet,
                "屋台をまるごと1つ書く"));

            items.Add(new CompletionItem(
                "装飾のひな形",
                $"装飾 \"{decorationName}\" {{\n    場所 0, 0\n}}\n",
                CompletionKind.Snippet,
                "装飾をまるごと1つ書く"));

            items.Add(new CompletionItem(
                "設備のひな形",
                $"設備 \"{facilityName}\" {{\n    場所 0, 0\n}}\n",
                CompletionKind.Snippet,
                "設備をまるごと1つ書く"));

            items.Add(new CompletionItem(
                "時間のひな形",
                "時間 19:00 {\n    盆踊り\n}\n",
                CompletionKind.Snippet,
                "決まった時刻に何かを起こす"));

            items.Add(new CompletionItem(
                "もしのひな形",
                $"もし 来場者数 > 500 {{\n    屋台 \"{stallName}\" {{ 場所 20, 10 }}\n}}\n",
                CompletionKind.Snippet,
                "様子を見て、祭りの最中に手を打つ"));
        }

        static string FirstName(IMatsuriCatalog catalog, MatsuriEntryKind kind, string fallback)
        {
            if (catalog == null) return fallback;
            var all = catalog.GetAll(kind);
            return (all != null && all.Count > 0) ? all[0].DisplayName : fallback;
        }

        // ── 絞り込み ─────────────────────────────────────────────
        static void AddAll(List<CompletionItem> items, IReadOnlyList<string> words, CompletionKind kind, string detail)
        {
            for (int i = 0; i < words.Count; i++)
                items.Add(new CompletionItem(words[i], words[i], kind, detail));
        }

        /// <summary>
        /// 打ちかけの語で絞る。前方一致を先に、部分一致をあとに並べる。
        /// 何も打っていなければ全部返す。
        /// </summary>
        static List<CompletionItem> Filter(List<CompletionItem> items, string word)
        {
            if (string.IsNullOrEmpty(word)) return items;

            var exact = new List<CompletionItem>();
            var partial = new List<CompletionItem>();

            for (int i = 0; i < items.Count; i++)
            {
                if (ScriptText.StartsWith(items[i].Label, word)) exact.Add(items[i]);
                else if (ScriptText.Contains(items[i].Label, word)) partial.Add(items[i]);
            }

            exact.AddRange(partial);
            return exact;
        }
    }
}
