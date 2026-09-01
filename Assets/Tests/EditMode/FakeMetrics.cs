using System.Collections.Generic;
using Matsuri.Script.Commands;

namespace Matsuri.Tests
{
    /// <summary>条件式のテスト用に、祭りの状態を手で作れるようにしたもの。</summary>
    public sealed class FakeMetrics : IFestivalMetrics
    {
        public int VisitorCount { get; set; }
        public int CurrentVisitorCount { get; set; }
        public long Revenue { get; set; }
        public long Budget { get; set; }

        /// <summary>0〜1。MetricCondition の中で 100倍されて比較される。</summary>
        public float AverageSatisfaction { get; set; }

        public int MinutesOfDay { get; set; } = 17 * 60;

        public readonly Dictionary<string, int> Queues = new Dictionary<string, int>();
        public readonly Dictionary<string, long> StallRevenues = new Dictionary<string, long>();
        public readonly Dictionary<string, int> StallCounts = new Dictionary<string, int>();

        public int GetQueueLength(string stallId)
            => stallId != null && Queues.TryGetValue(stallId, out int v) ? v : 0;

        public long GetStallRevenue(string stallId)
            => stallId != null && StallRevenues.TryGetValue(stallId, out long v) ? v : 0L;

        public int GetStallCount(string stallId)
            => stallId != null && StallCounts.TryGetValue(stallId, out int v) ? v : 0;
    }

    /// <summary>コマンドの実行先。何が呼ばれたかを記録するだけ。</summary>
    public sealed class RecordingSink : IFestivalCommandSink
    {
        public readonly List<CreateStallCommand> Stalls = new List<CreateStallCommand>();
        public readonly List<CreateDecorationCommand> Decorations = new List<CreateDecorationCommand>();
        public readonly List<CreateFacilityCommand> Facilities = new List<CreateFacilityCommand>();
        public readonly List<SetPriceCommand> Prices = new List<SetPriceCommand>();
        public readonly List<StartFireworksCommand> Fireworks = new List<StartFireworksCommand>();
        public readonly List<StartBonOdoriCommand> BonOdori = new List<StartBonOdoriCommand>();
        public readonly List<StartTaikoCommand> Taiko = new List<StartTaikoCommand>();
        public readonly List<string> Messages = new List<string>();

        public void CreateStall(CreateStallCommand cmd) => Stalls.Add(cmd);
        public void CreateDecoration(CreateDecorationCommand cmd) => Decorations.Add(cmd);
        public void CreateFacility(CreateFacilityCommand cmd) => Facilities.Add(cmd);
        public void SetPrice(SetPriceCommand cmd) => Prices.Add(cmd);
        public void StartFireworks(StartFireworksCommand cmd) => Fireworks.Add(cmd);
        public void StartBonOdori(StartBonOdoriCommand cmd) => BonOdori.Add(cmd);
        public void StartTaiko(StartTaikoCommand cmd) => Taiko.Add(cmd);

        public void ReportRuntimeMessage(string message, Matsuri.Script.DiagnosticSeverity severity = Matsuri.Script.DiagnosticSeverity.Info)
            => Messages.Add(message);
    }
}
