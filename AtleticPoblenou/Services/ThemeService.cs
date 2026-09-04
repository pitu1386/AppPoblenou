using Microsoft.JSInterop;

namespace AtleticPoblenou.Services;

/// <summary>
/// Tema visual de la app. "light" = Editorial rojiblanco, "dark" = Noche de partido.
/// La clase `dark` en &lt;html&gt; y la persistencia en localStorage las gestiona window.apnTheme (index.html).
/// </summary>
public class ThemeService
{
    public const string Light = "light";
    public const string Dark = "dark";

    private readonly IJSRuntime _js;
    private bool _initialized;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public string Current { get; private set; } = Light;
    public bool IsDark => Current == Dark;

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var stored = await _js.InvokeAsync<string?>("apnTheme.get");
            Current = stored == Dark ? Dark : Light;
        }
        catch
        {
            Current = Light;
        }
        OnChange?.Invoke();
    }

    public async Task SetAsync(string theme)
    {
        Current = theme == Dark ? Dark : Light;
        try
        {
            await _js.InvokeVoidAsync("apnTheme.set", Current);
        }
        catch
        {
            // Sin JS (prerender) el tema se aplica al cargar en el navegador.
        }
        OnChange?.Invoke();
    }

    public Task ToggleAsync() => SetAsync(IsDark ? Light : Dark);
}
