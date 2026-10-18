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

        public static bool CanEdit(User actor, Appeal appeal) =>
            actor.Role == UserRole.Administrator
            || IsHeadOf(actor, appeal)
            || (appeal.AuthorId == actor.Id && appeal.Status == AppealStatus.New);

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

    // The appeal life cycle (state diagram, Figure 2.10). Done and Rejected are final.
    public static class AppealWorkflow
    {
        private static readonly Dictionary<AppealStatus, AppealStatus[]> Transitions = new()
        {
            [AppealStatus.New] = new[] { AppealStatus.InProgress, AppealStatus.Rejected },
            [AppealStatus.InProgress] = new[] { AppealStatus.Done, AppealStatus.Rejected },
            [AppealStatus.Done] = Array.Empty<AppealStatus>(),
            [AppealStatus.Rejected] = Array.Empty<AppealStatus>()
        };

        public static IReadOnlyList<AppealStatus> NextStatuses(AppealStatus from) => Transitions[from];

        // Keeping the status (a comment or a priority change only) is always allowed.
        public static bool CanMove(AppealStatus from, AppealStatus to) => from == to || Transitions[from].Contains(to);

        public static bool IsFinal(AppealStatus status) => Transitions[status].Length == 0;
    }
}
