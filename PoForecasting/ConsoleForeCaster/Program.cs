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
            var result = svc.ForecastNominalPriceNextMonths(partCode, months, currency);

            Console.WriteLine($"PART_CODE={result.PartCode}");
            Console.WriteLine($"Last training month: {result.LastTrainingMonth:yyyy-MM-dd}");
            Console.WriteLine($"Monthly points used: {result.Diagnostics.MonthlyPointsUsed}");
            Console.WriteLine($"Base CPI: {result.Diagnostics.BaseCpi}");
            Console.WriteLine($"Price SSA: window={result.Diagnostics.PriceSsa.WindowSize}, seriesLen={result.Diagnostics.PriceSsa.SeriesLength}, train={result.Diagnostics.PriceSsa.TrainSize}");
            Console.WriteLine($"CPI   SSA: window={result.Diagnostics.CpiSsa.WindowSize}, seriesLen={result.Diagnostics.CpiSsa.SeriesLength}, train={result.Diagnostics.CpiSsa.TrainSize}");
            Console.WriteLine();

            foreach (var p in result.Points)
            {
                Console.WriteLine($"{p.Month:yyyy-MM-dd}  nominal={p.NominalForecast:F4}  95%=[{p.Lower95Nominal:F4}, {p.Upper95Nominal:F4}]  CPI={p.CpiForecast:F3}  real={p.RealForecast:F4}");
            }
        }
    }
}
