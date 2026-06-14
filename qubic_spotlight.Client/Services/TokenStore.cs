using Microsoft.JSInterop;

namespace qubic_spotlight.Client.Services;

// Speichert das JWT im localStorage des Browsers.
public class TokenStore
{
    private const string Key = "qspot_token";
    private readonly IJSRuntime _js;

    public TokenStore(IJSRuntime js) => _js = js;

    public ValueTask<string?> GetAsync() =>
        _js.InvokeAsync<string?>("localStorage.getItem", Key);

    public ValueTask SetAsync(string token) =>
        _js.InvokeVoidAsync("localStorage.setItem", Key, token);

    public ValueTask ClearAsync() =>
        _js.InvokeVoidAsync("localStorage.removeItem", Key);
}
