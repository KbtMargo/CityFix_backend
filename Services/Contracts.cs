using CityFix_backend.Models;

namespace CityFix_backend.Services
{
    // Data passed to and returned by the services (the interfaces are in Services/Interfaces).

    // ───── Appeals ─────

    // Reference data for the appeal form and the filters popover.
    public record AppealLookups(
        IReadOnlyList<Category> Categories,
        IReadOnlyList<Recipient> Recipients,
        IReadOnlyList<Acab> Acabs);

    // Sidebar stats box: open = New + InProgress, closed = Done + Rejected.
    public record AppealStats(int Open, int Closed);

    // What the appeal form submits. RecipientId is used when Responsible is Recipient.
    public record AppealInput(
        string Title,
        string? Description,
        decimal? Lat,
        decimal? Lng,
        string? Address,
        int CategoryId,
        ResponsibleKind Responsible,
        int? RecipientId);

    // The "Update appeal" panel. ExpectedStatus is the status the staff member saw: if someone changed it
    // in the meantime the update is refused instead of silently overwriting their work.
    public record AppealProcessInput(
        AppealStatus ExpectedStatus,
        AppealStatus NewStatus,
        AppealPriority NewPriority,
        string? Comment);

    // An appeal of the same category within the search radius. Visible: the user may open it.
    public record SimilarAppeal(int Id, string Title, AppealStatus Status, DateTime CreatedAt, int DistanceMeters, bool Visible);

    // One page of a list, plus the ids that have an open appeal of the same category nearby.
    public record AppealPage(PagedResult<Appeal> Page, IReadOnlySet<int> PossibleDuplicates);

    // The appeal card: the appeal, what this user may do with it, and the appeals around it.
    public record AppealDetails(
        Appeal Appeal,
        IReadOnlyList<SimilarAppeal> Similar,
        bool CanEdit,
        bool CanDelete,
        bool CanProcess,
        bool CanAssign,
        IReadOnlyList<AppealStatus> NextStatuses);

    // A row of the CSV / JSON export.
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

    // ───── Users and associations ─────

    public record UserInput(string FirstName, string? LastName, string Email, string? Phone, UserRole Role, int? AcabId);

    public record ProfileInput(string FirstName, string? LastName, string Email, string? Phone);

    // A row of the users table. DeleteBlockedKey says why the row cannot be deleted (a localization key).
    public record UserRow(User User, int AppealCount, string? DeleteBlockedKey)
    {
        public bool CanDelete => DeleteBlockedKey == null;
    }

    // The users page. OwnAcab is the head's association, so the page can show its residents' code.
    public record UsersPage(PagedResult<UserRow> Page, Acab? OwnAcab);

    public record AcabInput(string Name, string? Address);

    public record AcabRow(Acab Acab, int MemberCount, int AppealCount)
    {
        public bool CanDelete => MemberCount == 0 && AppealCount == 0;
    }

    public enum AcabCodeKind { Resident, Head }

    // ───── Shared ─────

    // Whether a record can be deleted. BlockedKey is a localization key taking UsageCount.
    public record DeleteCheck(int Id, string Name, int UsageCount, string? BlockedKey)
    {
        public bool Blocked => BlockedKey != null;
    }

    public record GeoPlace(double Lat, double Lng, string Address);

    // ───── Authentication ─────

    public record RegisterRequest(
        string Email,
        string Password,
        string FirstName,
        string? LastName,
        string? Phone,
        string? InvitationCode);

    // Either the created user, or field-level errors (field name → localization key).
    public record RegisterResult(User? User, IReadOnlyDictionary<string, string> Errors)
    {
        public bool Succeeded => User != null;
    }

    // ───── Analytics ─────

    // Whether a change is good, not which way it goes (KpiCard README).
    public enum DeltaKind { Good, Bad, Flat }

    public enum DeltaDirection { Up, Down, None }

    // One KpiCard: value, the change with its note, and a 7-point sparkline.
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
        ChartData ByCategory, ChartData PerDay, ChartData ByStatus, ChartData ByPriority);
}
