namespace Matsuri.Script.Commands
{
    /// <summary>会場グリッド上の座標。Matsuri Script の「場所 X, Z」に対応。</summary>
    public readonly struct GridPos
    {
        public readonly float X;
        public readonly float Z;

        public GridPos(float x, float z)
        {
            X = x;
            Z = z;
        }

        public override string ToString() => $"({X}, {Z})";
    }

    /// <summary>
    /// 仕様書 §53。Parser / Interpreter は GameObject を直接触らない。
    /// 必ずこの Command に変換し、FestivalManager (= IFestivalCommandSink) が実行する。
    /// </summary>
    public interface IFestivalCommand
    {
        /// <summary>ログ・UI表示用の日本語説明。「たこ焼きを (5, 10) に建てる」など。</summary>
        string Describe();

        /// <summary>このコマンドが由来するソースコードの行番号（1始まり）。</summary>
        int SourceLine { get; }

        /// <summary>建設に必要な費用。実行時に EconomyManager が参照する。0 なら無料。</summary>
        long Cost { get; }

        void Execute(IFestivalCommandSink sink);
    }

    /// <summary>屋台を建てる。</summary>
    public sealed class CreateStallCommand : IFestivalCommand
    {
        public string StallId;      // 正規ID (MatsuriIds.Takoyaki など)
        public string SourceName;   // ソースに書かれた表記 ("たこ焼き")
        public GridPos Position;
        public int? Price;          // 「値段」未指定なら null → DefaultPrice を使う
        public float RotationDegrees;
        public int SourceLine { get; set; }
        public long Cost { get; set; }

        public string Describe()
            => $"屋台「{SourceName}」を {Position} に建てる" + (Price.HasValue ? $"（値段 {Price.Value}円）" : "");

        public void Execute(IFestivalCommandSink sink) => sink.CreateStall(this);
    }

    /// <summary>装飾を置く (§21)。</summary>
    public sealed class CreateDecorationCommand : IFestivalCommand
    {
        public string DecorationId;
        public string SourceName;
        public GridPos Position;
        public float RotationDegrees;
        public int SourceLine { get; set; }
        public long Cost { get; set; }

        public string Describe() => $"装飾「{SourceName}」を {Position} に置く";
        public void Execute(IFestivalCommandSink sink) => sink.CreateDecoration(this);
    }

    /// <summary>設備を置く (§20)。</summary>
    public sealed class CreateFacilityCommand : IFestivalCommand
    {
        public string FacilityId;
        public string SourceName;
        public GridPos Position;
        public float RotationDegrees;
        public int SourceLine { get; set; }
        public long Cost { get; set; }

        public string Describe() => $"設備「{SourceName}」を {Position} に置く";
        public void Execute(IFestivalCommandSink sink) => sink.CreateFacility(this);
    }

    /// <summary>既に建っている屋台の値段を変更する。</summary>
    public sealed class SetPriceCommand : IFestivalCommand
    {
        public string StallId;
        public string SourceName;
        public int Price;
        public int SourceLine { get; set; }
        public long Cost => 0;

        public string Describe() => $"「{SourceName}」の値段を {Price}円 にする";
        public void Execute(IFestivalCommandSink sink) => sink.SetPrice(this);
    }

    /// <summary>花火を打ち上げる (§22 / §61)。</summary>
    public sealed class StartFireworksCommand : IFestivalCommand
    {
        public string Kind = MatsuriIds.FireworkKiku;  // 菊 / 牡丹 / 柳 / ハート / 大玉 / スペシャル
        public string SourceName = "花火";
        public int SourceLine { get; set; }
        public long Cost { get; set; }

        public string Describe() => $"花火「{SourceName}」を打ち上げる";
        public void Execute(IFestivalCommandSink sink) => sink.StartFireworks(this);
    }

    /// <summary>盆踊りを始める (§22)。</summary>
    public sealed class StartBonOdoriCommand : IFestivalCommand
    {
        public GridPos? Position;      // 未指定なら会場中央のやぐら
        public int SourceLine { get; set; }
        public long Cost { get; set; }

        public string Describe() => "盆踊りを始める";
        public void Execute(IFestivalCommandSink sink) => sink.StartBonOdori(this);
    }

    /// <summary>太鼓演奏を始める (§22)。</summary>
    public sealed class StartTaikoCommand : IFestivalCommand
    {
        public GridPos? Position;
        public int SourceLine { get; set; }
        public long Cost { get; set; }

        public string Describe() => "太鼓演奏を始める";
        public void Execute(IFestivalCommandSink sink) => sink.StartTaiko(this);
    }

    /// <summary>
    /// 仕様書 §53 の分離点。FestivalManager がこれを実装する。
    /// Matsuri.Script アセンブリは Unity のゲームシステムを一切知らない。
    /// </summary>
    public interface IFestivalCommandSink
    {
        void CreateStall(CreateStallCommand cmd);
        void CreateDecoration(CreateDecorationCommand cmd);
        void CreateFacility(CreateFacilityCommand cmd);
        void SetPrice(SetPriceCommand cmd);
        void StartFireworks(StartFireworksCommand cmd);
        void StartBonOdori(StartBonOdoriCommand cmd);
        void StartTaiko(StartTaikoCommand cmd);

        /// <summary>実行時に起きた出来事をプレイヤーに知らせる（予算不足など）。</summary>
        void ReportRuntimeMessage(string message, DiagnosticSeverity severity = DiagnosticSeverity.Info);
    }
}
