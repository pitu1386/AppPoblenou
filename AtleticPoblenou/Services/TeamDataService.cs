using System.Text.Json;
using AtleticPoblenou.Models;
using Microsoft.JSInterop;

namespace AtleticPoblenou.Services;

/// <summary>
/// Estado del club en memoria + caché en localStorage para pintar al instante.
/// La verdad vive en Supabase: cada mutación escribe solo la fila afectada, vuelve a leer la tabla
/// y avisa de errores por <see cref="OnError"/>. Los cambios de otros dispositivos llegan por Realtime.
/// </summary>
public class TeamDataService : ITeamDataService, IDisposable
{
    private const string OwnerAdminId = "user-1";
    private const string CachePrefix = "apn2_";
    private const string CacheVersion = "2";
    private const string PendingProfileKey = "apn2_pending_profile";

    private static readonly string[] Tables =
    {
        "profiles", "club_settings", "rival_teams", "matches", "attendance", "payments", "team_expenses", "match_events", "announcements"
    };

    private readonly IJSRuntime _js;
    private readonly SupabaseAuthService _auth;
    private readonly SupabaseClientService _supabase;
    private DotNetObjectReference<TeamDataService>? _selfRef;
    private bool _initialized;
    private bool _profilesLoaded;

    public event Action? OnChange;
    public event Action<string>? OnError;

    private ClubSettings _clubSettings = new();
    private List<UserProfile> _profiles = new();
    private List<RivalTeam> _rivalTeams = new();
    private List<TeamAnnouncement> _announcements = new();
    private List<Match> _matches = new();
    private List<Attendance> _attendance = new();
    private List<Payment> _payments = new();
    private List<TeamExpense> _expenses = new();
    private List<MatchEvent> _matchEvents = new();

    public TeamDataService(IJSRuntime js, SupabaseAuthService auth, SupabaseClientService supabase)
    {
        _js = js;
        _auth = auth;
        _supabase = supabase;
        _auth.OnSessionChanged += HandleSessionChanged;
    }

