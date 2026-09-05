namespace AtleticPoblenou.Models;

public class ClubSettings
{
    public string ClubName { get; set; } = "Atletic Poblenou";
    public string ShortName { get; set; } = "ATºPOBLENOU";
    public string LeagueName { get; set; } = "Sábados División Honor (Temp. 26/27)";
    public string SeasonName { get; set; } = "TEMP 26/27";
    public string PrimaryColorHex { get; set; } = "#E53935";
    public string SecondaryColorHex { get; set; } = "#FFFFFF";
    public string KitDescription { get; set; } = "Rojiblanca a rayas verticales";
    /// <summary>Segunda equipación (alternativa), para cuando la titular coincide con la del rival. Vacía = no configurada.</summary>
    public string AwayKitPrimaryColorHex { get; set; } = "#141210";
    public string AwayKitSecondaryColorHex { get; set; } = "#FFFFFF";
    public string AwayKitDescription { get; set; } = "";
    public bool HasAwayKit => !string.IsNullOrWhiteSpace(AwayKitDescription);
    public string HomeVenueName { get; set; } = "Camp Municipal Agapito Fernández";
    public string HomeVenueMapsUrl { get; set; } = "https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona";
    public decimal SeasonFeePerPlayer { get; set; } = 200;
    public string TeamSecretCode { get; set; } = "";
    public bool ShowDemoShortcuts { get; set; } = false;
    public List<SeasonArchive> SeasonHistory { get; set; } = new();
}

public enum UserRole
{
    Admin = 0,
    Treasurer = 1,
    FieldManager = 2, // Delegado (gestión de partidos y canchas)
    Player = 3,       // Jugador
    Coach = 4         // Director Técnico (DT) / Entrenador
}

public enum Position
{
    Portero = 0,
    Defensa = 1,
    Centrocampista = 2,
    Delantero = 3,
    CuerpoTecnico = 4 // Cuerpo Técnico / DT
}

public enum DominantFoot
{
    Diestro,
    Zurdo,
    Ambidiestro
}

