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

    public List<Ad> All() => _db.GetAllAds();
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
        ad.SortOrder = input.SortOrder;

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
