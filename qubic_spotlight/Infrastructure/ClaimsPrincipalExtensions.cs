using System.Security.Claims;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public static string? Email(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email);

    // Admin oder Marketing dürfen alle Anzeigen verwalten ("Manager").
    public static bool IsManager(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Marketing);
}
