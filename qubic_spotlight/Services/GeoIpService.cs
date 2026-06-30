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
// Die DB ist optional: fehlt die Datei (gesucht via GEOIP_DB, im Daten-Volume
// DATA_DIR, im fest ins Image gebackenen GeoData neben der App und in lokalen
// Data-/GeoData-Ordnern — siehe ResolvePath), liefert der Dienst null und die
// Besuche werden ohne Land gezählt.
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
    // bzw. die IP nicht zugeordnet werden kann.
    //
    // Sonderfall: lokale/private IPs (Loopback, LAN) kennt db-ip nicht. Sie
    // bekommen den Code "LO" statt anonymes "??", damit man lokale Aufrufe (z. B.
    // Entwicklung oder fehlendes Proxy-Forwarding) in der Statistik erkennt.
    // Bleibt es null, zählt die Besuchszählung den Treffer als "??" (Unbekannt).
    public string? LookupCountry(IPAddress? ip)
    {
        if (ip is null) return null;

        if (IsLocal(ip)) return "LO";

        if (_reader is null) return null;
        try
        {
            // TryCountry wirft nicht, wenn die IP nicht in der DB enthalten ist.
            if (_reader.TryCountry(ip, out var resp))
            {
                var code = resp?.Country.IsoCode;
                if (!string.IsNullOrWhiteSpace(code)) return code;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Länderermittlung fehlgeschlagen.");
            return null;
        }
    }

    // Loopback (127.0.0.1/::1), privates LAN (10/8, 172.16/12, 192.168/16) und
    // IPv6-Link-Local (fe80::/10) bzw. Unique-Local (fc00::/7) — alles, was db-ip
    // ohnehin keinem Land zuordnen kann.
    private static bool IsLocal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;

        // IPv4 (auch IPv4-mapped IPv6 wie ::ffff:192.168.0.1) auf private Bereiche prüfen.
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            || ip.IsIPv4MappedToIPv6)
        {
            var b = ip.MapToIPv4().GetAddressBytes();
            if (b[0] == 10) return true;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            if (b[0] == 192 && b[1] == 168) return true;
            if (b[0] == 127) return true;
            if (b[0] == 169 && b[1] == 254) return true; // link-local 169.254/16
        }

        // IPv6 Unique-Local fc00::/7.
        var raw = ip.GetAddressBytes();
        if (raw.Length == 16 && (raw[0] & 0xFE) == 0xFC) return true;

        return false;
    }

    // Bekannte Dateinamen (db-ip bevorzugt, GeoLite2 als Alt-Fallback).
    private static readonly string[] KnownFiles =
        { "dbip-country-lite.mmdb", "GeoLite2-Country.mmdb" };

    private static string? ResolvePath(IConfiguration cfg)
    {
        // 1) Expliziter Pfad gewinnt (GEOIP_DB bzw. Konfig).
        var explicitPath = Environment.GetEnvironmentVariable("GEOIP_DB") ?? cfg["GeoIp:Database"];
        if (!string.IsNullOrWhiteSpace(explicitPath)) return explicitPath;

        // 2) Mehrere Verzeichnisse in fester Reihenfolge durchsuchen:
        //    a) Daten-Volume (DATA_DIR) — falls dort manuell eine DB hinterlegt ist,
        //    b) das fest ins Image gebackene GeoData neben der App (Prod-Standard),
        //    c) lokale Data-/GeoData-Ordner für die Entwicklung.
        // appBase ist das Verzeichnis der laufenden App (im Container /app),
        // nicht das Arbeitsverzeichnis — so wird /app/GeoData zuverlässig gefunden.
        var appBase = AppContext.BaseDirectory;
        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");

        var dirs = new[]
        {
            string.IsNullOrEmpty(dataDir) ? null : dataDir,
            Path.Combine(appBase, "GeoData"),
            Path.Combine(appBase, "Data"),
            "GeoData",
            "Data",
        };

        foreach (var dir in dirs)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            foreach (var name in KnownFiles)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        }

        // Nichts gefunden — Standardpfad zurückgeben (Existenzprüfung im Ctor,
        // dort wird sauber geloggt, dass ohne Land gezählt wird).
        return Path.Combine(appBase, "GeoData", KnownFiles[0]);
    }

    public void Dispose() => _reader?.Dispose();
}
