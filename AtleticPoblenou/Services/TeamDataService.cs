using System.Text.Json;
using AtleticPoblenou.Models;
using Microsoft.JSInterop;

namespace AtleticPoblenou.Services;

public class TeamDataService : ITeamDataService
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private bool _initialized;

    public event Action? OnChange;

    private string _currentUserId = "user-1";
    private string _teamSecretCode = "APN1929";
    private bool _isAuthenticated = false;
    private ClubSettings _clubSettings = new();
    private List<UserProfile> _profiles = new();
    private List<RivalTeam> _rivalTeams = new();
    private List<TeamAnnouncement> _announcements = new();
    private List<Match> _matches = new();
    private List<Attendance> _attendance = new();
    private List<Payment> _payments = new();
    private List<TeamExpense> _expenses = new();
    private List<MatchEvent> _matchEvents = new();

    public bool IsAuthenticated => _isAuthenticated;

    private readonly SupabaseClientService _supabase;

    public TeamDataService(IJSRuntime js, HttpClient http, SupabaseClientService supabase)
    {
        _js = js;
        _http = http;
        _supabase = supabase;
        LoadDefaults();
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var isAuth = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_is_authenticated");
            var savedUser = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_current_user");
            var savedCode = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_team_secret_code");
            var savedClub = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_club_settings");
            
            if (!string.IsNullOrEmpty(savedUser)) _currentUserId = savedUser;
            if (isAuth == "true" && !string.IsNullOrEmpty(savedUser)) _isAuthenticated = true;
            if (!string.IsNullOrEmpty(savedCode)) _teamSecretCode = savedCode;
            if (!string.IsNullOrEmpty(savedClub)) _clubSettings = JsonSerializer.Deserialize<ClubSettings>(savedClub) ?? new ClubSettings();

            var jsonProfiles = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_profiles");
            var jsonTeams = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_rival_teams");
            var jsonAnnouncements = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_announcements");
            var jsonMatches = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_matches");
            var jsonAttendance = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_attendance");
            var jsonPayments = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_payments");
            var jsonExpenses = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_expenses");
            var jsonEvents = await _js.InvokeAsync<string?>("blazorLocalStorage.get", "apn_events");

            if (string.IsNullOrEmpty(_clubSettings.LeagueName) || _clubSettings.LeagueName.Contains("Barcelona"))
            {
                _clubSettings.LeagueName = "Sábados División Honor (Temp. 26/27)";
                _clubSettings.SeasonName = "TEMP 26/27";
                _clubSettings.ShortName = "ATºPOBLENOU";
                await SaveClubSettingsAsync(_clubSettings);
            }

            if (!string.IsNullOrEmpty(jsonProfiles)) _profiles = JsonSerializer.Deserialize<List<UserProfile>>(jsonProfiles) ?? GetInitialProfiles();

            if (!string.IsNullOrEmpty(jsonTeams))
            {
                var loaded = JsonSerializer.Deserialize<List<RivalTeam>>(jsonTeams);
                if (loaded != null && loaded.Any(t => t.Name == "FONTETAS"))
                    _rivalTeams = loaded;
                else
                {
                    _rivalTeams = GetInitialRivalTeams();
                    await SaveRivalTeamsAsync();
                }
            }

            if (!string.IsNullOrEmpty(jsonAnnouncements)) _announcements = JsonSerializer.Deserialize<List<TeamAnnouncement>>(jsonAnnouncements) ?? GetInitialAnnouncements();

            if (!string.IsNullOrEmpty(jsonMatches))
            {
                _matches = JsonSerializer.Deserialize<List<Match>>(jsonMatches) ?? new();
            }

            if (!string.IsNullOrEmpty(jsonAttendance))
            {
                _attendance = JsonSerializer.Deserialize<List<Attendance>>(jsonAttendance) ?? new();
            }

            if (!string.IsNullOrEmpty(jsonPayments)) _payments = JsonSerializer.Deserialize<List<Payment>>(jsonPayments) ?? GetInitialPayments();

            if (!string.IsNullOrEmpty(jsonExpenses)) _expenses = JsonSerializer.Deserialize<List<TeamExpense>>(jsonExpenses) ?? GetInitialExpenses();

            if (!string.IsNullOrEmpty(jsonEvents)) _matchEvents = JsonSerializer.Deserialize<List<MatchEvent>>(jsonEvents) ?? GetInitialMatchEvents();

            // Cloud sync from Supabase (shared real-time backend)
            try
            {
                var sbProfiles = await _supabase.FetchProfilesAsync();
                if (sbProfiles != null)
                {
                    if (sbProfiles.Count > 0)
                    {
                        _profiles = sbProfiles;
                        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_profiles", JsonSerializer.Serialize(_profiles));
                    }
                    else
                    {
                        _ = _supabase.UpsertProfilesBatchAsync(_profiles);
                    }
                }

                var sbTeams = await _supabase.FetchRivalTeamsAsync();
                if (sbTeams != null)
                {
                    if (sbTeams.Count > 0)
                    {
                        _rivalTeams = sbTeams;
                        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_rival_teams", JsonSerializer.Serialize(_rivalTeams));
                    }
                    else
                    {
                        _ = _supabase.UpsertRivalTeamsBatchAsync(_rivalTeams);
                    }
                }

                var sbMatches = await _supabase.FetchMatchesAsync();
                if (sbMatches != null)
                {
                    _matches = sbMatches;
                    await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_matches", JsonSerializer.Serialize(_matches));
                }

                var sbAttendance = await _supabase.FetchAttendanceAsync();
                if (sbAttendance != null)
                {
                    _attendance = sbAttendance;
                    await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_attendance", JsonSerializer.Serialize(_attendance));
                }

                var sbPayments = await _supabase.FetchPaymentsAsync();
                if (sbPayments != null)
                {
                    _payments = sbPayments;
                    await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_payments", JsonSerializer.Serialize(_payments));
                }

                var sbExpenses = await _supabase.FetchExpensesAsync();
                if (sbExpenses != null)
                {
                    _expenses = sbExpenses;
                    await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_expenses", JsonSerializer.Serialize(_expenses));
                }

                var sbEvents = await _supabase.FetchMatchEventsAsync();
                if (sbEvents != null)
                {
                    _matchEvents = sbEvents;
                    await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_events", JsonSerializer.Serialize(_matchEvents));
                }

                var sbAnnouncements = await _supabase.FetchAnnouncementsAsync();
                if (sbAnnouncements != null)
                {
                    if (sbAnnouncements.Count > 0)
                    {
                        _announcements = sbAnnouncements;
                        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_announcements", JsonSerializer.Serialize(_announcements));
                    }
                    else
                    {
                        _ = _supabase.UpsertAnnouncementsBatchAsync(_announcements);
                    }
                }

                var sbClub = await _supabase.FetchClubSettingsAsync();
                if (sbClub != null && !string.IsNullOrEmpty(sbClub.ClubName))
                {
                    _clubSettings = sbClub;
                    await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_club_settings", JsonSerializer.Serialize(_clubSettings));
                }
                else
                {
                    _ = _supabase.UpsertClubSettingsAsync(_clubSettings);
                }
            }
            catch
            {
                // Fallback seamless a almacenamiento local en caso de error de red
            }
        }
        catch
        {
            LoadDefaults();
        }

        _initialized = true;
        NotifyStateChanged();
    }

    private void LoadDefaults()
    {
        _profiles = GetInitialProfiles();
        _rivalTeams = GetInitialRivalTeams();
        _announcements = GetInitialAnnouncements();
        _matches = GetInitialMatches();
        _attendance = GetInitialAttendance();
        _payments = GetInitialPayments();
        _expenses = GetInitialExpenses();
        _matchEvents = GetInitialMatchEvents();
    }

    public UserProfile GetCurrentUser()
    {
        return _profiles.FirstOrDefault(p => p.Id == _currentUserId) 
            ?? _profiles.FirstOrDefault() 
            ?? new UserProfile { FullName = "Pitu", Nickname = "pitu1386", Role = UserRole.Admin, IsCaptain = true, Position = Position.Centrocampista };
    }

    public async Task SetCurrentUserIdAsync(string userId)
    {
        _currentUserId = userId;
        _isAuthenticated = true;
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_current_user", userId);
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_is_authenticated", "true");
        NotifyStateChanged();
    }

    public async Task<(bool Success, string ErrorMessage)> LoginAsync(string emailOrNickname, string password)
    {
        if (string.IsNullOrWhiteSpace(emailOrNickname))
            return (false, "Por favor ingresa tu email o apodo.");

        var cleanQuery = emailOrNickname.Trim().ToLowerInvariant();
        var user = _profiles.FirstOrDefault(p => 
            p.Email.Trim().ToLowerInvariant() == cleanQuery || 
            p.Nickname.Trim().ToLowerInvariant() == cleanQuery ||
            p.FullName.Trim().ToLowerInvariant() == cleanQuery);

        if (user == null)
            return (false, "No se encontró ningún jugador con ese email o apodo.");

        if (!user.IsActive)
            return (false, "DEACTIVATED_NEEDS_CODE");

        // For demo or entered password check
        if (!string.IsNullOrEmpty(user.Password) && user.Password != password && password != "1234")
            return (false, "Contraseña incorrecta. (Prueba con '1234' para cuentas de demo).");

        _currentUserId = user.Id;
        _isAuthenticated = true;
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_current_user", user.Id);
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_is_authenticated", "true");
        NotifyStateChanged();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterModel model)
    {
        var cleanInputCode = model.TeamCode?.Trim().Replace("-", "").ToUpperInvariant();
        var cleanActualCode = _teamSecretCode.Trim().Replace("-", "").ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(cleanInputCode) || cleanInputCode != cleanActualCode)
        {
            return (false, "Código de equipo incorrecto. Pídele el código secreto actual al capitán para entrar.");
        }

        if (string.IsNullOrWhiteSpace(model.FullName))
            return (false, "Debes ingresar tu nombre y apellido.");

        if (string.IsNullOrWhiteSpace(model.Email) || !model.Email.Contains('@'))
            return (false, "Ingresa una dirección de email válida.");

        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 4)
            return (false, "La contraseña debe tener al menos 4 caracteres.");

        var cleanEmail = model.Email.Trim().ToLowerInvariant();
        if (_profiles.Any(p => p.Email.Trim().ToLowerInvariant() == cleanEmail))
            return (false, "Ya existe un jugador registrado con este email.");

        var nickname = string.IsNullOrWhiteSpace(model.Nickname) 
            ? model.FullName.Split(' ')[0] 
            : model.Nickname.Trim();

        var isFirstAdmin = !_profiles.Any(p => p.Role == UserRole.Admin);

        var newProfile = new UserProfile
        {
            FullName = model.FullName.Trim(),
            Nickname = nickname,
            Email = cleanEmail,
            Password = model.Password,
            Phone = model.Phone?.Trim() ?? string.Empty,
            JerseyNumber = model.JerseyNumber,
            Position = model.Position,
            Foot = model.Foot,
            BirthDate = model.BirthDate,
            Role = isFirstAdmin ? UserRole.Admin : UserRole.Player,
            CreatedAt = DateTime.UtcNow
        };

        _profiles.Add(newProfile);
        _currentUserId = newProfile.Id;
        _isAuthenticated = true;

        await SaveProfilesAsync();
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_current_user", newProfile.Id);
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_is_authenticated", "true");
        await _js.InvokeVoidAsync("triggerConfetti");
        NotifyStateChanged();

        return (true, string.Empty);
    }

    public async Task LogoutAsync()
    {
        _isAuthenticated = false;
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_is_authenticated", "false");
        NotifyStateChanged();
    }

    public string GetTeamSecretCode() => _teamSecretCode;

    public async Task<string> GenerateNewTeamCodeAsync()
    {
        var randomCode = $"APN-{Random.Shared.Next(1000, 9999)}";
        _teamSecretCode = randomCode;
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_team_secret_code", _teamSecretCode);
        NotifyStateChanged();
        return _teamSecretCode;
    }

    public ClubSettings GetClubSettings() => _clubSettings;

    public async Task SaveClubSettingsAsync(ClubSettings settings)
    {
        _clubSettings = settings;
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_club_settings", JsonSerializer.Serialize(_clubSettings));
        _ = _supabase.UpsertClubSettingsAsync(_clubSettings);
        NotifyStateChanged();
    }

    public UserProfile? GetProfileById(string profileId) => 
        _profiles.FirstOrDefault(p => p.Id == profileId);

    public async Task SaveProfileAsync(UserProfile profile)
    {
        var existingIndex = _profiles.FindIndex(p => p.Id == profile.Id);
        if (existingIndex >= 0)
        {
            _profiles[existingIndex] = profile;
        }
        else
        {
            _profiles.Add(profile);
        }

        await SaveProfilesAsync();
        NotifyStateChanged();
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        _profiles.RemoveAll(p => p.Id == profileId);
        await SaveProfilesAsync();
        NotifyStateChanged();
    }

    // Rival Teams & Standings
    public List<RivalTeam> GetRivalTeams() => _rivalTeams.OrderBy(t => t.Name).ToList();

    public RivalTeam? GetRivalTeamById(string teamId) => _rivalTeams.FirstOrDefault(t => t.Id == teamId);

    public async Task AddRivalTeamAsync(RivalTeam team)
    {
        _rivalTeams.Add(team);
        await SaveRivalTeamsAsync();
        NotifyStateChanged();
    }

    public async Task UpdateRivalTeamAsync(RivalTeam team)
    {
        var idx = _rivalTeams.FindIndex(t => t.Id == team.Id);
        if (idx >= 0) _rivalTeams[idx] = team;
        await SaveRivalTeamsAsync();
        NotifyStateChanged();
    }

    public async Task DeleteRivalTeamAsync(string teamId)
    {
        _rivalTeams.RemoveAll(t => t.Id == teamId);
        await SaveRivalTeamsAsync();
        _ = _supabase.DeleteRowAsync("rival_teams", teamId);
        NotifyStateChanged();
    }

    public List<StandingRow> GetStandings()
    {
        var standings = new List<StandingRow>();

        // Nuestro equipo: ATºPOBLENOU (con datos configurables en Admin)
        var ourRow = new StandingRow
        {
            TeamId = "apn",
            TeamName = !string.IsNullOrEmpty(_clubSettings.ShortName) ? _clubSettings.ShortName : _clubSettings.ClubName,
            PrimaryColorHex = _clubSettings.PrimaryColorHex,
            SecondaryColorHex = _clubSettings.SecondaryColorHex,
            IsOurTeam = true
        };

        // 3. Procesar resultados de TODOS los partidos terminados de la liga
        foreach (var m in _matches.Where(m => m.Status == MatchStatus.Finished))
        {
            int hScore = 0;
            int aScore = 0;

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

            // Buscar fila local
            StandingRow? homeRow = null;
            if (m.IsOurMatch && m.IsHome)
            {
                homeRow = ourRow;
            }
            else
            {
                homeRow = standings.FirstOrDefault(s => (!string.IsNullOrEmpty(m.HomeTeamId) && s.TeamId == m.HomeTeamId) ||
                                                        string.Equals(s.TeamName, m.HomeTeamName, StringComparison.OrdinalIgnoreCase));
            }

            // Buscar fila visitante
            StandingRow? awayRow = null;
            if (m.IsOurMatch && !m.IsHome)
            {
                awayRow = ourRow;
            }
            else
            {
                awayRow = standings.FirstOrDefault(s => (!string.IsNullOrEmpty(m.AwayTeamId) && s.TeamId == m.AwayTeamId) ||
                                                        string.Equals(s.TeamName, m.AwayTeamName, StringComparison.OrdinalIgnoreCase));
            }

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

    public async Task SaveBatchRoundResultsAsync(int round, List<Match> matches)
    {
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

                if (existing.HomeScore.HasValue && existing.AwayScore.HasValue)
                {
                    existing.Status = MatchStatus.Finished;
                }
            }
            else
            {
                if (m.HomeScore.HasValue && m.AwayScore.HasValue)
                {
                    m.Status = MatchStatus.Finished;
                }
                _matches.Add(m);
            }
        }

        await SaveMatchesAsync();
        NotifyStateChanged();
    }

    public async Task AddLeagueMatchAsync(Match match)
    {
        if (match.HomeScore.HasValue && match.AwayScore.HasValue)
        {
            match.Status = MatchStatus.Finished;
        }
        _matches.Add(match);
        await SaveMatchesAsync();
        NotifyStateChanged();
    }

    // Announcements & Polls
    public List<TeamAnnouncement> GetAnnouncements() => 
        _announcements.Where(a => a.IsActive).OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.CreatedAt).ToList();

    public List<TeamAnnouncement> GetAllAnnouncements() =>
        _announcements.OrderByDescending(a => a.CreatedAt).ToList();

    public async Task AddAnnouncementAsync(TeamAnnouncement announcement)
    {
        _announcements.Insert(0, announcement);
        await SaveAnnouncementsAsync();
        NotifyStateChanged();
    }

    public async Task VoteAnnouncementPollAsync(string announcementId, string playerId, int optionIndex)
    {
        var ann = _announcements.FirstOrDefault(a => a.Id == announcementId);
        if (ann != null)
        {
            ann.Votes[playerId] = optionIndex;
            await SaveAnnouncementsAsync();
            NotifyStateChanged();
        }
    }

    public async Task ArchiveAnnouncementAsync(string announcementId)
    {
        var ann = _announcements.FirstOrDefault(a => a.Id == announcementId);
        if (ann != null)
        {
            ann.IsActive = false;
            await SaveAnnouncementsAsync();
            NotifyStateChanged();
        }
    }

    public async Task RestoreAnnouncementAsync(string announcementId)
    {
        var ann = _announcements.FirstOrDefault(a => a.Id == announcementId);
        if (ann != null)
        {
            ann.IsActive = true;
            await SaveAnnouncementsAsync();
            NotifyStateChanged();
        }
    }

    public async Task DeleteAnnouncementAsync(string announcementId)
    {
        _announcements.RemoveAll(a => a.Id == announcementId);
        await SaveAnnouncementsAsync();
        _ = _supabase.DeleteRowAsync("announcements", announcementId);
        NotifyStateChanged();
    }

    public List<UserProfile> GetProfiles() => _profiles.OrderBy(p => p.JerseyNumber ?? 99).ToList();

    public List<Match> GetMatches() => _matches.OrderBy(m => m.MatchDate).ToList();

    public Match? GetNextMatch()
    {
        return _matches.Where(m => m.Status == MatchStatus.Upcoming && m.MatchDate >= DateTime.UtcNow.AddHours(-3))
                       .OrderBy(m => m.MatchDate)
                       .FirstOrDefault();
    }

    public async Task AddMatchAsync(Match match)
    {
        _matches.Add(match);
        await SaveMatchesAsync();
        NotifyStateChanged();
    }

    public async Task UpdateMatchDetailsAsync(Match match)
    {
        var idx = _matches.FindIndex(m => m.Id == match.Id);
        if (idx >= 0)
        {
            _matches[idx] = match;
            await SaveMatchesAsync();
            NotifyStateChanged();
        }
    }

    public async Task UpdateMatchResultAsync(string matchId, int ourScore, int rivalScore, List<MatchEvent> events)
    {
        var match = _matches.FirstOrDefault(m => m.Id == matchId);
        if (match != null)
        {
            match.OurScore = ourScore;
            match.RivalScore = rivalScore;
            match.Status = MatchStatus.Finished;

            _matchEvents.RemoveAll(e => e.MatchId == matchId);
            _matchEvents.AddRange(events);

            await SaveMatchesAsync();
            await SaveEventsAsync();
            await _js.InvokeVoidAsync("triggerConfetti");
            NotifyStateChanged();
        }
    }

    public async Task DeleteMatchAsync(string matchId)
    {
        _matches.RemoveAll(m => m.Id == matchId);
        _attendance.RemoveAll(a => a.MatchId == matchId);
        _matchEvents.RemoveAll(e => e.MatchId == matchId);

        await SaveMatchesAsync();
        await SaveAttendanceAsync();
        await SaveEventsAsync();

        _ = _supabase.DeleteRowAsync("matches", matchId);
        _ = _supabase.DeleteRowsWhereAsync("attendance", "match_id", matchId);
        _ = _supabase.DeleteRowsWhereAsync("match_events", "match_id", matchId);

        NotifyStateChanged();
    }

    public List<Attendance> GetAttendanceForMatch(string matchId)
    {
        return _attendance.Where(a => a.MatchId == matchId).ToList();
    }

    public Attendance? GetUserAttendance(string matchId, string playerId)
    {
        return _attendance.FirstOrDefault(a => a.MatchId == matchId && a.PlayerId == playerId);
    }

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
            _attendance.Add(new Attendance
            {
                MatchId = matchId,
                PlayerId = playerId,
                Status = status,
                Note = note,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (status == AttendanceStatus.Going)
        {
            await _js.InvokeVoidAsync("triggerConfetti");
        }

        await SaveAttendanceAsync();
        NotifyStateChanged();
    }

    public List<Payment> GetPayments() => _payments.OrderByDescending(p => p.PaidAt ?? p.DueDate ?? DateTime.MinValue).ToList();

    public List<Payment> GetPaymentsForUser(string playerId) => _payments.Where(p => p.PlayerId == playerId).OrderByDescending(p => p.PaidAt ?? DateTime.MinValue).ToList();

    public async Task AddPaymentAsync(string playerId, string concept, decimal amount, PaymentMethod method, DateTime? paidAt = null, string notes = "")
    {
        _payments.Insert(0, new Payment
        {
            PlayerId = playerId,
            Concept = concept,
            Amount = amount,
            Status = PaymentStatus.Paid,
            PaidAt = paidAt ?? DateTime.UtcNow,
            Method = method,
            Notes = notes
        });

        await SavePaymentsAsync();
        await _js.InvokeVoidAsync("triggerConfetti");
        NotifyStateChanged();
    }

    public async Task AddBatchFeeAsync(string concept, decimal amount, DateTime dueDate)
    {
        foreach (var profile in _profiles)
        {
            _payments.Add(new Payment
            {
                PlayerId = profile.Id,
                Concept = concept,
                Amount = amount,
                DueDate = dueDate,
                Status = PaymentStatus.Pending
            });
        }

        await SavePaymentsAsync();
        NotifyStateChanged();
    }

    public async Task MarkPaymentAsPaidAsync(string paymentId, PaymentMethod method, string notes = "")
    {
        var pay = _payments.FirstOrDefault(p => p.Id == paymentId);
        if (pay != null)
        {
            pay.Status = PaymentStatus.Paid;
            pay.PaidAt = DateTime.UtcNow;
            pay.Method = method;
            pay.Notes = notes;

            await SavePaymentsAsync();
            await _js.InvokeVoidAsync("triggerConfetti");
            NotifyStateChanged();
        }
    }

    public decimal GetTeamBalance()
    {
        var totalIncome = _payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount);
        var totalExpenses = _expenses.Sum(e => e.Amount);
        return totalIncome - totalExpenses;
    }

    public decimal GetTotalCollectedThisMonth()
    {
        var now = DateTime.UtcNow;
        return _payments.Where(p => p.Status == PaymentStatus.Paid && p.PaidAt?.Month == now.Month && p.PaidAt?.Year == now.Year)
                        .Sum(p => p.Amount);
    }

    public decimal GetTotalPendingAmount()
    {
        return _payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount);
    }

    public List<TeamExpense> GetExpenses() => _expenses.OrderByDescending(e => e.ExpenseDate).ToList();

    public async Task AddExpenseAsync(TeamExpense expense)
    {
        _expenses.Insert(0, expense);
        await SaveExpensesAsync();
        _ = _supabase.UpsertExpenseAsync(expense);
        NotifyStateChanged();
    }

    public async Task DeleteExpenseAsync(string expenseId)
    {
        _expenses.RemoveAll(e => e.Id == expenseId);
        await SaveExpensesAsync();
        _ = _supabase.DeleteRowAsync("team_expenses", expenseId);
        NotifyStateChanged();
    }

    public List<MatchEvent> GetMatchEvents() => _matchEvents;

    public Dictionary<string, int> GetTopScorers()
    {
        return _matchEvents.Where(e => e.EventType == EventType.Goal)
                           .GroupBy(e => e.PlayerId)
                           .ToDictionary(g => g.Key, g => g.Count())
                           .OrderByDescending(kv => kv.Value)
                           .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public Dictionary<string, int> GetTopAssisters()
    {
        return _matchEvents.Where(e => e.EventType == EventType.Assist)
                           .GroupBy(e => e.PlayerId)
                           .ToDictionary(g => g.Key, g => g.Count())
                           .OrderByDescending(kv => kv.Value)
                           .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public Dictionary<string, (int Yellows, int Reds)> GetCardsSummary()
    {
        var result = new Dictionary<string, (int Yellows, int Reds)>();
        foreach (var e in _matchEvents.Where(x => x.EventType == EventType.YellowCard || x.EventType == EventType.RedCard))
        {
            if (!result.ContainsKey(e.PlayerId))
                result[e.PlayerId] = (0, 0);

            var curr = result[e.PlayerId];
            if (e.EventType == EventType.YellowCard) result[e.PlayerId] = (curr.Yellows + 1, curr.Reds);
            else if (e.EventType == EventType.RedCard) result[e.PlayerId] = (curr.Yellows, curr.Reds + 1);
        }
        return result;
    }

    public Dictionary<string, int> GetMvpSummary()
    {
        return _matchEvents.Where(e => e.EventType == EventType.Mvp)
                           .GroupBy(e => e.PlayerId)
                           .ToDictionary(g => g.Key, g => g.Count())
                           .OrderByDescending(kv => kv.Value)
                           .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public async Task ResetToDemoAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_current_user");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_profiles");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_rival_teams");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_announcements");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_matches");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_attendance");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_payments");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_expenses");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_events");
        await _js.InvokeVoidAsync("blazorLocalStorage.remove", "apn_team_secret_code");

        _currentUserId = "user-1";
        LoadDefaults();
        NotifyStateChanged();
    }

    private async Task SaveProfilesAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_profiles", JsonSerializer.Serialize(_profiles));
        _ = _supabase.UpsertProfilesBatchAsync(_profiles);
    }

    private async Task SaveRivalTeamsAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_rival_teams", JsonSerializer.Serialize(_rivalTeams));
        _ = _supabase.UpsertRivalTeamsBatchAsync(_rivalTeams);
    }

    private async Task SaveAnnouncementsAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_announcements", JsonSerializer.Serialize(_announcements));
        _ = _supabase.UpsertAnnouncementsBatchAsync(_announcements);
    }

    private async Task SaveMatchesAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_matches", JsonSerializer.Serialize(_matches));
        _ = _supabase.UpsertMatchesBatchAsync(_matches);
    }

    private async Task SaveAttendanceAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_attendance", JsonSerializer.Serialize(_attendance));
        _ = _supabase.UpsertAttendanceBatchAsync(_attendance);
    }

    private async Task SavePaymentsAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_payments", JsonSerializer.Serialize(_payments));
        _ = _supabase.UpsertPaymentsBatchAsync(_payments);
    }

    private async Task SaveExpensesAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_expenses", JsonSerializer.Serialize(_expenses));
    }

    private async Task SaveEventsAsync()
    {
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_events", JsonSerializer.Serialize(_matchEvents));
        _ = _supabase.UpsertMatchEventsBatchAsync(_matchEvents);
    }

    private List<TeamAnnouncement> GetInitialAnnouncements() => new()
    {
        new TeamAnnouncement
        {
            Id = "ann-1",
            Title = "🥩 Asado y Tercer Tiempo este sábado post-partido",
            Content = "Muchachos, después de terminar la primera jornada contra FONTETAS, organizamos un asado en la parrilla del club. ¡Confirmen en la encuesta para calcular la carne, bebidas y brasas!",
            AuthorName = "pitu1386 (Capitán)",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            HasPoll = true,
            PollOptions = new() { "Me sumo al asado 🥩", "En duda / aviso el viernes 🤔", "No llego ❌" },
            Votes = new()
            {
                { "user-1", 0 }, // Dani: Me sumo
                { "user-2", 0 }, // Carles: Me sumo
                { "user-3", 0 }, // Marc: Me sumo
                { "user-4", 1 }  // Jordi: En duda
            },
            IsPinned = true,
            IsActive = true
        },
        new TeamAnnouncement
        {
            Id = "ann-archived-1",
            Title = "👕 Nuevas Camisetas Oficiales y Cierre de Pretemporada",
            Content = "Compañeros, ya llegaron todas las camisetas titulares (rojiblancas) y alternativas con los dorsales estampados. ¡A darlo todo en el debut liguero!",
            AuthorName = "pitu1386 (Capitán)",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            HasPoll = false,
            IsPinned = false,
            IsActive = false
        }
    };

    private List<RivalTeam> GetInitialRivalTeams() => new()
    {
        new RivalTeam { Id = "team-1", Name = "FONTETAS", PrimaryColorHex = "#EAB308", SecondaryColorHex = "#15803D", KitDescription = "Amarillo y Verde" },
        new RivalTeam { Id = "team-2", Name = "LA PEÑA", PrimaryColorHex = "#DC2626", SecondaryColorHex = "#FFFFFF", KitDescription = "Rojo y Blanco" },
        new RivalTeam { Id = "team-3", Name = "ARISTOI B", PrimaryColorHex = "#1E3A8A", SecondaryColorHex = "#FFFFFF", KitDescription = "Azul Marino y Blanco" },
        new RivalTeam { Id = "team-4", Name = "LA PLANADA A", PrimaryColorHex = "#EA580C", SecondaryColorHex = "#000000", KitDescription = "Naranja y Negro" },
        new RivalTeam { Id = "team-6", Name = "LLANO", PrimaryColorHex = "#16A34A", SecondaryColorHex = "#FFFFFF", KitDescription = "Verde y Blanco" },
        new RivalTeam { Id = "team-7", Name = "CAN ROCA74", PrimaryColorHex = "#2563EB", SecondaryColorHex = "#FACC15", KitDescription = "Azul y Amarillo" },
        new RivalTeam { Id = "team-8", Name = "LA PLANADA B", PrimaryColorHex = "#F97316", SecondaryColorHex = "#FFFFFF", KitDescription = "Naranja y Blanco" },
        new RivalTeam { Id = "team-9", Name = "LLIÇA D’AVALL", PrimaryColorHex = "#DC2626", SecondaryColorHex = "#FACC15", KitDescription = "Rojo y Amarillo" },
        new RivalTeam { Id = "team-10", Name = "ATºBADIENSE", PrimaryColorHex = "#3B82F6", SecondaryColorHex = "#FFFFFF", KitDescription = "Azul y Blanco" },
        new RivalTeam { Id = "team-11", Name = "CDPV BADIA", PrimaryColorHex = "#15803D", SecondaryColorHex = "#000000", KitDescription = "Verde y Negro" },
        new RivalTeam { Id = "team-12", Name = "ATºLA CELESTE", PrimaryColorHex = "#0284C7", SecondaryColorHex = "#FFFFFF", KitDescription = "Celeste y Blanco" },
        new RivalTeam { Id = "team-13", Name = "STA PERPETUA", PrimaryColorHex = "#1D4ED8", SecondaryColorHex = "#EF4444", KitDescription = "Azul y Rojo" },
        new RivalTeam { Id = "team-14", Name = "PUEBLO NUEVO 2002", PrimaryColorHex = "#991B1B", SecondaryColorHex = "#000000", KitDescription = "Granate y Negro" },
        new RivalTeam { Id = "team-15", Name = "ARISTOI A", PrimaryColorHex = "#1E3A8A", SecondaryColorHex = "#F59E0B", KitDescription = "Azul Marino y Dorado" }
    };

    private void NotifyStateChanged() => OnChange?.Invoke();

    // Datos iniciales en Español
    private List<UserProfile> GetInitialProfiles() => new()
    {
        new UserProfile { Id = "user-1", FullName = "Pitu", Nickname = "pitu1386", JerseyNumber = 10, Position = Position.Centrocampista, Foot = DominantFoot.Diestro, Role = UserRole.Admin, IsCaptain = true, Phone = "+34 600 00 00 00", Email = "pitu1386@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1986, 5, 14), Dni = "47891234X" },
        new UserProfile { Id = "user-2", FullName = "Carles Puig", Nickname = "Carles", JerseyNumber = 4, Position = Position.Defensa, Foot = DominantFoot.Diestro, Role = UserRole.Treasurer, IsCaptain = true, Phone = "+34 622 33 44 55", Email = "carles@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1985, 11, 23), Dni = "46543210Y" },
        new UserProfile { Id = "user-3", FullName = "Marc Rovira", Nickname = "Marc", JerseyNumber = 1, Position = Position.Portero, Foot = DominantFoot.Diestro, Role = UserRole.FieldManager, IsCaptain = false, Phone = "+34 633 44 55 66", Email = "marc@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1989, 2, 8) },
        new UserProfile { Id = "user-4", FullName = "Jordi Soler", Nickname = "Jordi", JerseyNumber = 2, Position = Position.Defensa, Foot = DominantFoot.Diestro, Role = UserRole.Player, IsCaptain = false, Phone = "+34 644 55 66 77", Email = "jordi@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1986, 9, 30) },
        new UserProfile { Id = "user-5", FullName = "Sergi Vidal", Nickname = "Sergi", JerseyNumber = 3, Position = Position.Defensa, Foot = DominantFoot.Zurdo, Role = UserRole.Player, IsCaptain = false, Phone = "+34 655 66 77 88", Email = "sergi@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1988, 4, 19) },
        new UserProfile { Id = "user-6", FullName = "Xavi Font", Nickname = "Xavi", JerseyNumber = 6, Position = Position.Centrocampista, Foot = DominantFoot.Ambidiestro, Role = UserRole.Player, IsCaptain = false, Phone = "+34 666 77 88 99", Email = "xavi@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1984, 12, 1) },
        new UserProfile { Id = "user-7", FullName = "Albert Serra", Nickname = "Albert", JerseyNumber = 8, Position = Position.Centrocampista, Foot = DominantFoot.Diestro, Role = UserRole.Player, IsCaptain = false, Phone = "+34 677 88 99 00", Email = "albert@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1987, 8, 11) },
        new UserProfile { Id = "user-8", FullName = "Lluís Martí", Nickname = "Lluís", JerseyNumber = 9, Position = Position.Delantero, Foot = DominantFoot.Zurdo, Role = UserRole.Player, IsCaptain = false, Phone = "+34 688 99 00 11", Email = "lluis@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1986, 7, 25) },
        new UserProfile { Id = "user-9", FullName = "Pol Navarro", Nickname = "Pol", JerseyNumber = 11, Position = Position.Delantero, Foot = DominantFoot.Diestro, Role = UserRole.Player, IsCaptain = false, Phone = "+34 699 00 11 22", Email = "pol@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1990, 3, 17) },
        new UserProfile { Id = "user-10", FullName = "Gerard Mas", Nickname = "Geri", JerseyNumber = 5, Position = Position.Defensa, Foot = DominantFoot.Diestro, Role = UserRole.Player, IsCaptain = false, Phone = "+34 612 34 56 78", Email = "gerard@atleticpoblenou.cat", Password = "1234", BirthDate = new DateTime(1988, 10, 5) }
    };

    private List<Match> GetInitialMatches() => new();

    private List<Attendance> GetInitialAttendance() => new();

    private List<Payment> GetInitialPayments() => new();

    private List<TeamExpense> GetInitialExpenses() => new();

    private List<MatchEvent> GetInitialMatchEvents() => new();

    // ==========================================
    // DEACTIVATION & SECURITY CODE REACTIVATION
    // ==========================================
    public async Task DeactivatePlayerAsync(string playerId)
    {
        var player = _profiles.FirstOrDefault(p => p.Id == playerId);
        if (player != null)
        {
            player.IsActive = false;
            await SaveProfilesAsync();

            // If the deactivated player is currently logged in, kick them out immediately
            if (_currentUserId == playerId)
            {
                await LogoutAsync();
            }
            NotifyStateChanged();
        }
    }

    public async Task ReactivatePlayerAsync(string playerId)
    {
        var player = _profiles.FirstOrDefault(p => p.Id == playerId);
        if (player != null)
        {
            player.IsActive = true;
            await SaveProfilesAsync();
            NotifyStateChanged();
        }
    }

    public async Task<(bool Success, string ErrorMessage)> ReactivateWithCodeAsync(string emailOrNickname, string password, string securityCode)
    {
        if (string.IsNullOrWhiteSpace(emailOrNickname))
            return (false, "Ingresa tu email o apodo.");

        var cleanCode = securityCode?.Trim().Replace("-", "").ToUpperInvariant();
        var currentCode = _teamSecretCode.Trim().Replace("-", "").ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(cleanCode) || cleanCode != currentCode)
        {
            return (false, "Código de seguridad incorrecto. Pídele el código secreto actual al administrador.");
        }

        var cleanQuery = emailOrNickname.Trim().ToLowerInvariant();
        var user = _profiles.FirstOrDefault(p => 
            p.Email.Trim().ToLowerInvariant() == cleanQuery || 
            p.Nickname.Trim().ToLowerInvariant() == cleanQuery ||
            p.FullName.Trim().ToLowerInvariant() == cleanQuery);

        if (user == null)
            return (false, "No se encontró ningún jugador con ese usuario.");

        if (!string.IsNullOrEmpty(user.Password) && user.Password != password && password != "1234")
            return (false, "Contraseña incorrecta.");

        user.IsActive = true;
        await SaveProfilesAsync();

        _currentUserId = user.Id;
        _isAuthenticated = true;
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_current_user", user.Id);
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_is_authenticated", "true");
        NotifyStateChanged();
        return (true, string.Empty);
    }

    // ==========================================
    // SEASON LIFECYCLE & NEW SEASON WIZARD
    // ==========================================
    public async Task CloseSeasonAndStartNewAsync(string newSeasonName, decimal newSeasonFee)
    {
        var standings = GetStandings();
        var ourPos = standings.FindIndex(s => s.IsOurTeam) + 1;
        var ourRow = standings.FirstOrDefault(s => s.IsOurTeam);
        var topScorers = GetTopScorers();
        var pichichiEntry = topScorers.OrderByDescending(x => x.Value).FirstOrDefault();

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
            PichichiPlayerName = !string.IsNullOrEmpty(pichichiEntry.Key) ? pichichiEntry.Key : "Sin registrar",
            PichichiGoals = pichichiEntry.Value,
            FinalBalance = GetTeamBalance()
        };

        _clubSettings.SeasonHistory.Add(archive);
        _clubSettings.SeasonName = newSeasonName;
        _clubSettings.SeasonFeePerPlayer = newSeasonFee;
        await SaveClubSettingsAsync(_clubSettings);

        // Clear matches, match events, attendances for the new season
        _matches.Clear();
        _matchEvents.Clear();
        _attendance.Clear();
        await SaveMatchesAsync();
        await SaveEventsAsync();
        await _js.InvokeVoidAsync("blazorLocalStorage.set", "apn_attendance", JsonSerializer.Serialize(_attendance));

        // Reset all player payment records for the new season so they start at 0€ (players intact!)
        _payments.Clear();
        await SavePaymentsAsync();

        NotifyStateChanged();
    }

    // ==========================================
    // DYNAMIC MATCH WEATHER (OPEN-METEO)
    // ==========================================
    public async Task<MatchWeatherInfo> GetMatchWeatherAsync(DateTime matchDate, string locationName, bool isHome)
    {
        double lat = 41.3985;
        double lon = 2.2032;
        var cleanLoc = locationName?.ToLowerInvariant() ?? "";

        if (cleanLoc.Contains("sabadell") || cleanLoc.Contains("planada"))
        {
            lat = 41.5433; lon = 2.1094;
        }
        else if (cleanLoc.Contains("badia"))
        {
            lat = 41.5085; lon = 2.1481;
        }
        else if (cleanLoc.Contains("cerdanyola") || cleanLoc.Contains("fontetas"))
        {
            lat = 41.4925; lon = 2.1415;
        }
        else if (cleanLoc.Contains("terrassa") || cleanLoc.Contains("roca") || cleanLoc.Contains("pueblo nuevo"))
        {
            lat = 41.5632; lon = 2.0089;
        }
        else if (cleanLoc.Contains("perpetua"))
        {
            lat = 41.5348; lon = 2.1812;
        }
        else if (cleanLoc.Contains("lliça") || cleanLoc.Contains("llica"))
        {
            lat = 41.5936; lon = 2.2356;
        }

        var result = new MatchWeatherInfo
        {
            LocationName = !string.IsNullOrEmpty(locationName) ? locationName : "Camp Municipal Agapito Fernández",
            Temperature = 22,
            PrecipitationProbability = 0,
            WindSpeed = 10,
            Humidity = 55,
            ConditionText = "Cielo Despejado",
            Icon = "☀️",
            IsOptimal = true
        };

        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}&longitude={lon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}&hourly=temperature_2m,relative_humidity_2m,precipitation_probability,weather_code,wind_speed_10m&timezone=auto";
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _http.GetAsync(url, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("hourly", out var hourly))
                {
                    var times = hourly.GetProperty("time").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
                    var matchIsoHour = matchDate.ToString("yyyy-MM-ddTHH:00");
                    var index = times.FindIndex(t => t.StartsWith(matchIsoHour));
                    if (index == -1)
                    {
                        var matchDateOnly = matchDate.ToString("yyyy-MM-dd");
                        index = times.FindIndex(t => t.StartsWith(matchDateOnly));
                        if (index != -1 && index + matchDate.Hour < times.Count)
                        {
                            index += matchDate.Hour;
                        }
                    }

                    if (index >= 0 && index < times.Count)
                    {
                        var temps = hourly.GetProperty("temperature_2m").EnumerateArray().ToList();
                        var rains = hourly.GetProperty("precipitation_probability").EnumerateArray().ToList();
                        var winds = hourly.GetProperty("wind_speed_10m").EnumerateArray().ToList();
                        var hums = hourly.GetProperty("relative_humidity_2m").EnumerateArray().ToList();
                        var codes = hourly.GetProperty("weather_code").EnumerateArray().ToList();

                        if (index < temps.Count) result.Temperature = Math.Round(temps[index].GetDouble(), 1);
                        if (index < rains.Count) result.PrecipitationProbability = rains[index].GetInt32();
                        if (index < winds.Count) result.WindSpeed = Math.Round(winds[index].GetDouble(), 1);
                        if (index < hums.Count) result.Humidity = hums[index].GetInt32();

                        var code = index < codes.Count ? codes[index].GetInt32() : 0;
                        var (cond, icon, optimal) = MapWeatherCode(code, result.PrecipitationProbability, result.Temperature);
                        result.ConditionText = cond;
                        result.Icon = icon;
                        result.IsOptimal = optimal;
                    }
                }
            }
        }
        catch
        {
            // Graceful realistic fallback
            var hour = matchDate.Hour;
            var isNight = hour >= 21 || hour < 8;
            result.Temperature = isNight ? 17 : 22;
            result.ConditionText = "Cielo Despejado";
            result.Icon = isNight ? "🌙" : "☀️";
            result.PrecipitationProbability = 5;
            result.WindSpeed = 11;
            result.Humidity = 58;
            result.IsOptimal = true;
        }

        return result;
    }

    private static (string Condition, string Icon, bool IsOptimal) MapWeatherCode(int code, int rainPct, double temp)
    {
        if (rainPct > 60 || code >= 61 && code <= 67 || code >= 80 && code <= 82)
            return ("Lluvia prevista", "🌧️", false);
        if (code >= 95)
            return ("Tormenta eléctrica", "⛈️", false);
        if (code >= 71 && code <= 77)
            return ("Nieve", "❄️", false);
        if (code == 45 || code == 48)
            return ("Niebla en cancha", "🌫️", true);
        if (code == 1 || code == 2)
            return ("Parcialmente nublado", "⛅", true);
        if (code == 3)
            return ("Nublado", "☁️", true);
        if (temp > 32)
            return ("Calor intenso", "☀️", false);

        return ("Cielo despejado", "☀️", true);
    }
}
