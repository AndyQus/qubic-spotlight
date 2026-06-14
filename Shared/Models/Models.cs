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
    Click = 1
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
    public int SortOrder { get; set; }
    public AdStatus Status { get; set; } = AdStatus.Approved;
    public long ImpressionCount { get; set; }
    public long ClickCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? ApiKey { get; set; }
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
    public int SortOrder { get; set; }
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
