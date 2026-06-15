using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using qubic_spotlight.Shared.Models;

namespace qubic_spotlight.Client.Services;

// Schmaler Wrapper um die REST-API. Hängt bei geschützten Aufrufen das JWT an.
public class SpotlightApi
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public SpotlightApi(HttpClient http, TokenStore tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    // ── Öffentlich ─────────────────────────────────────────────────────────
    public async Task<QubicStats?> GetStatsAsync()
    {
        try { return await _http.GetFromJsonAsync<QubicStats>("api/qubic/stats"); }
        catch { return null; }
    }

    public async Task<List<PublicAd>> GetPublicAdsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<PublicAd>>("api/ads") ?? new(); }
        catch { return new(); }
    }

    // ── Login ──────────────────────────────────────────────────────────────
    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        var res = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = email, Password = password });
        if (!res.IsSuccessStatusCode) return null;
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>();
        if (login is not null) await _tokens.SetAsync(login.Token);
        return login;
    }

    public Task LogoutAsync() => _tokens.ClearAsync().AsTask();

    // ── Eigene Anzeigen ──────────────────────────────────────────────────────
    public Task<List<Ad>> GetMyAdsAsync() => GetList<Ad>("api/my/ads");
    public Task<List<Ad>> GetAllAdsAsync() => GetList<Ad>("api/admin/ads");

    // Klick-/Impression-Statistik pro Anzeige im Zeitfenster (UTC, ISO-8601).
    public Task<List<AdClickStat>> GetAdClickStatsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var url = $"api/admin/ads/stats?from={Uri.EscapeDataString(fromUtc.ToString("o"))}&to={Uri.EscapeDataString(toUtc.ToString("o"))}";
        return GetList<AdClickStat>(url);
    }

    // Speichert über die passende Route (Manager = admin, sonst eigene).
    public Task<(bool ok, string? error)> SaveAdAsync(AdInput input, Guid? id, bool manager)
    {
        var baseUrl = manager ? "api/admin/ads" : "api/my/ads";
        return id is null
            ? Send(HttpMethod.Post, baseUrl, input)
            : Send(HttpMethod.Put, $"{baseUrl}/{id}", input);
    }

    public Task<(bool ok, string? error)> DeleteAdAsync(Guid id, bool manager)
        => Send(HttpMethod.Delete, $"{(manager ? "api/admin/ads" : "api/my/ads")}/{id}");

    public async Task<string?> RegenerateMyApiKeyAsync()
    {
        var res = await SendRaw(HttpMethod.Post, "api/my/apikey");
        if (res is null || !res.IsSuccessStatusCode) return null;
        var doc = await res.Content.ReadFromJsonAsync<ApiKeyResult>();
        return doc?.ApiKey;
    }

    // ── Eigenes Profil / Account ─────────────────────────────────────────────
    public async Task<MeDto?> GetMeAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "api/my/me");
        await Authorize(req);
        try
        {
            var res = await _http.SendAsync(req);
            return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<MeDto>() : null;
        }
        catch { return null; }
    }

    public Task<(bool ok, string? error)> ChangePasswordAsync(string current, string newPassword)
        => Send(HttpMethod.Post, "api/my/password",
            new ChangePasswordRequest { CurrentPassword = current, NewPassword = newPassword });

    // ── Benutzer (Admin) ─────────────────────────────────────────────────────
    public Task<List<UserDto>> GetUsersAsync() => GetList<UserDto>("api/admin/users");

    public Task<(bool ok, string? error)> SaveUserAsync(UserInput input, Guid? id)
        => id is null
            ? Send(HttpMethod.Post, "api/admin/users", input)
            : Send(HttpMethod.Put, $"api/admin/users/{id}", input);

    public Task<(bool ok, string? error)> DeleteUserAsync(Guid id)
        => Send(HttpMethod.Delete, $"api/admin/users/{id}");

    public async Task<string?> RegenerateUserApiKeyAsync(Guid id)
    {
        var res = await SendRaw(HttpMethod.Post, $"api/admin/users/{id}/apikey");
        if (res is null || !res.IsSuccessStatusCode) return null;
        var doc = await res.Content.ReadFromJsonAsync<ApiKeyResult>();
        return doc?.ApiKey;
    }

    // ── Bild-Upload ──────────────────────────────────────────────────────────
    public async Task<string?> UploadImageAsync(IBrowserFile file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream(500 * 1024);   // wirft bei > 500 KB
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, "file", file.Name);

            var req = new HttpRequestMessage(HttpMethod.Post, "api/uploads") { Content = content };
            await Authorize(req);
            var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            var doc = await res.Content.ReadFromJsonAsync<UploadResult>();
            return doc?.Url;
        }
        catch
        {
            return null;
        }
    }

    // ── Helfer ───────────────────────────────────────────────────────────────
    private async Task<List<T>> GetList<T>(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        await Authorize(req);
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return new();
        return await res.Content.ReadFromJsonAsync<List<T>>() ?? new();
    }

    private async Task<(bool ok, string? error)> Send(HttpMethod method, string url, object? body = null)
    {
        var res = await SendRaw(method, url, body);
        if (res is null) return (false, "Netzwerkfehler.");
        if (res.IsSuccessStatusCode) return (true, null);
        var err = await TryReadError(res);
        return (false, err);
    }

    private async Task<HttpResponseMessage?> SendRaw(HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        await Authorize(req);
        try { return await _http.SendAsync(req); }
        catch { return null; }
    }

    private async Task Authorize(HttpRequestMessage req)
    {
        var token = await _tokens.GetAsync();
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<string?> TryReadError(HttpResponseMessage res)
    {
        try { var e = await res.Content.ReadFromJsonAsync<ErrorResult>(); return e?.Error ?? res.ReasonPhrase; }
        catch { return res.ReasonPhrase; }
    }

    private record ErrorResult(string? Error);
    private record ApiKeyResult(string ApiKey);
    private record UploadResult(string Url);
}
