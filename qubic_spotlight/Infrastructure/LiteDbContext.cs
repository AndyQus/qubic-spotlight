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
        users.EnsureIndex(x => x.ApiKeyHash);

        var events = _db.GetCollection<AdEvent>("ad_events");
        events.EnsureIndex(x => x.AdId);
        events.EnsureIndex(x => x.Timestamp);
        events.EnsureIndex(x => x.IpHash);
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

    // ── Bewertungen (👍/👎) ───────────────────────────────────────────────────
    // Setzt die Stimme eines anonymen Besuchers (identifiziert über voterKey,
    // der bereits aus VoterId/IP gehasht wurde). Pro Besucher genau eine Stimme
    // je Anzeige: alte Vote-Events werden entfernt, das neue (falls value != 0)
    // eingefügt, danach die Zähler auf der Anzeige aus den Events neu berechnet.
    // value: 1 = Like, -1 = Dislike, 0 = Stimme zurücknehmen.
    // Rückgabe: aktuelle Zähler + die nun gültige eigene Stimme.
    public (long likes, long dislikes, int myVote) SetVote(Guid adId, string voterKey, int value)
    {
        lock (_lock)
        {
            var ads = _db.GetCollection<Ad>("ads");
            var ad = ads.FindById(adId);
            if (ad is null) return (0, 0, 0);

            var events = _db.GetCollection<AdEvent>("ad_events");

            // Bestehende Stimme dieses Besuchers für diese Anzeige entfernen.
            events.DeleteMany(x => x.AdId == adId && x.IpHash == voterKey
                && (x.Type == AdEventType.Like || x.Type == AdEventType.Dislike));

            var myVote = 0;
            if (value == 1)
            {
                events.Insert(new AdEvent { AdId = adId, Type = AdEventType.Like, IpHash = voterKey });
                myVote = 1;
            }
            else if (value == -1)
            {
                events.Insert(new AdEvent { AdId = adId, Type = AdEventType.Dislike, IpHash = voterKey });
                myVote = -1;
            }

            // Zähler aus den Events neu berechnen (klein & konsistent).
            ad.LikeCount = events.Count(x => x.AdId == adId && x.Type == AdEventType.Like);
            ad.DislikeCount = events.Count(x => x.AdId == adId && x.Type == AdEventType.Dislike);
            ads.Update(ad);

            return (ad.LikeCount, ad.DislikeCount, myVote);
        }
    }

    // Aktuelle Stimmen eines Besuchers über alle Anzeigen: AdId -> 1 (👍) / -1 (👎).
    // Dient dem Feed, um die eigenen Buttons hervorzuheben.
    public Dictionary<Guid, int> GetVotesByVoter(string voterKey)
    {
        var result = new Dictionary<Guid, int>();
        if (string.IsNullOrEmpty(voterKey)) return result;
        lock (_lock)
        {
            var events = _db.GetCollection<AdEvent>("ad_events")
                .Find(x => x.IpHash == voterKey
                    && (x.Type == AdEventType.Like || x.Type == AdEventType.Dislike));
            foreach (var ev in events)
                result[ev.AdId] = ev.Type == AdEventType.Like ? 1 : -1;
        }
        return result;
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

    // Lookup über den gespeicherten SHA-256-Hash des Keys (nicht den Klartext).
    public User? GetUserByApiKeyHash(string apiKeyHash)
    {
        if (string.IsNullOrWhiteSpace(apiKeyHash)) return null;
        lock (_lock)
            return _db.GetCollection<User>("users").FindOne(x => x.ApiKeyHash == apiKeyHash);
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

    // Klicks + Impressionen + 👍/👎 pro Anzeige im Zeitfenster [from, to). Grundlage
    // für den Statistik-Tab — echte Werte aus den Event-Timestamps, kein Gesamt-Count.
    public Dictionary<Guid, (long clicks, long impressions, long likes, long dislikes)> GetEventCountsByAd(DateTime from, DateTime to)
    {
        lock (_lock)
        {
            var events = _db.GetCollection<AdEvent>("ad_events")
                .Find(x => x.Timestamp >= from && x.Timestamp < to);

            var result = new Dictionary<Guid, (long clicks, long impressions, long likes, long dislikes)>();
            foreach (var ev in events)
            {
                result.TryGetValue(ev.AdId, out var cur);
                switch (ev.Type)
                {
                    case AdEventType.Click: cur.clicks++; break;
                    case AdEventType.Like: cur.likes++; break;
                    case AdEventType.Dislike: cur.dislikes++; break;
                    default: cur.impressions++; break;
                }
                result[ev.AdId] = cur;
            }
            return result;
        }
    }

    public void Dispose() => _db.Dispose();
}
