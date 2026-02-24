using System.Net;
using System.Net.Http.Json;
using WebAppPOForecaster.Models;

namespace WebAppPOForecaster.Services;

public sealed class ForecastApiClient
{
    private readonly HttpClient _http;

    public ForecastApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ForecastResponseDto> GetForecastAsync(string partCode, int monthsAhead, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(partCode))
            throw new ArgumentException("Part Code is required.", nameof(partCode));

        if (monthsAhead < 1 || monthsAhead > 60)
            throw new ArgumentOutOfRangeException(nameof(monthsAhead), "Months Ahead must be between 1 and 60.");

        var url = $"/forecast?partCode={Uri.EscapeDataString(partCode.Trim())}&monthsAhead={monthsAhead}";

        using var resp = await _http.GetAsync(url, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("No forecast found for that part code.");

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"API error ({(int)resp.StatusCode}): {body}");
        }

        var dto = await resp.Content.ReadFromJsonAsync<ForecastResponseDto>(cancellationToken: ct);

        if (dto is null)
            throw new InvalidOperationException("API returned an empty response.");

        return dto;
    }
}