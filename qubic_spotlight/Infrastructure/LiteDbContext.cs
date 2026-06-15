using LiteDB;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Infrastructure;

// Eine einzige LiteDB-Datei, gekapselt hinter einem Lock — gleiches Muster wie
// in qubic_doge_stats. Bewusst einfach gehalten: klar benannte Methoden statt
// generischer Repository-Magie, damit Änderungen leicht fallen.
public class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly object _lock = new();

    public LiteDbContext(IConfiguration configuration)
    {
        var filename = configuration["LiteDb:Filename"] ?? "Data/spotlight.db";
        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
        if (!string.IsNullOrEmpty(dataDir))
        {
            var dbFile = Environment.GetEnvironmentVariable("LITEDB_FILE") ?? "spotlight.db";
            filename = Path.Combine(dataDir, dbFile);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filename))!);
        // Shared-Verbindung: mehrere Prozesse dürfen dieselbe DB öffnen (z. B.
        // dotnet watch + paralleler F5-Start im Dev). Verhindert den exklusiven
        // Datei-Lock von LiteDB ("being used by another process").
        _db = new LiteDatabase(new ConnectionString
        {
            Filename = filename,
            Connection = ConnectionType.Shared,
        });
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var ads = _db.GetCollection<Ad>("ads");
        ads.EnsureIndex(x => x.OwnerUserId);
        ads.EnsureIndex(x => x.IsActive);
        ads.EnsureIndex(x => x.StartDate);

        var users = _db.GetCollection<User>("users");
        users.EnsureIndex(x => x.Email, unique: true);
        users.EnsureIndex(x => x.ApiKey);

        var events = _db.GetCollection<AdEvent>("ad_events");
        events.EnsureIndex(x => x.AdId);
        events.EnsureIndex(x => x.Timestamp);
    }

    // ── Ads ──────────────────────────────────────────────────────────────────

    public List<Ad> GetAllAds()
    {
        lock (_lock)
            return _db.GetCollection<Ad>("ads")
                .Query().OrderBy(x => x.SortOrder).ToList();
    }

    public List<Ad> GetAdsByOwner(string ownerUserId)
    {
        lock (_lock)
            return _db.GetCollection<Ad>("ads")
                .Find(x => x.OwnerUserId == ownerUserId)
                .OrderBy(x => x.SortOrder).ToList();
    }

    // Öffentlich sichtbare Anzeigen (Sichtbarkeitsregel aus dem Konzept).
    // Bewusst In-Memory gefiltert: klar lesbar und unabhängig von der
    // Ausdrucks-Übersetzung von LiteDB (Datenmenge ist klein).
    public List<Ad> GetVisibleAds()
    {
        var today = DateTime.UtcNow.Date;
        lock (_lock)
            return _db.GetCollection<Ad>("ads").FindAll()
                .Where(x => x.IsActive
                        && x.Status == AdStatus.Approved
                        && x.StartDate.Date <= today
                        && (x.ExpiryDate == null || x.ExpiryDate.Value.Date >= today))
                .OrderBy(x => x.SortOrder)
                .ToList();
    }

    public Ad? GetAd(Guid id)
    {
        lock (_lock)
            return _db.GetCollection<Ad>("ads").FindById(id);
    }

    public void InsertAd(Ad ad)
    {
        lock (_lock)
            _db.GetCollection<Ad>("ads").Insert(ad);
    }

    public void UpdateAd(Ad ad)
    {
        ad.UpdatedAt = DateTime.UtcNow;
        lock (_lock)
            _db.GetCollection<Ad>("ads").Update(ad);
    }

    public void DeleteAd(Guid id)
    {
        lock (_lock)
        {
            _db.GetCollection<Ad>("ads").Delete(id);
            _db.GetCollection<AdEvent>("ad_events").DeleteMany(x => x.AdId == id);
        }
    }

    public int CountActiveByOwner(string ownerUserId)
    {
        lock (_lock)
            return _db.GetCollection<Ad>("ads")
                .Count(x => x.OwnerUserId == ownerUserId && x.IsActive);
    }

    public void IncrementImpression(Guid id)
    {
        lock (_lock)
        {
            var col = _db.GetCollection<Ad>("ads");
            var ad = col.FindById(id);
            if (ad is null) return;
            ad.ImpressionCount++;
            col.Update(ad);
        }
    }

    public void IncrementClick(Guid id)
    {
        lock (_lock)
        {
            var col = _db.GetCollection<Ad>("ads");
            var ad = col.FindById(id);
            if (ad is null) return;
            ad.ClickCount++;
            col.Update(ad);
        }
    }

    // ── Users ────────────────────────────────────────────────────────────────

    public List<User> GetAllUsers()
    {
        lock (_lock)
            return _db.GetCollection<User>("users").Query().OrderBy(x => x.Email).ToList();
    }

    public User? GetUserById(Guid id)
    {
        lock (_lock)
            return _db.GetCollection<User>("users").FindById(id);
    }

    public User? GetUserByEmail(string email)
    {
        lock (_lock)
            return _db.GetCollection<User>("users")
                .FindOne(x => x.Email == email.ToLowerInvariant());
    }

    public User? GetUserByApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        lock (_lock)
            return _db.GetCollection<User>("users").FindOne(x => x.ApiKey == apiKey);
    }

    public void InsertUser(User user)
    {
        user.Email = user.Email.ToLowerInvariant();
        lock (_lock)
            _db.GetCollection<User>("users").Insert(user);
    }

    public void UpdateUser(User user)
    {
        user.Email = user.Email.ToLowerInvariant();
        lock (_lock)
            _db.GetCollection<User>("users").Update(user);
    }

    public void DeleteUser(Guid id)
    {
        lock (_lock)
            _db.GetCollection<User>("users").Delete(id);
    }

    // ── Ad-Events (Tracking) ──────────────────────────────────────────────────

    public void InsertEvent(AdEvent ev)
    {
        lock (_lock)
            _db.GetCollection<AdEvent>("ad_events").Insert(ev);
    }

    public List<AdEvent> GetEventsForAd(Guid adId)
    {
        lock (_lock)
            return _db.GetCollection<AdEvent>("ad_events")
                .Find(x => x.AdId == adId).ToList();
    }

    public void Dispose() => _db.Dispose();
}
