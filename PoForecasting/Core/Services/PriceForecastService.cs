using Core.Abstractions;
using Core.Domain;

namespace Core.Services
{
    public sealed class PriceForecastService
    {

        private readonly IPurchaseOrderRepository _poRepo;
        private readonly ICpiRepository _cpiRepo;
        private readonly SsaForecaster _forecaster;

        public PriceForecastService(IPurchaseOrderRepository poRepo, ICpiRepository cpiRepo, SsaForecaster? forecaster = null)
        {
            _poRepo = poRepo;
            _cpiRepo = cpiRepo;
            _forecaster = forecaster ?? new SsaForecaster();
        }

        public ForecastResult ForecastNominalPriceNextMonths(string partCode, int months, string? currencyCode = null)
        {
            // 1) Load
            var poLines = _poRepo.GetLines(partCode, currencyCode);
            var monthlyNominal = MonthlySeriesBuilder.BuildMonthlyAvgPrice(poLines);

            if (monthlyNominal.Count < 24)
                throw new InvalidOperationException($"Not enough monthly history for PART_CODE={partCode}. Got {monthlyNominal.Count} months.");

            // 2) CPI map
            var cpiMap = MonthlySeriesBuilder.ToCpiMap(_cpiRepo.GetMonthlyCpi());

            // 3) Align + compute real series
            var aligned = new List<(DateTime Month, decimal Nominal, decimal Cpi)>();
            foreach (var m in monthlyNominal)
            {
                if (cpiMap.TryGetValue(m.Month, out var cpi))
                    aligned.Add((m.Month, m.AvgNominalPrice, cpi));
            }

            if (aligned.Count < 24)
                throw new InvalidOperationException($"Not enough CPI-aligned months for PART_CODE={partCode}. Got {aligned.Count} months after join.");

            var lastTrainingMonth = aligned[^1].Month;
            var baseCpi = aligned[^1].Cpi; // base = last observed CPI

            var realSeries = aligned.Select(x => InflationAdjuster.ToReal(x.Nominal, x.Cpi, baseCpi)).ToList();

            // 4) Forecast real price
            var (realF, realLo, realHi, priceParams) = _forecaster.ForecastSeries(realSeries, months);

            // 5) Forecast CPI for the same horizon (so we can re-inflate)
            // If you prefer “flat CPI”, replace this with repeating last CPI.
            var cpiSeries = aligned.Select(x => x.Cpi).ToList();
            var (cpiF, _, _, cpiParams) = _forecaster.ForecastSeries(cpiSeries, months);

            // 6) Build nominal forecast points
            var startMonth = lastTrainingMonth.AddMonths(1);
            var points = new List<ForecastPoint>(months);

            for (int i = 0; i < months; i++)
            {
                var month = startMonth.AddMonths(i);
                var cpi = cpiF[i];

                var nominal = InflationAdjuster.ToNominal(realF[i], cpi, baseCpi);
                var nominalLo = InflationAdjuster.ToNominal(realLo[i], cpi, baseCpi);
                var nominalHi = InflationAdjuster.ToNominal(realHi[i], cpi, baseCpi);

                points.Add(new ForecastPoint
                {
                    Month = month,
                    RealForecast = realF[i],
                    NominalForecast = nominal,
                    CpiForecast = cpi,
                    Lower95Nominal = nominalLo,
                    Upper95Nominal = nominalHi
                });
            }

            return new ForecastResult
            {
                PartCode = partCode,
                LastTrainingMonth = lastTrainingMonth,
                Points = points,
                Diagnostics = new ForecastDiagnostics
                {
                    MonthlyPointsUsed = aligned.Count,
                    BaseCpi = baseCpi,
                    Notes = "Option A: forecast REAL price via SSA, forecast CPI via SSA, then re-inflate to nominal.",
                    PriceSsa = priceParams,
                    CpiSsa = cpiParams
                }
            };
        }

    }
}