public class UserProfile
{
    /// <summary>Id del perfil. Para cuentas nuevas coincide con el UID de Supabase Auth. El admin principal es "user-1".</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>UID de la cuenta en Supabase Auth. Null si el perfil aún no tiene cuenta vinculada.</summary>
    public string? AuthUid { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }
    public Position Position { get; set; } = Position.Centrocampista;
    public DominantFoot Foot { get; set; } = DominantFoot.Diestro;
    public UserRole Role { get; set; } = UserRole.Player;
    public bool IsCaptain { get; set; } = false;
    public bool IsSubCaptain { get; set; } = false;
    public string Phone { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string Dni { get; set; } = string.Empty;
    public string MedicalNotes { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public int? Age => BirthDate.HasValue
        ? DateTime.Today.Year - BirthDate.Value.Year - (DateTime.Today.DayOfYear < BirthDate.Value.DayOfYear ? 1 : 0)
        : null;

    public UserProfile Clone() => (UserProfile)MemberwiseClone();
}

public class LoginModel
{
    public string EmailOrNickname { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterModel
{
    public string FullName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TeamCode { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }
    public Position Position { get; set; } = Position.Centrocampista;
    public DominantFoot Foot { get; set; } = DominantFoot.Diestro;
    public string Phone { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
}

public class RivalTeam
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string PrimaryColorHex { get; set; } = "#1E3A8A";
    public string SecondaryColorHex { get; set; } = "#FFFFFF";
    public string KitDescription { get; set; } = "Azul y blanco";
    public string Notes { get; set; } = string.Empty;
}

public class StandingRow
{
    public string TeamId { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string PrimaryColorHex { get; set; } = "#3B82F6";
    public string SecondaryColorHex { get; set; } = "#FFFFFF";
    public bool IsOurTeam { get; set; } = false;
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference => GoalsFor - GoalsAgainst;
    public int Points => (Won * 3) + (Drawn * 1);
}

public enum MatchStatus
{
    Upcoming,
    Finished,
    Cancelled
}

public class Match
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Round { get; set; } = 1;
    public DateTime MatchDate { get; set; } = DateTime.UtcNow;
    public string Competition { get; set; } = "Sábados División Honor (Temp. 26/27)";

    // Equipos
    public string HomeTeamId { get; set; } = "apn";
    public string HomeTeamName { get; set; } = "Atletic Poblenou";
    public string AwayTeamId { get; set; } = "";
    public string AwayTeamName { get; set; } = "";

    // Marcador
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Upcoming;

    // Cancha y notas
    public string LocationName { get; set; } = "Camp Agapito Fernández (Poblenou)";
    public string LocationUrl { get; set; } = "https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona";
    public string Notes { get; set; } = string.Empty;

    // Propiedades de conveniencia (100% retrocompatibles con el código de nuestro equipo)
    public bool IsOurMatch =>
        HomeTeamId == "apn" || AwayTeamId == "apn" ||
        HomeTeamName.Contains("Poblenou", StringComparison.OrdinalIgnoreCase) ||
        AwayTeamName.Contains("Poblenou", StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrEmpty(Opponent) && !Opponent.Contains(" vs ") && string.IsNullOrEmpty(HomeTeamName));

    public bool IsHome
    {
        get => HomeTeamId == "apn" || HomeTeamName.Contains("Poblenou", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                HomeTeamId = "apn";
                HomeTeamName = "Atletic Poblenou";
            }
            else
            {
                AwayTeamId = "apn";
                AwayTeamName = "Atletic Poblenou";
            }
        }
    }

    public string Opponent
    {
        get => IsHome ? AwayTeamName : HomeTeamName;
        set
        {
            if (IsHome) AwayTeamName = value ?? "";
            else HomeTeamName = value ?? "";
        }
    }

    public string? RivalTeamId
    {
        get => IsHome ? AwayTeamId : HomeTeamId;
        set
        {
            if (IsHome) AwayTeamId = value ?? "";
            else HomeTeamId = value ?? "";
        }
    }

    public int? OurScore
    {
        get => IsHome ? HomeScore : AwayScore;
        set { if (IsHome) HomeScore = value; else AwayScore = value; }
    }

    public int? RivalScore
    {
        get => IsHome ? AwayScore : HomeScore;
        set { if (IsHome) AwayScore = value; else HomeScore = value; }
    }
}

public enum AttendanceStatus
{
    Going,
    NotGoing,
    Maybe
}

public class Attendance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MatchId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Going;
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PaymentStatus
{
    Pending,
    Paid
}

public enum PaymentMethod
{
    Bizum,
    Cash,
    Transfer
}

public class Payment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlayerId { get; set; } = string.Empty;
    public string Concept { get; set; } = "Quota Mensual";
    public decimal Amount { get; set; } = 20.00m;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentMethod? Method { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class TeamExpense
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Concept { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = "Otros"; // Árbitros, Campos, Material, Tercer Tiempo, Inscripción, Otros
    public string? PaidBy { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public enum EventType
{
    Goal,
    Assist,
    YellowCard,
    RedCard,
    Mvp
}

public class MatchEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MatchId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    public int? Minute { get; set; }
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Alineación guardada de la pizarra táctica de un partido. Vive en Supabase, no solo en memoria.</summary>
public class MatchLineup
{
    public string MatchId { get; set; } = string.Empty;
    public string Formation { get; set; } = "4-3-3";
    /// <summary>11 huecos en el mismo orden que la pizarra. Null en un hueco = sin asignar.</summary>
    public List<string?> StartingPlayerIds { get; set; } = new();
}

public class TeamAnnouncement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AuthorName { get; set; } = "Capitán";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool HasPoll { get; set; } = true;
    public List<string> PollOptions { get; set; } = new() { "Me sumo 🥩", "En duda 🤔", "No puedo ❌" };
    public Dictionary<string, int> Votes { get; set; } = new(); // PlayerId -> OptionIndex
    public bool IsPinned { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class SeasonArchive
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SeasonName { get; set; } = string.Empty;
    public string LeagueName { get; set; } = string.Empty;
    public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
    public int Position { get; set; } = 1;
    public int Played { get; set; }
    public int Points { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public string PichichiPlayerName { get; set; } = string.Empty;
    public int PichichiGoals { get; set; }
    public decimal FinalBalance { get; set; }
}

public class MatchWeatherInfo
{
    /// <summary>False cuando no se pudo obtener la previsión. En ese caso el resto de campos no son datos reales.</summary>
    public bool IsAvailable { get; set; } = false;
    public double Temperature { get; set; }
    public int PrecipitationProbability { get; set; }
    public double WindSpeed { get; set; }
    public int Humidity { get; set; }
    public string ConditionText { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public bool IsOptimal { get; set; }
}
