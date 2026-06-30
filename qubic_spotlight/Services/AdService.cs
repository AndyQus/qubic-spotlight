using qubic_spotlight.Infrastructure;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Services;

// Geschäftsregeln rund um Anzeigen (Limit, Besitz). Bewusst kleine, klare
// Methoden — die DB-Zugriffe liegen im LiteDbContext.
public class AdService
{
    public const int MaxActivePerOwner = SpotlightLimits.MaxActiveAdsPerOwner;

    private readonly LiteDbContext _db;

    public AdService(LiteDbContext db) => _db = db;

    // Für die Verwaltungsliste: alle Anzeigen mit aufgelöstem Autor (OwnerEmail).
    public List<Ad> All()
    {
        var ads = _db.GetAllAds();
        var emails = _db.GetAllUsers().ToDictionary(u => u.Id.ToString(), u => u.Email);
        foreach (var ad in ads)
            ad.OwnerEmail = emails.TryGetValue(ad.OwnerUserId, out var email) ? email : null;
        return ads;
    }

    // Klick-Statistik pro Anzeige für [from, to), absteigend nach Klicks.
    // Anzeigen ohne Events im Zeitraum erscheinen mit 0 (volle Übersicht).
    public List<AdClickStat> ClickStats(DateTime from, DateTime to)
    {
        var counts = _db.GetEventCountsByAd(from, to);
        return _db.GetAllAds()
            .Select(a =>
            {
                counts.TryGetValue(a.Id, out var c);
                return new AdClickStat
                {
                    AdId = a.Id,
                    Title = a.Title,
                    Ecosystem = a.Ecosystem,
                    ImageUrl = a.ImageUrl,
                    Clicks = c.clicks,
                    Impressions = c.impressions,
                    Likes = c.likes,
                    Dislikes = c.dislikes
                };
            })
            .OrderByDescending(s => s.Clicks)
            .ThenByDescending(s => s.Impressions)
            .ToList();
    }
    public List<Ad> ByOwner(string ownerUserId) => _db.GetAdsByOwner(ownerUserId);
    public Ad? Get(Guid id) => _db.GetAd(id);

    // Öffentliche Liste fürs Widget/Dashboard, gemischt für faire Sichtbarkeit.
    // Sind gerade Anzeigen "gepinnt" (Priorität, im Zeitfenster, noch nicht
    // abgelaufen), übernehmen ausschließlich diese – sie rotieren untereinander,
    // alle übrigen pausieren, bis der Pin abgelaufen ist.
    public List<PublicAd> Visible(int max = 50)
    {
        var now = DateTime.UtcNow;
        var all = _db.GetVisibleAds();

        var pinned = all.Where(a => IsPinnedNow(a, now)).ToList();
        var pool = pinned.Count > 0 ? pinned : all;

        var ads = pool
            .OrderBy(_ => Random.Shared.Next())   // Rotation pro Abruf
            .Take(max);

        return ads.Select(a => new PublicAd
        {
            Id = a.Id,
            Title = a.Title,
            Description = a.Description,
            LinkUrl = a.LinkUrl,
            ImageUrl = a.ImageUrl,
            Ecosystem = a.Ecosystem,
            Pinned = pinned.Count > 0,
            // Frühestes Pin-Ende: danach soll das Widget neu laden und wieder rotieren.
            PinnedUntil = pinned.Count > 0 ? pinned.Min(p => p.PinnedUntil) : null
        }).ToList();
    }

