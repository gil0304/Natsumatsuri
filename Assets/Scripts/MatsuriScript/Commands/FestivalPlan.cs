using System.Collections.Generic;

namespace Matsuri.Script.Commands
{
    /// <summary>
    /// スクリプトから参照できる祭りの状態。§14 / §15 / §16 / §17 の条件式が読む値。
    /// ゲーム側 (FestivalManager) が実装する。
    /// </summary>
    public interface IFestivalMetrics
    {
        /// <summary>来場者数（累計。§14「もし 来場者数 > 500」が読む値）。</summary>
        int VisitorCount { get; }

        /// <summary>現在会場にいる人数。</summary>
        int CurrentVisitorCount { get; }

        /// <summary>売上 (§16)。</summary>
        long Revenue { get; }

        /// <summary>残り予算 (§31)。</summary>
        long Budget { get; }

        /// <summary>平均満足度 0〜1 (§34)。</summary>
        float AverageSatisfaction { get; }

        /// <summary>ゲーム内時刻を「その日の分」で表す。17:00 = 1020。</summary>
        int MinutesOfDay { get; }

        /// <summary>屋台の待ち人数の合計 (§17)。同名の屋台が複数あれば合算。</summary>
        int GetQueueLength(string stallId);

        /// <summary>屋台ごとの売上。</summary>
        long GetStallRevenue(string stallId);

        /// <summary>同じ屋台が何軒建っているか。</summary>
        int GetStallCount(string stallId);
    }

    public enum CompareOp
    {
        Greater,
        GreaterEqual,
        Less,
        LessEqual,
        Equal,
        NotEqual
    }

    public enum MetricKind
    {
        Visitors,
        CurrentVisitors,
        Revenue,
        Budget,
        Satisfaction,
        Clock,
        StallQueue,
        StallRevenue,
        StallCount
    }

    /// <summary>祭り開催中に評価される条件。</summary>
    public interface ICondition
    {
        bool Evaluate(IFestivalMetrics metrics);
        string Describe();
    }

    /// <summary>「時間 20:00 { }」(§15)。指定時刻を過ぎたら真。</summary>
    public sealed class TimeCondition : ICondition
    {
        public int MinutesOfDay;

        public bool Evaluate(IFestivalMetrics m) => m.MinutesOfDay >= MinutesOfDay;

        public string Describe() => $"{MinutesOfDay / 60:00}:{MinutesOfDay % 60:00} になったら";
    }

    /// <summary>「もし 来場者数 &gt; 500」(§14) / 「もし たこ焼き.待ち人数 &gt; 20」(§17)。</summary>
    public sealed class MetricCondition : ICondition
    {
        public MetricKind Kind;
        public string StallId;      // Kind が Stall* のときのみ使用
        public string StallName;    // 表示用
        public CompareOp Op;
        public double Value;

        public bool Evaluate(IFestivalMetrics m)
        {
            double left = Kind switch
            {
                MetricKind.Visitors        => m.VisitorCount,
                MetricKind.CurrentVisitors => m.CurrentVisitorCount,
                MetricKind.Revenue         => m.Revenue,
                MetricKind.Budget          => m.Budget,
                MetricKind.Satisfaction    => m.AverageSatisfaction * 100.0,
                MetricKind.Clock           => m.MinutesOfDay,
                MetricKind.StallQueue      => m.GetQueueLength(StallId),
                MetricKind.StallRevenue    => m.GetStallRevenue(StallId),
                MetricKind.StallCount      => m.GetStallCount(StallId),
                _ => 0.0
            };

            return Op switch
            {
                CompareOp.Greater      => left > Value,
                CompareOp.GreaterEqual => left >= Value,
                CompareOp.Less         => left < Value,
                CompareOp.LessEqual    => left <= Value,
                CompareOp.Equal        => System.Math.Abs(left - Value) < 0.0001,
                CompareOp.NotEqual     => System.Math.Abs(left - Value) >= 0.0001,
                _ => false
            };
        }

        public string MetricLabel => Kind switch
        {
            MetricKind.Visitors        => "来場者数",
            MetricKind.CurrentVisitors => "現在の来場者",
            MetricKind.Revenue         => "売上",
            MetricKind.Budget          => "予算",
            MetricKind.Satisfaction    => "満足度",
            MetricKind.Clock           => "時刻",
            MetricKind.StallQueue      => $"{StallName}.待ち人数",
            MetricKind.StallRevenue    => $"{StallName}.売上",
            MetricKind.StallCount      => $"{StallName}.軒数",
            _ => "?"
        };

        public string OpLabel => Op switch
        {
            CompareOp.Greater      => ">",
            CompareOp.GreaterEqual => ">=",
            CompareOp.Less         => "<",
            CompareOp.LessEqual    => "<=",
            CompareOp.Equal        => "==",
            CompareOp.NotEqual     => "!=",
            _ => "?"
        };

        public string Describe() => $"{MetricLabel} {OpLabel} {Value:0.##} なら";
    }

    /// <summary>複数条件の AND / OR。</summary>
    public sealed class LogicalCondition : ICondition
    {
        public bool IsAnd;
        public ICondition Left;
        public ICondition Right;

        public bool Evaluate(IFestivalMetrics m)
            => IsAnd
                ? Left.Evaluate(m) && Right.Evaluate(m)
                : Left.Evaluate(m) || Right.Evaluate(m);

        public string Describe() => $"({Left.Describe()} {(IsAnd ? "かつ" : "または")} {Right.Describe()})";
    }

    /// <summary>
    /// 祭り開催中に毎tick評価されるルール。
    /// 条件が真になったら Body のコマンドを実行する。既定では一度だけ (§14 の想定)。
    /// </summary>
    public sealed class TriggerRule
    {
        public ICondition Condition;
        public IReadOnlyList<IFestivalCommand> Body = System.Array.Empty<IFestivalCommand>();
        public bool Once = true;
        public bool Fired;
        public int SourceLine;

        /// <summary>ネストされた入れ子ルール（もしの中の 時間 など）。</summary>
        public IReadOnlyList<TriggerRule> NestedRules = System.Array.Empty<TriggerRule>();

        public string Describe() => Condition?.Describe() ?? "(条件なし)";

        public void Reset() => Fired = false;
    }

    /// <summary>
    /// Interpreter の出力。RUN 時に即実行するコマンドと、
    /// 開催中に評価するルールに分かれる。
    /// </summary>
    public sealed class FestivalPlan
    {
        public IReadOnlyList<IFestivalCommand> ImmediateCommands = System.Array.Empty<IFestivalCommand>();
        public IReadOnlyList<TriggerRule> Rules = System.Array.Empty<TriggerRule>();
        public IReadOnlyList<Diagnostic> Diagnostics = System.Array.Empty<Diagnostic>();

        /// <summary>祭りの名前（「祭り "夏の宴" { }」のように書かれた場合）。</summary>
        public string FestivalName = "MY MATSURI";

        /// <summary>即時コマンドの合計建設費用。UI の見積り表示に使う。</summary>
        public long EstimatedCost;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                    if (Diagnostics[i].Severity == DiagnosticSeverity.Error) return true;
                return false;
            }
        }

        public static FestivalPlan Failed(IReadOnlyList<Diagnostic> diagnostics)
            => new FestivalPlan { Diagnostics = diagnostics };

        public void ResetRules()
        {
            for (int i = 0; i < Rules.Count; i++) ResetRecursive(Rules[i]);
        }

        static void ResetRecursive(TriggerRule rule)
        {
            rule.Reset();
            for (int i = 0; i < rule.NestedRules.Count; i++) ResetRecursive(rule.NestedRules[i]);
        }
    }
}
