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
    public List<PublicAd> Visible(int max = 50)
    {
        var ads = _db.GetVisibleAds()
            .OrderBy(_ => Random.Shared.Next())   // Rotation pro Abruf
            .Take(max);

        return ads.Select(a => new PublicAd
        {
            Id = a.Id,
            Title = a.Title,
            Description = a.Description,
            LinkUrl = a.LinkUrl,
            ImageUrl = a.ImageUrl,
            Ecosystem = a.Ecosystem
        }).ToList();
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
    }
}
