using System.Collections.Generic;
using Matsuri.Script.Ast;
using Matsuri.Script.Lexing;

namespace Matsuri.Script.Validation
{
    /// <summary>
    /// 意味の検証 (§41)。構文が正しくても「その屋台は無い」「場所が抜けている」
    /// 「敷地の外」「予算オーバー」を、日本語で・行番号つきで・直し方つきで指摘する。
    ///
    /// エラーメッセージの原則:
    ///   1. 何行目か   2. 何が悪いか   3. どう直すか（Example）
    ///   「SyntaxError token 402」のような書き方は絶対にしない。
    /// </summary>
    public static class Validator
    {
        /// <summary>祭りの開始時刻（分）。17:00。Matsuri.TimeSystem.FestivalClock と同じ値。</summary>
        public const int FestivalStartMinutes = 17 * 60;

        /// <summary>祭りの終了時刻（分）。22:00。</summary>
        public const int FestivalEndMinutes = 22 * 60;

        public static void Validate(FestivalProgram program, IMatsuriCatalog catalog, List<Diagnostic> diagnostics)
        {
            if (program == null) return;
            var diags = diagnostics ?? new List<Diagnostic>();

            if (catalog == null)
            {
                diags.Add(Diagnostic.Error(1, 1, 1,
                    "祭りのデータが読み込まれていないため、コードを確かめられませんでした。",
                    null));
                return;
            }

            var ctx = new ValidationContext(catalog, diags);
            VisitAll(program.Body, ctx, false);

            if (catalog.InitialBudget > 0 && ctx.UnconditionalCost > catalog.InitialBudget)
            {
                diags.Add(Diagnostic.Warning(1, 1, 1,
                    $"合計 {ScriptText.Yen(ctx.UnconditionalCost)}円 は予算 {ScriptText.Yen(catalog.InitialBudget)}円 を超えています。"
                    + "このままだと、あとの方に書いた物が建たないかもしれません。",
                    "屋台をへらすか、値段のやすい装飾にしてみましょう。"));
            }
        }

        static void VisitAll(List<Node> body, ValidationContext ctx, bool insideConditional)
        {
            if (body == null) return;
            for (int i = 0; i < body.Count; i++) Visit(body[i], ctx, insideConditional);
        }

        static void Visit(Node node, ValidationContext ctx, bool insideConditional)
        {
            switch (node)
            {
                case StallNode stall:
                    ValidateEntity(stall, stall.Name, stall.Properties, MatsuriEntryKind.Stall, ctx, insideConditional);
                    break;

                case DecorationNode decoration:
                    ValidateEntity(decoration, decoration.Name, decoration.Properties, MatsuriEntryKind.Decoration, ctx, insideConditional);
                    break;

                case FacilityNode facility:
                    ValidateEntity(facility, facility.Name, facility.Properties, MatsuriEntryKind.Facility, ctx, insideConditional);
                    break;

                case EventNode ev:
                    ValidateEvent(ev, ctx, insideConditional);
                    break;

                case IfNode ifNode:
                    ConditionValidator.Validate(ifNode.Condition, ctx);
                    if (ifNode.Body.Count == 0)
                    {
                        ctx.Warn(ifNode, "「もし」の中に何も書かれていません。条件が成り立っても何も起きません。",
                            "もし 来場者数 > 500 {\n    屋台 \"焼きそば\" { 場所 20, 10 }\n}");
                    }
                    VisitAll(ifNode.Body, ctx, true);
                    break;

                case TimeNode timeNode:
                    ValidateTime(timeNode, ctx);
                    VisitAll(timeNode.Body, ctx, true);
                    break;
            }
        }

