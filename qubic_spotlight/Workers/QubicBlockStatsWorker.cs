using qubic_spotlight.Services;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Workers;

// Pollt die Pool-Block-Kennzahlen (doge.qubic.tools) im Hintergrund und hält
// den letzten Wert im Speicher. Das Dashboard liest nur diesen Cache.
public class QubicBlockStatsWorker : BackgroundService
{
    public static volatile QubicBlockStats? Latest;

    private readonly IServiceProvider _services;
    private readonly ILogger<QubicBlockStatsWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public QubicBlockStatsWorker(IServiceProvider services, ILogger<QubicBlockStatsWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<QubicBlockStatsClient>();
                var stats = await client.GetBlockStatsAsync(stoppingToken);
                if (stats is not null) Latest = stats;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Block stats poll failed: {Message}", ex.Message);
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { }
        }
    }
}
