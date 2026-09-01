using System.Collections.Generic;
using Matsuri.Script.Ast;
using Matsuri.Script.Commands;
using Matsuri.Script.Lexing;
using Matsuri.Script.Validation;

namespace Matsuri.Script.Interpreting
{
    /// <summary>
    /// AST を <see cref="FestivalPlan"/> に変換する (§51 / §53)。
    ///
    ///  - トップレベルの 屋台 / 装飾 / 設備 / イベント → ImmediateCommands（RUN した瞬間に建つ）
    ///  - もし / 時間 → TriggerRule（祭りの最中に評価される）。入れ子も NestedRules として保持する。
    ///
    /// ここでは GameObject に一切触れない。触るのは IFestivalCommandSink 側 (§53)。
    /// </summary>
    public static class Interpreter
    {
        public static FestivalPlan Build(FestivalProgram program, IMatsuriCatalog catalog, List<Diagnostic> diagnostics)
        {
            var diags = diagnostics ?? new List<Diagnostic>();

            if (program == null || catalog == null) return FestivalPlan.Failed(diags);

            var immediate = new List<IFestivalCommand>();
            var rules = new List<TriggerRule>();

            BuildNodes(program.Body, catalog, immediate, rules, false);

            long estimated = 0;
            for (int i = 0; i < immediate.Count; i++) estimated += immediate[i].Cost;

            return new FestivalPlan
            {
                ImmediateCommands = immediate,
                Rules = rules,
                Diagnostics = diags,
                FestivalName = string.IsNullOrEmpty(program.Name) ? "MY MATSURI" : program.Name,
                EstimatedCost = estimated
            };
        }

        static void BuildNodes(List<Node> body, IMatsuriCatalog catalog,
            List<IFestivalCommand> commands, List<TriggerRule> rules, bool insideConditional)
        {
            if (body == null) return;

            for (int i = 0; i < body.Count; i++)
            {
                switch (body[i])
                {
                    case StallNode stall:
                    {
                        var cmd = BuildStall(stall, catalog, insideConditional);
                        if (cmd != null) commands.Add(cmd);
                        break;
                    }
                    case DecorationNode decoration:
                    {
                        var cmd = BuildDecoration(decoration, catalog);
                        if (cmd != null) commands.Add(cmd);
                        break;
                    }
                    case FacilityNode facility:
                    {
                        var cmd = BuildFacility(facility, catalog);
                        if (cmd != null) commands.Add(cmd);
                        break;
                    }
                    case EventNode ev:
                    {
                        var cmd = BuildEvent(ev, catalog);
                        if (cmd != null) commands.Add(cmd);
                        break;
                    }
                    case IfNode ifNode:
                    {
                        var condition = ConditionBuilder.Build(ifNode.Condition, catalog);
                        if (condition == null) break;      // 読めない条件は Validator が報告済み
                        rules.Add(BuildRule(condition, ifNode.Body, ifNode.Line, catalog));
                        break;
                    }
                    case TimeNode timeNode:
                    {
                        var condition = new TimeCondition { MinutesOfDay = timeNode.MinutesOfDay };
                        rules.Add(BuildRule(condition, timeNode.Body, timeNode.Line, catalog));
                        break;
                    }
                }
            }
        }

        static TriggerRule BuildRule(ICondition condition, List<Node> body, int line, IMatsuriCatalog catalog)
        {
            var innerCommands = new List<IFestivalCommand>();
            var nested = new List<TriggerRule>();
            BuildNodes(body, catalog, innerCommands, nested, true);

            return new TriggerRule
            {
                Condition = condition,
                Body = innerCommands,
                NestedRules = nested,
                Once = true,
                SourceLine = line
            };
        }

        // ── 屋台 ─────────────────────────────────────────────────
        static IFestivalCommand BuildStall(StallNode node, IMatsuriCatalog catalog, bool insideConditional)
        {
            if (!catalog.TryResolve(node.Name, MatsuriEntryKind.Stall, out CatalogEntry entry)) return null;

            var positionProperty = Validator.FindProperty(node.Properties, "場所");
            var priceProperty = Validator.FindProperty(node.Properties, "値段");

            // 「もし」「時間」の中で、「場所」が無く「値段」だけある屋台ブロックは、
            // 「祭りの最中に値段を変える」命令として扱う (§16)。
            if (positionProperty == null)
            {
                if (!insideConditional) return null;   // トップレベルの書き忘れは Validator が指摘済み
                if (priceProperty == null || priceProperty.Numbers.Count == 0) return null;
                return new SetPriceCommand
                {
                    StallId = entry.Id,
                    SourceName = DisplayName(node.Properties, node.Name),
                    Price = ClampPrice(entry, (int)System.Math.Round(priceProperty.Numbers[0])),
                    SourceLine = node.Line
                };
            }

            if (positionProperty.Numbers.Count < 2) return null;

            int? price = null;
            if (priceProperty != null && priceProperty.Numbers.Count > 0)
                price = ClampPrice(entry, (int)System.Math.Round(priceProperty.Numbers[0]));

            return new CreateStallCommand
            {
                StallId = entry.Id,
                SourceName = DisplayName(node.Properties, node.Name),
                Position = ToGrid(positionProperty),
                Price = price,
                RotationDegrees = ReadRotation(node.Properties),
                SourceLine = node.Line,
                Cost = entry.BuildCost
            };
        }

