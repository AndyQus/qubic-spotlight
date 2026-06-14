using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Infrastructure;

// Zweites Auth-Schema neben JWT: liest den Header "X-Api-Key" und schlägt den
// Benutzer in der DB nach. Fehlt der Header, übernimmt das JWT-Schema.
public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly LiteDbContext _db;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        LiteDbContext db) : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        var user = _db.GetUserByApiKey(provided.ToString());
        if (user is null || !user.IsActive)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("ecosystem", user.Ecosystem ?? "")
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