        // ── 屋台 / 装飾 / 設備 ───────────────────────────────────
        static void ValidateEntity(Node node, string writtenName, List<PropertyNode> properties,
            MatsuriEntryKind kind, ValidationContext ctx, bool insideConditional)
        {
            string label = KindLabel(kind);
            var catalog = ctx.Catalog;

            bool resolved = catalog.TryResolve(writtenName, kind, out CatalogEntry entry);
            if (!resolved)
            {
                // 種類の取り違え（「屋台 "ベンチ"」）は、それ専用の案内を出す
                if (catalog.TryResolveAny(writtenName, out CatalogEntry other) && other.IsValid)
                {
                    string otherLabel = KindLabel(other.Kind);
                    ctx.Error(node,
                        $"「{writtenName}」は{label}ではなく{otherLabel}です。「{otherLabel} \"{other.DisplayName}\"」と書いてください。",
                        $"{otherLabel} \"{other.DisplayName}\" {{\n    場所 5, 5\n}}");
                }
                else
                {
                    ctx.Error(node,
                        $"{label}「{writtenName}」は見つかりません。",
                        BuildKindExample(kind, catalog),
                        catalog.SuggestNames(writtenName, kind, 3));
                }
            }

            // 「場所」が無く「値段」だけ書かれた屋台ブロックは、値段の変更命令として扱う (§16)。
            //   もし 売上 > 300000 { 屋台 "たこ焼き" { 値段 400 } }
            //（トップレベルは「建てる」意味しかないので、そこでは「場所」を必ず求める）
            bool isPriceChange = insideConditional
                                 && kind == MatsuriEntryKind.Stall
                                 && FindProperty(properties, "場所") == null
                                 && FindProperty(properties, "値段") != null;

            if (!isPriceChange) ValidatePosition(node, writtenName, properties, kind, ctx);
            ValidatePrice(node, writtenName, properties, kind, entry, resolved, ctx);
            ValidateRotation(node, properties, ctx);

            // 値段の変更は建設ではないので費用はかからない
            if (resolved && !insideConditional && !isPriceChange) ctx.UnconditionalCost += entry.BuildCost;
        }

        static void ValidatePosition(Node node, string writtenName, List<PropertyNode> properties,
            MatsuriEntryKind kind, ValidationContext ctx)
        {
            string label = KindLabel(kind);
            PropertyNode position = FindProperty(properties, "場所");

            if (position == null)
            {
                ctx.Error(node,
                    $"{label}「{writtenName}」に「場所」が設定されていません。どこに置くかを書いてください。",
                    $"{label} \"{writtenName}\" {{\n    場所 5, 5\n}}");
                return;
            }

            if (position.Numbers.Count < 2) return;   // 数の不足は Parser が指摘済み

            double x = position.Numbers[0];
            double z = position.Numbers[1];
            var bounds = ctx.Catalog.Bounds;

            if (!bounds.Contains((float)x, (float)z))
            {
                ctx.Error(position,
                    $"場所 ({Num(x)}, {Num(z)}) は会場の外です。{bounds} の中に置いてください。",
                    "場所 5, 10");
                return;
            }

            if (!ctx.TryOccupy(x, z, writtenName, out string existing))
            {
                ctx.Warn(position,
                    $"({Num(x)}, {Num(z)}) にはすでに「{existing}」が置かれています。少しずらして置いてください。",
                    "場所 8, 10");
            }
        }

        static void ValidatePrice(Node node, string writtenName, List<PropertyNode> properties,
            MatsuriEntryKind kind, CatalogEntry entry, bool resolved, ValidationContext ctx)
        {
            PropertyNode price = FindProperty(properties, "値段");
            if (price == null || price.Numbers.Count == 0) return;

            if (kind != MatsuriEntryKind.Stall)
            {
                ctx.Warn(price,
                    $"{KindLabel(kind)}「{writtenName}」に「値段」はつけられません。この行は無視されます。",
                    "装飾 \"提灯\" {\n    場所 3, 4\n}");
                return;
            }

            if (!resolved || entry.MaxPrice <= 0) return;

            int value = (int)System.Math.Round(price.Numbers[0]);

            if (value < entry.MinPrice)
            {
                ctx.Warn(price,
                    $"値段 {ScriptText.Yen(value)}円 は「{writtenName}」には安すぎます。"
                    + $"{ScriptText.Yen(entry.MinPrice)}円〜{ScriptText.Yen(entry.MaxPrice)}円 のあいだにしてください。",
                    $"値段 {entry.DefaultPrice}");
            }
            else if (value > entry.MaxPrice)
            {
                ctx.Warn(price,
                    $"値段 {ScriptText.Yen(value)}円 は「{writtenName}」には高すぎます。"
                    + $"{ScriptText.Yen(entry.MinPrice)}円〜{ScriptText.Yen(entry.MaxPrice)}円 のあいだにしてください。",
                    $"値段 {entry.DefaultPrice}");
            }
        }