        // ── 装飾 ─────────────────────────────────────────────────
        static IFestivalCommand BuildDecoration(DecorationNode node, IMatsuriCatalog catalog)
        {
            if (!catalog.TryResolve(node.Name, MatsuriEntryKind.Decoration, out CatalogEntry entry)) return null;

            var position = Validator.FindProperty(node.Properties, "場所");
            if (position == null || position.Numbers.Count < 2) return null;

            return new CreateDecorationCommand
            {
                DecorationId = entry.Id,
                SourceName = DisplayName(node.Properties, node.Name),
                Position = ToGrid(position),
                RotationDegrees = ReadRotation(node.Properties),
                SourceLine = node.Line,
                Cost = entry.BuildCost
            };
        }

        // ── 設備 ─────────────────────────────────────────────────
        static IFestivalCommand BuildFacility(FacilityNode node, IMatsuriCatalog catalog)
        {
            if (!catalog.TryResolve(node.Name, MatsuriEntryKind.Facility, out CatalogEntry entry)) return null;

            var position = Validator.FindProperty(node.Properties, "場所");
            if (position == null || position.Numbers.Count < 2) return null;

            return new CreateFacilityCommand
            {
                FacilityId = entry.Id,
                SourceName = DisplayName(node.Properties, node.Name),
                Position = ToGrid(position),
                RotationDegrees = ReadRotation(node.Properties),
                SourceLine = node.Line,
                Cost = entry.BuildCost
            };
        }

        // ── イベント ─────────────────────────────────────────────
        static IFestivalCommand BuildEvent(EventNode node, IMatsuriCatalog catalog)
        {
            if (!catalog.TryResolve(node.Name, MatsuriEntryKind.Event, out CatalogEntry entry)) return null;

            var position = Validator.FindProperty(node.Properties, "場所");
            GridPos? where = (position != null && position.Numbers.Count >= 2) ? ToGrid(position) : (GridPos?)null;

            switch (entry.Id)
            {
                case MatsuriIds.Fireworks:
                    return new StartFireworksCommand
                    {
                        Kind = MatsuriKeywords.ResolveFireworkKind(node.Argument),
                        SourceName = string.IsNullOrEmpty(node.Argument) ? entry.DisplayName : node.Argument,
                        SourceLine = node.Line,
                        Cost = entry.BuildCost
                    };

                case MatsuriIds.BonOdori:
                    return new StartBonOdoriCommand
                    {
                        Position = where,
                        SourceLine = node.Line,
                        Cost = entry.BuildCost
                    };

                case MatsuriIds.Taiko:
                    return new StartTaikoCommand
                    {
                        Position = where,
                        SourceLine = node.Line,
                        Cost = entry.BuildCost
                    };
            }

            return null;
        }

        // ── 小道具 ───────────────────────────────────────────────
        static GridPos ToGrid(PropertyNode position)
            => new GridPos((float)position.Numbers[0], (float)position.Numbers[1]);

        static float ReadRotation(List<PropertyNode> properties)
        {
            var rotation = Validator.FindProperty(properties, "向き");
            if (rotation == null || rotation.Numbers.Count == 0) return 0f;
            return (float)rotation.Numbers[0];
        }

        /// <summary>「名前」で表示名を上書きできる。書かれていなければ、そのまま書かれた名前を使う。</summary>
        static string DisplayName(List<PropertyNode> properties, string fallback)
        {
            var nameProperty = Validator.FindProperty(properties, "名前");
            if (nameProperty != null && !string.IsNullOrEmpty(nameProperty.Text)) return nameProperty.Text;
            return fallback;
        }

        static int ClampPrice(CatalogEntry entry, int price)
        {
            if (entry.MaxPrice <= 0) return price;
            if (price < entry.MinPrice) return entry.MinPrice;
            if (price > entry.MaxPrice) return entry.MaxPrice;
            return price;
        }
    }
}
