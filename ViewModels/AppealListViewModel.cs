using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;

namespace CityFix_backend.ViewModels
{
    // An appeal list page: filters, tabs, one page of results, and the detail modal (?appealId=).
    public class AppealListViewModel
    {
        public PagedResult<Appeal> Page { get; init; } = new();
        public AppealFilter Filter { get; init; } = new();
        public AppealLookups Lookups { get; init; } = null!;

        // Controller + action the filters, tabs and modal links point at (Dashboard/Index or Appeals/Index).
        public string Controller { get; init; } = "Appeals";
        public string Action { get; init; } = "Index";

        public bool IsStaff { get; init; }
        public bool IsAdministrator { get; init; }

        // The appeal shown in the ReportDetailModal, if any.
        public Appeal? Selected { get; init; }
        public bool CanEditSelected { get; init; }
    }

    public class DashboardViewModel
    {
        public DashboardKpis Kpis { get; init; } = null!;
        public AppealListViewModel List { get; init; } = null!;
    }

    // Admin DataTable pages: one page of rows plus a per-row count (appeals, members, ...).
    public class TablePageViewModel<T>
    {
        public PagedResult<T> Page { get; init; } = new();
        public IReadOnlyDictionary<int, int> Counts { get; init; } = new Dictionary<int, int>();
        public int Count(int id) => Counts.TryGetValue(id, out var n) ? n : 0;
    }

    // The Pagination bar: "Showing x–y of N" plus Previous / Page x of y / Next.
    public class PaginationModel
    {
        public int PageNumber { get; init; }
        public int TotalPages { get; init; }
        public int From { get; init; }
        public int To { get; init; }
        public int TotalCount { get; init; }

        // Route values to keep (filters); "page" is added per link.
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

        public IDictionary<string, string> RouteFor(int page)
        {
            var values = new Dictionary<string, string>(Route);
            if (page > 1) values["page"] = page.ToString(); else values.Remove("page");
            return values;
        }
    }

    public class NotificationsViewModel
    {
        public List<Notification> Items { get; init; } = new();
        public int UnreadCount { get; init; }
        public bool UnreadTab { get; init; }
    }
}
