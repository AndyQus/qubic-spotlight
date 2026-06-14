using Microsoft.JSInterop;

namespace qubic_spotlight.Client.Services;

// Schlanke Lokalisierung ohne resx: zwei Wörterbücher (en/de), Auswahl im
// localStorage gemerkt. Default ist Englisch.
public class Localizer
{
    public const string Key = "qspot_lang";
    private readonly IJSRuntime _js;

    public string Lang { get; private set; } = "en";
    public event Action? Changed;

    public Localizer(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        var v = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
        if (v is "en" or "de" && v != Lang)
        {
            Lang = v;
            Changed?.Invoke();
        }
    }

    public async Task SetAsync(string lang)
    {
        if (lang is not ("en" or "de") || lang == Lang) return;
        Lang = lang;
        await _js.InvokeVoidAsync("localStorage.setItem", Key, lang);
        Changed?.Invoke();
    }

    // Zugriff per Indexer: @L["nav.dashboard"]
    public string this[string key] => Translations.Get(Lang, key);

    // Mit Platzhaltern: L.T("ads.ownHint", 5)
    public string T(string key, params object[] args) => string.Format(this[key], args);
}
