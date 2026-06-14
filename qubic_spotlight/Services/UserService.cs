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

    // Erzeugt/erneuert den API-Key und gibt ihn EINMAL im Klartext zurück.
    public string? RegenerateApiKey(Guid id)
    {
        var user = _db.GetUserById(id);
        if (user is null) return null;
        user.ApiKey = PasswordHasher.NewApiKey();
        _db.UpdateUser(user);
        return user.ApiKey;
    }

    public static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        Roles = u.Roles,
        Ecosystem = u.Ecosystem,
        IsActive = u.IsActive,
        HasApiKey = !string.IsNullOrEmpty(u.ApiKey),
        CreatedAt = u.CreatedAt
    };
}
