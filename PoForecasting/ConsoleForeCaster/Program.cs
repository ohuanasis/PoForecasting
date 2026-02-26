using Core.Abstractions;
using Core.Domain;
using Core.Infrastructure.Csv;
using Core.Infrastructure.Oracle;
using Core.Services;
using Microsoft.Extensions.Configuration;

namespace ConsoleForeCaster
{
    internal class Program
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

            var (poRepo, cpiRepo) = CreateRepositories(config, parsed.PoPath, parsed.CpiPath);

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
        // Configuration (appsettings.json)
        // -------------------------------

        private static IConfiguration BuildConfig()
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                              ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                              ?? "Production";

            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
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

            // In CSV mode, --po and --cpi can come from appsettings.json, so we only require part+months here.
            parsed.IsValid =
                !string.IsNullOrWhiteSpace(parsed.PartCode) &&
                parsed.Months > 0;

            return parsed;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- --part <partCode> --months <n> [--currency USD] [--log true|false] [--confidence 0.95] [--po <poCsvPath>] [--cpi <cpiCsvPath>]");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  Data source is controlled by appsettings.json: DataSource = Csv or Oracle");
            Console.WriteLine("  In Csv mode you can supply --po/--cpi or set Csv:PoPath/Csv:CpiPath in appsettings.json");
            Console.WriteLine("  In Oracle mode, --po/--cpi are ignored and Oracle:ConnectionString must be set in appsettings.json");
            Console.WriteLine();
            Console.WriteLine("Example (Csv):");
            Console.WriteLine(@"  dotnet run -- --part 888012 --months 6 --currency USD --log true --confidence 0.95 --po ""E:\data\po.csv"" --cpi ""E:\data\cpi.csv""");
            Console.WriteLine();
            Console.WriteLine("Example (Oracle):");
            Console.WriteLine(@"  dotnet run -- --part 888012 --months 6 --currency USD --log true --confidence 0.95");
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