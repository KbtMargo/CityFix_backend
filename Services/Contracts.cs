using CityFix_backend.Models;

namespace CityFix_backend.Services
{
    public record AppealLookups(
        IReadOnlyList<Category> Categories,
        IReadOnlyList<Recipient> Recipients,
        IReadOnlyList<Acab> Acabs);

    public record AppealStats(int Open, int Closed);

    public record AppealInput(
        string Title,
        string? Description,
        decimal? Lat,
        decimal? Lng,
        string? Address,
        int CategoryId,
        ResponsibleKind Responsible,
        int? RecipientId);

    public record AppealProcessInput(
        AppealStatus ExpectedStatus,
        AppealStatus NewStatus,
        AppealPriority NewPriority,
        string? Comment);

    public record SimilarAppeal(int Id, string? Title,AppealStatus Status, DateTime CreatedAt, int DistanceMeters, bool Visible);

    public record AppealPage(PagedResult<Appeal> Page, IReadOnlySet<int> PossibleDuplicates);

    public record AppealDetails(
        Appeal Appeal,
        IReadOnlyList<SimilarAppeal> Similar,
        bool CanEdit,
        bool CanDelete,
        bool CanProcess,
        bool CanAssign,
        IReadOnlyList<AppealStatus> NextStatuses);

    public record AppealExportRow(
        int Id,
        string Title,
        string Status,
        string Priority,
        string Category,
        string Responsible,
        string? Association,
        string Author,
        string? Address,
        decimal? Lat,
        decimal? Lng,
        string CreatedAt);

    public record UserInput(string FirstName, string? LastName, string Email, string? Phone, UserRole Role, int? AcabId);

    public record ProfileInput(string FirstName, string? LastName, string Email, string? Phone);

    public record NotificationSettings(bool InApp, bool Email, bool Sms);

    public record UserRow(User User, int AppealCount, string? DeleteBlockedKey)
    {
        public bool CanDelete => DeleteBlockedKey == null;
    }

    public record UsersPage(PagedResult<UserRow> Page, Acab? OwnAcab);

    public record AcabInput(string Name, string? Address, string? NameEn = null, string? AddressEn = null);

    public record AcabRow(Acab Acab, int MemberCount, int AppealCount)
    {
        public bool CanDelete => MemberCount == 0 && AppealCount == 0;
    }

    public enum AcabCodeKind { Resident, Head }

    public record DeleteCheck(int Id, string Name, int UsageCount, string? BlockedKey)
    {
        public bool Blocked => BlockedKey != null;
    }

    public record GeoPlace(double Lat, double Lng, string Address);

    public record RegisterRequest(
        string Email,
        string Password,
        string FirstName,
        string? LastName,
        string? Phone,
        string? InvitationCode);

    public record LoginResult(User? User, bool LockedOut);

    public record PersonalDataExport(
        PersonalDataExport.ProfileData Profile,
        IReadOnlyList<PersonalDataExport.AppealData> Appeals,
        IReadOnlyList<PersonalDataExport.NotificationData> Notifications,
        DateTime ExportedAt)
    {
        public record ProfileData(string Email, string? PendingEmail, string FirstName, string? LastName, string? Phone, string Role,
            string? Association, string Language, bool NotifyInApp, bool NotifyEmail, bool NotifySms, DateTime RegisteredAt);

        public record AppealData(int Id, string Title, string? Description, string? Address, decimal? Lat, decimal? Lng, string? Category,
            string Status, string Priority, DateTime CreatedAt, IReadOnlyList<string> Photos, IReadOnlyList<UpdateData> History);

        public record UpdateData(DateTime At, string OldStatus, string NewStatus, string? Comment, string? By);

        public record NotificationData(DateTime At, string Type, int? AppealId, string Message, bool IsRead);
    }

    public record RegisterResult(User? User, IReadOnlyDictionary<string, string> Errors, User? ExistingAccount = null)
    {
        public bool Succeeded => User != null;
    }

    public enum EmailConfirmation { Confirmed, Invalid, EmailTaken }

    public enum DeltaKind { Good, Bad, Flat }

    public enum DeltaDirection { Up, Down, None }

    public record Kpi(
        string Value,
        string? Unit,
        string DeltaText,
        DeltaDirection Direction,
        DeltaKind Kind,
        string Note,
        IReadOnlyList<double> Series);

    public record DashboardKpis(Kpi Active, Kpi ClosedThisMonth, Kpi Total, Kpi AvgResponse);

    public record ChartData(IReadOnlyList<string> Labels, IReadOnlyList<int> Values);

    public record AnalyticsData(
        int Total, int New, int InProgress, int Done, int Rejected,
        ChartData ByCategory, ChartData PerDay, ChartData ByStatus, ChartData ByPriority,
        IReadOnlyList<string> StatusKeys);
}
