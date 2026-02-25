using System.Text.Json.Serialization;

namespace WebAppPOForecaster.Models;

public sealed class ForecastResponseDto
{
    [JsonPropertyName("partCode")]
    public string? PartCode { get; set; }

    [JsonPropertyName("today")]
    public DateTimeOffset Today { get; set; }

    [JsonPropertyName("currentMonth")]
    public DateTime CurrentMonth { get; set; }

    [JsonPropertyName("lastTrainingMonth")]
    public DateTime LastTrainingMonth { get; set; }

    [JsonPropertyName("requested")]
    public ForecastRequestedDto? Requested { get; set; }

    [JsonPropertyName("dataCoverage")]
    public DataCoverageDto? DataCoverage { get; set; }

    [JsonPropertyName("previousPurchasePrices")]
    public List<PreviousPurchasePriceDto> PreviousPurchasePrices { get; set; } = new();

    [JsonPropertyName("forecastPrices")]
    public List<ForecastPriceDto> ForecastPrices { get; set; } = new();

    [JsonPropertyName("defaults")]
    public ForecastDefaultsDto? Defaults { get; set; }
}

public sealed class ForecastRequestedDto
{
    [JsonPropertyName("monthsAhead")]
    public int MonthsAhead { get; set; }

    [JsonPropertyName("startMonth")]
    public DateTime StartMonth { get; set; }

    [JsonPropertyName("endMonth")]
    public DateTime EndMonth { get; set; }
}

public sealed class DataCoverageDto
{
    [JsonPropertyName("poRangeStart")]
    public DateTime PoRangeStart { get; set; }

    [JsonPropertyName("poRangeEnd")]
    public DateTime PoRangeEnd { get; set; }

    [JsonPropertyName("cpiRangeStart")]
    public DateTime CpiRangeStart { get; set; }

    [JsonPropertyName("cpiRangeEnd")]
    public DateTime CpiRangeEnd { get; set; }

    [JsonPropertyName("alignedStart")]
    public DateTime AlignedStart { get; set; }

    [JsonPropertyName("alignedEnd")]
    public DateTime AlignedEnd { get; set; }

    [JsonPropertyName("monthsBeforeJoin")]
    public int MonthsBeforeJoin { get; set; }

    [JsonPropertyName("monthsAfterJoin")]
    public int MonthsAfterJoin { get; set; }

    [JsonPropertyName("droppedNoCpi")]
    public int DroppedNoCpi { get; set; }
}

public sealed class PreviousPurchasePriceDto
{
    [JsonPropertyName("month")]
    public DateTime Month { get; set; }

    [JsonPropertyName("pricePerUnit")]
    public decimal PricePerUnit { get; set; }
}

public sealed class ForecastPriceDto
{
    [JsonPropertyName("month")]
    public DateTime Month { get; set; }

    // Your JSON has lots of decimal precision — keep decimal to avoid float rounding.
    [JsonPropertyName("pricePrediction")]
    public decimal PricePrediction { get; set; }
}

public sealed class ForecastDefaultsDto
{
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("useLogTransform")]
    public bool UseLogTransform { get; set; }

    [JsonPropertyName("confidenceLevel")]
    public decimal ConfidenceLevel { get; set; }
}