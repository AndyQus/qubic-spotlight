using Microsoft.AspNetCore.Components;

namespace qubic_spotlight.Client.Services;

// Basisklasse für Seiten/Dialoge: stellt L bereit und rendert neu, wenn die
// Sprache umgeschaltet wird. Seiten verwenden: @inherits LocalizedComponentBase
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject] protected Localizer L { get; set; } = null!;

    protected override void OnInitialized() => L.Changed += OnLanguageChanged;

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => L.Changed -= OnLanguageChanged;
}
