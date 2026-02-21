namespace Core.Domain
{
    public sealed class ForecastResult
    {
        public string PartCode { get; init; } = "";
        public DateTime LastTrainingMonth { get; init; }
        public IReadOnlyList<ForecastPoint> Points { get; init; } = Array.Empty<ForecastPoint>();
        public ForecastDiagnostics Diagnostics { get; init; } = new();
    }

    public sealed class ForecastPoint
    {
        public DateTime Month { get; init; }
        public decimal RealForecast { get; init; }
        public decimal NominalForecast { get; init; }
        public decimal CpiForecast { get; init; }
        public decimal Lower95Nominal { get; init; }
        public decimal Upper95Nominal { get; init; }
    }

    public sealed class ForecastDiagnostics
    {
        public int MonthlyPointsUsed { get; init; }
        public decimal BaseCpi { get; init; }
        public string Notes { get; init; } = "";
        public SsaParams PriceSsa { get; init; } = new();
        public SsaParams CpiSsa { get; init; } = new();
    }

    public sealed class SsaParams
    {
        public int TrainSize { get; init; }
        public int WindowSize { get; init; }
        public int SeriesLength { get; init; }
        public int Horizon { get; init; }
    }
}
