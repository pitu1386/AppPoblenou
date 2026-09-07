using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace AtleticPoblenou.Services;

/// <summary>
/// Notificaciones push (Web Push). El navegador se suscribe y la suscripción se guarda en
/// <c>push_subscriptions</c>. El envío lo hace la Edge Function <c>send-push</c>.
/// En iPhone solo funciona con la app instalada en la pantalla de inicio (iOS 16.4+).
/// </summary>
public class PushService
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private readonly SupabaseAuthService _auth;
    private readonly SupabaseClientService _sb;
    private readonly ITeamDataService _team;

    public bool IsSupported { get; private set; } = true;
    public string Permission { get; private set; } = "default";   // default | granted | denied | unsupported
    public bool IsEnabled { get; private set; }

    public event Action? OnChange;

    public PushService(IJSRuntime js, HttpClient http, SupabaseAuthService auth,
        SupabaseClientService sb, ITeamDataService team)
    {
        _js = js;
        _http = http;
        _auth = auth;
        _sb = sb;
        _team = team;
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsSupported = await _js.InvokeAsync<bool>("apnPush.isSupported");
            if (!IsSupported) { Permission = "unsupported"; OnChange?.Invoke(); return; }
            Permission = await _js.InvokeAsync<string>("apnPush.permission");
            var endpoint = await _js.InvokeAsync<string?>("apnPush.currentEndpoint");
            IsEnabled = Permission == "granted" && !string.IsNullOrEmpty(endpoint);
        }
        catch
        {
            IsSupported = false;
            Permission = "unsupported";
        }
        OnChange?.Invoke();
    }

    /// <summary>Pide permiso, suscribe el navegador y guarda la suscripción en la nube.</summary>
    public async Task<(bool Ok, string? Error)> EnableAsync()
    {
        var profileId = _team.GetCurrentUser()?.Id;
        if (string.IsNullOrEmpty(profileId)) return (false, "Todavía no tenés ficha de jugador.");

        string? raw;
        try { raw = await _js.InvokeAsync<string?>("apnPush.subscribe", AppInfo.VapidPublicKey); }
        catch (Exception e) { return (false, e.Message); }

        if (string.IsNullOrEmpty(raw))
            return (false, "Este navegador no soporta notificaciones push.");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out _))
        {
            Permission = root.TryGetProperty("permission", out var p) ? p.GetString() ?? "denied" : "denied";
            OnChange?.Invoke();
            return (false, Permission == "denied"
                ? "Bloqueaste las notificaciones. Habilitalas desde los ajustes del navegador para este sitio."
                : "No se pudo activar la notificación.");
        }

        var sub = new
        {
            profile_id = profileId,
            endpoint = root.GetProperty("endpoint").GetString(),
            p256dh = root.GetProperty("p256dh").GetString(),
            auth = root.GetProperty("auth").GetString(),
            user_agent = "pwa"
        };

        try
        {
            // on_conflict=endpoint: si el navegador ya tenía una suscripción, se actualiza.
            await _sb.UpsertRowAsync("push_subscriptions?on_conflict=endpoint", sub);
        }
        catch (SupabaseException e) { return (false, e.Message); }
        catch (Exception e) { return (false, e.Message); }

        Permission = "granted";
        IsEnabled = true;
        OnChange?.Invoke();
        return (true, null);
    }

    public async Task DisableAsync()
    {
        string? endpoint = null;
        try { endpoint = await _js.InvokeAsync<string?>("apnPush.unsubscribe"); } catch { }

        if (!string.IsNullOrEmpty(endpoint))
        {
            try { await _sb.DeleteAsync("push_subscriptions", $"endpoint=eq.{Uri.EscapeDataString(endpoint)}"); }
            catch { }
        }

        IsEnabled = false;
        OnChange?.Invoke();
    }

    // ---- Envíos (solo staff; la Edge Function valida el rol) ----

    public Task<(bool Ok, int Sent, string? Error)> SendToAllAsync(string title, string body) =>
        PostAsync(new { kind = "manual", title, body });

    public Task<(bool Ok, int Sent, string? Error)> SendToAsync(string title, string body, IEnumerable<string> profileIds) =>
        PostAsync(new { kind = "custom", title, body, profileIds = profileIds.ToArray() });

    private async Task<(bool Ok, int Sent, string? Error)> PostAsync(object payload)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (token == null) return (false, 0, "Tu sesión caducó. Volvé a entrar.");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, AppInfo.SendPushUrl);
            req.Headers.Add("Authorization", $"Bearer {token}");
            req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
            req.Content = JsonContent.Create(payload);
            using var resp = await _http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return (false, 0, $"({(int)resp.StatusCode}) {text}");
            using var doc = JsonDocument.Parse(text);
            var sent = doc.RootElement.TryGetProperty("sent", out var s) ? s.GetInt32() : 0;
            return (true, sent, null);
        }
        catch (Exception e)
        {
            return (false, 0, e.Message);
        }
    }
}
