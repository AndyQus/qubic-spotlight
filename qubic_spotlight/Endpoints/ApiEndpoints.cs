using System.Security.Cryptography;
using System.Text;
using qubic_spotlight.Infrastructure;
using qubic_spotlight.Services;
using qubic_spotlight.Shared.Models;
using qubic_spotlight.Workers;

namespace qubic_spotlight.Endpoints;

public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ── Öffentlich ────────────────────────────────────────────────────────
        api.MapGet("/ads", (AdService ads) => Results.Ok(ads.Visible()))
           .WithSummary("Aktive Anzeigen (öffentlich, fürs Widget/Dashboard)");

        api.MapGet("/qubic/stats", () =>
            QubicStatsWorker.Latest is { } s ? Results.Ok(s) : Results.NoContent())
           .WithSummary("Qubic-Netzwerk-Kennzahlen (gecacht)");

        api.MapGet("/qubic/blocks", () =>
            QubicBlockStatsWorker.Latest is { } b ? Results.Ok(b) : Results.NoContent())
           .WithSummary("DOGE/LTC-Block-Kennzahlen des Mining-Pools (gecacht)");

        api.MapGet("/qubic/price-history", (LiteDbContext db) => Results.Ok(db.GetPriceHistory()))
           .WithSummary("Qubic-Kurs der letzten 24h (für den Chart)");

        // Seitenbesuche der Spotlight-Seite. Der Client zählt pro Browser-Session
        // genau einmal hoch (POST) und liest den Gesamtwert für die Kachel (GET).
        // Beim Zählen wird die IP NUR flüchtig zur Länderermittlung genutzt und
        // nicht gespeichert — abgelegt wird ausschließlich der Ländercode.
        api.MapPost("/visit", (HttpContext ctx, LiteDbContext db, GeoIpService geo) =>
        {
            var country = geo.LookupCountry(ClientIp(ctx));
            return Results.Ok(new VisitCount { Total = db.IncrementVisit(country) });
        }).WithSummary("Einen Seitenbesuch zählen");

        api.MapGet("/visits", (LiteDbContext db) => Results.Ok(new VisitCount { Total = db.GetVisitCount() }))
           .WithSummary("Gesamtzahl der Seitenbesuche");

        // ── Spotlight-/Feed-Seite ───────────────────────────────────────────────
        // Sortierter, optional gefilterter Anzeigen-Strom für die zweite öffentliche
        // Seite. voterId (anonyme Browser-Kennung) markiert die eigene Stimme.
        api.MapGet("/feed", (AdService ads, HttpContext ctx, string? sort, string? ecosystem, string? voterId) =>
            Results.Ok(ads.Feed(sort, ecosystem, VoterKey(ctx, voterId))))
           .WithSummary("Anzeigen-Feed (sort=new|top, optional ecosystem)");

        api.MapGet("/feed/ecosystems", (AdService ads) => Results.Ok(ads.Ecosystems()))
           .WithSummary("Verfügbare Ecosystem-Gruppen (für Feed-Filter)");

        // Stimme abgeben (👍/👎). Value: 1 = Like, -1 = Dislike, 0 = zurücknehmen.
        api.MapPost("/ads/{id:guid}/vote", (Guid id, VoteRequest req, HttpContext ctx, AdService ads, LiteDbContext db) =>
        {
            if (db.GetAd(id) is null) return Results.NotFound();
            return Results.Ok(ads.Vote(id, VoterKey(ctx, req.VoterId), req.Value));
        }).WithSummary("Anzeige bewerten (Like/Dislike)");

        // Impression-Zählung (Beacon vom Widget)
        api.MapPost("/ads/{id:guid}/impression", (Guid id, HttpContext ctx, LiteDbContext db) =>
        {
            if (db.GetAd(id) is null) return Results.NotFound();
            db.IncrementImpression(id);
            db.InsertEvent(new AdEvent
            {
                AdId = id,
                Type = AdEventType.Impression,
                IpHash = HashIp(ctx),
                Referer = ctx.Request.Headers.Referer.ToString()
            });
            return Results.NoContent();
        }).WithSummary("Eine Einblendung zählen");

        // Klick-Zählung + Weiterleitung auf die Ziel-URL
        api.MapGet("/ads/{id:guid}/click", (Guid id, HttpContext ctx, LiteDbContext db) =>
        {
            var ad = db.GetAd(id);
            if (ad is null) return Results.NotFound();
            db.IncrementClick(id);
            db.InsertEvent(new AdEvent
            {
                AdId = id,
                Type = AdEventType.Click,
                IpHash = HashIp(ctx),
                Referer = ctx.Request.Headers.Referer.ToString()
            });
            var url = ad.LinkUrl;
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;
            return Results.Redirect(url);
        }).WithSummary("Klick zählen und weiterleiten");

        // ── Auth ──────────────────────────────────────────────────────────────
        api.MapPost("/auth/login", (LoginRequest req, UserService users, TokenService tokens) =>
        {
            var user = users.Authenticate(req.Email, req.Password);
            if (user is null) return Results.Unauthorized();
            var (token, expires) = tokens.Create(user);
            return Results.Ok(new LoginResponse
            {
                Token = token,
                ExpiresAt = expires,
                Email = user.Email,
                Roles = user.Roles
            });
        }).WithSummary("Login -> JWT");

        // ── Eigene Anzeigen (JWT ODER API-Key) ─────────────────────────────────
        var mine = api.MapGroup("/my").RequireAuthorization("ApiUser");

        // Profil des angemeldeten Benutzers (Account-Seite).
        mine.MapGet("/me", (HttpContext ctx, UserService users) =>
        {
            var me = users.GetMe(Guid.Parse(ctx.User.UserId()));
            return me is null ? Results.NotFound() : Results.Ok(me);
        }).WithSummary("Profil des angemeldeten Benutzers");

        // Eigenes Passwort ändern (prüft das aktuelle Passwort).
        mine.MapPost("/password", (ChangePasswordRequest req, HttpContext ctx, UserService users) =>
        {
            var (ok, error) = users.ChangePassword(
                Guid.Parse(ctx.User.UserId()), req.CurrentPassword, req.NewPassword);
            return ok ? Results.NoContent() : Results.BadRequest(new { error });
        }).WithSummary("Eigenes Passwort ändern");

        mine.MapGet("/ads", (HttpContext ctx, AdService ads) =>
            Results.Ok(ads.ByOwner(ctx.User.UserId())));

        mine.MapGet("/ads/{id:guid}", (Guid id, HttpContext ctx, AdService ads) =>
        {
            var ad = ads.Get(id);
            if (ad is null || ad.OwnerUserId != ctx.User.UserId()) return Results.NotFound();
            return Results.Ok(ad);
        });

        mine.MapPost("/ads", (AdInput input, HttpContext ctx, AdService ads) =>
        {
            // Ecosystem-Partner werden fest auf ihre eigene Gruppe gesetzt.
            if (!ctx.User.IsManager())
                input.Ecosystem = ctx.User.FindFirst("ecosystem")?.Value;
            var (ok, error, ad) = ads.Create(input, ctx.User.UserId(), ctx.User.IsManager());
            return ok ? Results.Ok(ad) : Results.BadRequest(new { error });
        });

        mine.MapPut("/ads/{id:guid}", (Guid id, AdInput input, HttpContext ctx, AdService ads) =>
        {
            var (ok, error, ad) = ads.Update(id, input, ctx.User.UserId(), ctx.User.IsManager());
            return ok ? Results.Ok(ad) : Results.BadRequest(new { error });
        });

        mine.MapDelete("/ads/{id:guid}", (Guid id, HttpContext ctx, AdService ads) =>
        {
            var (ok, error) = ads.Delete(id, ctx.User.UserId(), ctx.User.IsManager());
            return ok ? Results.NoContent() : Results.BadRequest(new { error });
        });

        mine.MapPost("/apikey", (HttpContext ctx, UserService users) =>
        {
            var key = users.RegenerateApiKey(Guid.Parse(ctx.User.UserId()));
            return key is null ? Results.NotFound() : Results.Ok(new { apiKey = key });
        }).WithSummary("Eigenen API-Key (neu) erzeugen");

        // ── Verwaltung aller Anzeigen (Admin + Marketing) ──────────────────────
        var manage = api.MapGroup("/admin/ads").RequireAuthorization("Manager");

        manage.MapGet("", (AdService ads) => Results.Ok(ads.All()));

        // Klick-Statistik pro Anzeige im Zeitfenster [from, to) (UTC, ISO-8601).
        // Fehlende Grenzen: alles bis jetzt bzw. ab Epoche.
        manage.MapGet("/stats", (AdService ads, DateTime? from, DateTime? to) =>
        {
            var fromUtc = from?.ToUniversalTime() ?? DateTime.MinValue;
            var toUtc = to?.ToUniversalTime() ?? DateTime.UtcNow;
            return Results.Ok(ads.ClickStats(fromUtc, toUtc));
        }).WithSummary("Klick-/Impression-Statistik pro Anzeige im Zeitraum");

        // Besucher-Statistik (Zeitreihe + Länder) im Zeitfenster [from, to).
        // bucket=day|month steuert die Auflösung der Zeitreihe (Tag bzw. Monat).
        manage.MapGet("/visitors", (LiteDbContext db, DateTime? from, DateTime? to, string? bucket) =>
        {
            var fromUtc = from?.ToUniversalTime() ?? DateTime.MinValue;
            var toUtc = to?.ToUniversalTime() ?? DateTime.UtcNow;
            Func<DateTime, DateTime> bucketStart = bucket == "month"
                ? d => new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                : d => d.Date;
            return Results.Ok(db.GetVisitorStats(fromUtc, toUtc, bucketStart));
        }).WithSummary("Besucher-Statistik (Zeitreihe + Länder) im Zeitraum");

        manage.MapPost("", (AdInput input, HttpContext ctx, AdService ads) =>
        {
            var (ok, error, ad) = ads.Create(input, ctx.User.UserId(), isManager: true);
            return ok ? Results.Ok(ad) : Results.BadRequest(new { error });
        });

        manage.MapPut("/{id:guid}", (Guid id, AdInput input, HttpContext ctx, AdService ads) =>
        {
            var (ok, error, ad) = ads.Update(id, input, ctx.User.UserId(), isManager: true);
            return ok ? Results.Ok(ad) : Results.BadRequest(new { error });
        });

        manage.MapDelete("/{id:guid}", (Guid id, HttpContext ctx, AdService ads) =>
        {
            var (ok, error) = ads.Delete(id, ctx.User.UserId(), isManager: true);
            return ok ? Results.NoContent() : Results.BadRequest(new { error });
        });

        // ── Benutzerverwaltung (nur Admin) ─────────────────────────────────────
        var admin = api.MapGroup("/admin/users").RequireAuthorization("Admin");

        admin.MapGet("", (UserService users) => Results.Ok(users.All()));

        admin.MapPost("", (UserInput input, UserService users) =>
        {
            var (ok, error, user) = users.Create(input);
            return ok ? Results.Ok(user) : Results.BadRequest(new { error });
        });

        admin.MapPut("/{id:guid}", (Guid id, UserInput input, UserService users) =>
        {
            var (ok, error, user) = users.Update(id, input);
            return ok ? Results.Ok(user) : Results.BadRequest(new { error });
        });

        admin.MapDelete("/{id:guid}", (Guid id, UserService users) =>
        {
            var (ok, error) = users.Delete(id);
            return ok ? Results.NoContent() : Results.BadRequest(new { error });
        });

        admin.MapPost("/{id:guid}/apikey", (Guid id, UserService users) =>
        {
            var key = users.RegenerateApiKey(id);
            return key is null ? Results.NotFound() : Results.Ok(new { apiKey = key });
        });

        // ── Bild-Upload (eingeloggte Benutzer) ─────────────────────────────────
        api.MapPost("/uploads", async (HttpRequest req, IWebHostEnvironment env, IConfiguration cfg) =>
        {
            if (!req.HasFormContentType) return Results.BadRequest(new { error = "Keine Datei." });
            var form = await req.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file is null || file.Length == 0) return Results.BadRequest(new { error = "Keine Datei." });
            if (file.Length > 500 * 1024) return Results.BadRequest(new { error = "Datei > 500 KB." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            string[] allowed = { ".png", ".jpg", ".jpeg", ".svg", ".webp" };
            if (!allowed.Contains(ext)) return Results.BadRequest(new { error = "Nur PNG/JPG/SVG/WebP." });

            var dir = UploadsDir(env);
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid():N}{ext}";
            await using (var stream = File.Create(Path.Combine(dir, name)))
                await file.CopyToAsync(stream);

            return Results.Ok(new { url = $"/uploads/{name}" });
        }).RequireAuthorization("ApiUser").DisableAntiforgery().WithSummary("Bild hochladen (<=500 KB)");
    }

    // Uploads liegen im Volume (DATA_DIR/uploads) bzw. wwwroot/uploads lokal.
    public static string UploadsDir(IWebHostEnvironment env)
    {
        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
        return string.IsNullOrEmpty(dataDir)
            ? Path.Combine(env.WebRootPath ?? "wwwroot", "uploads")
            : Path.Combine(dataDir, "uploads");
    }

    // Echte Client-IP — hinter dem Reverse Proxy (Caddy) steht sie im ersten
    // Eintrag von X-Forwarded-For, sonst die direkte Verbindungs-IP. Wird nur
    // flüchtig zur Länderermittlung genutzt und nirgends gespeichert.
    private static System.Net.IPAddress? ClientIp(HttpContext ctx)
    {
        var fwd = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(fwd))
        {
            var first = fwd.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           .FirstOrDefault();
            if (System.Net.IPAddress.TryParse(first, out var ip)) return ip;
        }
        return ctx.Connection.RemoteIpAddress;
    }

    private static string HashIp(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("qspot:" + ip));
        return Convert.ToHexString(bytes)[..16];
    }

    // Identität für die anonyme Vote-Begrenzung: bevorzugt die im Browser erzeugte
    // VoterId (genau ein Besucher), sonst Fallback auf die gehashte IP.
    private static string VoterKey(HttpContext ctx, string? voterId)
    {
        if (!string.IsNullOrWhiteSpace(voterId))
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("qspot-voter:" + voterId));
            return "v" + Convert.ToHexString(bytes)[..15];
        }
        return HashIp(ctx);
    }
}
