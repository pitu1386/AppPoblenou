using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AtleticPoblenou.Models;

namespace AtleticPoblenou.Services;

public class SupabaseClientService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://dlajpiuuslegmoedslux.supabase.co/rest/v1";
    private const string ApiKey = "sb_publishable_2jgFAT8ePAK6BJOyPDUImA_-BC8NXjq";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SupabaseClientService(HttpClient http)
    {
        _http = http;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string pathAndQuery)
    {
        var req = new HttpRequestMessage(method, $"{BaseUrl}/{pathAndQuery}");
        req.Headers.Add("apikey", ApiKey);
        req.Headers.Add("Authorization", $"Bearer {ApiKey}");
        return req;
    }

    // ==========================================
    // 1. PROFILES
    // ==========================================
    public async Task<List<UserProfile>?> FetchProfilesAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "profiles?select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabaseProfileDto>>(json, _jsonOptions);
            return dtos?.Select(FromDto).ToList();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertProfileAsync(UserProfile profile)
    {
        try
        {
            var dto = ToDto(profile);
            using var req = CreateRequest(HttpMethod.Post, "profiles");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpsertProfilesBatchAsync(IEnumerable<UserProfile> profiles)
    {
        try
        {
            var dtos = profiles.Select(ToDto).ToList();
            using var req = CreateRequest(HttpMethod.Post, "profiles");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dtos, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 2. RIVAL TEAMS
    // ==========================================
    public async Task<List<RivalTeam>?> FetchRivalTeamsAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "rival_teams?select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabaseRivalTeamDto>>(json, _jsonOptions);
            return dtos?.Select(FromDto).ToList();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertRivalTeamsBatchAsync(IEnumerable<RivalTeam> teams)
    {
        try
        {
            var dtos = teams.Select(ToDto).ToList();
            using var req = CreateRequest(HttpMethod.Post, "rival_teams");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dtos, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpsertRivalTeamAsync(RivalTeam team)
    {
        try
        {
            var dto = ToDto(team);
            using var req = CreateRequest(HttpMethod.Post, "rival_teams");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 3. MATCHES
    // ==========================================
    public async Task<List<Match>?> FetchMatchesAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "matches?select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabaseMatchDto>>(json, _jsonOptions);
            return dtos?.Select(FromDto).ToList();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertMatchAsync(Match match)
    {
        try
        {
            var dto = ToDto(match);
            using var req = CreateRequest(HttpMethod.Post, "matches");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpsertMatchesBatchAsync(IEnumerable<Match> matches)
    {
        try
        {
            var dtos = matches.Select(ToDto).ToList();
            using var req = CreateRequest(HttpMethod.Post, "matches");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dtos, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 4. ATTENDANCE
    // ==========================================
    public async Task<List<Attendance>?> FetchAttendanceAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "attendance?select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabaseAttendanceDto>>(json, _jsonOptions);
            return dtos?.Select(FromDto).ToList();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertAttendanceAsync(Attendance att)
    {
        try
        {
            var dto = ToDto(att);
            using var req = CreateRequest(HttpMethod.Post, "attendance");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpsertAttendanceBatchAsync(IEnumerable<Attendance> attendanceList)
    {
        try
        {
            var dtos = attendanceList.Select(ToDto).ToList();
            using var req = CreateRequest(HttpMethod.Post, "attendance");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dtos, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 5. PAYMENTS
    // ==========================================
    public async Task<List<Payment>?> FetchPaymentsAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "payments?select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabasePaymentDto>>(json, _jsonOptions);
            return dtos?.Select(FromDto).ToList();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertPaymentAsync(Payment payment)
    {
        try
        {
            var dto = ToDto(payment);
            using var req = CreateRequest(HttpMethod.Post, "payments");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpsertPaymentsBatchAsync(IEnumerable<Payment> payments)
    {
        try
        {
            var dtos = payments.Select(ToDto).ToList();
            using var req = CreateRequest(HttpMethod.Post, "payments");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dtos, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 6. TEAM EXPENSES
    // ==========================================
    public async Task<List<TeamExpense>?> FetchExpensesAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "team_expenses?select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabaseExpenseDto>>(json, _jsonOptions);
            return dtos?.Select(FromDto).ToList();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertExpenseAsync(TeamExpense expense)
    {
        try
        {
            var dto = ToDto(expense);
            using var req = CreateRequest(HttpMethod.Post, "team_expenses");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 7. MATCH EVENTS
    // ==========================================
    public async Task<List<MatchEvent>?> FetchMatchEventsAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "match_events?select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabaseEventDto>>(json, _jsonOptions);
            return dtos?.Select(FromDto).ToList();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertMatchEventsBatchAsync(IEnumerable<MatchEvent> events)
    {
        try
        {
            var dtos = events.Select(ToDto).ToList();
            using var req = CreateRequest(HttpMethod.Post, "match_events");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dtos, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 8. ANNOUNCEMENTS
    // ==========================================
    public async Task<List<TeamAnnouncement>?> FetchAnnouncementsAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "announcements?select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabaseAnnouncementDto>>(json, _jsonOptions);
            return dtos?.Select(FromDto).ToList();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertAnnouncementAsync(TeamAnnouncement ann)
    {
        try
        {
            var dto = ToDto(ann);
            using var req = CreateRequest(HttpMethod.Post, "announcements");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpsertAnnouncementsBatchAsync(IEnumerable<TeamAnnouncement> announcements)
    {
        try
        {
            var dtos = announcements.Select(ToDto).ToList();
            using var req = CreateRequest(HttpMethod.Post, "announcements");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dtos, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // 9. CLUB SETTINGS
    // ==========================================
    public async Task<ClubSettings?> FetchClubSettingsAsync()
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "club_settings?id=eq.current&select=*");
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<SupabaseClubSettingsDto>>(json, _jsonOptions);
            var first = dtos?.FirstOrDefault();
            return first != null ? FromDto(first) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpsertClubSettingsAsync(ClubSettings settings)
    {
        try
        {
            var dto = ToDto(settings);
            using var req = CreateRequest(HttpMethod.Post, "club_settings");
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            req.Content = new StringContent(JsonSerializer.Serialize(dto, _jsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // GENERIC DELETE
    // ==========================================
    public async Task<bool> DeleteRowAsync(string table, string id)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Delete, $"{table}?id=eq.{id}");
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==========================================
    // DTO MAPPERS
    // ==========================================
    private static SupabaseProfileDto ToDto(UserProfile p) => new()
    {
        id = p.Id,
        full_name = p.FullName,
        nickname = p.Nickname,
        jersey_number = p.JerseyNumber,
        position = (int)p.Position,
        foot = (int)p.Foot,
        role = (int)p.Role,
        is_captain = p.IsCaptain,
        is_sub_captain = p.IsSubCaptain,
        phone = p.Phone,
        email = p.Email,
        password = p.Password,
        birth_date = p.BirthDate?.ToString("yyyy-MM-dd"),
        dni = p.Dni,
        medical_notes = p.MedicalNotes,
        avatar_url = p.AvatarUrl,
        is_active = p.IsActive,
        created_at = p.CreatedAt
    };

    private static UserProfile FromDto(SupabaseProfileDto d) => new()
    {
        Id = d.id,
        FullName = d.full_name,
        Nickname = d.nickname ?? "",
        JerseyNumber = d.jersey_number,
        Position = (Position)d.position,
        Foot = (DominantFoot)d.foot,
        Role = (UserRole)d.role,
        IsCaptain = d.is_captain,
        IsSubCaptain = d.is_sub_captain,
        Phone = d.phone ?? "",
        Email = d.email ?? "",
        Password = d.password ?? "1234",
        BirthDate = DateTime.TryParse(d.birth_date, out var b) ? b : null,
        Dni = d.dni ?? "",
        MedicalNotes = d.medical_notes ?? "",
        AvatarUrl = d.avatar_url ?? "",
        IsActive = d.is_active,
        CreatedAt = d.created_at ?? DateTime.UtcNow
    };

    private static SupabaseMatchDto ToDto(Match m) => new()
    {
        id = m.Id,
        match_date = m.MatchDate,
        opponent = m.Opponent,
        rival_team_id = m.RivalTeamId,
        competition = m.Competition,
        location_name = m.LocationName,
        location_url = m.LocationUrl,
        is_home = m.IsHome,
        our_score = m.OurScore,
        rival_score = m.RivalScore,
        status = (int)m.Status,
        notes = m.Notes
    };

    private static Match FromDto(SupabaseMatchDto d) => new()
    {
        Id = d.id,
        MatchDate = d.match_date,
        Opponent = d.opponent,
        RivalTeamId = d.rival_team_id,
        Competition = d.competition ?? "Liga Veteranos Barcelona",
        LocationName = d.location_name,
        LocationUrl = d.location_url ?? "",
        IsHome = d.is_home,
        OurScore = d.our_score,
        RivalScore = d.rival_score,
        Status = (MatchStatus)d.status,
        Notes = d.notes ?? ""
    };

    private static SupabaseRivalTeamDto ToDto(RivalTeam t) => new()
    {
        id = t.Id,
        name = t.Name,
        primary_color_hex = t.PrimaryColorHex,
        secondary_color_hex = t.SecondaryColorHex,
        kit_description = t.KitDescription,
        notes = t.Notes
    };

    private static RivalTeam FromDto(SupabaseRivalTeamDto d) => new()
    {
        Id = d.id,
        Name = d.name,
        PrimaryColorHex = d.primary_color_hex ?? "#1E3A8A",
        SecondaryColorHex = d.secondary_color_hex ?? "#FFFFFF",
        KitDescription = d.kit_description ?? "",
        Notes = d.notes ?? ""
    };

    private static SupabaseAttendanceDto ToDto(Attendance a) => new()
    {
        id = a.Id,
        match_id = a.MatchId,
        player_id = a.PlayerId,
        status = (int)a.Status,
        note = a.Note,
        updated_at = a.UpdatedAt
    };

    private static Attendance FromDto(SupabaseAttendanceDto d) => new()
    {
        Id = d.id,
        MatchId = d.match_id,
        PlayerId = d.player_id,
        Status = (AttendanceStatus)d.status,
        Note = d.note,
        UpdatedAt = d.updated_at ?? DateTime.UtcNow
    };

    private static SupabasePaymentDto ToDto(Payment p) => new()
    {
        id = p.Id,
        player_id = p.PlayerId,
        concept = p.Concept,
        amount = p.Amount,
        status = (int)p.Status,
        due_date = p.DueDate?.ToString("yyyy-MM-dd"),
        paid_at = p.PaidAt,
        method = p.Method.HasValue ? (int)p.Method.Value : null,
        notes = p.Notes
    };

    private static Payment FromDto(SupabasePaymentDto d) => new()
    {
        Id = d.id,
        PlayerId = d.player_id,
        Concept = d.concept,
        Amount = d.amount,
        Status = (PaymentStatus)d.status,
        DueDate = DateTime.TryParse(d.due_date, out var dt) ? dt : null,
        PaidAt = d.paid_at,
        Method = d.method.HasValue ? (PaymentMethod)d.method.Value : null,
        Notes = d.notes ?? ""
    };

    private static SupabaseExpenseDto ToDto(TeamExpense e) => new()
    {
        id = e.Id,
        concept = e.Concept,
        amount = e.Amount,
        expense_date = e.ExpenseDate.ToString("yyyy-MM-dd"),
        category = 0,
        paid_by_player_id = e.PaidBy,
        notes = e.Notes
    };

    private static TeamExpense FromDto(SupabaseExpenseDto d) => new()
    {
        Id = d.id,
        Concept = d.concept,
        Amount = d.amount,
        ExpenseDate = DateTime.TryParse(d.expense_date, out var dt) ? dt : DateTime.UtcNow,
        Category = "Àrbitres",
        PaidBy = d.paid_by_player_id,
        Notes = d.notes ?? ""
    };

    private static SupabaseEventDto ToDto(MatchEvent ev) => new()
    {
        id = ev.Id,
        match_id = ev.MatchId,
        player_id = ev.PlayerId,
        event_type = (int)ev.EventType,
        minute = ev.Minute,
        notes = ev.Notes
    };

    private static MatchEvent FromDto(SupabaseEventDto d) => new()
    {
        Id = d.id,
        MatchId = d.match_id,
        PlayerId = d.player_id,
        EventType = (EventType)d.event_type,
        Minute = d.minute,
        Notes = d.notes ?? ""
    };

    private static SupabaseAnnouncementDto ToDto(TeamAnnouncement a) => new()
    {
        id = a.Id,
        title = a.Title,
        content = a.Content,
        author_name = a.AuthorName,
        created_at = a.CreatedAt,
        has_poll = a.HasPoll,
        poll_options = a.PollOptions,
        votes = a.Votes,
        is_pinned = a.IsPinned,
        is_active = a.IsActive
    };

    private static TeamAnnouncement FromDto(SupabaseAnnouncementDto d) => new()
    {
        Id = d.id,
        Title = d.title,
        Content = d.content,
        AuthorName = d.author_name ?? "Capitán",
        CreatedAt = d.created_at,
        HasPoll = d.has_poll,
        PollOptions = d.poll_options ?? new(),
        Votes = d.votes ?? new(),
        IsPinned = d.is_pinned,
        IsActive = d.is_active
    };

    private static SupabaseClubSettingsDto ToDto(ClubSettings s) => new()
    {
        id = "current",
        club_name = s.ClubName,
        short_name = s.ShortName,
        league_name = s.LeagueName,
        season_name = s.SeasonName,
        primary_color_hex = s.PrimaryColorHex,
        secondary_color_hex = s.SecondaryColorHex,
        kit_description = s.KitDescription,
        home_venue_name = s.HomeVenueName,
        home_venue_maps_url = s.HomeVenueMapsUrl,
        season_fee_per_player = s.SeasonFeePerPlayer,
        team_secret_code = "APN1929",
        season_history = s.SeasonHistory
    };

    private static ClubSettings FromDto(SupabaseClubSettingsDto d) => new()
    {
        ClubName = d.club_name,
        ShortName = d.short_name ?? "ATºPOBLENOU",
        LeagueName = d.league_name ?? "Sábados División Honor (Temp. 26/27)",
        SeasonName = d.season_name ?? "TEMP 26/27",
        PrimaryColorHex = d.primary_color_hex ?? "#E53935",
        SecondaryColorHex = d.secondary_color_hex ?? "#FFFFFF",
        KitDescription = d.kit_description ?? "Rojiblanca a rayas verticales",
        HomeVenueName = d.home_venue_name ?? "Camp Municipal Agapito Fernández",
        HomeVenueMapsUrl = d.home_venue_maps_url ?? "https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona",
        SeasonFeePerPlayer = d.season_fee_per_player > 0 ? d.season_fee_per_player : 200,
        ShowDemoShortcuts = false,
        SeasonHistory = d.season_history ?? new()
    };
}

// ==========================================
// DTO DEFINITIONS MATCHING SUPABASE EXACTLY
// ==========================================
public class SupabaseProfileDto
{
    public string id { get; set; } = "";
    public string full_name { get; set; } = "";
    public string? nickname { get; set; }
    public int? jersey_number { get; set; }
    public int position { get; set; }
    public int foot { get; set; }
    public int role { get; set; }
    public bool is_captain { get; set; }
    public bool is_sub_captain { get; set; }
    public string? phone { get; set; }
    public string? email { get; set; }
    public string? password { get; set; }
    public string? birth_date { get; set; }
    public string? dni { get; set; }
    public string? medical_notes { get; set; }
    public string? avatar_url { get; set; }
    public bool is_active { get; set; } = true;
    public DateTime? created_at { get; set; }
}

public class SupabaseMatchDto
{
    public string id { get; set; } = "";
    public DateTime match_date { get; set; }
    public string opponent { get; set; } = "";
    public string? rival_team_id { get; set; }
    public string? competition { get; set; }
    public string location_name { get; set; } = "";
    public string? location_url { get; set; }
    public bool is_home { get; set; } = true;
    public int? our_score { get; set; }
    public int? rival_score { get; set; }
    public int status { get; set; }
    public string? notes { get; set; }
}

public class SupabaseRivalTeamDto
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string? primary_color_hex { get; set; }
    public string? secondary_color_hex { get; set; }
    public string? kit_description { get; set; }
    public string? notes { get; set; }
}

public class SupabaseAttendanceDto
{
    public string id { get; set; } = "";
    public string match_id { get; set; } = "";
    public string player_id { get; set; } = "";
    public int status { get; set; }
    public string? note { get; set; }
    public DateTime? updated_at { get; set; }
}

public class SupabasePaymentDto
{
    public string id { get; set; } = "";
    public string player_id { get; set; } = "";
    public string concept { get; set; } = "";
    public decimal amount { get; set; }
    public int status { get; set; }
    public string? due_date { get; set; }
    public DateTime? paid_at { get; set; }
    public int? method { get; set; }
    public string? notes { get; set; }
}

public class SupabaseExpenseDto
{
    public string id { get; set; } = "";
    public string concept { get; set; } = "";
    public decimal amount { get; set; }
    public string? expense_date { get; set; }
    public int category { get; set; }
    public string? paid_by_player_id { get; set; }
    public string? notes { get; set; }
}

public class SupabaseEventDto
{
    public string id { get; set; } = "";
    public string match_id { get; set; } = "";
    public string player_id { get; set; } = "";
    public int event_type { get; set; }
    public int? minute { get; set; }
    public string? notes { get; set; }
}

public class SupabaseAnnouncementDto
{
    public string id { get; set; } = "";
    public string title { get; set; } = "";
    public string content { get; set; } = "";
    public string? author_name { get; set; }
    public DateTime created_at { get; set; }
    public bool has_poll { get; set; }
    public List<string>? poll_options { get; set; }
    public Dictionary<string, int>? votes { get; set; }
    public bool is_pinned { get; set; }
    public bool is_active { get; set; } = true;
}

public class SupabaseClubSettingsDto
{
    public string id { get; set; } = "current";
    public string club_name { get; set; } = "Atletic Poblenou";
    public string? short_name { get; set; }
    public string? league_name { get; set; }
    public string? season_name { get; set; }
    public string? primary_color_hex { get; set; }
    public string? secondary_color_hex { get; set; }
    public string? kit_description { get; set; }
    public string? home_venue_name { get; set; }
    public string? home_venue_maps_url { get; set; }
    public decimal season_fee_per_player { get; set; }
    public string? team_secret_code { get; set; }
    public List<SeasonArchive>? season_history { get; set; }
}
