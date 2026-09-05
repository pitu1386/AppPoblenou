using AtleticPoblenou.Models;

namespace AtleticPoblenou.Services;

// ==========================================
// DTOs: nombres de propiedad = columnas de Supabase
// ==========================================
public class SupabaseProfileDto
{
    public string id { get; set; } = "";
    public string? auth_uid { get; set; }
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
    public int round { get; set; } = 1;
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
    public string? category { get; set; }
    public string? paid_by { get; set; }
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

public class SupabaseMatchLineupDto
{
    public string match_id { get; set; } = "";
    public string formation { get; set; } = "4-3-3";
    public List<string?>? starting_player_ids { get; set; }
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
    public string? away_kit_primary_color_hex { get; set; }
    public string? away_kit_secondary_color_hex { get; set; }
    public string? away_kit_description { get; set; }
    public string? home_venue_name { get; set; }
    public string? home_venue_maps_url { get; set; }
    public decimal season_fee_per_player { get; set; }
    public string? team_secret_code { get; set; }
    public List<SeasonArchive>? season_history { get; set; }
}

// ==========================================
// MAPEADORES modelo <-> DTO
// ==========================================
public static class SupabaseMappers
{
    public static SupabaseProfileDto ToDto(UserProfile p) => new()
    {
        id = p.Id,
        auth_uid = p.AuthUid,
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
        birth_date = p.BirthDate?.ToString("yyyy-MM-dd"),
        dni = p.Dni,
        medical_notes = p.MedicalNotes,
        avatar_url = p.AvatarUrl,
        is_active = p.IsActive,
        created_at = p.CreatedAt
    };

    public static UserProfile FromDto(SupabaseProfileDto d) => new()
    {
        Id = d.id,
        AuthUid = d.auth_uid,
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
        BirthDate = DateTime.TryParse(d.birth_date, out var b) ? b : null,
        Dni = d.dni ?? "",
        MedicalNotes = d.medical_notes ?? "",
        AvatarUrl = d.avatar_url ?? "",
        IsActive = d.is_active,
        CreatedAt = d.created_at ?? DateTime.UtcNow
    };

    public static SupabaseMatchDto ToDto(Match m)
    {
        var isOur = m.IsOurMatch;
        return new SupabaseMatchDto
        {
            id = m.Id,
            round = m.Round,
            match_date = m.MatchDate,
            opponent = isOur ? m.Opponent : $"{m.HomeTeamName} vs {m.AwayTeamName}",
            rival_team_id = isOur ? m.RivalTeamId : m.HomeTeamId,
            competition = m.Competition,
            location_name = m.LocationName,
            location_url = m.LocationUrl,
            is_home = isOur ? m.IsHome : false,
            our_score = isOur ? m.OurScore : m.HomeScore,
            rival_score = isOur ? m.RivalScore : m.AwayScore,
            status = (int)m.Status,
            notes = isOur ? m.Notes : $"LM|{m.HomeTeamId}|{m.HomeTeamName}|{m.AwayTeamId}|{m.AwayTeamName}"
        };
    }

    public static Match FromDto(SupabaseMatchDto d)
    {
        var m = new Match
        {
            Id = d.id,
            Round = d.round > 0 ? d.round : 1,
            MatchDate = d.match_date,
            Competition = d.competition ?? "Sábados División Honor (Temp. 26/27)",
            LocationName = d.location_name,
            LocationUrl = d.location_url ?? "",
            Status = (MatchStatus)d.status
        };

        if (!string.IsNullOrEmpty(d.notes) && d.notes.StartsWith("LM|"))
        {
            var parts = d.notes.Split('|');
            m.HomeTeamId = parts.Length > 1 ? parts[1] : "";
            m.HomeTeamName = parts.Length > 2 ? parts[2] : "";
            m.AwayTeamId = parts.Length > 3 ? parts[3] : "";
            m.AwayTeamName = parts.Length > 4 ? parts[4] : "";
            m.HomeScore = d.our_score;
            m.AwayScore = d.rival_score;
        }
        else if (d.opponent.Contains(" vs "))
        {
            var parts = d.opponent.Split(" vs ");
            m.HomeTeamName = parts[0].Trim();
            m.AwayTeamName = parts.Length > 1 ? parts[1].Trim() : "";
            m.HomeTeamId = d.rival_team_id ?? "";
            m.HomeScore = d.our_score;
            m.AwayScore = d.rival_score;
        }
        else
        {
            m.IsHome = d.is_home;
            m.Opponent = d.opponent;
            m.RivalTeamId = d.rival_team_id;
            m.OurScore = d.our_score;
            m.RivalScore = d.rival_score;
            m.Notes = d.notes ?? "";
        }

        return m;
    }

    public static SupabaseRivalTeamDto ToDto(RivalTeam t) => new()
    {
        id = t.Id,
        name = t.Name,
        primary_color_hex = t.PrimaryColorHex,
        secondary_color_hex = t.SecondaryColorHex,
        kit_description = t.KitDescription,
        notes = t.Notes
    };

    public static RivalTeam FromDto(SupabaseRivalTeamDto d) => new()
    {
        Id = d.id,
        Name = d.name,
        PrimaryColorHex = d.primary_color_hex ?? "#1E3A8A",
        SecondaryColorHex = d.secondary_color_hex ?? "#FFFFFF",
        KitDescription = d.kit_description ?? "",
        Notes = d.notes ?? ""
    };