    // ==========================================
    // ESTADO DE SESIÓN
    // ==========================================
    private UserProfile? CurrentProfile =>
        _profiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.AuthUid) && p.AuthUid == _auth.UserId)
        ?? _profiles.FirstOrDefault(p => !string.IsNullOrEmpty(_auth.Email) && p.Email.Equals(_auth.Email, StringComparison.OrdinalIgnoreCase));

    public bool IsAuthenticated => _auth.IsSignedIn && CurrentProfile is { IsActive: true };
    public bool NeedsProfile => _auth.IsSignedIn && _profilesLoaded && CurrentProfile == null;
    public bool IsDeactivated => _auth.IsSignedIn && CurrentProfile is { IsActive: false };
    public string? SessionEmail => _auth.Email;

    public bool IsCloudConnected { get; private set; }
    public bool IsRealtimeConnected { get; private set; }
    public DateTime? LastSyncUtc { get; private set; }

    public UserProfile GetCurrentUser()
    {
        var user = CurrentProfile;
        if (user != null)
        {
            if (IsOwnerAdmin(user))
            {
                user.Role = UserRole.Admin;
                user.IsActive = true;
            }
            return user;
        }

        // Sin ficha todavía: perfil mínimo para que los componentes no fallen.
        return new UserProfile
        {
            Id = _auth.UserId ?? "",
            AuthUid = _auth.UserId,
            Email = _auth.Email ?? "",
            FullName = _auth.Email ?? "Invitado",
            Nickname = _auth.Email?.Split('@')[0] ?? "invitado",
            Role = UserRole.Player,
            IsActive = false
        };
    }

    public bool IsOwnerAdmin(UserProfile? p) => p?.Id == OwnerAdminId;

    // ==========================================
    // ARRANQUE Y SINCRONIZACIÓN
    // ==========================================
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await PurgeLegacyCacheAsync();
        await _auth.LoadAsync();

        if (_auth.IsSignedIn)
        {
            await LoadCacheAsync();
            NotifyStateChanged();
            await RefreshFromCloudAsync();
            await StartRealtimeAsync();
        }

        NotifyStateChanged();
    }

    public async Task RefreshFromCloudAsync()
    {
        if (!_auth.IsSignedIn) return;

        var results = await Task.WhenAll(Tables.Select(t => RefreshTableCoreAsync(t)));
        var anyOk = results.Any(r => r);
        var allFailed = results.All(r => !r);

        IsCloudConnected = anyOk;
        if (anyOk) LastSyncUtc = DateTime.UtcNow;
        if (allFailed && _lastCloudError != null)
        {
            OnError?.Invoke(_lastCloudError);
        }
        NotifyStateChanged();
    }

    private string? _lastCloudError;

    /// <summary>Lee una tabla de la nube y sustituye la copia local. Devuelve false si falló (sin lanzar).</summary>
    private async Task<bool> RefreshTableCoreAsync(string table)
    {
        try
        {
            switch (table)
            {
                case "profiles":
                    _profiles = await _supabase.FetchProfilesAsync();
                    EnsureOwnerAdminProtected();
                    _profilesLoaded = true;
                    break;
                case "club_settings":
                    var club = await _supabase.FetchClubSettingsAsync();
                    if (club != null) _clubSettings = club;
                    break;
                case "rival_teams":
                    _rivalTeams = await _supabase.FetchRivalTeamsAsync();
                    break;
                case "matches":
                    _matches = await _supabase.FetchMatchesAsync();
                    break;
                case "attendance":
                    _attendance = await _supabase.FetchAttendanceAsync();
                    break;
                case "payments":
                    _payments = await _supabase.FetchPaymentsAsync();
                    break;
                case "team_expenses":
                    _expenses = await _supabase.FetchExpensesAsync();
                    break;
                case "match_events":
                    _matchEvents = await _supabase.FetchMatchEventsAsync();
                    break;
                case "announcements":
                    _announcements = await _supabase.FetchAnnouncementsAsync();
                    break;
                default:
                    return false;
            }
            await SaveCacheAsync(table);
            return true;
        }
        catch (SupabaseException ex)
        {
            _lastCloudError = ex.Message;
            if (ex.StatusCode == null) IsCloudConnected = false;
            return false;
        }
        catch (Exception ex)
        {
            _lastCloudError = $"Error leyendo {table}: {ex.Message}";
            return false;
        }
    }

    private async Task RefreshTablesAsync(params string[] tables)
    {
        var results = await Task.WhenAll(tables.Select(RefreshTableCoreAsync));
        if (results.Any(r => r))
        {
            IsCloudConnected = true;
            LastSyncUtc = DateTime.UtcNow;
        }
        NotifyStateChanged();
    }

    /// <summary>Ejecuta una escritura en la nube. Si falla, avisa al usuario y devuelve false.</summary>
    private async Task<bool> CloudWriteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            IsCloudConnected = true;
            return true;
        }
        catch (SupabaseException ex)
        {
            if (ex.StatusCode == null) IsCloudConnected = false;
            OnError?.Invoke(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Error inesperado: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> WriteAndRefreshAsync(Func<Task> operation, params string[] tables)
    {
        var ok = await CloudWriteAsync(operation);
        // Se refresca también tras un fallo para deshacer el cambio optimista local.
        await RefreshTablesAsync(tables);
        return ok;
    }

    // ---------- Realtime (puente JS) ----------
    private async Task StartRealtimeAsync()
    {
        try
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            var token = await _auth.GetAccessTokenAsync();
            await _js.InvokeVoidAsync("apnRealtime.start", AppInfo.SupabaseUrl, AppInfo.SupabaseAnonKey, token, _selfRef);
        }
        catch
        {
            IsRealtimeConnected = false;
        }
    }

    private async Task StopRealtimeAsync()
    {
        try { await _js.InvokeVoidAsync("apnRealtime.stop"); } catch { }
        IsRealtimeConnected = false;
    }

    private async void HandleSessionChanged()
    {
        if (!_auth.IsSignedIn) return;
        try
        {
            var token = await _auth.GetAccessTokenAsync();
            if (token != null) await _js.InvokeVoidAsync("apnRealtime.setAuth", token);
        }
        catch { }
    }

    [JSInvokable]
    public async Task OnCloudChange(string table)
    {
        if (!_auth.IsSignedIn) return;
        if (Tables.Contains(table)) await RefreshTablesAsync(table);
        else await RefreshFromCloudAsync();
    }

    [JSInvokable]
    public Task OnRealtimeStatus(bool connected)
    {
        IsRealtimeConnected = connected;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnAppResumed() => RefreshFromCloudAsync();

    // ---------- Caché local ----------
    private async Task PurgeLegacyCacheAsync()
    {
        try
        {
            var marker = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_cache_version");
            if (marker == CacheVersion) return;
            // Claves de versiones anteriores (incluían contraseñas en claro).
            foreach (var key in new[] { "apn_is_authenticated", "apn_current_user", "apn_team_secret_code", "apn_club_settings", "apn_profiles",
                                        "apn_rival_teams", "apn_announcements", "apn_matches", "apn_attendance", "apn_payments", "apn_expenses", "apn_events" })
            {
                await _js.InvokeVoidAsync("blazorLocalStorage.remove", key);
            }
            await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_cache_version", CacheVersion);
        }
        catch { }
    }

    private async Task LoadCacheAsync()
    {
        try
        {
            _profiles = await ReadCacheAsync<List<UserProfile>>("profiles") ?? new();
            _profilesLoaded = _profiles.Count > 0;
            EnsureOwnerAdminProtected();
            _clubSettings = await ReadCacheAsync<ClubSettings>("club_settings") ?? new();
            _rivalTeams = await ReadCacheAsync<List<RivalTeam>>("rival_teams") ?? new();
            _matches = await ReadCacheAsync<List<Match>>("matches") ?? new();
            _attendance = await ReadCacheAsync<List<Attendance>>("attendance") ?? new();
            _payments = await ReadCacheAsync<List<Payment>>("payments") ?? new();
            _expenses = await ReadCacheAsync<List<TeamExpense>>("team_expenses") ?? new();
            _matchEvents = await ReadCacheAsync<List<MatchEvent>>("match_events") ?? new();
            _announcements = await ReadCacheAsync<List<TeamAnnouncement>>("announcements") ?? new();
        }
        catch
        {
            ClearMemory();
        }
    }

    private async Task<T?> ReadCacheAsync<T>(string table)
    {
        var raw = await _js.InvokeAsync<string?>("blazorLocalStorage.get", CachePrefix + table);
        return string.IsNullOrEmpty(raw) ? default : JsonSerializer.Deserialize<T>(raw);
    }

    private async Task SaveCacheAsync(string table)
    {
        object data = table switch
        {
            "profiles" => _profiles,
            "club_settings" => _clubSettings,
            "rival_teams" => _rivalTeams,
            "matches" => _matches,
            "attendance" => _attendance,
            "payments" => _payments,
            "team_expenses" => _expenses,
            "match_events" => _matchEvents,
            "announcements" => _announcements,
            _ => new()
        };
        try { await _js.InvokeVoidAsync("blazorLocalStorage.set", CachePrefix + table, JsonSerializer.Serialize(data)); } catch { }
    }

    private async Task ClearCacheAsync()
    {
        foreach (var t in Tables)
        {
            try { await _js.InvokeVoidAsync("blazorLocalStorage.remove", CachePrefix + t); } catch { }
        }
    }

    private void ClearMemory()
    {
        _profiles = new();
        _profilesLoaded = false;
        _clubSettings = new();
        _rivalTeams = new();
        _matches = new();
        _attendance = new();
        _payments = new();
        _expenses = new();
        _matchEvents = new();
        _announcements = new();
    }

    private void EnsureOwnerAdminProtected()
    {
        foreach (var p in _profiles.Where(IsOwnerAdmin))
        {
            p.Role = UserRole.Admin;
            p.IsActive = true;
        }
    }

    // ==========================================
    // AUTENTICACIÓN
    // ==========================================
    public async Task<(bool Success, string ErrorMessage)> LoginAsync(string emailOrNickname, string password)
    {
        if (string.IsNullOrWhiteSpace(emailOrNickname))
            return (false, "Por favor ingresa tu email o apodo.");
        if (string.IsNullOrEmpty(password))
            return (false, "Ingresa tu contraseña.");

        var identifier = emailOrNickname.Trim();
        string? email = identifier.Contains('@') ? identifier.ToLowerInvariant() : null;

        if (email == null)
        {
            try
            {
                email = await _supabase.RpcAsync<string?>("lookup_login_email", new { identifier });
            }
            catch (SupabaseException ex)
            {
                return (false, ex.StatusCode == null ? ex.Message : "No se pudo consultar el apodo. ¿Está aplicado el script SQL de migración?");
            }
            if (string.IsNullOrEmpty(email))
                return (false, "No se encontró ningún jugador con ese apodo. Prueba con tu email.");
        }

        var (ok, error) = await _auth.SignInWithPasswordAsync(email, password);
        if (!ok) return (false, error);

        return await AfterSignInAsync();
    }

    private async Task<(bool Success, string ErrorMessage)> AfterSignInAsync()
    {
        ClearMemory();
        await ClearCacheAsync();
        await RefreshFromCloudAsync();

        if (!_profilesLoaded)
        {
            return (false, _lastCloudError ?? "No se pudieron cargar los datos del club.");
        }

        if (CurrentProfile == null)
        {
            // Alta a medias (por ejemplo, confirmación de email): completar con lo guardado.
            var pending = await ReadPendingProfileAsync();
            if (pending != null)
            {
                var (done, err) = await CompleteRegistrationAsync(pending);
                if (!done) return (false, err);
            }
        }

        await StartRealtimeAsync();
        NotifyStateChanged();

        if (IsDeactivated) return (false, "DEACTIVATED_NEEDS_CODE");
        return (true, "");
    }

    public async Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterModel model)
    {
        if (string.IsNullOrWhiteSpace(model.TeamCode))
            return (false, "Introduce el código de equipo que te pasó el capitán.");
        if (string.IsNullOrWhiteSpace(model.FullName))
            return (false, "Debes ingresar tu nombre y apellido.");
        if (string.IsNullOrWhiteSpace(model.Email) || !model.Email.Contains('@'))
            return (false, "Ingresa una dirección de email válida.");
        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            return (false, "La contraseña debe tener al menos 6 caracteres.");

        try
        {
            var valid = await _supabase.RpcAsync<bool>("validate_team_code", new { p_code = model.TeamCode });
            if (!valid) return (false, "Código de equipo incorrecto. Pídele el código actual al capitán para entrar.");
        }
        catch (SupabaseException ex)
        {
            return (false, ex.StatusCode == null ? ex.Message : "No se pudo validar el código. ¿Está aplicado el script SQL de migración?");
        }

        var email = model.Email.Trim().ToLowerInvariant();
        var (ok, hasSession, error) = await _auth.SignUpAsync(email, model.Password);
        if (!ok) return (false, error);

        if (!hasSession)
        {
            await SavePendingProfileAsync(model);
            return (false, "CONFIRM_EMAIL");
        }

        var (done, err) = await CompleteRegistrationAsync(model);
        if (!done) return (false, err);

        await StartRealtimeAsync();
        try { await _js.InvokeVoidAsync("triggerConfetti"); } catch { }
        NotifyStateChanged();
        return (true, "");
    }

    public async Task<(bool Success, string ErrorMessage)> CompleteRegistrationAsync(RegisterModel model)
    {
        if (!_auth.IsSignedIn) return (false, "No hay sesión activa.");
        if (string.IsNullOrWhiteSpace(model.FullName)) return (false, "Debes ingresar tu nombre y apellido.");

        try
        {
            await _supabase.RpcAsync("register_profile", new
            {
                p_team_code = model.TeamCode ?? "",
                p_full_name = model.FullName.Trim(),
                p_nickname = (model.Nickname ?? "").Trim(),
                p_jersey_number = model.JerseyNumber,
                p_position = (int)model.Position,
                p_foot = (int)model.Foot,
                p_phone = (model.Phone ?? "").Trim(),
                p_birth_date = model.BirthDate?.ToString("yyyy-MM-dd")
            });
        }
        catch (SupabaseException ex)
        {
            return (false, ex.Message);
        }

        await ClearPendingProfileAsync();
        await RefreshFromCloudAsync();
        return CurrentProfile != null ? (true, "") : (false, "La ficha se creó pero no se pudo cargar. Recarga la app.");
    }

    public async Task<(bool Success, string ErrorMessage)> ReactivateWithCodeAsync(string securityCode)
    {
        if (!_auth.IsSignedIn) return (false, "Inicia sesión primero con tu email y contraseña.");
        if (string.IsNullOrWhiteSpace(securityCode)) return (false, "Introduce el código de seguridad.");

        try
        {
            await _supabase.RpcAsync("reactivate_with_code", new { p_team_code = securityCode });
        }
        catch (SupabaseException ex)
        {
            return (false, ex.Message);
        }

        await RefreshTablesAsync("profiles");
        return IsAuthenticated ? (true, "") : (false, "No se pudo reactivar la ficha.");
    }

    public async Task<(bool Success, string ErrorMessage)> ChangeMyPasswordAsync(string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "La contraseña debe tener al menos 6 caracteres.");
        return await _auth.UpdatePasswordAsync(newPassword);
    }

    public async Task<(bool Success, string ErrorMessage)> AdminSetPasswordAsync(string profileId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "La contraseña debe tener al menos 6 caracteres.");
        try
        {
            await _supabase.RpcAsync("admin_set_password", new { p_profile_id = profileId, p_new_password = newPassword });
            return (true, "");
        }
        catch (SupabaseException ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task LogoutAsync()
    {
        await StopRealtimeAsync();
        await _auth.SignOutAsync();
        ClearMemory();
        await ClearCacheAsync();
        IsCloudConnected = false;
        LastSyncUtc = null;
        NotifyStateChanged();
    }

    private async Task SavePendingProfileAsync(RegisterModel model)
    {
        var copy = new RegisterModel
        {
            FullName = model.FullName, Nickname = model.Nickname, Email = model.Email, TeamCode = model.TeamCode,
            JerseyNumber = model.JerseyNumber, Position = model.Position, Foot = model.Foot, Phone = model.Phone, BirthDate = model.BirthDate
        };
        try { await _js.InvokeVoidAsync("blazorLocalStorage.set", PendingProfileKey, JsonSerializer.Serialize(copy)); } catch { }
    }

    private async Task<RegisterModel?> ReadPendingProfileAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("blazorLocalStorage.get", PendingProfileKey);
            return string.IsNullOrEmpty(raw) ? null : JsonSerializer.Deserialize<RegisterModel>(raw);
        }
        catch { return null; }
    }

    private async Task ClearPendingProfileAsync()
    {
        try { await _js.InvokeVoidAsync("blazorLocalStorage.remove", PendingProfileKey); } catch { }
    }

    // ==========================================
    // CLUB
    // ==========================================
    public string GetTeamSecretCode() => _clubSettings.TeamSecretCode;

    public async Task<string> GenerateNewTeamCodeAsync()
    {
        var code = $"APN-{Random.Shared.Next(1000, 9999)}";
        var settings = GetClubSettings();
        settings.TeamSecretCode = code;
        await SaveClubSettingsAsync(settings);
        return _clubSettings.TeamSecretCode;
    }

    public ClubSettings GetClubSettings() => _clubSettings;

    public async Task SaveClubSettingsAsync(ClubSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.TeamSecretCode)) settings.TeamSecretCode = _clubSettings.TeamSecretCode;
        if (settings.SeasonHistory.Count == 0 && _clubSettings.SeasonHistory.Count > 0) settings.SeasonHistory = _clubSettings.SeasonHistory;

        _clubSettings = settings;
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertClubSettingsAsync(settings), "club_settings");
    }

    // ==========================================
    // PERFILES
    // ==========================================
    public List<UserProfile> GetProfiles() => _profiles.OrderBy(p => p.JerseyNumber ?? 99).ToList();

    public UserProfile? GetProfileById(string profileId) => _profiles.FirstOrDefault(p => p.Id == profileId);

    public async Task SaveProfileAsync(UserProfile profile)
    {
        if (IsOwnerAdmin(profile))
        {
            profile.Role = UserRole.Admin;
            profile.IsActive = true;
        }

        var idx = _profiles.FindIndex(p => p.Id == profile.Id);
        if (idx >= 0) _profiles[idx] = profile; else _profiles.Add(profile);
        NotifyStateChanged();

        await WriteAndRefreshAsync(() => _supabase.UpsertProfileAsync(profile), "profiles");
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        var target = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (target == null || IsOwnerAdmin(target)) return;

        _profiles.RemoveAll(p => p.Id == profileId);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.DeleteByIdAsync("profiles", profileId), "profiles");
    }

    public async Task DeactivatePlayerAsync(string playerId)
    {
        var player = _profiles.FirstOrDefault(p => p.Id == playerId);
        if (player == null || IsOwnerAdmin(player)) return;

        player.IsActive = false;
        NotifyStateChanged();
        var ok = await WriteAndRefreshAsync(() => _supabase.UpsertProfileAsync(player), "profiles");

        if (ok && CurrentProfile?.Id == playerId)
        {
            await LogoutAsync();
        }
    }

    public async Task ReactivatePlayerAsync(string playerId)
    {
        var player = _profiles.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return;

        player.IsActive = true;
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertProfileAsync(player), "profiles");
    }

    // ==========================================
    // RIVALES Y CLASIFICACIÓN
    // ==========================================
    public List<RivalTeam> GetRivalTeams() => _rivalTeams.OrderBy(t => t.Name).ToList();

    public RivalTeam? GetRivalTeamById(string teamId) => _rivalTeams.FirstOrDefault(t => t.Id == teamId);

    public async Task AddRivalTeamAsync(RivalTeam team)
    {
        _rivalTeams.Add(team);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertRivalTeamAsync(team), "rival_teams");
    }

    public async Task UpdateRivalTeamAsync(RivalTeam team)
    {
        var idx = _rivalTeams.FindIndex(t => t.Id == team.Id);
        if (idx >= 0) _rivalTeams[idx] = team;
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertRivalTeamAsync(team), "rival_teams");
    }

    public async Task DeleteRivalTeamAsync(string teamId)
    {
        _rivalTeams.RemoveAll(t => t.Id == teamId);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.DeleteByIdAsync("rival_teams", teamId), "rival_teams");
    }

    public List<StandingRow> GetStandings()
    {
        var standings = new List<StandingRow>();

        var ourRow = new StandingRow
        {
            TeamId = "apn",
            TeamName = !string.IsNullOrEmpty(_clubSettings.ShortName) ? _clubSettings.ShortName : _clubSettings.ClubName,
            PrimaryColorHex = _clubSettings.PrimaryColorHex,
            SecondaryColorHex = _clubSettings.SecondaryColorHex,
            IsOurTeam = true
        };
        standings.Add(ourRow);

        foreach (var rival in _rivalTeams)
        {
            standings.Add(new StandingRow
            {
                TeamId = rival.Id,
                TeamName = rival.Name,
                PrimaryColorHex = rival.PrimaryColorHex,
                SecondaryColorHex = rival.SecondaryColorHex
            });
        }

        foreach (var m in _matches.Where(m => m.Status == MatchStatus.Finished))
        {
            int hScore, aScore;
            if (m.IsOurMatch)
            {
                if (!m.OurScore.HasValue || !m.RivalScore.HasValue) continue;
                hScore = m.IsHome ? m.OurScore.Value : m.RivalScore.Value;
                aScore = m.IsHome ? m.RivalScore.Value : m.OurScore.Value;
            }
            else
            {
                if (!m.HomeScore.HasValue || !m.AwayScore.HasValue) continue;
                hScore = m.HomeScore.Value;
                aScore = m.AwayScore.Value;
            }

            var homeRow = m.IsOurMatch && m.IsHome ? ourRow : FindRow(standings, m.HomeTeamId, m.HomeTeamName);
            var awayRow = m.IsOurMatch && !m.IsHome ? ourRow : FindRow(standings, m.AwayTeamId, m.AwayTeamName);

            if (homeRow != null)
            {
                homeRow.Played++;
                homeRow.GoalsFor += hScore;
                homeRow.GoalsAgainst += aScore;
                if (hScore > aScore) homeRow.Won++;
                else if (hScore == aScore) homeRow.Drawn++;
                else homeRow.Lost++;
            }

            if (awayRow != null)
            {
                awayRow.Played++;
                awayRow.GoalsFor += aScore;
                awayRow.GoalsAgainst += hScore;
                if (aScore > hScore) awayRow.Won++;
                else if (aScore == hScore) awayRow.Drawn++;
                else awayRow.Lost++;
            }
        }

        return standings.OrderByDescending(s => s.Points)
                        .ThenByDescending(s => s.GoalDifference)
                        .ThenByDescending(s => s.GoalsFor)
                        .ThenBy(s => s.TeamName)
                        .ToList();
    }

    private static StandingRow? FindRow(List<StandingRow> rows, string teamId, string teamName)
    {
        if (!string.IsNullOrEmpty(teamId) && teamId != "apn")
        {
            var byId = rows.FirstOrDefault(s => s.TeamId == teamId);
            if (byId != null) return byId;
        }
        if (string.IsNullOrEmpty(teamName)) return null;
        var row = rows.FirstOrDefault(s => string.Equals(s.TeamName, teamName, StringComparison.OrdinalIgnoreCase));
        if (row == null)
        {
            // Rival que no está en la lista de equipos: aparece igualmente en la tabla.
            row = new StandingRow { TeamId = teamId, TeamName = teamName };
            rows.Add(row);
        }
        return row;
    }

    // ==========================================
    // COMUNICADOS
    // ==========================================
    public List<TeamAnnouncement> GetAnnouncements() =>
        _announcements.Where(a => a.IsActive).OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.CreatedAt).ToList();

    public List<TeamAnnouncement> GetAllAnnouncements() => _announcements.OrderByDescending(a => a.CreatedAt).ToList();

    public async Task AddAnnouncementAsync(TeamAnnouncement announcement)
    {
        _announcements.Insert(0, announcement);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertAnnouncementAsync(announcement), "announcements");
    }

    public async Task VoteAnnouncementPollAsync(string announcementId, string playerId, int optionIndex)
    {
        var ann = _announcements.FirstOrDefault(a => a.Id == announcementId);
        if (ann == null) return;

        ann.Votes[playerId] = optionIndex;
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.RpcAsync("vote_poll", new { p_announcement_id = announcementId, p_option = optionIndex }), "announcements");
    }

    public Task ArchiveAnnouncementAsync(string announcementId) => SetAnnouncementActiveAsync(announcementId, false);
    public Task RestoreAnnouncementAsync(string announcementId) => SetAnnouncementActiveAsync(announcementId, true);

    private async Task SetAnnouncementActiveAsync(string announcementId, bool active)
    {
        var ann = _announcements.FirstOrDefault(a => a.Id == announcementId);
        if (ann == null) return;
        ann.IsActive = active;
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertAnnouncementAsync(ann), "announcements");
    }

    public async Task DeleteAnnouncementAsync(string announcementId)
    {
        _announcements.RemoveAll(a => a.Id == announcementId);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.DeleteByIdAsync("announcements", announcementId), "announcements");
    }

    // ==========================================
    // PARTIDOS Y ASISTENCIA
    // ==========================================
    public List<Match> GetMatches() => _matches.OrderBy(m => m.MatchDate).ToList();

    public Match? GetNextMatch() =>
        _matches.Where(m => m.IsOurMatch && m.Status == MatchStatus.Upcoming && m.MatchDate >= DateTime.UtcNow.AddHours(-3))
                .OrderBy(m => m.MatchDate)
                .FirstOrDefault();

    public async Task AddMatchAsync(Match match)
    {
        _matches.Add(match);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertMatchAsync(match), "matches");
    }

    public async Task AddLeagueMatchAsync(Match match)
    {
        if (match.HomeScore.HasValue && match.AwayScore.HasValue) match.Status = MatchStatus.Finished;
        await AddMatchAsync(match);
    }

    public async Task UpdateMatchDetailsAsync(Match match)
    {
        var idx = _matches.FindIndex(m => m.Id == match.Id);
        if (idx < 0) return;
        _matches[idx] = match;
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertMatchAsync(match), "matches");
    }

    public async Task UpdateMatchResultAsync(string matchId, int ourScore, int rivalScore, List<MatchEvent> events)
    {
        var match = _matches.FirstOrDefault(m => m.Id == matchId);
        if (match == null) return;

        match.OurScore = ourScore;
        match.RivalScore = rivalScore;
        match.Status = MatchStatus.Finished;
        _matchEvents.RemoveAll(e => e.MatchId == matchId);
        _matchEvents.AddRange(events);
        NotifyStateChanged();

        var ok = await WriteAndRefreshAsync(async () =>
        {
            await _supabase.UpsertMatchAsync(match);
            await _supabase.DeleteWhereAsync("match_events", "match_id", matchId);
            await _supabase.UpsertMatchEventsAsync(events);
        }, "matches", "match_events");

        if (ok) { try { await _js.InvokeVoidAsync("triggerConfetti"); } catch { } }
    }

    public async Task SaveBatchRoundResultsAsync(int round, List<Match> matches)
    {
        var changed = new List<Match>();
        foreach (var m in matches)
        {
            var existing = _matches.FirstOrDefault(x => x.Id == m.Id);
            if (existing != null)
            {
                existing.HomeScore = m.HomeScore;
                existing.AwayScore = m.AwayScore;
                existing.HomeTeamName = m.HomeTeamName;
                existing.AwayTeamName = m.AwayTeamName;
                existing.HomeTeamId = m.HomeTeamId;
                existing.AwayTeamId = m.AwayTeamId;
                existing.Round = m.Round;
                existing.MatchDate = m.MatchDate;
                if (existing.HomeScore.HasValue && existing.AwayScore.HasValue) existing.Status = MatchStatus.Finished;
                changed.Add(existing);
            }
            else
            {
                if (m.HomeScore.HasValue && m.AwayScore.HasValue) m.Status = MatchStatus.Finished;
                _matches.Add(m);
                changed.Add(m);
            }
        }
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertMatchesAsync(changed), "matches");
    }

    public async Task DeleteMatchAsync(string matchId)
    {
        _matches.RemoveAll(m => m.Id == matchId);
        _attendance.RemoveAll(a => a.MatchId == matchId);
        _matchEvents.RemoveAll(e => e.MatchId == matchId);
        NotifyStateChanged();
        // Asistencias y eventos caen en cascada por la clave foránea.
        await WriteAndRefreshAsync(() => _supabase.DeleteByIdAsync("matches", matchId), "matches", "attendance", "match_events");
    }

    public async Task ClearAllMatchesAsync()
    {
        _matches.Clear();
        _attendance.Clear();
        _matchEvents.Clear();
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.RpcAsync("clear_all_matches"), "matches", "attendance", "match_events");
    }

    public List<Attendance> GetAttendanceForMatch(string matchId) => _attendance.Where(a => a.MatchId == matchId).ToList();

    public Attendance? GetUserAttendance(string matchId, string playerId) =>
        _attendance.FirstOrDefault(a => a.MatchId == matchId && a.PlayerId == playerId);

    public async Task SetAttendanceAsync(string matchId, string playerId, AttendanceStatus status, string? note = null)
    {
        var att = _attendance.FirstOrDefault(a => a.MatchId == matchId && a.PlayerId == playerId);
        if (att != null)
        {
            att.Status = status;
            att.Note = note ?? att.Note;
            att.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            att = new Attendance { MatchId = matchId, PlayerId = playerId, Status = status, Note = note, UpdatedAt = DateTime.UtcNow };
            _attendance.Add(att);
        }
        NotifyStateChanged();

        // on_conflict sobre (match_id, player_id): si otro dispositivo creó la fila antes, se actualiza en vez de duplicar.
        var ok = await WriteAndRefreshAsync(
            () => _supabase.UpsertRowAsync("attendance?on_conflict=match_id,player_id", SupabaseMappers.ToDto(att)),
            "attendance");

        if (ok && status == AttendanceStatus.Going) { try { await _js.InvokeVoidAsync("triggerConfetti"); } catch { } }
    }

    // ==========================================
    // PAGOS Y CAJA
    // ==========================================
    public List<Payment> GetPayments() => _payments.OrderByDescending(p => p.PaidAt ?? p.DueDate ?? DateTime.MinValue).ToList();

    public List<Payment> GetPaymentsForUser(string playerId) =>
        _payments.Where(p => p.PlayerId == playerId).OrderByDescending(p => p.PaidAt ?? DateTime.MinValue).ToList();

    public async Task AddPaymentAsync(string playerId, string concept, decimal amount, PaymentMethod method, DateTime? paidAt = null, string notes = "")
    {
        var payment = new Payment
        {
            PlayerId = playerId,
            Concept = concept,
            Amount = amount,
            Status = PaymentStatus.Paid,
            PaidAt = paidAt ?? DateTime.UtcNow,
            Method = method,
            Notes = notes
        };
        _payments.Insert(0, payment);
        NotifyStateChanged();

        var ok = await WriteAndRefreshAsync(() => _supabase.UpsertPaymentAsync(payment), "payments");
        if (ok) { try { await _js.InvokeVoidAsync("triggerConfetti"); } catch { } }
    }

    public async Task AddBatchFeeAsync(string concept, decimal amount, DateTime dueDate)
    {
        var fees = _profiles.Where(p => p.IsActive).Select(p => new Payment
        {
            PlayerId = p.Id,
            Concept = concept,
            Amount = amount,
            DueDate = dueDate,
            Status = PaymentStatus.Pending
        }).ToList();

        _payments.AddRange(fees);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertPaymentsAsync(fees), "payments");
    }

    public async Task MarkPaymentAsPaidAsync(string paymentId, PaymentMethod method, string notes = "")
    {
        var pay = _payments.FirstOrDefault(p => p.Id == paymentId);
        if (pay == null) return;

        pay.Status = PaymentStatus.Paid;
        pay.PaidAt = DateTime.UtcNow;
        pay.Method = method;
        pay.Notes = notes;
        NotifyStateChanged();

        var ok = await WriteAndRefreshAsync(() => _supabase.UpsertPaymentAsync(pay), "payments");
        if (ok) { try { await _js.InvokeVoidAsync("triggerConfetti"); } catch { } }
    }

    public async Task DeletePaymentAsync(string paymentId)
    {
        _payments.RemoveAll(p => p.Id == paymentId);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.DeleteByIdAsync("payments", paymentId), "payments");
    }

    public decimal GetTeamBalance() =>
        _payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount) - _expenses.Sum(e => e.Amount);

    public decimal GetTotalCollectedThisMonth()
    {
        var now = DateTime.UtcNow;
        return _payments.Where(p => p.Status == PaymentStatus.Paid && p.PaidAt?.Month == now.Month && p.PaidAt?.Year == now.Year).Sum(p => p.Amount);
    }

    public decimal GetTotalPendingAmount() => _payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount);

    public List<TeamExpense> GetExpenses() => _expenses.OrderByDescending(e => e.ExpenseDate).ToList();

    public async Task AddExpenseAsync(TeamExpense expense)
    {
        _expenses.Insert(0, expense);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.UpsertExpenseAsync(expense), "team_expenses");
    }

    public async Task DeleteExpenseAsync(string expenseId)
    {
        _expenses.RemoveAll(e => e.Id == expenseId);
        NotifyStateChanged();
        await WriteAndRefreshAsync(() => _supabase.DeleteByIdAsync("team_expenses", expenseId), "team_expenses");
    }

    // ==========================================
    // ESTADÍSTICAS
    // ==========================================
    public List<MatchEvent> GetMatchEvents() => _matchEvents;

    public Dictionary<string, int> GetTopScorers() => CountByPlayer(EventType.Goal);
    public Dictionary<string, int> GetTopAssisters() => CountByPlayer(EventType.Assist);
    public Dictionary<string, int> GetMvpSummary() => CountByPlayer(EventType.Mvp);

    private Dictionary<string, int> CountByPlayer(EventType type) =>
        _matchEvents.Where(e => e.EventType == type)
                    .GroupBy(e => e.PlayerId)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());

    public Dictionary<string, (int Yellows, int Reds)> GetCardsSummary()
    {
        var result = new Dictionary<string, (int Yellows, int Reds)>();
        foreach (var e in _matchEvents.Where(x => x.EventType is EventType.YellowCard or EventType.RedCard))
        {
            (int Yellows, int Reds) curr = result.TryGetValue(e.PlayerId, out var c) ? c : (0, 0);
            result[e.PlayerId] = e.EventType == EventType.YellowCard ? (curr.Yellows + 1, curr.Reds) : (curr.Yellows, curr.Reds + 1);
        }
        return result;
    }

    // ==========================================
    // TEMPORADAS
    // ==========================================
    public async Task CloseSeasonAndStartNewAsync(string newSeasonName, decimal newSeasonFee)
    {
        var standings = GetStandings();
        var ourPos = standings.FindIndex(s => s.IsOurTeam) + 1;
        var ourRow = standings.FirstOrDefault(s => s.IsOurTeam);
        var pichichi = GetTopScorers().FirstOrDefault();
        var pichichiName = !string.IsNullOrEmpty(pichichi.Key) ? (GetProfileById(pichichi.Key)?.Nickname ?? pichichi.Key) : "Sin registrar";

        var archive = new SeasonArchive
        {
            SeasonName = _clubSettings.SeasonName,
            LeagueName = _clubSettings.LeagueName,
            ClosedAt = DateTime.UtcNow,
            Position = ourPos > 0 ? ourPos : 1,
            Played = ourRow?.Played ?? 0,
            Won = ourRow?.Won ?? 0,
            Drawn = ourRow?.Drawn ?? 0,
            Lost = ourRow?.Lost ?? 0,
            Points = ourRow?.Points ?? 0,
            GoalsFor = ourRow?.GoalsFor ?? 0,
            GoalsAgainst = ourRow?.GoalsAgainst ?? 0,
            PichichiPlayerName = pichichiName,
            PichichiGoals = pichichi.Value,
            FinalBalance = GetTeamBalance()
        };

        // Todo en una transacción en el servidor: archivo + limpieza de partidos, asistencias, eventos y cobros.
        var ok = await CloudWriteAsync(() => _supabase.RpcAsync("close_season", new
        {
            p_archive = archive,
            p_new_season_name = newSeasonName,
            p_new_fee = newSeasonFee
        }));

        if (ok)
        {
            _matches.Clear();
            _matchEvents.Clear();
            _attendance.Clear();
            _payments.Clear();
        }
        await RefreshFromCloudAsync();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _auth.OnSessionChanged -= HandleSessionChanged;
        _selfRef?.Dispose();
    }
}