        static void ValidateRotation(Node node, List<PropertyNode> properties, ValidationContext ctx)
        {
            PropertyNode rotation = FindProperty(properties, "向き");
            if (rotation == null || rotation.Numbers.Count == 0) return;

            double deg = rotation.Numbers[0];
            if (deg < -360.0 || deg > 360.0)
            {
                ctx.Warn(rotation,
                    $"向き {Num(deg)} は回りすぎです。0〜360 の角度で書いてください。",
                    "向き 90");
            }
        }

        // ── イベント ─────────────────────────────────────────────
        static void ValidateEvent(EventNode ev, ValidationContext ctx, bool insideConditional)
        {
            bool resolved = ctx.Catalog.TryResolve(ev.Name, MatsuriEntryKind.Event, out CatalogEntry entry);
            if (!resolved)
            {
                ctx.Error(ev,
                    $"イベント「{ev.Name}」は見つかりません。",
                    BuildKindExample(MatsuriEntryKind.Event, ctx.Catalog),
                    ctx.Catalog.SuggestNames(ev.Name, MatsuriEntryKind.Event, 3));
                return;
            }

            if (entry.Id == MatsuriIds.Fireworks && !string.IsNullOrEmpty(ev.Argument)
                && !MatsuriKeywords.TryFireworkKind(ev.Argument, out _))
            {
                ctx.Warn(ev,
                    $"花火に「{ev.Argument}」という種類はありません。菊 / 牡丹 / 柳 / ハート / 大玉 / スペシャル が使えます。",
                    "花火 \"大玉\"");
            }

            PropertyNode position = FindProperty(ev.Properties, "場所");
            if (position != null && position.Numbers.Count >= 2)
            {
                double x = position.Numbers[0];
                double z = position.Numbers[1];
                if (!ctx.Catalog.Bounds.Contains((float)x, (float)z))
                {
                    ctx.Error(position,
                        $"場所 ({Num(x)}, {Num(z)}) は会場の外です。{ctx.Catalog.Bounds} の中に置いてください。",
                        "場所 0, 0");
                }
            }

            if (!insideConditional) ctx.UnconditionalCost += entry.BuildCost;
        }

        // ── 時間 ─────────────────────────────────────────────────
        static void ValidateTime(TimeNode node, ValidationContext ctx)
        {
            if (node.MinutesOfDay < FestivalStartMinutes || node.MinutesOfDay > FestivalEndMinutes)
            {
                ctx.Error(node,
                    $"{ScriptText.ClockText(node.MinutesOfDay)} には祭りが開いていません。"
                    + $"時刻は {ScriptText.ClockText(FestivalStartMinutes)}〜{ScriptText.ClockText(FestivalEndMinutes)} のあいだで書いてください。",
                    "時間 19:00 {\n    盆踊り\n}");
            }

            if (node.Body.Count == 0)
            {
                ctx.Warn(node,
                    $"{ScriptText.ClockText(node.MinutesOfDay)} の「{{ }}」の中に何も書かれていません。その時刻に何も起きません。",
                    "時間 19:00 {\n    盆踊り\n}");
            }
        }

        // ── 小道具 ───────────────────────────────────────────────
        internal static PropertyNode FindProperty(List<PropertyNode> properties, string canonicalKeyword)
        {
            if (properties == null) return null;
            for (int i = 0; i < properties.Count; i++)
                if (properties[i] != null && properties[i].Keyword == canonicalKeyword) return properties[i];
            return null;
        }

        internal static string KindLabel(MatsuriEntryKind kind) => kind switch
        {
            MatsuriEntryKind.Stall      => "屋台",
            MatsuriEntryKind.Decoration => "装飾",
            MatsuriEntryKind.Facility   => "設備",
            MatsuriEntryKind.Event      => "イベント",
            _ => "もの"
        };

        static string Num(double v)
            => v == System.Math.Floor(v)
                ? ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>「こう書けば動く」を、そのカタログに実在する名前で作る。</summary>
        static string BuildKindExample(MatsuriEntryKind kind, IMatsuriCatalog catalog)
        {
            var all = catalog.GetAll(kind);
            string sample = (all != null && all.Count > 0) ? all[0].DisplayName : "たこ焼き";
            string label = KindLabel(kind);

            if (kind == MatsuriEntryKind.Event) return $"イベント \"{sample}\"";
            if (kind == MatsuriEntryKind.Stall) return $"屋台 \"{sample}\" {{\n    場所 5, 5\n    値段 500\n}}";
            return $"{label} \"{sample}\" {{\n    場所 5, 5\n}}";
        }
    }
}
