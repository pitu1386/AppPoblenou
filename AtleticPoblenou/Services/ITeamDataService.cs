using AtleticPoblenou.Models;

namespace AtleticPoblenou.Services;

public interface ITeamDataService
{
    /// <summary>Se dispara cuando cambian los datos en memoria (local o desde la nube).</summary>
    event Action? OnChange;
    /// <summary>Se dispara cuando una operación contra la nube falla. El texto es apto para mostrar al usuario.</summary>
    event Action<string>? OnError;

    Task InitializeAsync();

    // Estado de sesión
    /// <summary>Hay sesión, ficha y la ficha está activa: puede usar la app.</summary>
    bool IsAuthenticated { get; }
    /// <summary>Hay sesión de Auth pero aún no existe ficha de jugador (alta a medias).</summary>
    bool NeedsProfile { get; }
    /// <summary>Hay sesión y ficha, pero la ficha está dada de baja: necesita el código del equipo.</summary>
    bool IsDeactivated { get; }
    string? SessionEmail { get; }

    // Estado de sincronización
    bool IsCloudConnected { get; }
    bool IsRealtimeConnected { get; }
    DateTime? LastSyncUtc { get; }
    Task RefreshFromCloudAsync();

    UserProfile GetCurrentUser();
    bool IsOwnerAdmin(UserProfile? p);
    Task<(bool Success, string ErrorMessage)> LoginAsync(string emailOrNickname, string password);
    Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterModel model);
    /// <summary>Completa la ficha cuando ya existe la cuenta de Auth pero no el perfil.</summary>
    Task<(bool Success, string ErrorMessage)> CompleteRegistrationAsync(RegisterModel model);
    Task<(bool Success, string ErrorMessage)> ReactivateWithCodeAsync(string securityCode);
    Task<(bool Success, string ErrorMessage)> ChangeMyPasswordAsync(string newPassword);
    Task<(bool Success, string ErrorMessage)> AdminSetPasswordAsync(string profileId, string newPassword);
    Task LogoutAsync();
    string GetTeamSecretCode();
    Task<string> GenerateNewTeamCodeAsync();

    // Club Identity & Settings
    ClubSettings GetClubSettings();
    Task SaveClubSettingsAsync(ClubSettings settings);

    // Profiles & Player Sheets
    List<UserProfile> GetProfiles();
    UserProfile? GetProfileById(string profileId);
    Task SaveProfileAsync(UserProfile profile);
    Task DeleteProfileAsync(string profileId);

    // Rival Teams & Standings
    List<RivalTeam> GetRivalTeams();
    RivalTeam? GetRivalTeamById(string teamId);
    Task AddRivalTeamAsync(RivalTeam team);
    Task UpdateRivalTeamAsync(RivalTeam team);
    Task DeleteRivalTeamAsync(string teamId);
    List<StandingRow> GetStandings();

    // Announcements & Polls
    List<TeamAnnouncement> GetAnnouncements();
    List<TeamAnnouncement> GetAllAnnouncements();
    Task AddAnnouncementAsync(TeamAnnouncement announcement);
    Task VoteAnnouncementPollAsync(string announcementId, string playerId, int optionIndex);
    Task ArchiveAnnouncementAsync(string announcementId);
    Task RestoreAnnouncementAsync(string announcementId);
    Task DeleteAnnouncementAsync(string announcementId);

    // Tactical lineups
    MatchLineup? GetLineupForMatch(string matchId);
    Task SaveLineupAsync(MatchLineup lineup);

    // Matches & RSVP
    List<Match> GetMatches();
    Match? GetNextMatch();
    Task AddMatchAsync(Match match);
    Task UpdateMatchDetailsAsync(Match match);
    Task UpdateMatchResultAsync(string matchId, int ourScore, int rivalScore, List<MatchEvent> events);
    Task DeleteMatchAsync(string matchId);
    Task ClearAllMatchesAsync();
    Task SaveBatchRoundResultsAsync(int round, List<Match> matches);
    Task AddLeagueMatchAsync(Match match);
    List<Attendance> GetAttendanceForMatch(string matchId);
    Attendance? GetUserAttendance(string matchId, string playerId);
    Task SetAttendanceAsync(string matchId, string playerId, AttendanceStatus status, string? note = null);

    // Payments & Treasury
    List<Payment> GetPayments();
    List<Payment> GetPaymentsForUser(string playerId);
    Task AddPaymentAsync(string playerId, string concept, decimal amount, PaymentMethod method, DateTime? paidAt = null, string notes = "");
    Task AddBatchFeeAsync(string concept, decimal amount, DateTime dueDate);
    Task MarkPaymentAsPaidAsync(string paymentId, PaymentMethod method, string notes = "");
    /// <summary>Elimina un cobro o cuota registrado por error. Solo admin o tesorero (lo aplica el servidor).</summary>
    Task DeletePaymentAsync(string paymentId);
    decimal GetTeamBalance();
    decimal GetTotalCollectedThisMonth();
    decimal GetTotalPendingAmount();

    // Expenses
    List<TeamExpense> GetExpenses();
    Task AddExpenseAsync(TeamExpense expense);
    Task DeleteExpenseAsync(string expenseId);

    // Stats & Events
    List<MatchEvent> GetMatchEvents();
    Dictionary<string, int> GetTopScorers();
    Dictionary<string, int> GetTopAssisters();
    Dictionary<string, (int Yellows, int Reds)> GetCardsSummary();
    Dictionary<string, int> GetMvpSummary();

    // Player Deactivation
    Task DeactivatePlayerAsync(string playerId);
    Task ReactivatePlayerAsync(string playerId);

    // Season Lifecycle
    Task CloseSeasonAndStartNewAsync(string newSeasonName, decimal newSeasonFee);
}
