using Core.Domain;
using Core.Infrastructure.Csv;
using Core.Services;

namespace ConsoleForeCaster
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var parsed = ParseArgs(args);

            if (!parsed.IsValid)
            {
                PrintUsage();
                return;
            }

            var poRepo = new CsvPurchaseOrderRepository(parsed.PoPath!);
            var cpiRepo = new CsvCpiRepository(parsed.CpiPath!);

            var svc = new PriceForecastService(poRepo, cpiRepo);

            var options = new ForecastOptions
            {
                UseLogTransform = parsed.UseLogTransform,
                ConfidenceLevel = parsed.ConfidenceLevel
            };

            var result = svc.ForecastNominalPriceNextMonths(
                parsed.PartCode!,
                parsed.Months,
                parsed.Currency,
                options);

            PrintResult(result);
        }

        // -------------------------------
        // Argument Parsing
        // -------------------------------

        private static ParsedArgs ParseArgs(string[] args)
        {
            var parsed = new ParsedArgs();

            for (int i = 0; i < args.Length; i++)
            {
                var key = args[i].ToLowerInvariant();

                if (!key.StartsWith("--"))
                    continue;

                if (i + 1 >= args.Length)
                    continue;

                var value = args[i + 1];

                switch (key)
                {
                    case "--po":
                        parsed.PoPath = value;
                        break;
                    case "--cpi":
                        parsed.CpiPath = value;
                        break;
                    case "--part":
                        parsed.PartCode = value;
                        break;
                    case "--months":
                        if (int.TryParse(value, out int m) && m > 0)
                            parsed.Months = m;
                        break;
                    case "--currency":
                        parsed.Currency = value;
                        break;
                    case "--log":
                        if (bool.TryParse(value, out bool log))
                            parsed.UseLogTransform = log;
                        break;
                    case "--confidence":
                        if (float.TryParse(value, out float c) && c > 0 && c < 1)
                            parsed.ConfidenceLevel = c;
                        break;
                }
            }

            parsed.IsValid =
                !string.IsNullOrWhiteSpace(parsed.PoPath) &&
                !string.IsNullOrWhiteSpace(parsed.CpiPath) &&
                !string.IsNullOrWhiteSpace(parsed.PartCode) &&
                parsed.Months > 0;

            return parsed;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- --po <poCsvPath> --cpi <cpiCsvPath> --part <partCode> --months <n> [--currency USD] [--log true|false] [--confidence 0.95]");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine(@"  dotnet run -- --po ""E:\data\po.csv"" --cpi ""E:\data\cpi.csv"" --part 888012 --months 6 --currency USD --log true --confidence 0.95");
        }

        // -------------------------------
        // Output Formatting
        // -------------------------------

        private static void PrintResult(ForecastResult result)
        {
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
                Console.WriteLine($"{p.Month:yyyy-MM-dd}  nominal={p.NominalForecast:F4}  95%=[{p.Lower95Nominal:F4}, {p.Upper95Nominal:F4}]  CPI={p.CpiForecast:F3}  real={p.RealForecast:F4}");
            }
        }

        private static string Fmt(DateTime? dt)
            => dt.HasValue ? dt.Value.ToString("yyyy-MM-dd") : "(null)";
    }

    internal class ParsedArgs
    {
        public string? PoPath { get; set; }
        public string? CpiPath { get; set; }
        public string? PartCode { get; set; }
        public int Months { get; set; }
        public string? Currency { get; set; }
        public bool UseLogTransform { get; set; } = true;
        public float ConfidenceLevel { get; set; } = 0.95f;
        public bool IsValid { get; set; }
    }
}