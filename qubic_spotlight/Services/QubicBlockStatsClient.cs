using System.Text.Json;
using System.Text.Json.Serialization;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Services;

// Holt die Block-Kennzahlen des Mining-Pools von doge.qubic.tools/api/blocks/summary.
public class QubicBlockStatsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<QubicBlockStatsClient> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public QubicBlockStatsClient(HttpClient http, ILogger<QubicBlockStatsClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<QubicBlockStats?> GetBlockStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync("api/blocks/summary", ct);
            var d = JsonSerializer.Deserialize<Dto>(json, _json);
            if (d is null) return null;

            return new QubicBlockStats
            {
                CurrentEpoch              = d.CurrentEpoch,
                DogeTotalConfirmed        = d.DogeTotalConfirmed,
                DogeCurrentEpochConfirmed = d.DogeCurrentEpochConfirmed,
                LtcTotalConfirmed         = d.LtcTotalConfirmed,
                LtcCurrentEpochConfirmed  = d.LtcCurrentEpochConfirmed,
                UpdatedAt                 = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Qubic block stats unreachable: {Message}", ex.Message);
            return null;
        }
    }

    private sealed class Dto
    {
        [JsonPropertyName("currentEpoch")] public int CurrentEpoch { get; set; }
        [JsonPropertyName("dogeTotalConfirmed")] public int DogeTotalConfirmed { get; set; }
        [JsonPropertyName("dogeCurrentEpochConfirmed")] public int DogeCurrentEpochConfirmed { get; set; }
        [JsonPropertyName("ltcTotalConfirmed")] public int LtcTotalConfirmed { get; set; }
        [JsonPropertyName("ltcCurrentEpochConfirmed")] public int LtcCurrentEpochConfirmed { get; set; }
    }
}
