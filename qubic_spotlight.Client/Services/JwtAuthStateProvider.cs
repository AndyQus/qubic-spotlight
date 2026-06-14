using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace qubic_spotlight.Client.Services;

// Liest das JWT aus dem TokenStore und baut daraus den AuthenticationState.
// Bewusst klein: nur Parsen der Claims, keine Server-Validierung (das macht die API).
public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStore _tokens;
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public JwtAuthStateProvider(TokenStore tokens) => _tokens = tokens;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetAsync();
        if (string.IsNullOrWhiteSpace(token)) return Anonymous;

        var claims = ParseClaims(token);
        if (IsExpired(claims))
        {
            await _tokens.ClearAsync();
            return Anonymous;
        }
        var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static bool IsExpired(IEnumerable<Claim> claims)
    {
        var exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        if (long.TryParse(exp, out var seconds))
            return DateTimeOffset.FromUnixTimeSeconds(seconds) < DateTimeOffset.UtcNow;
        return false;
    }

    private static IEnumerable<Claim> ParseClaims(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) yield break;
        var payload = Decode(parts[1]);
        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload);
        if (map is null) yield break;

        foreach (var kv in map)
        {
            if (kv.Value.ValueKind == JsonValueKind.Array)
                foreach (var item in kv.Value.EnumerateArray())
                    yield return new Claim(Map(kv.Key), item.ToString());
            else
                yield return new Claim(Map(kv.Key), kv.Value.ToString());
        }
    }

    // JWT-Kurznamen auf die von Blazor erwarteten Claim-Typen abbilden.
    private static string Map(string key) => key switch
    {
        "role" or "roles" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" => ClaimTypes.Role,
        "email" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress" => ClaimTypes.Email,
        "nameid" or "sub" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" => ClaimTypes.NameIdentifier,
        _ => key
    };

    private static string Decode(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
