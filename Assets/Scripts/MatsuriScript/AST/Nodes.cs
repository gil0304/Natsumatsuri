using System.Collections.Generic;

namespace Matsuri.Script.Ast
{
    /// <summary>
    /// 構文木の基底 (§52)。すべてのノードは、エラー表示のために
    /// ソース上の位置（行・列・長さ）を持つ。
    /// </summary>
    public abstract class Node
    {
        public int Line = 1;
        public int Column = 1;
        public int Length;

        public Node SetPosition(int line, int column, int length)
        {
            Line = line;
            Column = column;
            Length = length;
            return this;
        }

        /// <summary>ログ・テスト用の短い説明。</summary>
        public abstract string Describe();
    }

    /// <summary>
    /// 1本のスクリプト全体。「祭り "夏の宴" { }」で囲っても、囲わなくても同じ形になる (§11)。
    /// </summary>
    public sealed class FestivalProgram : Node
    {
        public string Name = "MY MATSURI";
        public List<Node> Body = new List<Node>();

        public override string Describe() => $"祭り「{Name}」({Body.Count}個の命令)";
    }

    /// <summary>「屋台 "たこ焼き" { 場所 5, 10  値段 500 }」</summary>
    public sealed class StallNode : Node
    {
        public string Name = string.Empty;
        public List<PropertyNode> Properties = new List<PropertyNode>();

        public override string Describe() => $"屋台「{Name}」";
    }

    /// <summary>「装飾 "提灯" { 場所 3, 4 }」</summary>
    public sealed class DecorationNode : Node
    {
        public string Name = string.Empty;
        public List<PropertyNode> Properties = new List<PropertyNode>();

        public override string Describe() => $"装飾「{Name}」";
    }

    /// <summary>「設備 "ベンチ" { 場所 8, 2 }」</summary>
    public sealed class FacilityNode : Node
    {
        public string Name = string.Empty;
        public List<PropertyNode> Properties = new List<PropertyNode>();

        public override string Describe() => $"設備「{Name}」";
    }

    /// <summary>
    /// 「イベント "花火" { }」「花火 "大玉"」「盆踊り」「太鼓」。
    /// Name は "花火"/"盆踊り"/"太鼓"、Argument は「大玉」のような種類 (§22 / §61)。
    /// </summary>
    public sealed class EventNode : Node
    {
        public string Name = string.Empty;
        public string Argument;
        public List<PropertyNode> Properties = new List<PropertyNode>();

        public override string Describe()
            => string.IsNullOrEmpty(Argument) ? $"イベント「{Name}」" : $"イベント「{Name}」({Argument})";
    }

    /// <summary>
    /// ブロックの中の設定行。「場所 5, 10」「値段 500」「向き 90」「名前 "…"」。
    /// Keyword は日本語の正規表記（場所 / 値段 / 向き / 名前）に揃えてある。
    /// </summary>
    public sealed class PropertyNode : Node
    {
        public string Keyword = string.Empty;
        public List<double> Numbers = new List<double>();
        public string Text;

        public double Number0 => Numbers.Count > 0 ? Numbers[0] : 0.0;
        public double Number1 => Numbers.Count > 1 ? Numbers[1] : 0.0;

        public override string Describe()
        {
            if (Text != null) return $"{Keyword} \"{Text}\"";
            return $"{Keyword} {string.Join(", ", Numbers)}";
        }
    }

    /// <summary>「もし 来場者数 &gt; 500 { … }」(§14 / §16 / §17)</summary>
    public sealed class IfNode : Node
    {
        public ExpressionNode Condition;
        public List<Node> Body = new List<Node>();

        public override string Describe() => $"もし {Condition?.Describe() ?? "?"}";
    }

    /// <summary>「時間 19:00 { … }」「20:00 { … }」(§15)</summary>
    public sealed class TimeNode : Node
    {
        public int MinutesOfDay;
        public List<Node> Body = new List<Node>();

        public override string Describe() => $"時間 {ScriptText.ClockText(MinutesOfDay)}";
    }

    /// <summary>条件式の基底。</summary>
    public abstract class ExpressionNode : Node
    {
    }

    /// <summary>
    /// 「来場者数 &gt; 500」「たこ焼き.待ち人数 &gt; 20」。
    /// LeftTarget が非 null なら「屋台名.指標」の形 (§17)。
    /// </summary>
    public sealed class ComparisonNode : ExpressionNode
    {
        public string LeftMetric = string.Empty;
        public string LeftTarget;
        public string Op = ">";
        public double Right;

        public override string Describe()
            => (LeftTarget == null ? LeftMetric : LeftTarget + "." + LeftMetric) + $" {Op} {Right:0.##}";
    }

    /// <summary>「A かつ B」「A または B」</summary>
    public sealed class LogicalNode : ExpressionNode
    {
        public bool IsAnd = true;
        public ExpressionNode Left;
        public ExpressionNode Right;

        public override string Describe()
            => $"({Left?.Describe()} {(IsAnd ? "かつ" : "または")} {Right?.Describe()})";
    }
}
