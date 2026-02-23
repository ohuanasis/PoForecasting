using Core.Domain;
using Core.Infrastructure.Csv;
using Core.Services;
using Microsoft.Extensions.Options;

namespace ApiPOForecaster
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Ensure deterministic config layering (do this BEFORE binding options)
            builder.Configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

            // Bind file paths from config
            builder.Services.Configure<DataFilesOptions>(builder.Configuration.GetSection("DataFiles"));

            // OpenAPI (built-in .NET 9)
            builder.Services.AddOpenApi();

            // Explicitly tell the app that the HTTPS port is 5017
            builder.Services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = 5017;
            });

            var app = builder.Build();

            // Map OpenAPI for Dev environments
            if (app.Environment.IsEnvironment("DevelopmentHTTP") ||
                app.Environment.IsEnvironment("DevelopmentHTTPS") ||
                app.Environment.IsDevelopment())
            {
                app.MapOpenApi(); // This generates the /openapi/v1.json

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/openapi/v1.json", "v1");
                });
            }

            // Surgical HTTPS Redirection:
            // ONLY redirect if in HTTPS profile and NOT in Docker
            if (app.Environment.IsEnvironment("DevelopmentHTTPS") && !app.Environment.IsEnvironment("Docker"))
            {
                app.UseHttpsRedirection();
            }

            // Health
            app.MapGet("/", () => Results.Ok(new { status = "ApiPOForecaster running" }));

            // Forecast endpoint:
            // GET /forecast?partCode=888012&monthsAhead=6
            app.MapGet("/forecast", (string partCode, int monthsAhead, IOptions<DataFilesOptions> files) =>
            {
                if (string.IsNullOrWhiteSpace(partCode))
                    return Results.BadRequest(new { error = "partCode is required." });

                if (monthsAhead <= 0)
                    return Results.BadRequest(new { error = "monthsAhead must be > 0." });

                var poPath = files.Value.PurchaseOrdersCsvPath;
                var cpiPath = files.Value.CpiCsvPath;

                if (string.IsNullOrWhiteSpace(poPath) || !File.Exists(poPath))
                    return Results.Problem($"PO CSV file not found. Path='{poPath}'", statusCode: 500);

                if (string.IsNullOrWhiteSpace(cpiPath) || !File.Exists(cpiPath))
                    return Results.Problem($"CPI CSV file not found. Path='{cpiPath}'", statusCode: 500);

                // Defaults baked in (as requested)
                const string DefaultCurrency = "USD";
                var options = new ForecastOptions
                {
                    UseLogTransform = true,
                    ConfidenceLevel = 0.95f
                };

                var poRepo = new CsvPurchaseOrderRepository(poPath);
                var cpiRepo = new CsvCpiRepository(cpiPath);
                var svc = new PriceForecastService(poRepo, cpiRepo);

                // Current month = first day of current month
                var today = DateTime.Today;
                var currentMonth = new DateTime(today.Year, today.Month, 1);

                // Previous purchase prices (monthly average nominal PRICE_PER_UNIT in USD)
                var poLines = poRepo.GetLines(partCode, DefaultCurrency);
                var monthlyNominal = MonthlySeriesBuilder.BuildMonthlyAvgPrice(poLines);

                var previousPrices = monthlyNominal
                    .OrderByDescending(x => x.Month)
                    .Take(10)
                    .OrderBy(x => x.Month)
                    .Select(x => new PreviousPriceDto
                    {
                        Month = x.Month,
                        PricePerUnit = x.AvgNominalPrice
                    })
                    .ToList();

                // Probe forecast (learn last training month)
                ForecastResult probe;
                try
                {
                    probe = svc.ForecastNominalPriceNextMonths(
                        partCode,
                        months: 1,
                        currencyCode: DefaultCurrency,
                        options: options);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }

                var lastTrainingMonth = probe.LastTrainingMonth;

                int monthsGap = MonthsBetween(lastTrainingMonth, currentMonth);
                if (monthsGap < 0) monthsGap = 0;

                int horizonNeeded = checked(monthsGap + monthsAhead);

                ForecastResult result;
                try
                {
                    result = svc.ForecastNominalPriceNextMonths(
                        partCode,
                        months: horizonNeeded,
                        currencyCode: DefaultCurrency,
                        options: options);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }

                // Forecast window is N months ahead of current month (starting next month)
                var displayStart = currentMonth.AddMonths(1);
                var displayEnd = currentMonth.AddMonths(monthsAhead);

                var forecast = result.Points
                    .Where(p => p.Month >= displayStart && p.Month <= displayEnd)
                    .OrderBy(p => p.Month)
                    .Select(p => new ForecastPointDto
                    {
                        Month = p.Month,
                        PricePrediction = p.NominalForecast
                    })
                    .ToList();

                var response = new ForecastResponseDto
                {
                    PartCode = result.PartCode,
                    Today = today,
                    CurrentMonth = currentMonth,
                    LastTrainingMonth = result.LastTrainingMonth,

                    Requested = new RequestedWindowDto
                    {
                        MonthsAhead = monthsAhead,
                        StartMonth = displayStart,
                        EndMonth = displayEnd
                    },

                    DataCoverage = new DataCoverageDto
                    {
                        PoRangeStart = result.Diagnostics.FirstPoMonth,
                        PoRangeEnd = result.Diagnostics.LastPoMonth,
                        CpiRangeStart = result.Diagnostics.FirstCpiMonth,
                        CpiRangeEnd = result.Diagnostics.LastCpiMonth,
                        AlignedStart = result.Diagnostics.FirstAlignedMonth,
                        AlignedEnd = result.Diagnostics.LastAlignedMonth,
                        MonthsBeforeJoin = result.Diagnostics.MonthlyPointsBeforeJoin,
                        MonthsAfterJoin = result.Diagnostics.MonthlyPointsUsed,
                        DroppedNoCpi = result.Diagnostics.MonthsDroppedDueToMissingCpi
                    },

                    PreviousPurchasePrices = previousPrices,
                    ForecastPrices = forecast,

                    Defaults = new DefaultsDto
                    {
                        Currency = DefaultCurrency,
                        UseLogTransform = true,
                        ConfidenceLevel = 0.95f
                    }
                };

                return Results.Ok(response);
            })
            .WithName("ForecastPrice")
            .Produces<ForecastResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

            app.Run();
        }

        static int MonthsBetween(DateTime fromMonth, DateTime toMonth)
        {
            var from = new DateTime(fromMonth.Year, fromMonth.Month, 1);
            var to = new DateTime(toMonth.Year, toMonth.Month, 1);
            return (to.Year - from.Year) * 12 + (to.Month - from.Month);
        }
    }

    // -------------------- Config options --------------------
    public sealed class DataFilesOptions
    {
        public string PurchaseOrdersCsvPath { get; set; } = "";
        public string CpiCsvPath { get; set; } = "";
    }

    // -------------------- Response DTOs --------------------
    public sealed class ForecastResponseDto
    {
        public string PartCode { get; set; } = "";
        public DateTime Today { get; set; }
        public DateTime CurrentMonth { get; set; }
        public DateTime LastTrainingMonth { get; set; }

        public RequestedWindowDto Requested { get; set; } = new();
        public DataCoverageDto DataCoverage { get; set; } = new();

        public List<PreviousPriceDto> PreviousPurchasePrices { get; set; } = new();
        public List<ForecastPointDto> ForecastPrices { get; set; } = new();

        public DefaultsDto Defaults { get; set; } = new();
    }

    public sealed class RequestedWindowDto
    {
        public int MonthsAhead { get; set; }
        public DateTime StartMonth { get; set; }
        public DateTime EndMonth { get; set; }
    }

    public sealed class DataCoverageDto
    {
        public DateTime? PoRangeStart { get; set; }
        public DateTime? PoRangeEnd { get; set; }
        public DateTime? CpiRangeStart { get; set; }
        public DateTime? CpiRangeEnd { get; set; }
        public DateTime? AlignedStart { get; set; }
        public DateTime? AlignedEnd { get; set; }

        public int MonthsBeforeJoin { get; set; }
        public int MonthsAfterJoin { get; set; }
        public int DroppedNoCpi { get; set; }
    }

    public sealed class PreviousPriceDto
    {
        public DateTime Month { get; set; }
        public decimal PricePerUnit { get; set; }
    }

    public sealed class ForecastPointDto
    {
        public DateTime Month { get; set; }
        public decimal PricePrediction { get; set; }
    }

    public sealed class DefaultsDto
    {
        public string Currency { get; set; } = "USD";
        public bool UseLogTransform { get; set; } = true;
        public float ConfidenceLevel { get; set; } = 0.95f;
    }
}