using qubic_spotlight.Infrastructure;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Services;

public class UserService
{
    private readonly LiteDbContext _db;

    public UserService(LiteDbContext db) => _db = db;

    public List<UserDto> All() => _db.GetAllUsers().Select(ToDto).ToList();
    public User? GetById(Guid id) => _db.GetUserById(id);

    // Login-Prüfung: liefert den Benutzer bei korrektem Passwort.
    public User? Authenticate(string email, string password)
    {
        var user = _db.GetUserByEmail(email.Trim());
        if (user is null || !user.IsActive) return null;
        return PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public (bool ok, string? error, UserDto? user) Create(UserInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Email)) return (false, "E-Mail fehlt.", null);
        if (string.IsNullOrWhiteSpace(input.Password)) return (false, "Passwort fehlt.", null);
        if (_db.GetUserByEmail(input.Email.Trim()) is not null)
            return (false, "E-Mail bereits vergeben.", null);

        var user = new User
        {
            Email = input.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(input.Password),
            Roles = input.Roles.Count > 0 ? input.Roles : new() { Roles.Ecosystem },
            Ecosystem = string.IsNullOrWhiteSpace(input.Ecosystem) ? null : input.Ecosystem.Trim(),
            IsActive = input.IsActive
        };
        _db.InsertUser(user);
        return (true, null, ToDto(user));
    }

    public (bool ok, string? error, UserDto? user) Update(Guid id, UserInput input)
    {
        var user = _db.GetUserById(id);
        if (user is null) return (false, "Benutzer nicht gefunden.", null);

        user.Email = input.Email.Trim();
        user.Roles = input.Roles;
        user.Ecosystem = string.IsNullOrWhiteSpace(input.Ecosystem) ? null : input.Ecosystem.Trim();
        user.IsActive = input.IsActive;
        if (!string.IsNullOrWhiteSpace(input.Password))
            user.PasswordHash = PasswordHasher.Hash(input.Password);

        _db.UpdateUser(user);
        return (true, null, ToDto(user));
    }

    public (bool ok, string? error) Delete(Guid id)
    {
        if (_db.GetUserById(id) is null) return (false, "Benutzer nicht gefunden.");
        _db.DeleteUser(id);
        return (true, null);
    }

    // Erzeugt/erneuert den API-Key. Gespeichert wird nur dessen Hash + die letzten
    // 4 Zeichen + Erstelldatum. Der volle Key wird EINMALIG hier zurückgegeben –
    // danach ist er nicht mehr rekonstruierbar.
    public string? RegenerateApiKey(Guid id)
    {
        var user = _db.GetUserById(id);
        if (user is null) return null;

        var key = PasswordHasher.NewApiKey();
        user.ApiKeyHash = PasswordHasher.HashApiKey(key);
        user.ApiKeyLast4 = key.Length >= 4 ? key[^4..] : key;
        user.ApiKeyCreatedAt = DateTime.UtcNow;
        _db.UpdateUser(user);
        return key;
    }

    // Profil des angemeldeten Benutzers inkl. maskierter Key-Vorschau.
    public MeDto? GetMe(Guid id)
    {
        var u = _db.GetUserById(id);
        if (u is null) return null;
        return new MeDto
        {
            Email = u.Email,
            Roles = u.Roles,
            Ecosystem = u.Ecosystem,
            HasApiKey = !string.IsNullOrEmpty(u.ApiKeyHash),
            ApiKeyPreview = string.IsNullOrEmpty(u.ApiKeyHash)
                ? null
                : $"qsp_••••••••{u.ApiKeyLast4}",
            ApiKeyCreatedAt = u.ApiKeyCreatedAt
        };
    }

    // Selbst-Service Passwortänderung: prüft das aktuelle Passwort.
    public (bool ok, string? error) ChangePassword(Guid id, string current, string newPassword)
    {
        var user = _db.GetUserById(id);
        if (user is null) return (false, "Benutzer nicht gefunden.");
        if (!PasswordHasher.Verify(current, user.PasswordHash))
            return (false, "Aktuelles Passwort ist falsch.");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return (false, "Neues Passwort muss mindestens 8 Zeichen haben.");

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        _db.UpdateUser(user);
        return (true, null);
    }

    public static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        Roles = u.Roles,
        Ecosystem = u.Ecosystem,
        IsActive = u.IsActive,
        HasApiKey = !string.IsNullOrEmpty(u.ApiKeyHash),
        CreatedAt = u.CreatedAt
    };
}
