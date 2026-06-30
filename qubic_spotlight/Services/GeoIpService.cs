using System.Net;
using MaxMind.GeoIP2;

namespace qubic_spotlight.Services;

// Ermittelt aus einer IP-Adresse den 2-Buchstaben-Ländercode (ISO 3166-1, z. B.
// "DE") über eine lokale MaxMind-GeoLite2-Country-Datenbank (.mmdb). Bewusst
// datensparsam: die IP wird ausschließlich flüchtig im Speicher zur Abfrage
// genutzt und NIE gespeichert — zurückgegeben wird nur der Ländercode.
//
// Die DB ist optional: fehlt die Datei (Pfad via GEOIP_DB bzw. DATA_DIR/GeoLite2-
// Country.mmdb), liefert der Dienst null und die Besuche werden ohne Land gezählt.
public sealed class GeoIpService : IDisposable
{
    private readonly DatabaseReader? _reader;
    private readonly ILogger<GeoIpService> _logger;

    public GeoIpService(IConfiguration cfg, ILogger<GeoIpService> logger)
    {
        _logger = logger;
        var path = ResolvePath(cfg);
        if (path is not null && File.Exists(path))
        {
            try
            {
                _reader = new DatabaseReader(path);
                _logger.LogInformation("GeoLite2-Country-DB geladen: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GeoLite2-DB konnte nicht geladen werden: {Path}", path);
            }
        }
        else
        {
            _logger.LogInformation("Keine GeoLite2-DB gefunden — Besuche werden ohne Land gezählt.");
        }
    }

    // Liefert den Ländercode (z. B. "DE") oder null, wenn keine DB vorhanden ist
    // bzw. die IP nicht zugeordnet werden kann (lokale/private IPs, Fehler).
    public string? LookupCountry(IPAddress? ip)
    {
        if (_reader is null || ip is null) return null;
        try
        {
            return _reader.Country(ip).Country.IsoCode;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolvePath(IConfiguration cfg)
    {
        var explicitPath = Environment.GetEnvironmentVariable("GEOIP_DB") ?? cfg["GeoIp:Database"];
        if (!string.IsNullOrWhiteSpace(explicitPath)) return explicitPath;

        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
        if (!string.IsNullOrEmpty(dataDir))
            return Path.Combine(dataDir, "GeoLite2-Country.mmdb");

        // Lokaler Standard (neben der DB-Datei im Data-Ordner).
        return Path.Combine("Data", "GeoLite2-Country.mmdb");
    }

    public void Dispose() => _reader?.Dispose();
}
