using AtleticPoblenou.Models;

namespace AtleticPoblenou.Services;

public interface ITeamDataService
{
    event Action? OnChange;

    Task InitializeAsync();
    bool IsAuthenticated { get; }
    UserProfile GetCurrentUser();
    Task SetCurrentUserIdAsync(string userId);
    Task<(bool Success, string ErrorMessage)> LoginAsync(string emailOrNickname, string password);
    Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterModel model);
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
    Task AddAnnouncementAsync(TeamAnnouncement announcement);
    Task VoteAnnouncementPollAsync(string announcementId, string playerId, int optionIndex);
    Task DeleteAnnouncementAsync(string announcementId);

    // Matches & RSVP
    List<Match> GetMatches();
    Match? GetNextMatch();
    Task AddMatchAsync(Match match);
    Task UpdateMatchDetailsAsync(Match match);
    Task UpdateMatchResultAsync(string matchId, int ourScore, int rivalScore, List<MatchEvent> events);
    List<Attendance> GetAttendanceForMatch(string matchId);
    Attendance? GetUserAttendance(string matchId, string playerId);
    Task SetAttendanceAsync(string matchId, string playerId, AttendanceStatus status, string? note = null);

    // Payments & Treasury
    List<Payment> GetPayments();
    List<Payment> GetPaymentsForUser(string playerId);
    Task AddPaymentAsync(string playerId, string concept, decimal amount, PaymentMethod method, DateTime? paidAt = null, string notes = "");
    Task AddBatchFeeAsync(string concept, decimal amount, DateTime dueDate);
    Task MarkPaymentAsPaidAsync(string paymentId, PaymentMethod method, string notes = "");
    decimal GetTeamBalance();
    decimal GetTotalCollectedThisMonth();
    decimal GetTotalPendingAmount();

    // Expenses
    List<TeamExpense> GetExpenses();
    Task AddExpenseAsync(TeamExpense expense);

    // Stats & Events
    List<MatchEvent> GetMatchEvents();
    Dictionary<string, int> GetTopScorers();
    Dictionary<string, int> GetTopAssisters();
    Dictionary<string, (int Yellows, int Reds)> GetCardsSummary();
    Dictionary<string, int> GetMvpSummary();

    // Player Deactivation & Security Reactivation
    Task DeactivatePlayerAsync(string playerId);
    Task ReactivatePlayerAsync(string playerId);
    Task<(bool Success, string ErrorMessage)> ReactivateWithCodeAsync(string emailOrNickname, string password, string securityCode);

    // Season Lifecycle
    Task CloseSeasonAndStartNewAsync(string newSeasonName, decimal newSeasonFee);

    // Dynamic Weather
    Task<MatchWeatherInfo> GetMatchWeatherAsync(DateTime matchDate, string locationName, bool isHome);

    Task ResetToDemoAsync();
}

