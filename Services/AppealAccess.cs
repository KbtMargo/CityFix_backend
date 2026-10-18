using CityFix_backend.Models;

namespace CityFix_backend.Services
{
    public static class AppealAccess
    {
        // The appeals a user may see (and manage). Used by every service that reads appeals.
        public static IQueryable<Appeal> VisibleTo(this IQueryable<Appeal> appeals, User actor) => actor.Role switch
        {
            UserRole.Administrator => appeals,
            UserRole.AcabAdmin when actor.AcabId != null =>
                appeals.Where(a => a.AuthorId == actor.Id || a.Author!.AcabId == actor.AcabId),
            _ => appeals.Where(a => a.AuthorId == actor.Id)
        };
    }
}