    public static SupabaseAttendanceDto ToDto(Attendance a) => new()
    {
        id = a.Id,
        match_id = a.MatchId,
        player_id = a.PlayerId,
        status = (int)a.Status,
        note = a.Note,
        updated_at = a.UpdatedAt
    };

    public static Attendance FromDto(SupabaseAttendanceDto d) => new()
    {
        Id = d.id,
        MatchId = d.match_id,
        PlayerId = d.player_id,
        Status = (AttendanceStatus)d.status,
        Note = d.note,
        UpdatedAt = d.updated_at ?? DateTime.UtcNow
    };

    public static SupabasePaymentDto ToDto(Payment p) => new()
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

    public static Payment FromDto(SupabasePaymentDto d) => new()
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

    public static SupabaseExpenseDto ToDto(TeamExpense e) => new()
    {
        id = e.Id,
        concept = e.Concept,
        amount = e.Amount,
        expense_date = e.ExpenseDate.ToString("yyyy-MM-dd"),
        category = e.Category,
        paid_by = e.PaidBy,
        notes = e.Notes
    };

    public static TeamExpense FromDto(SupabaseExpenseDto d) => new()
    {
        Id = d.id,
        Concept = d.concept,
        Amount = d.amount,
        ExpenseDate = DateTime.TryParse(d.expense_date, out var dt) ? dt : DateTime.UtcNow,
        Category = string.IsNullOrWhiteSpace(d.category) || d.category == "0" ? "Otros" : d.category,
        PaidBy = d.paid_by,
        Notes = d.notes ?? ""
    };

    public static SupabaseEventDto ToDto(MatchEvent ev) => new()
    {
        id = ev.Id,
        match_id = ev.MatchId,
        player_id = ev.PlayerId,
        event_type = (int)ev.EventType,
        minute = ev.Minute,
        notes = ev.Notes
    };

    public static MatchEvent FromDto(SupabaseEventDto d) => new()
    {
        Id = d.id,
        MatchId = d.match_id,
        PlayerId = d.player_id,
        EventType = (EventType)d.event_type,
        Minute = d.minute,
        Notes = d.notes ?? ""
    };

    public static SupabaseMatchLineupDto ToDto(MatchLineup l) => new()
    {
        match_id = l.MatchId,
        formation = l.Formation,
        starting_player_ids = l.StartingPlayerIds
    };

    public static MatchLineup FromDto(SupabaseMatchLineupDto d) => new()
    {
        MatchId = d.match_id,
        Formation = string.IsNullOrWhiteSpace(d.formation) ? "4-3-3" : d.formation,
        StartingPlayerIds = d.starting_player_ids ?? new()
    };

    public static SupabaseAnnouncementDto ToDto(TeamAnnouncement a) => new()
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

    public static TeamAnnouncement FromDto(SupabaseAnnouncementDto d) => new()
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

    public static SupabaseClubSettingsDto ToDto(ClubSettings s) => new()
    {
        id = "current",
        club_name = s.ClubName,
        short_name = s.ShortName,
        league_name = s.LeagueName,
        season_name = s.SeasonName,
        primary_color_hex = s.PrimaryColorHex,
        secondary_color_hex = s.SecondaryColorHex,
        kit_description = s.KitDescription,
        away_kit_primary_color_hex = s.AwayKitPrimaryColorHex,
        away_kit_secondary_color_hex = s.AwayKitSecondaryColorHex,
        away_kit_description = s.AwayKitDescription,
        home_venue_name = s.HomeVenueName,
        home_venue_maps_url = s.HomeVenueMapsUrl,
        season_fee_per_player = s.SeasonFeePerPlayer,
        team_secret_code = s.TeamSecretCode,
        season_history = s.SeasonHistory
    };

    public static ClubSettings FromDto(SupabaseClubSettingsDto d) => new()
    {
        ClubName = d.club_name,
        ShortName = d.short_name ?? "ATºPOBLENOU",
        LeagueName = d.league_name ?? "Sábados División Honor (Temp. 26/27)",
        SeasonName = d.season_name ?? "TEMP 26/27",
        PrimaryColorHex = d.primary_color_hex ?? "#E53935",
        SecondaryColorHex = d.secondary_color_hex ?? "#FFFFFF",
        KitDescription = d.kit_description ?? "Rojiblanca a rayas verticales",
        AwayKitPrimaryColorHex = d.away_kit_primary_color_hex ?? "#141210",
        AwayKitSecondaryColorHex = d.away_kit_secondary_color_hex ?? "#FFFFFF",
        AwayKitDescription = d.away_kit_description ?? "",
        HomeVenueName = d.home_venue_name ?? "Camp Municipal Agapito Fernández",
        HomeVenueMapsUrl = d.home_venue_maps_url ?? "https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona",
        SeasonFeePerPlayer = d.season_fee_per_player > 0 ? d.season_fee_per_player : 200,
        TeamSecretCode = d.team_secret_code ?? "",
        ShowDemoShortcuts = false,
        SeasonHistory = d.season_history ?? new()
    };
}
