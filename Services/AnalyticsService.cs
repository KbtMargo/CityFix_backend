using System.Globalization;
using CityFix_backend.Data;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppealService _appeals;

        public AnalyticsService(ApplicationDbContext context, IAppealService appeals)
        {
            _context = context;
            _appeals = appeals;
        }

        private record Row(int Id, DateTime CreatedAt, AppealStatus Status, AppealPriority Priority, string Category);

        private record Change(int AppealId, AppealStatus OldStatus, AppealStatus NewStatus, DateTime ChangedAt);

        public async Task<DashboardKpis> GetDashboardKpisAsync(int userId)
        {
            var (rows, changes) = await LoadAsync(userId);
            var now = DateTime.UtcNow;
            var todayKyiv = now.ToKyiv().Date;

            return new DashboardKpis(
                ActiveKpi(rows, changes, now, todayKyiv),
                ClosedThisMonthKpi(changes, todayKyiv),
                TotalKpi(rows, todayKyiv),
                AvgResponseKpi(rows, changes, now));
        }

        public async Task<AnalyticsData> GetAnalyticsAsync(int userId)
        {
            var (rows, _) = await LoadAsync(userId);
            var todayKyiv = DateTime.UtcNow.ToKyiv().Date;

            var byCategory = rows.GroupBy(r => r.Category)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .ToList();

            var days = Enumerable.Range(0, 30).Select(i => todayKyiv.AddDays(i - 29)).ToList();
            var perDay = days.Select(d => rows.Count(r => r.CreatedAt.ToKyiv().Date == d)).ToList();

            var statuses = Enum.GetValues<AppealStatus>();
            var priorities = Enum.GetValues<AppealPriority>();

            return new AnalyticsData(
                rows.Count,
                rows.Count(r => r.Status == AppealStatus.New),
                rows.Count(r => r.Status == AppealStatus.InProgress),
                rows.Count(r => r.Status == AppealStatus.Done),
                rows.Count(r => r.Status == AppealStatus.Rejected),
                new ChartData(byCategory.Select(g => g.Key).ToList(), byCategory.Select(g => g.Count()).ToList()),
                new ChartData(days.Select(d => d.ToString("dd.MM", CultureInfo.InvariantCulture)).ToList(), perDay),
                new ChartData(statuses.Select(s => s.GetDisplayName()).ToList(), statuses.Select(s => rows.Count(r => r.Status == s)).ToList()),
                new ChartData(priorities.Select(p => p.GetDisplayName()).ToList(), priorities.Select(p => rows.Count(r => r.Priority == p)).ToList()));
        }

        private async Task<(List<Row> Rows, List<Change> Changes)> LoadAsync(int userId)
        {
            var actor = await _appeals.GetUserAsync(userId);
            var visible = _context.Appeals.AsNoTracking().VisibleTo(actor);

            var rows = await visible
                .Select(a => new Row(a.Id, a.CreatedAt, a.Status, a.Priority, a.Category!.Name))
                .ToListAsync();
            var changes = await _context.AppealUpdates.AsNoTracking()
                .Where(u => visible.Any(a => a.Id == u.AppealId))
                .Select(u => new Change(u.AppealId, u.OldStatus, u.NewStatus, u.ChangedAt))
                .ToListAsync();
            return (rows, changes);
        }

        // Active appeals now vs a week ago; fewer is good. Sparkline: open count at the end of each of the last 7 days.
        private static Kpi ActiveKpi(List<Row> rows, List<Change> changes, DateTime now, DateTime todayKyiv)
        {
            var byAppeal = changes.GroupBy(c => c.AppealId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.ChangedAt).ToList());

            int OpenAt(DateTime utc) => rows.Count(r =>
            {
                if (r.CreatedAt > utc) return false;
                var last = byAppeal.TryGetValue(r.Id, out var list) ? list.LastOrDefault(c => c.ChangedAt <= utc) : null;
                return !(last?.NewStatus ?? AppealStatus.New).IsClosed();
            });

            var current = rows.Count(r => !r.Status.IsClosed());
            var weekAgo = OpenAt(now.AddDays(-7));
            var series = Enumerable.Range(0, 7)
                .Select(i => (double)OpenAt(UiFormat.KyivDayStartUtc(todayKyiv.AddDays(i - 5))))
                .ToList();
            series[^1] = current;

            var delta = current - weekAgo;
            return new Kpi(
                current.ToString(CultureInfo.CurrentUICulture), null,
                Math.Abs(delta).ToString(CultureInfo.CurrentUICulture),
                Direction(delta), delta < 0 ? DeltaKind.Good : delta > 0 ? DeltaKind.Bad : DeltaKind.Flat,
                SharedText.Get("Kpi.ThisWeek"), series);
        }

        // Appeals closed (Done or Rejected) this calendar month vs last month; more is good.
        private static Kpi ClosedThisMonthKpi(List<Change> changes, DateTime todayKyiv)
        {
            var monthStart = new DateTime(todayKyiv.Year, todayKyiv.Month, 1);
            int ClosedIn(DateTime start) => changes
                .Where(c => c.NewStatus.IsClosed() && !c.OldStatus.IsClosed())
                .Where(c => { var k = c.ChangedAt.ToKyiv(); return k >= start && k < start.AddMonths(1); })
                .Select(c => c.AppealId).Distinct().Count();

            var current = ClosedIn(monthStart);
            var previous = ClosedIn(monthStart.AddMonths(-1));
            var series = Enumerable.Range(0, 7).Select(i => (double)ClosedIn(monthStart.AddMonths(i - 6))).ToList();

            var diff = current - previous;
            var text = previous > 0
                ? $"{Math.Abs(Math.Round(diff * 100.0 / previous)).ToString(CultureInfo.CurrentUICulture)}%"
                : Math.Abs(diff).ToString(CultureInfo.CurrentUICulture);
            var previousMonth = CultureInfo.CurrentUICulture.DateTimeFormat.MonthGenitiveNames[monthStart.AddMonths(-1).Month - 1];

            return new Kpi(
                current.ToString(CultureInfo.CurrentUICulture), null, text,
                Direction(diff), diff > 0 ? DeltaKind.Good : diff < 0 ? DeltaKind.Bad : DeltaKind.Flat,
                SharedText.Format("Kpi.VsMonth", previousMonth), series);
        }

        // All appeals; the change is how many were created this month (neither good nor bad).
        private static Kpi TotalKpi(List<Row> rows, DateTime todayKyiv)
        {
            var monthStart = new DateTime(todayKyiv.Year, todayKyiv.Month, 1);
            var createdThisMonth = rows.Count(r => r.CreatedAt.ToKyiv() >= monthStart);
            var series = Enumerable.Range(0, 7)
                .Select(i => (double)rows.Count(r => r.CreatedAt.ToKyiv() < monthStart.AddMonths(i - 5)))
                .ToList();
            series[^1] = rows.Count;

            return new Kpi(
                rows.Count.ToString(CultureInfo.CurrentUICulture), null,
                "+" + createdThisMonth.ToString(CultureInfo.CurrentUICulture),
                createdThisMonth > 0 ? DeltaDirection.Up : DeltaDirection.None, DeltaKind.Flat,
                SharedText.Get("Kpi.ThisMonth"), series);
        }

        // Days from submission to the first status change, for first responses in the last 30 days
        // vs the 30 days before; faster is good. Sparkline: weekly averages for the last 7 weeks.
        private static Kpi AvgResponseKpi(List<Row> rows, List<Change> changes, DateTime now)
        {
            var created = rows.ToDictionary(r => r.Id, r => r.CreatedAt);
            var responses = changes.GroupBy(c => c.AppealId)
                .Where(g => created.ContainsKey(g.Key))
                .Select(g =>
                {
                    var first = g.Min(c => c.ChangedAt);
                    return (At: first, Days: (first - created[g.Key]).TotalDays);
                })
                .ToList();

            double? AvgBetween(DateTime from, DateTime to)
            {
                var window = responses.Where(r => r.At >= from && r.At < to).ToList();
                return window.Count == 0 ? null : window.Average(r => r.Days);
            }

            var current = AvgBetween(now.AddDays(-30), now);
            var previous = AvgBetween(now.AddDays(-60), now.AddDays(-30));
            var series = Enumerable.Range(0, 7)
                .Select(i => AvgBetween(now.AddDays(-7 * (7 - i)), now.AddDays(-7 * (6 - i))) ?? 0)
                .ToList();
            var unit = SharedText.Get("Kpi.Days");

            if (current == null)
            {
                return new Kpi("—", null, "—", DeltaDirection.None, DeltaKind.Flat, SharedText.Get("Kpi.NoData"), series);
            }
            if (previous == null)
            {
                return new Kpi(FormatDays(current.Value), unit, "—", DeltaDirection.None, DeltaKind.Flat, SharedText.Get("Kpi.NoComparison"), series);
            }

            var change = Math.Round(current.Value - previous.Value, 1);
            return new Kpi(
                FormatDays(current.Value), unit,
                $"{FormatDays(Math.Abs(change))} {unit}",
                change < 0 ? DeltaDirection.Down : change > 0 ? DeltaDirection.Up : DeltaDirection.None,
                change < 0 ? DeltaKind.Good : change > 0 ? DeltaKind.Bad : DeltaKind.Flat,
                SharedText.Get(change < 0 ? "Kpi.Faster" : change > 0 ? "Kpi.Slower" : "Kpi.NoChange"),
                series);
        }

        private static string FormatDays(double days) => days.ToString("0.#", CultureInfo.CurrentUICulture);

        private static DeltaDirection Direction(int delta) =>
            delta > 0 ? DeltaDirection.Up : delta < 0 ? DeltaDirection.Down : DeltaDirection.None;
    }
}
