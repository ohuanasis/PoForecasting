using Core.Domain;
using Core.Infrastructure.Csv;
using Core.Services;

namespace ConsoleForeCaster
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  dotnet run -- <poCsvPath> <cpiCsvPath> <partCode> <months> [currency]");
                Console.WriteLine(@"Example:");
                Console.WriteLine(@"  dotnet run -- ""E:\data\po.csv"" ""E:\data\cpi.csv"" 888012 6 USD");
                return;
            }

            string poPath = args[0];
            string cpiPath = args[1];
            string partCode = args[2];

            if (!int.TryParse(args[3], out int months) || months <= 0)
            {
                Console.WriteLine("months must be a positive integer.");
                return;
            }

            string? currency = args.Length >= 5 ? args[4] : null;

            var poRepo = new CsvPurchaseOrderRepository(poPath);
            var cpiRepo = new CsvCpiRepository(cpiPath);

            var svc = new PriceForecastService(poRepo, cpiRepo);

            var options = new ForecastOptions
            {
                UseLogTransform = true
            };

            var result = svc.ForecastNominalPriceNextMonths(partCode, months, currency, options);

            Console.WriteLine($"PART_CODE={result.PartCode}");
            Console.WriteLine($"Last training month: {result.LastTrainingMonth:yyyy-MM-dd}");
            Console.WriteLine($"Monthly points used: {result.Diagnostics.MonthlyPointsUsed}");
            Console.WriteLine($"Base CPI: {result.Diagnostics.BaseCpi}");
            Console.WriteLine($"Price SSA: window={result.Diagnostics.PriceSsa.WindowSize}, seriesLen={result.Diagnostics.PriceSsa.SeriesLength}, train={result.Diagnostics.PriceSsa.TrainSize}");
            Console.WriteLine($"CPI   SSA: window={result.Diagnostics.CpiSsa.WindowSize}, seriesLen={result.Diagnostics.CpiSsa.SeriesLength}, train={result.Diagnostics.CpiSsa.TrainSize}");
            Console.WriteLine();

            Console.WriteLine("---- Data Coverage ----");
            Console.WriteLine($"PO Range:   {Fmt(result.Diagnostics.FirstPoMonth)} --> {Fmt(result.Diagnostics.LastPoMonth)}");
            Console.WriteLine($"CPI Range:  {Fmt(result.Diagnostics.FirstCpiMonth)} --> {Fmt(result.Diagnostics.LastCpiMonth)}");
            Console.WriteLine($"Aligned:    {Fmt(result.Diagnostics.FirstAlignedMonth)} --> {Fmt(result.Diagnostics.LastAlignedMonth)}");
            Console.WriteLine($"Months before join: {result.Diagnostics.MonthlyPointsBeforeJoin}");
            Console.WriteLine($"Months after join:  {result.Diagnostics.MonthlyPointsUsed}");
            Console.WriteLine($"Dropped (no CPI):   {result.Diagnostics.MonthsDroppedDueToMissingCpi}");
            Console.WriteLine();

            foreach (var p in result.Points)
            {
                Console.WriteLine(
                    $"{p.Month:yyyy-MM-dd}  nominal={p.NominalForecast:F4}  95%=[{p.Lower95Nominal:F4}, {p.Upper95Nominal:F4}]  CPI={p.CpiForecast:F3}  real={p.RealForecast:F4}");
            }
        }

        private static string Fmt(DateTime? dt)
            => dt.HasValue ? dt.Value.ToString("yyyy-MM-dd") : "(null)";
    }
}