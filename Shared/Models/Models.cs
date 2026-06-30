namespace qubic_spotlight.Shared.Models;

// ── Rollen ───────────────────────────────────────────────────────────────────
// Einfache Konstanten statt Enum, damit sie 1:1 in [Authorize(Roles=...)] passen.
public static class Roles
{
    public const string Admin = "Admin";
    public const string Marketing = "Marketing";
    public const string Ecosystem = "Ecosystem";
}

public static class SpotlightLimits
{
    // Max. aktive Anzeigen pro Ecosystem-Partner (Manager unbegrenzt).
    public const int MaxActiveAdsPerOwner = 5;
}

public enum AdStatus
{
    Approved = 0,   // v1: alles sofort freigegeben
    Pending = 1,    // vorbereitet für späteren Freigabe-Workflow
    Rejected = 2
}

public enum AdEventType
{
    Impression = 0,
    Click = 1,
    Like = 2,       // 👍 auf der Spotlight-/Feed-Seite
    Dislike = 3     // 👎 auf der Spotlight-/Feed-Seite
}

// ── Datenmodelle (LiteDB-Collections) ────────────────────────────────────────

public class Ad
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string LinkUrl { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Ecosystem { get; set; }
    public string OwnerUserId { get; set; } = "";

    // Anzeigename des Autors (E-Mail des Erstellers). Wird NICHT persistiert
    // (siehe BsonMapper-Konfiguration im LiteDbContext), sondern beim Ausliefern
    // der Verwaltungsliste aus OwnerUserId aufgelöst.
    public string? OwnerEmail { get; set; }

    public AdStatus Status { get; set; } = AdStatus.Approved;

    // ── Priorisierung / "Pin" (nur Admin + Marketing) ────────────────────────
    // Wird das Flag gesetzt, übernimmt die Anzeige innerhalb des Zeitfensters
    // [PriorityStart, PriorityEnd] das Widget: ab Aktivierung für PriorityMinutes
    // global fixiert (PinnedUntil), danach rotieren die Anzeigen wieder normal.
    public bool Priority { get; set; }
    public int PriorityMinutes { get; set; } = 30;
    public DateTime? PriorityStart { get; set; }
    public DateTime? PriorityEnd { get; set; }
    public DateTime? PinnedUntil { get; set; }   // berechnet beim Speichern (UTC)

    public long ImpressionCount { get; set; }
    public long ClickCount { get; set; }

    // ── Bewertungen (Spotlight-/Feed-Seite, 👍/👎) ───────────────────────────
    // Denormalisierte Zähler, bei jedem Vote aus ad_events neu berechnet.
    public long LikeCount { get; set; }
    public long DislikeCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    // API-Key wird NICHT im Klartext gespeichert. Wir halten nur den SHA-256-Hash
    // (für die Authentifizierung) sowie die letzten 4 Zeichen + Erstelldatum für
    // eine maskierte Vorschau in der Oberfläche. Der volle Key wird ausschließlich
    // einmalig bei der Erzeugung zurückgegeben.
    public string? ApiKeyHash { get; set; }
    public string? ApiKeyLast4 { get; set; }
    public DateTime? ApiKeyCreatedAt { get; set; }

    public List<string> Roles { get; set; } = new();
    public string? Ecosystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AdEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdId { get; set; }
    public AdEventType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpHash { get; set; }
    public string? Referer { get; set; }
}

// ── DTOs (Eingaben / Ausgaben der API) ───────────────────────────────────────

// Eingabe beim Anlegen/Ändern einer Anzeige (UI und API gleichermaßen).
public class AdInput
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string LinkUrl { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Ecosystem { get; set; }

    // Priorisierung / "Pin" – nur von Admin/Marketing setzbar (siehe Ad).
    public bool Priority { get; set; }
    public int PriorityMinutes { get; set; } = 30;
    public DateTime? PriorityStart { get; set; }
    public DateTime? PriorityEnd { get; set; }
}

// Öffentliche Anzeige fürs Embed-Widget und Dashboard (nur das Nötige).
public class PublicAd
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string LinkUrl { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? Ecosystem { get; set; }

    // Ist diese Anzeige aktuell gepinnt? Dann fixiert das Widget die Pin-Gruppe
    // und lädt nach PinnedUntil neu, um wieder zu rotieren.
    public bool Pinned { get; set; }
    public DateTime? PinnedUntil { get; set; }

    // ── Felder für die Spotlight-/Feed-Seite ─────────────────────────────────
    // Vom /api/feed-Endpunkt befüllt (beim Widget/Dashboard bleiben sie 0/null).
    public DateTime CreatedAt { get; set; }
    public long ClickCount { get; set; }
    public long LikeCount { get; set; }
    public long DislikeCount { get; set; }
    // Stimme des aktuellen Besuchers: 1 = 👍, -1 = 👎, 0 = keine.
    public int MyVote { get; set; }
}

// Eingabe beim Abstimmen (👍/👎) auf der Spotlight-Seite.
// Value: 1 = Like, -1 = Dislike, 0 = Stimme zurücknehmen.
// VoterId: anonyme, im Browser (localStorage) erzeugte Kennung für die
// serverseitige Mehrfach-Vote-Begrenzung (kein Login nötig).
public class VoteRequest
{
    public int Value { get; set; }
    public string? VoterId { get; set; }
}

// Antwort nach dem Abstimmen: aktueller Stand der Anzeige + eigene Stimme.
public class VoteResult
{
    public Guid AdId { get; set; }
    public long LikeCount { get; set; }
    public long DislikeCount { get; set; }
    public int MyVote { get; set; }
}

