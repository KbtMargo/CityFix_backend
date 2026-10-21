using CityFix_backend.Models;

namespace CityFix_backend.Services
{
    // Who may do what with an appeal. Every service that reads or changes appeals goes through here.
    //  - Resident: sees their own appeals; edits and deletes them while they are still New.
    //  - Association head: the appeals of their association; processes and reassigns them.
    //  - Administrator: everything.
    public static class AppealPolicy
    {
        public static IQueryable<Appeal> VisibleTo(this IQueryable<Appeal> appeals, User actor) => actor.Role switch
        {
            UserRole.Administrator => appeals,
            UserRole.AcabAdmin when actor.AcabId != null =>
                appeals.Where(a => a.AuthorId == actor.Id || a.AcabId == actor.AcabId),
            _ => appeals.Where(a => a.AuthorId == actor.Id)
        };

        public static bool CanView(User actor, Appeal appeal) =>
            actor.Role == UserRole.Administrator
            || appeal.AuthorId == actor.Id
            || IsHeadOf(actor, appeal);

        // The author edits while the appeal is New, and again when staff ask for more details (NeedsInfo).
        public static bool CanEdit(User actor, Appeal appeal) =>
            actor.Role == UserRole.Administrator
            || IsHeadOf(actor, appeal)
            || (appeal.AuthorId == actor.Id && appeal.Status is AppealStatus.New or AppealStatus.NeedsInfo);

        public static bool CanDelete(User actor, Appeal appeal) => CanEdit(actor, appeal);

        // Status, priority and comments.
        public static bool CanProcess(User actor, Appeal appeal) =>
            actor.Role == UserRole.Administrator || IsHeadOf(actor, appeal);

        // Choosing the responsible party.
        public static bool CanAssign(User actor, Appeal appeal) => CanProcess(actor, appeal);

        public static void EnsureCanView(User actor, Appeal appeal)
        {
            if (!CanView(actor, appeal)) throw Denied(actor, appeal, "view");
        }

        public static void EnsureCanEdit(User actor, Appeal appeal)
        {
            if (!CanEdit(actor, appeal)) throw Denied(actor, appeal, "edit");
        }

        public static void EnsureCanProcess(User actor, Appeal appeal)
        {
            if (!CanProcess(actor, appeal)) throw Denied(actor, appeal, "process");
        }

        private static bool IsHeadOf(User actor, Appeal appeal) =>
            actor.Role == UserRole.AcabAdmin && actor.AcabId != null && appeal.AcabId == actor.AcabId;

        private static ForbiddenException Denied(User actor, Appeal appeal, string action) =>
            new($"User {actor.Id} may not {action} appeal {appeal.Id}.");
    }

    // The appeal life cycle. Done, Rejected and Duplicate are final.
    //   New → Under review → In progress → Done
    //   Needs info: staff wait for the author's details, then review or work goes on.
    //   On hold: work is paused (weather, materials, queue) and resumed later.
    public static class AppealWorkflow
    {
        private static readonly Dictionary<AppealStatus, AppealStatus[]> Transitions = new()
        {
            [AppealStatus.New] = new[] { AppealStatus.UnderReview, AppealStatus.InProgress, AppealStatus.NeedsInfo, AppealStatus.Rejected, AppealStatus.Duplicate },
            [AppealStatus.UnderReview] = new[] { AppealStatus.InProgress, AppealStatus.NeedsInfo, AppealStatus.OnHold, AppealStatus.Rejected, AppealStatus.Duplicate },
            [AppealStatus.NeedsInfo] = new[] { AppealStatus.UnderReview, AppealStatus.InProgress, AppealStatus.Rejected },
            [AppealStatus.InProgress] = new[] { AppealStatus.OnHold, AppealStatus.Done, AppealStatus.Rejected },
            [AppealStatus.OnHold] = new[] { AppealStatus.InProgress, AppealStatus.Rejected },
            [AppealStatus.Done] = Array.Empty<AppealStatus>(),
            [AppealStatus.Rejected] = Array.Empty<AppealStatus>(),
            [AppealStatus.Duplicate] = Array.Empty<AppealStatus>()
        };

        // Life-cycle order, for lists and charts (the enum's numbers are storage only).
        public static readonly IReadOnlyList<AppealStatus> Ordered = new[]
        {
            AppealStatus.New, AppealStatus.UnderReview, AppealStatus.NeedsInfo, AppealStatus.InProgress,
            AppealStatus.OnHold, AppealStatus.Done, AppealStatus.Rejected, AppealStatus.Duplicate
        };

        // Being worked on: neither New nor closed (the "In progress" tab and counters).
        public static readonly AppealStatus[] Active =
            { AppealStatus.UnderReview, AppealStatus.NeedsInfo, AppealStatus.InProgress, AppealStatus.OnHold };

        public static readonly AppealStatus[] Closed = { AppealStatus.Done, AppealStatus.Rejected, AppealStatus.Duplicate };

        // New or active: still counts as open (sidebar stats, similar appeals, duplicates).
        public static readonly AppealStatus[] Open = [AppealStatus.New, .. Active];

        // In life-cycle order, as the status list shows them.
        public static IReadOnlyList<AppealStatus> NextStatuses(AppealStatus from) => Ordered.Where(Transitions[from].Contains).ToList();

        // Keeping the status (a comment or a priority change only) is always allowed.
        public static bool CanMove(AppealStatus from, AppealStatus to) => from == to || Transitions[from].Contains(to);

        public static bool IsFinal(AppealStatus status) => Transitions[status].Length == 0;
    }
}
