using System.Security.Cryptography;
using System.Text;

namespace qubic_spotlight.Infrastructure;

// PBKDF2-Hashing ohne externe Pakete. Format: "iterations.salt.hash" (Base64).
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.', 3);
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    // Langlebiger API-Key (URL-sicher).
    public static string NewApiKey() =>
        "qsp_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace("+", "").Replace("/", "").Replace("=", "");

    // API-Keys sind hochentropische Zufallstoken – ein schneller SHA-256 (hex)
    // genügt zum Speichern/Vergleichen (kein langsames PBKDF2 nötig wie bei
    // Passwörtern). Gespeichert wird nur dieser Hash, nie der Key selbst.
    public static string HashApiKey(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
}
