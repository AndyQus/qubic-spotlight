using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Infrastructure;

// Erstellt JWTs für den interaktiven Login. Schlüssel/Issuer kommen aus der Config.
public class TokenService
{
    private readonly byte[] _key;
    private readonly string _issuer;
    private readonly int _hours;

    public TokenService(IConfiguration config)
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
                     ?? config["Jwt:Secret"]
                     ?? "CHANGE_ME_dev_secret_at_least_32_chars_long!!";
        _key = Encoding.UTF8.GetBytes(secret);
        _issuer = config["Jwt:Issuer"] ?? "qubic_spotlight";
        _hours = int.TryParse(config["Jwt:Hours"], out var h) ? h : 12;
    }

    public string Issuer => _issuer;
    public SymmetricSecurityKey SigningKey => new(_key);

    public (string token, DateTime expiresAt) Create(User user)
    {
        var expires = DateTime.UtcNow.AddHours(_hours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("ecosystem", user.Ecosystem ?? "")
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var creds = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_issuer, _issuer, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
