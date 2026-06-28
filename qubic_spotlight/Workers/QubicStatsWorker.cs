using qubic_spotlight.Infrastructure;
using qubic_spotlight.Services;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Workers;

// Pollt die Qubic-Stats im Hintergrund und hält den letzten Wert im Speicher.
// Das Dashboard liest nur diesen Cache -> schnell und schont die RPC.
public class QubicStatsWorker : BackgroundService
{
    public static volatile QubicStats? Latest;

    private readonly IServiceProvider _services;
    private readonly ILogger<QubicStatsWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public QubicStatsWorker(IServiceProvider services, ILogger<QubicStatsWorker> logger)
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
                var client = scope.ServiceProvider.GetRequiredService<QubicStatsClient>();
                var stats = await client.GetLatestStatsAsync(stoppingToken);
                if (stats is not null)
                {
                    Latest = stats;
                    // Kurspunkt für den 24h-Chart festhalten (gleicher Takt, kein Extra-Call).
                    if (stats.Price > 0)
                    {
                        var db = scope.ServiceProvider.GetRequiredService<LiteDbContext>();
                        db.AddPricePoint(stats.Price);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Stats poll failed: {Message}", ex.Message);
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { }
        }
    }
}
