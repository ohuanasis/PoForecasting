using Core.Abstractions;
using Core.Domain;
using Core.Infrastructure.Csv;
using Core.Infrastructure.Oracle;
using Core.Services;
using Microsoft.Extensions.Configuration;

namespace ConsoleForeCasterSimple
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            var config = BuildConfig();

            var parsed = ParseArgs(args);

            if (!parsed.IsValid)
            {
                PrintUsage();
                return;
            }

            // Defaults (baked in)
            const string DefaultCurrency = "USD";

            var options = new ForecastOptions
            {
                UseLogTransform = true,
                ConfidenceLevel = 0.95f
            };

            // Repos (Csv or Oracle based on appsettings.json)
            var (poRepo, cpiRepo) = CreateRepositories(config, parsed.PoPath, parsed.CpiPath);

            // Service
            var svc = new PriceForecastService(poRepo, cpiRepo);

            // Build the monthly nominal history so we can show the last 10 purchases (or less)
            // Note: this is "monthly average nominal PRICE_PER_UNIT" for the part code, filtered to USD
            var poLines = poRepo.GetLines(parsed.PartCode!, DefaultCurrency);
            var monthlyNominal = MonthlySeriesBuilder.BuildMonthlyAvgPrice(poLines);

            var lastHistory = monthlyNominal
                .OrderByDescending(x => x.Month)
                .Take(10)
                .OrderBy(x => x.Month)
                .ToList();

            // Current month normalized to first-of-month
            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            // Probe forecast (to discover lastTrainingMonth)
            var probe = svc.ForecastNominalPriceNextMonths(
                parsed.PartCode!,
                months: 1,
                currencyCode: DefaultCurrency,
                options: options);

            var lastTrainingMonth = probe.LastTrainingMonth;

            int monthsGap = MonthsBetween(lastTrainingMonth, currentMonth);
            if (monthsGap < 0) monthsGap = 0;

            int horizonNeeded = checked(monthsGap + parsed.MonthsAhead);

            // Run forecast far enough to reach "months ahead of now"
            var result = svc.ForecastNominalPriceNextMonths(
                parsed.PartCode!,
                months: horizonNeeded,
                currencyCode: DefaultCurrency,
                options: options);

            // We only display the next N months from now (starting next month)
            var displayStart = currentMonth.AddMonths(1);
            var displayEnd = currentMonth.AddMonths(parsed.MonthsAhead);

            var displayPoints = result.Points
                .Where(p => p.Month >= displayStart && p.Month <= displayEnd)
                .OrderBy(p => p.Month)
                .ToList();

            // ---- Summary ----
            Console.WriteLine($"PART_CODE={result.PartCode}");
            Console.WriteLine($"Today: {DateTime.Today:yyyy-MM-dd} (current month={currentMonth:yyyy-MM-dd})");
            Console.WriteLine($"Last training month: {result.LastTrainingMonth:yyyy-MM-dd}");
            Console.WriteLine($"Requested: {parsed.MonthsAhead} months ahead of current month ({displayStart:yyyy-MM-dd} --> {displayEnd:yyyy-MM-dd})");
            Console.WriteLine();

            //Console.WriteLine("---- Defaults ----");
            //Console.WriteLine($"Currency: USD");
            //Console.WriteLine($"Log transform: true");
            //Console.WriteLine($"Confidence: 0.95");
            //Console.WriteLine();

            // ---- Data Coverage ----
            Console.WriteLine("---- Data Coverage ----");
            Console.WriteLine($"PO Range:   {Fmt(result.Diagnostics.FirstPoMonth)} --> {Fmt(result.Diagnostics.LastPoMonth)}");
            Console.WriteLine($"CPI Range:  {Fmt(result.Diagnostics.FirstCpiMonth)} --> {Fmt(result.Diagnostics.LastCpiMonth)}");
            Console.WriteLine($"Aligned:    {Fmt(result.Diagnostics.FirstAlignedMonth)} --> {Fmt(result.Diagnostics.LastAlignedMonth)}");
            Console.WriteLine($"Months before join: {result.Diagnostics.MonthlyPointsBeforeJoin}");
            Console.WriteLine($"Months after join:  {result.Diagnostics.MonthlyPointsUsed}");
            Console.WriteLine($"Dropped (no CPI):   {result.Diagnostics.MonthsDroppedDueToMissingCpi}");
            Console.WriteLine();

            // ---- Previous purchase prices (after Data Coverage, before Forecast) ----
            Console.WriteLine($"---- Previous purchase prices for Part Code {parsed.PartCode} (Price per Unit) ----");
            if (lastHistory.Count == 0)
            {
                Console.WriteLine("No purchase history found for this part code (after currency filter).");
                Console.WriteLine();
            }
            else
            {
                foreach (var h in lastHistory)
                {
                    Console.WriteLine($"{h.Month:yyyy-MM-dd}  Price Per Unit={(double)h.AvgNominalPrice:F4}");
                }
                Console.WriteLine();
            }

            // ---- Forecast output (simple) ----
            Console.WriteLine($"---- Forecast Price for Part Code {result.PartCode} (Nominal PRICE_PER_UNIT + Expected Inflation) in the next {parsed.MonthsAhead} months ----");

            if (displayPoints.Count == 0)
            {
                Console.WriteLine("No forecast points available for the requested window.");
                return;
            }

            foreach (var p in displayPoints)
            {
                Console.WriteLine($"{p.Month:yyyy-MM-dd}  Price Prediction={p.NominalForecast:F4}");
            }
        }

        // -------------------------------
        // Configuration (appsettings.json)
        // -------------------------------

        private static IConfiguration BuildConfig()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();
        }

        private static (IPurchaseOrderRepository PoRepo, ICpiRepository CpiRepo) CreateRepositories(
            IConfiguration config,
            string? poPathFromArgs,
            string? cpiPathFromArgs)
        {
            var source = (config["DataSource"] ?? "Csv").Trim();

            if (source.Equals("Oracle", StringComparison.OrdinalIgnoreCase))
            {
                var connStr = config["Oracle:ConnectionString"];
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("Missing Oracle:ConnectionString in appsettings.json");

                return (new OraclePurchaseOrderRepository(connStr),
                        new OracleCpiRepository(connStr));
            }

            // Default: CSV
            // Prefer CLI args if provided; otherwise fall back to appsettings.json
            var poPath = !string.IsNullOrWhiteSpace(poPathFromArgs) ? poPathFromArgs : config["Csv:PoPath"];
            var cpiPath = !string.IsNullOrWhiteSpace(cpiPathFromArgs) ? cpiPathFromArgs : config["Csv:CpiPath"];

            if (string.IsNullOrWhiteSpace(poPath) || string.IsNullOrWhiteSpace(cpiPath))
                throw new InvalidOperationException("CSV mode requires Csv:PoPath and Csv:CpiPath in appsettings.json (or pass --po/--cpi).");

            return (new CsvPurchaseOrderRepository(poPath),
                    new CsvCpiRepository(cpiPath));
        }

        // Number of whole months between two first-of-month dates, e.g.
        // MonthsBetween(2022-10-01, 2026-02-01) = 40
        private static int MonthsBetween(DateTime fromMonth, DateTime toMonth)
        {
            var from = new DateTime(fromMonth.Year, fromMonth.Month, 1);
            var to = new DateTime(toMonth.Year, toMonth.Month, 1);
            return (to.Year - from.Year) * 12 + (to.Month - from.Month);
        }

        private static string Fmt(DateTime? dt)
            => dt.HasValue ? dt.Value.ToString("yyyy-MM-dd") : "(null)";

        // -------------------------------
        // Arg parsing (4 required flags)
        // -------------------------------
        private sealed class ParsedArgs
        {
            public string? PoPath { get; set; }
            public string? CpiPath { get; set; }
            public string? PartCode { get; set; }
            public int MonthsAhead { get; set; }
            public bool IsValid { get; set; }
        }

        private static ParsedArgs ParseArgs(string[] args)
        {
            var p = new ParsedArgs();

            for (int i = 0; i < args.Length; i++)
            {
                var key = args[i].ToLowerInvariant();
                if (!key.StartsWith("--")) continue;
                if (i + 1 >= args.Length) continue;

                var val = args[i + 1];

                switch (key)
                {
                    case "--po":
                        p.PoPath = val;
                        break;
                    case "--cpi":
                        p.CpiPath = val;
                        break;
                    case "--part":
                        p.PartCode = val;
                        break;
                    case "--months":
                        if (int.TryParse(val, out int m) && m > 0)
                            p.MonthsAhead = m;
                        break;
                }
            }

            // In Csv mode, --po/--cpi can come from appsettings.json, so only require part+months.
            p.IsValid =
                !string.IsNullOrWhiteSpace(p.PartCode) &&
                p.MonthsAhead > 0;

            return p;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- --part <partCode> --months <n> [--po <poCsvPath>] [--cpi <cpiCsvPath>]");
            Console.WriteLine();
            Console.WriteLine("Meaning of --months:");
            Console.WriteLine("  Forecast N months ahead of the CURRENT month (not N months after the last training month).");
            Console.WriteLine();
            Console.WriteLine("Data source:");
            Console.WriteLine("  Controlled by appsettings.json: DataSource = Csv or Oracle");
            Console.WriteLine("  Csv mode uses Csv:PoPath and Csv:CpiPath (or pass --po/--cpi).");
            Console.WriteLine("  Oracle mode uses Oracle:ConnectionString.");
            Console.WriteLine();
            Console.WriteLine("Defaults (not passed as args):");
            Console.WriteLine("  currency=USD, log=true, confidence=0.95");
            Console.WriteLine();
            Console.WriteLine("Example (Csv):");
            Console.WriteLine(@"  dotnet run -- --part 888012 --months 6 --po ""E:\data\po.csv"" --cpi ""E:\data\cpi.csv""");
            Console.WriteLine();
            Console.WriteLine("Example (Oracle):");
            Console.WriteLine(@"  dotnet run -- --part 888012 --months 6");
        }
    }
}