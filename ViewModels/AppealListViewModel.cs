using CityFix_backend.Models;
using CityFix_backend.Services;

namespace CityFix_backend.ViewModels
{
    public class AppealListViewModel
    {
        public PagedResult<Appeal> Page { get; init; } = new();
        public IReadOnlySet<int> PossibleDuplicates { get; init; } = new HashSet<int>();
        public AppealFilter Filter { get; init; } = new();
        public AppealLookups Lookups { get; init; } = null!;

        public string Controller { get; init; } = "Appeals";
        public string Action { get; init; } = "Index";

        public User Actor { get; init; } = null!;
        public bool IsStaff => Actor.IsStaff;
        public bool IsAdministrator => Actor.Role == UserRole.Administrator;

        public AppealDetails? Selected { get; init; }
    }

    public class DashboardViewModel
    {
        public DashboardKpis Kpis { get; init; } = null!;
        public AppealListViewModel List { get; init; } = null!;
    }

    public class TablePageViewModel<T>
    {
        public PagedResult<T> Page { get; init; } = new();
        public IReadOnlyDictionary<int, int> Counts { get; init; } = new Dictionary<int, int>();
        public string? Q { get; init; }
        public int Count(int id) => Counts.TryGetValue(id, out var n) ? n : 0;
    }

    public class UsersIndexViewModel
    {
        public UsersPage Data { get; init; } = null!;
        public string? Q { get; init; }
        public bool IsAdministrator { get; init; }
        public int CurrentUserId { get; init; }
    }

    public class PaginationModel
    {
        public int PageNumber { get; init; }
        public int TotalPages { get; init; }
        public int From { get; init; }
        public int To { get; init; }
        public int TotalCount { get; init; }

        public IDictionary<string, string> Route { get; init; } = new Dictionary<string, string>();

        public static PaginationModel For<T>(PagedResult<T> page, IDictionary<string, string>? route = null) => new()
        {
            PageNumber = page.PageNumber,
            TotalPages = page.TotalPages,
            From = page.From,
            To = page.To,
            TotalCount = page.TotalCount,
            Route = route ?? new Dictionary<string, string>()
        };

        public static PaginationModel For<T>(PagedResult<T> page, string? q) =>
            For(page, string.IsNullOrWhiteSpace(q) ? null : new Dictionary<string, string> { ["q"] = q });

        public IDictionary<string, string> RouteFor(int page)
        {
            var values = new Dictionary<string, string>(Route);
            if (page > 1) values["page"] = page.ToString(); else values.Remove("page");
            return values;
        }
    }

    public class NotificationsViewModel
    {
        public PagedResult<Notification> Page { get; init; } = new();
        public IReadOnlyList<Notification> Items => Page.Items;
        public int UnreadCount { get; init; }
        public bool UnreadTab { get; init; }
    }
}
