using Core.Abstractions;
using Core.Domain;

namespace Core.Services
{
    public sealed class PriceForecastService
    {
        private readonly IPurchaseOrderRepository _poRepo;
        private readonly ICpiRepository _cpiRepo;
        private readonly SsaForecaster _forecaster;

        public PriceForecastService(
            IPurchaseOrderRepository poRepo,
            ICpiRepository cpiRepo,
            SsaForecaster? forecaster = null)
        {
            _poRepo = poRepo;
            _cpiRepo = cpiRepo;
            _forecaster = forecaster ?? new SsaForecaster();
        }

        public ForecastResult ForecastNominalPriceNextMonths(
            string partCode,
            int months,
            string? currencyCode = null,
            ForecastOptions? options = null)
        {
            options ??= new ForecastOptions();

            // 1) Load PO lines and aggregate to monthly average nominal price
            var poLines = _poRepo.GetLines(partCode, currencyCode);
            var monthlyNominal = MonthlySeriesBuilder.BuildMonthlyAvgPrice(poLines);

            var firstPoMonth = monthlyNominal.Count > 0 ? monthlyNominal[0].Month : (DateTime?)null;
            var lastPoMonth = monthlyNominal.Count > 0 ? monthlyNominal[^1].Month : (DateTime?)null;
            var monthlyBeforeJoin = monthlyNominal.Count;

            if (monthlyNominal.Count < options.MinMonthlyPoints)
                throw new InvalidOperationException(
                    $"Not enough monthly history for PART_CODE={partCode}. Got {monthlyNominal.Count} months.");

            // 2) Load CPI and build a month->CPI lookup
            var cpiPoints = _cpiRepo.GetMonthlyCpi();
            var firstCpiMonth = cpiPoints.Count > 0 ? cpiPoints[0].Month : (DateTime?)null;
            var lastCpiMonth = cpiPoints.Count > 0 ? cpiPoints[^1].Month : (DateTime?)null;

            var cpiMap = MonthlySeriesBuilder.ToCpiMap(cpiPoints);

            // 3) Align monthly nominal prices with CPI by Year+Month (DateTime normalized to yyyy-MM-01)
            var aligned = new List<(DateTime Month, decimal Nominal, decimal Cpi)>();

            foreach (var m in monthlyNominal)
            {
                if (cpiMap.TryGetValue(m.Month, out var cpi))
                    aligned.Add((m.Month, m.AvgNominalPrice, cpi));
            }

            // Diagnostics depending on aligned MUST be computed after the join is done
            var firstAlignedMonth = aligned.Count > 0 ? aligned[0].Month : (DateTime?)null;
            var lastAlignedMonth = aligned.Count > 0 ? aligned[^1].Month : (DateTime?)null;

            var monthlyAfterJoin = aligned.Count;
            var dropped = monthlyBeforeJoin - monthlyAfterJoin;

            if (aligned.Count < options.MinMonthlyPoints)
                throw new InvalidOperationException(
                    $"Not enough CPI-aligned months for PART_CODE={partCode}. Got {aligned.Count} months after join.");

            // Base CPI = last observed CPI in training window (prices will be expressed in "base-month dollars")
            var lastTrainingMonth = aligned[^1].Month;
            var baseCpi = aligned[^1].Cpi;

            // Convert nominal -> real (inflation-adjusted) series
            var realSeries = aligned
                .Select(x => InflationAdjuster.ToReal(x.Nominal, x.Cpi, baseCpi))
                .ToList();

            // 4) Forecast real price (optionally in log space for stability / non-negativity)
            decimal[] realF, realLo, realHi;
            SsaParams priceParams;

            if (options.UseLogTransform)
            {
                var logSeries = realSeries
                    .Select(v => LogTransform.ToLog(v, options.LogEpsilon))
                    .ToList();

                var (logF, logLo, logHi, p) = _forecaster.ForecastSeries(
                    logSeries,
                    months,
                    options.ConfidenceLevel);

                priceParams = p;

                realF = logF.Select(v => LogTransform.FromLog(v, options.LogEpsilon)).ToArray();
                realLo = logLo.Select(v => LogTransform.FromLog(v, options.LogEpsilon)).ToArray();
                realHi = logHi.Select(v => LogTransform.FromLog(v, options.LogEpsilon)).ToArray();
            }
            else
            {
                var (f, lo, hi, p) = _forecaster.ForecastSeries(
                    realSeries,
                    months,
                    options.ConfidenceLevel);

                realF = f;
                realLo = lo;
                realHi = hi;
                priceParams = p;
            }

            // 5) Forecast CPI for the same horizon so we can re-inflate real -> nominal
            var cpiSeries = aligned.Select(x => x.Cpi).ToList();
            var (cpiF, _, _, cpiParams) = _forecaster.ForecastSeries(
                cpiSeries,
                months,
                options.ConfidenceLevel);

            // 6) Build forecast points in nominal dollars
            var startMonth = lastTrainingMonth.AddMonths(1);
            var points = new List<ForecastPoint>(months);

            for (int i = 0; i < months; i++)
            {
                var month = startMonth.AddMonths(i);
                var cpi = cpiF[i];

                var nominal = InflationAdjuster.ToNominal(realF[i], cpi, baseCpi);
                var nominalLo = InflationAdjuster.ToNominal(realLo[i], cpi, baseCpi);
                var nominalHi = InflationAdjuster.ToNominal(realHi[i], cpi, baseCpi);

                // Domain constraints: prices should not be negative
                if (nominal < 0) nominal = 0;
                if (nominalLo < 0) nominalLo = 0;
                if (nominalHi < 0) nominalHi = 0;

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
                    // Coverage
                    FirstPoMonth = firstPoMonth,
                    LastPoMonth = lastPoMonth,
                    FirstCpiMonth = firstCpiMonth,
                    LastCpiMonth = lastCpiMonth,
                    FirstAlignedMonth = firstAlignedMonth,
                    LastAlignedMonth = lastAlignedMonth,

                    // Counts
                    MonthlyPointsBeforeJoin = monthlyBeforeJoin,
                    MonthlyPointsUsed = monthlyAfterJoin,
                    MonthsDroppedDueToMissingCpi = dropped,

                    // Model
                    BaseCpi = baseCpi,
                    Notes = options.UseLogTransform
                        ? "Option A: SSA forecast of log(REAL price) + SSA forecast CPI; then re-inflate."
                        : "Option A: SSA forecast of REAL price + SSA forecast CPI; then re-inflate.",
                    PriceSsa = priceParams,
                    CpiSsa = cpiParams
                }
            };
        }
    }
}