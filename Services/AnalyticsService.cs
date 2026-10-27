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
        private readonly TimeProvider _clock;

        public AnalyticsService(ApplicationDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        private record Row(int Id, DateTime CreatedAt, AppealStatus Status);

        private record Change(int AppealId, AppealStatus OldStatus, AppealStatus NewStatus, DateTime ChangedAt);

        public async Task<DashboardKpis> GetDashboardKpisAsync(User actor, bool community = false)
        {
            var visible = community ? _context.Appeals.AsNoTracking() : _context.Appeals.AsNoTracking().VisibleTo(actor);
            var rows = await visible.Select(a => new Row(a.Id, a.CreatedAt, a.Status)).ToListAsync();

            var changes = await _context.AppealUpdates.AsNoTracking()
                .Where(u => u.OldStatus != u.NewStatus && visible.Any(a => a.Id == u.AppealId))
                .Select(u => new Change(u.AppealId, u.OldStatus, u.NewStatus, u.ChangedAt))
                .ToListAsync();

            var now = _clock.GetUtcNow().UtcDateTime;
            var todayKyiv = now.ToKyiv().Date;
            return new DashboardKpis(
                ActiveKpi(rows, changes, now, todayKyiv),
                ClosedThisMonthKpi(changes, todayKyiv),
                TotalKpi(rows, todayKyiv),
                AvgResponseKpi(rows, changes, now));
        }

        public async Task<AnalyticsData> GetAnalyticsAsync(User actor)
        {
            var visible = _context.Appeals.AsNoTracking().VisibleTo(actor);

            var byStatus = await visible.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var byPriority = await visible.GroupBy(a => a.Priority).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var byCategory = (await visible.GroupBy(a => new { a.Category!.Name, a.Category.NameEn }).Select(g => new { g.Key.Name, g.Key.NameEn, Count = g.Count() })
                .ToListAsync())
                .Select(x => new { Name = Localized.Pick(x.Name, x.NameEn), x.Count })
                .OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.CurrentCulture).ToList();

            var todayKyiv = _clock.GetUtcNow().UtcDateTime.ToKyiv().Date;
            var days = Enumerable.Range(0, 30).Select(i => todayKyiv.AddDays(i - 29)).ToList();
            var fromUtc = UiFormat.KyivDayStartUtc(days[0]);
            var recent = await visible.Where(a => a.CreatedAt >= fromUtc).Select(a => a.CreatedAt).ToListAsync();
            var perDay = days.Select(d => recent.Count(c => c.ToKyiv().Date == d)).ToList();

            var statuses = AppealWorkflow.Ordered;
            var priorities = Enum.GetValues<AppealPriority>();
            int Status(AppealStatus s) => byStatus.GetValueOrDefault(s);

            return new AnalyticsData(
                byStatus.Values.Sum(),
                Status(AppealStatus.New), AppealWorkflow.Active.Sum(Status), Status(AppealStatus.Done), Status(AppealStatus.Rejected),
                new ChartData(byCategory.Select(c => c.Name).ToList(), byCategory.Select(c => c.Count).ToList()),
                new ChartData(days.Select(d => d.ToString("dd.MM", CultureInfo.InvariantCulture)).ToList(), perDay),
                new ChartData(statuses.Select(s => s.GetDisplayName()).ToList(), statuses.Select(Status).ToList()),
                new ChartData(priorities.Select(p => p.GetDisplayName()).ToList(), priorities.Select(p => byPriority.GetValueOrDefault(p)).ToList()),
                statuses.Select(s => s.PillModifier()).ToList());
        }

        private const int FlowWeeks = 12;
        private const int ListSize = 6;

        public async Task<ManagementData> GetManagementAsync(User actor)
        {
            var rows = await _context.Appeals.AsNoTracking().VisibleTo(actor)
                .Select(a => new
                {
                    a.Id, a.Title, a.Language, a.TitleTranslated, a.Address, a.AddressTranslated, a.Status, a.Priority,
                    a.CreatedAt, a.ClosedAt, a.DueAt, a.Rating, a.ReopenCount, a.SupportCount, a.ResponsibleKind,
                    Category = a.Category!.Name, CategoryEn = a.Category.NameEn,
                    Recipient = a.Recipient != null ? a.Recipient.Name : null, RecipientEn = a.Recipient != null ? a.Recipient.NameEn : null,
                    Acab = a.Acab != null ? a.Acab.Name : null, AcabEn = a.Acab != null ? a.Acab.NameEn : null
                })
                .ToListAsync();

            var now = _clock.GetUtcNow().UtcDateTime;
            var items = rows.Select(r => new
            {
                Row = r,
                Title = new Appeal { Title = r.Title, Language = r.Language, TitleTranslated = r.TitleTranslated }.DisplayTitle(),
                Address = new Appeal { Address = r.Address, AddressTranslated = r.AddressTranslated }.DisplayAddress(),
                Open = AppealWorkflow.Open.Contains(r.Status),
                Responsible = r.ResponsibleKind == ResponsibleKind.Association
                    ? (r.Acab == null ? null : Localized.Pick(r.Acab, r.AcabEn))
                    : (r.Recipient == null ? null : Localized.Pick(r.Recipient, r.RecipientEn))
            }).ToList();
            var noResponsible = SharedText.Get("Appeal.NoResponsible");

            // Deadlines: share of finished appeals done in time, and what is late right now.
            var doneWithDeadline = items.Where(i => i.Row.Status == AppealStatus.Done && i.Row.DueAt != null && i.Row.ClosedAt != null).ToList();
            int? onTime = doneWithDeadline.Count == 0 ? null
                : (int)Math.Round(doneWithDeadline.Count(i => i.Row.ClosedAt <= i.Row.DueAt) * 100.0 / doneWithDeadline.Count);
            var overdue = items.Where(i => i.Open && i.Row.DueAt < now).ToList();
            var resolved = items.Where(i => i.Row.Status == AppealStatus.Done && i.Row.ClosedAt != null).ToList();
            double? avgResolution = resolved.Count == 0 ? null : Math.Round(resolved.Average(i => (i.Row.ClosedAt!.Value - i.Row.CreatedAt).TotalDays), 1);

            // Residents' verdict.
            var rated = items.Where(i => i.Row.Rating != null).ToList();
            double? avgRating = rated.Count == 0 ? null : Math.Round(rated.Average(i => i.Row.Rating!.Value), 1);
            var ratings = new ChartData(
                Enumerable.Range(1, 5).Select(n => $"{n} ★").ToList(),
                Enumerable.Range(1, 5).Select(n => rated.Count(i => i.Row.Rating == n)).ToList());

            // Inflow against outflow per week: when the first line stays above the second, the backlog grows.
            var thisWeek = StartOfWeek(now.ToKyiv().Date);
            var weeks = Enumerable.Range(0, FlowWeeks).Select(i => thisWeek.AddDays(7 * (i - FlowWeeks + 1))).ToList();
            int InWeek(IEnumerable<DateTime> dates, DateTime week) => dates.Count(d => StartOfWeek(d.ToKyiv().Date) == week);
            var createdDates = items.Select(i => i.Row.CreatedAt).ToList();
            var closedDates = items.Where(i => i.Row.ClosedAt != null && AppealWorkflow.Closed.Contains(i.Row.Status)).Select(i => i.Row.ClosedAt!.Value).ToList();
            var flow = new WeeklyFlow(
                weeks.Select(w => w.ToString("dd.MM", CultureInfo.InvariantCulture)).ToList(),
                weeks.Select(w => InWeek(createdDates, w)).ToList(),
                weeks.Select(w => InWeek(closedDates, w)).ToList());

            // Which kinds of problems take longest.
            var byCategory = resolved
                .GroupBy(i => Localized.Pick(i.Row.Category, i.Row.CategoryEn))
                .Select(g => (Name: g.Key, Days: Math.Round(g.Average(i => (i.Row.ClosedAt!.Value - i.Row.CreatedAt).TotalDays), 1)))
                .OrderByDescending(x => x.Days)
                .ToList();

            var overdueList = overdue
                .OrderByDescending(i => now - i.Row.DueAt!.Value)
                .Take(ListSize)
                .Select(i => new OverdueItem(i.Row.Id, i.Title, Math.Max(1, (int)Math.Ceiling((now - i.Row.DueAt!.Value).TotalDays)), i.Responsible ?? noResponsible, i.Row.Priority))
                .ToList();

            var supported = items.Where(i => i.Open && i.Row.SupportCount > 0)
                .OrderByDescending(i => i.Row.SupportCount).ThenBy(i => i.Row.CreatedAt)
                .Take(ListSize)
                .Select(i => new SupportedItem(i.Row.Id, i.Title, i.Row.SupportCount, i.Row.Status))
                .ToList();

            var load = items.Where(i => i.Open)
                .GroupBy(i => i.Responsible ?? noResponsible)
                .Select(g => new ResponsibleLoad(g.Key, g.Count(), g.Count(i => i.Row.DueAt < now), Math.Round(g.Average(i => (now - i.Row.CreatedAt).TotalDays), 1)))
                .OrderByDescending(l => l.Overdue).ThenByDescending(l => l.Open)
                .Take(ListSize)
                .ToList();

            // The same address again and again points at a cause worth fixing for good.
            var hotSpots = items.Where(i => i.Address != null)
                .GroupBy(i => i.Address!.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => new HotSpot(g.Key, g.Count(), g.Count(i => i.Open)))
                .OrderByDescending(h => h.Total).ThenByDescending(h => h.Open)
                .Take(ListSize)
                .ToList();

            return new ManagementData(
                onTime, doneWithDeadline.Count, overdue.Count, avgResolution,
                avgRating, rated.Count, items.Count(i => i.Row.ReopenCount > 0), items.Where(i => i.Open).Sum(i => i.Row.SupportCount),
                flow, ratings, new ChartSeries(byCategory.Select(c => c.Name).ToList(), byCategory.Select(c => c.Days).ToList()),
                overdueList, supported, load, hotSpots);
        }

        private static DateTime StartOfWeek(DateTime day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

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