    // ── Spotlight-/Feed-Seite ──────────────────────────────────────────────────
    // Liste aller öffentlich sichtbaren Anzeigen für die Feed-Seite, sortiert und
    // optional nach Ecosystem gefiltert. voterKey (gehasht) markiert die eigene
    // Stimme je Anzeige. Kein Zufall, kein Pin – hier zählt Aktualität/Beliebtheit.
    // sort: "top" = Trending (Score mit Zeit-Abfall), sonst "new" (neueste zuerst).
    public List<PublicAd> Feed(string? sort, string? ecosystem, string? voterKey, int max = 200)
    {
        var all = _db.GetVisibleAds();

        if (!string.IsNullOrWhiteSpace(ecosystem))
            all = all.Where(a => string.Equals(a.Ecosystem, ecosystem, StringComparison.OrdinalIgnoreCase)).ToList();

        var now = DateTime.UtcNow;
        IEnumerable<Ad> ordered = string.Equals(sort, "top", StringComparison.OrdinalIgnoreCase)
            ? all.OrderByDescending(a => TrendingScore(a, now)).ThenByDescending(a => a.CreatedAt)
            : all.OrderByDescending(a => a.CreatedAt);

        var votes = string.IsNullOrEmpty(voterKey)
            ? new Dictionary<Guid, int>()
            : _db.GetVotesByVoter(voterKey);

        return ordered.Take(max).Select(a => new PublicAd
        {
            Id = a.Id,
            Title = a.Title,
            Description = a.Description,
            LinkUrl = a.LinkUrl,
            ImageUrl = a.ImageUrl,
            Ecosystem = a.Ecosystem,
            CreatedAt = a.CreatedAt,
            ClickCount = a.ClickCount,
            LikeCount = a.LikeCount,
            DislikeCount = a.DislikeCount,
            MyVote = votes.TryGetValue(a.Id, out var v) ? v : 0
        }).ToList();
    }

    // Reddit-/HN-naher Score: (Likes − Dislikes) mit logarithmischer Dämpfung,
    // plus Zeit-Bonus, damit Neues mit guter Resonanz nach oben kommt.
    private static double TrendingScore(Ad a, DateTime now)
    {
        var net = a.LikeCount - a.DislikeCount;
        var sign = Math.Sign(net);
        var magnitude = Math.Log10(Math.Abs(net) + 1);
        var ageHours = Math.Max((now - a.CreatedAt).TotalHours, 0);
        var recency = 1.0 / Math.Pow(ageHours + 2, 0.5);   // jüngere Anzeigen leicht bevorzugt
        return sign * magnitude + recency;
    }

    // Verfügbare Ecosystem-Gruppen unter den sichtbaren Anzeigen (für Filter-Chips).
    public List<string> Ecosystems()
        => _db.GetVisibleAds()
            .Select(a => a.Ecosystem)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Registriert eine Stimme (👍/👎) und liefert den neuen Stand zurück.
    public VoteResult Vote(Guid adId, string voterKey, int value)
    {
        var clamped = Math.Sign(value);   // nur -1 / 0 / 1 zulassen
        var (likes, dislikes, myVote) = _db.SetVote(adId, voterKey, clamped);
        return new VoteResult { AdId = adId, LikeCount = likes, DislikeCount = dislikes, MyVote = myVote };
    }

    // Anzeige ist jetzt aktiv gepinnt, wenn das Flag gesetzt ist, die aktuelle
    // Zeit im [PriorityStart, PriorityEnd]-Fenster liegt und PinnedUntil (ab
    // Aktivierung gesetzt) noch in der Zukunft liegt.
    private static bool IsPinnedNow(Ad a, DateTime now)
    {
        if (!a.Priority) return false;
        if (a.PriorityStart is { } s && now < s) return false;
        if (a.PriorityEnd is { } e && now > e) return false;
        return a.PinnedUntil is { } until && now < until;
    }

    // isManager = Admin oder Marketing (dürfen alles, ohne Limit).
    public (bool ok, string? error, Ad? ad) Create(AdInput input, string ownerUserId, bool isManager)
    {
        if (!isManager && input.IsActive && _db.CountActiveByOwner(ownerUserId) >= MaxActivePerOwner)
            return (false, $"Limit von {MaxActivePerOwner} aktiven Anzeigen erreicht.", null);

        if (DuplicateLinkUrl(input.LinkUrl, null) is { } dup)
            return (false, DuplicateUrlError(dup), null);

        var ad = new Ad { OwnerUserId = ownerUserId };
        Apply(ad, input);
        _db.InsertAd(ad);
        return (true, null, ad);
    }