// Klick-Statistik einer Anzeige für einen Zeitraum (Statistik-Tab im Admin).
public class AdClickStat
{
    public Guid AdId { get; set; }
    public string Title { get; set; } = "";
    public string? Ecosystem { get; set; }
    public string? ImageUrl { get; set; }
    public long Clicks { get; set; }
    public long Impressions { get; set; }
    public long Likes { get; set; }       // 👍 im Zeitraum
    public long Dislikes { get; set; }     // 👎 im Zeitraum
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
}

// Benutzer-Anlage/-Änderung durch Admin (ohne Passwort-Hash nach außen).
public class UserInput
{
    public string Email { get; set; } = "";
    public string? Password { get; set; }          // optional: nur setzen wenn ändern
    public List<string> Roles { get; set; } = new();
    public string? Ecosystem { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public string? Ecosystem { get; set; }
    public bool IsActive { get; set; }
    public bool HasApiKey { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Profil des aktuell angemeldeten Benutzers (für die Account-Seite).
// Enthält bewusst KEINEN vollständigen API-Key – nur eine maskierte Vorschau.
public class MeDto
{
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public string? Ecosystem { get; set; }
    public bool HasApiKey { get; set; }
    public string? ApiKeyPreview { get; set; }      // z. B. "qsp_••••••••a1b2"
    public DateTime? ApiKeyCreatedAt { get; set; }
}

// Selbst-Service Passwortänderung des angemeldeten Benutzers.
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

// Live-Werte des Qubic-Netzwerks (aus rpc.qubic.org/v1/latest-stats).
public class QubicStats
{
    public double Price { get; set; }
    public string MarketCap { get; set; } = "0";
    public string CirculatingSupply { get; set; } = "0";
    public long ActiveAddresses { get; set; }
    public int Epoch { get; set; }
    public long CurrentTick { get; set; }
    public long TicksInCurrentEpoch { get; set; }
    public long EmptyTicksInCurrentEpoch { get; set; }
    public double EpochTickQuality { get; set; }
    public string BurnedQus { get; set; } = "0";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Block-Kennzahlen des Qubic-Mining-Pools (aus doge.qubic.tools/api/blocks/summary).
// Großer Wert = Total bestätigt, Klammerwert = bestätigt in der aktuellen Epoche.
public class QubicBlockStats
{
    public int CurrentEpoch { get; set; }
    public int DogeTotalConfirmed { get; set; }
    public int DogeCurrentEpochConfirmed { get; set; }
    public int LtcTotalConfirmed { get; set; }
    public int LtcCurrentEpochConfirmed { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Ein Kurspunkt für den 24h-Chart. Der Server schreibt im Stats-Takt (60s)
// einen Punkt; alles älter als 24h wird verworfen.
public class PricePoint
{
    public DateTime Timestamp { get; set; }
    public double Price { get; set; }
}

// Einfacher benannter Zähler (z. B. Gesamt-Seitenbesuche der Spotlight-Seite).
public class Counter
{
    public const string SiteVisits = "site_visits";

    public string Id { get; set; } = "";
    public long Value { get; set; }
}

// Antwort der Besuchs-Endpunkte (/api/visit, /api/visits).
public class VisitCount
{
    public long Total { get; set; }
}

// Aggregierte Tageskennzahlen der Spotlight-Seite — bewusst schlank: genau EINE
// Zeile pro Tag, deren Zähler bei jedem Ereignis hochgezählt werden (kein
// Datensatz je Besuch). Id ist das Datum als "yyyy-MM-dd" (UTC). Clicks =
// Klicks auf eine Anzeige (= Anzeige angeschaut/geöffnet, ein Ereignis).
public class DailyStat
{
    public string Id { get; set; } = "";       // yyyy-MM-dd (UTC)
    public DateTime Date { get; set; }          // Tagesbeginn UTC (für Bereichsabfragen)
    public long Visits { get; set; }
    public long Clicks { get; set; }
    public long Likes { get; set; }
    public long Impressions { get; set; }
}

// Kumulativer Besuchszähler je Land (eine Zeile pro Land, Gesamtsumme). Id ist
// der ISO-3166-Alpha-2-Code (z. B. "DE"); "??" = unbekannt/nicht zugeordnet.
// Datensparsam: speichert NIE eine IP, nur den Ländercode und die Anzahl.
public class CountryStat
{
    public string Id { get; set; } = "";       // ISO-Ländercode oder "??"
    public long Visits { get; set; }
}

// Ein Punkt der Besucher-Zeitreihe (für die Admin-Charts). Bucket ist der
// Beginn des Intervalls (Tag/Monat, UTC); die Metriken sind die Summen im
// jeweiligen Intervall.
public class VisitorTimePoint
{
    public DateTime Bucket { get; set; }
    public long Visits { get; set; }
    public long Clicks { get; set; }       // Klicks auf eine Anzeige (= Anzeige angeschaut/geöffnet)
    public long Likes { get; set; }
    public long Impressions { get; set; }  // Einblendungen im Widget
}

// Besucher-Auswertung für ein Zeitfenster: Zeitreihe (Chart) + Summen + Länder.
public class VisitorStats
{
    public List<VisitorTimePoint> Series { get; set; } = new();
    public long TotalVisits { get; set; }
    public long TotalClicks { get; set; }
    public long TotalLikes { get; set; }
    public long TotalImpressions { get; set; }
    public List<CountryCount> Countries { get; set; } = new();
}

// Besuche je Land (absteigend sortiert). Country ist der ISO-3166-Alpha-2-Code
// (z. B. "DE"); "??" bzw. leer = unbekannt/nicht zugeordnet.
public class CountryCount
{
    public string Country { get; set; } = "";
    public long Visits { get; set; }
}
