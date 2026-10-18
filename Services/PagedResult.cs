using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class PagedResult<T>
    {
        public const int DefaultPageSize = 10;

        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = DefaultPageSize;
        public int TotalCount { get; init; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

        // "Showing From–To of TotalCount"
        public int From => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
        public int To => Math.Min(PageNumber * PageSize, TotalCount);

        // Clamps the requested page into 1..TotalPages for the given total.
        public static int ClampPage(int page, int totalCount, int pageSize = DefaultPageSize)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            return Math.Clamp(page, 1, totalPages);
        }
    }

    public static class PagingExtensions
    {
        // Count, clamp the page, then Skip/Take — one call for every paged list.
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int page, int pageSize = PagedResult<T>.DefaultPageSize)
        {
            var total = await query.CountAsync();
            var current = PagedResult<T>.ClampPage(page, total, pageSize);
            var items = await query.Skip((current - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PagedResult<T> { Items = items, PageNumber = current, PageSize = pageSize, TotalCount = total };
        }
    }
}