    public (bool ok, string? error, Ad? ad) Update(Guid id, AdInput input, string currentUserId, bool isManager)
    {
        var ad = _db.GetAd(id);
        if (ad is null) return (false, "Anzeige nicht gefunden.", null);
        if (!isManager && ad.OwnerUserId != currentUserId) return (false, "Keine Berechtigung.", null);

        // Wird gerade von inaktiv auf aktiv gestellt? Dann Limit prüfen.
        if (!isManager && input.IsActive && !ad.IsActive
            && _db.CountActiveByOwner(currentUserId) >= MaxActivePerOwner)
            return (false, $"Limit von {MaxActivePerOwner} aktiven Anzeigen erreicht.", null);

        if (DuplicateLinkUrl(input.LinkUrl, id) is { } dup)
            return (false, DuplicateUrlError(dup), null);

        Apply(ad, input);
        _db.UpdateAd(ad);
        return (true, null, ad);
    }

    public (bool ok, string? error) Delete(Guid id, string currentUserId, bool isManager)
    {
        var ad = _db.GetAd(id);
        if (ad is null) return (false, "Anzeige nicht gefunden.");
        if (!isManager && ad.OwnerUserId != currentUserId) return (false, "Keine Berechtigung.");
        _db.DeleteAd(id);
        return (true, null);
    }

    // Liefert eine bereits existierende Anzeige mit derselben Ziel-URL (oder null).
    // Beim Update wird die Anzeige selbst (excludeId) ausgenommen.
    private Ad? DuplicateLinkUrl(string? linkUrl, Guid? excludeId)
        => _db.FindByLinkUrl(linkUrl ?? string.Empty, excludeId);

    // Maschinenlesbarer Fehlercode + Titel der kollidierenden Anzeige. Der Client
    // übersetzt das anhand der aktiven Sprache (siehe Translations: addlg.duplicateUrl).
    private static string DuplicateUrlError(Ad dup) => $"duplicate_url:{dup.Title}";

    private static void Apply(Ad ad, AdInput input)
    {
        ad.Title = input.Title.Trim();
        ad.Description = input.Description.Trim();
        ad.LinkUrl = input.LinkUrl.Trim();
        ad.ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? null : input.ImageUrl.Trim();
        ad.StartDate = input.StartDate.Date;
        ad.ExpiryDate = input.ExpiryDate?.Date;
        ad.IsActive = input.IsActive;
        ad.Ecosystem = string.IsNullOrWhiteSpace(input.Ecosystem) ? null : input.Ecosystem.Trim();

        ApplyPriority(ad, input);
    }

    // Übernimmt die Priorisierung und berechnet PinnedUntil neu. Der Pin läuft
    // ab Fensterstart (oder jetzt, falls das Fenster schon offen ist) für
    // PriorityMinutes – jedoch nie über PriorityEnd hinaus.
    private static void ApplyPriority(Ad ad, AdInput input)
    {
        ad.Priority = input.Priority;
        ad.PriorityMinutes = Math.Clamp(input.PriorityMinutes, 1, 24 * 60);
        ad.PriorityStart = input.PriorityStart;
        ad.PriorityEnd = input.PriorityEnd;

        if (!input.Priority)
        {
            ad.PinnedUntil = null;
            return;
        }

        var begin = input.PriorityStart is { } s && s > DateTime.UtcNow ? s : DateTime.UtcNow;
        var until = begin.AddMinutes(ad.PriorityMinutes);
        if (input.PriorityEnd is { } e && until > e) until = e;
        ad.PinnedUntil = until;
    }
}
