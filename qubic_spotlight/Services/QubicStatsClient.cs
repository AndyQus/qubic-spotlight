using System.Text.Json;
using System.Text.Json.Serialization;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Services;

// Holt die Netzwerk-Kennzahlen von rpc.qubic.org/v1/latest-stats.
public class QubicStatsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<QubicStatsClient> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public QubicStatsClient(HttpClient http, ILogger<QubicStatsClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<QubicStats?> GetLatestStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync("v1/latest-stats", ct);
            var wrap = JsonSerializer.Deserialize<Wrapper>(json, _json);
            var d = wrap?.Data;
            if (d is null) return null;

            return new QubicStats
            {
                Price = d.Price,
                MarketCap = d.MarketCap ?? "0",
                CirculatingSupply = d.CirculatingSupply ?? "0",
                ActiveAddresses = d.ActiveAddresses,
                Epoch = d.Epoch,
                CurrentTick = d.CurrentTick,
                TicksInCurrentEpoch = d.TicksInCurrentEpoch,
                EmptyTicksInCurrentEpoch = d.EmptyTicksInCurrentEpoch,
                EpochTickQuality = d.EpochTickQuality,
                BurnedQus = d.BurnedQus ?? "0",
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Qubic stats unreachable: {Message}", ex.Message);
            return null;
        }
    }

    private sealed class Wrapper
    {
        [JsonPropertyName("data")] public Data? Data { get; set; }
    }

    private sealed class Data
    {
        [JsonPropertyName("price")] public double Price { get; set; }
        [JsonPropertyName("marketCap")] public string? MarketCap { get; set; }
        [JsonPropertyName("circulatingSupply")] public string? CirculatingSupply { get; set; }
        [JsonPropertyName("activeAddresses")] public long ActiveAddresses { get; set; }
        [JsonPropertyName("epoch")] public int Epoch { get; set; }
        [JsonPropertyName("currentTick")] public long CurrentTick { get; set; }
        [JsonPropertyName("ticksInCurrentEpoch")] public long TicksInCurrentEpoch { get; set; }
        [JsonPropertyName("emptyTicksInCurrentEpoch")] public long EmptyTicksInCurrentEpoch { get; set; }
        [JsonPropertyName("epochTickQuality")] public double EpochTickQuality { get; set; }
        [JsonPropertyName("burnedQus")] public string? BurnedQus { get; set; }
    }
}
