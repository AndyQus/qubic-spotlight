using System.Net;
using MaxMind.GeoIP2;

namespace qubic_spotlight.Services;

// Ermittelt aus einer IP-Adresse den 2-Buchstaben-Ländercode (ISO 3166-1, z. B.
// "DE") über eine lokale Country-Datenbank im MMDB-Format. Bewusst datensparsam:
// die IP wird ausschließlich flüchtig im Speicher zur Abfrage genutzt und NIE
// gespeichert — zurückgegeben wird nur der Ländercode. Es findet KEIN externer
// Aufruf statt; die IP verlässt den Server nicht.
//
// Empfohlene DB: db-ip.com "IP to Country Lite" (kostenlos, OHNE Account,
// CC-BY-4.0). Das MMDB-Format wird vom MaxMind-Reader generisch gelesen, daher
// funktioniert auch eine GeoLite2-Country.mmdb, falls vorhanden.
//
// Die DB ist optional: fehlt die Datei (Pfad via GEOIP_DB bzw.
// DATA_DIR/dbip-country-lite.mmdb), liefert der Dienst null und die Besuche
// werden ohne Land gezählt.
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
                _logger.LogInformation("Country-Geo-DB geladen: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Country-Geo-DB konnte nicht geladen werden: {Path}", path);
            }
        }
        else
        {
            _logger.LogInformation("Keine Country-Geo-DB gefunden — Besuche werden ohne Land gezählt.");
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

    // Bekannte Dateinamen (db-ip bevorzugt, GeoLite2 als Alt-Fallback).
    private static readonly string[] KnownFiles =
        { "dbip-country-lite.mmdb", "GeoLite2-Country.mmdb" };

    private static string? ResolvePath(IConfiguration cfg)
    {
        // 1) Expliziter Pfad gewinnt (GEOIP_DB bzw. Konfig).
        var explicitPath = Environment.GetEnvironmentVariable("GEOIP_DB") ?? cfg["GeoIp:Database"];
        if (!string.IsNullOrWhiteSpace(explicitPath)) return explicitPath;

        // 2) Im Daten-Volume bzw. lokalen Data-Ordner nach bekannten Namen suchen.
        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
        var baseDir = string.IsNullOrEmpty(dataDir) ? "Data" : dataDir;
        foreach (var name in KnownFiles)
        {
            var candidate = Path.Combine(baseDir, name);
            if (File.Exists(candidate)) return candidate;
        }

        // Nichts gefunden — Standardname zurückgeben (Existenzprüfung im Ctor).
        return Path.Combine(baseDir, KnownFiles[0]);
    }

    public void Dispose() => _reader?.Dispose();
}
