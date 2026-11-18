using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Tests.Integration;

[Trait("Category", "Integration")]
public class NotificationServiceTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncLifetime
{
    private ServiceHarness _app = null!;
    private INotificationService Notifications => _app.Get<INotificationService>();

    public Task InitializeAsync()
    {
        _app = new ServiceHarness(database);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private async Task<Notification> AddAsync(User user, bool read = false, DateTime? at = null, DeliveryState email = DeliveryState.None, bool inApp = true)
    {
        var note = new Notification { UserId = user.Id, IsRead = read, InApp = inApp, EmailState = email, CreatedAt = at ?? DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow };
        _app.Db.Notifications.Add(note);
        await _app.Db.SaveChangesAsync();
        return note;
    }

    [Fact]
    public async Task Recipients_preferences_decide_the_channels()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var appeal = await _app.Data.AppealAsync(n.Resident, n.Category);
        await _app.Get<IUserService>().UpdateNotificationSettingsAsync(n.Resident.Id, new NotificationSettings(InApp: false, Email: true));
        var quiet = await _app.Data.UserAsync(acab: n.Acab);
        await _app.Get<IUserService>().UpdateNotificationSettingsAsync(quiet.Id, new NotificationSettings(false, false));
        var unconfirmed = await _app.Data.UserAsync(acab: n.Acab, confirmed: false);

        await Notifications.AddStatusChangedAsync(appeal, AppealStatus.InProgress, null);
        _app.Db.AppealSupports.AddRange(
            new AppealSupport { AppealId = appeal.Id, UserId = quiet.Id, CreatedAt = DateTime.UtcNow },
            new AppealSupport { AppealId = appeal.Id, UserId = unconfirmed.Id, CreatedAt = DateTime.UtcNow });
        await _app.Db.SaveChangesAsync();
        await Notifications.AddSupportersStatusChangedAsync(appeal, AppealStatus.InProgress, n.Head.Id);
        await _app.Db.SaveChangesAsync();

        var notes = await _app.Db.Notifications.AsNoTracking().Where(x => x.AppealId == appeal.Id).ToListAsync();
        var toAuthor = notes.Single(x => x.UserId == n.Resident.Id);
        Assert.Equal((false, true, DeliveryState.Pending), (toAuthor.InApp, toAuthor.IsRead, toAuthor.EmailState));
        Assert.DoesNotContain(notes, x => x.UserId == quiet.Id);
        var toUnconfirmed = notes.Single(x => x.UserId == unconfirmed.Id);
        Assert.Equal((true, DeliveryState.None), (toUnconfirmed.InApp, toUnconfirmed.EmailState));
    }

    [Fact]
    public async Task Actor_is_never_notified_about_own_action()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var appeal = await _app.Data.AppealAsync(n.Resident, n.Category);
        await Notifications.AddForHeadsAsync(appeal, NotificationType.Rated, "5", n.Head.Id);
        await Notifications.AddAssignedAsync(appeal, "ОСББ", null, n.Resident.Id);
        await _app.Db.SaveChangesAsync();

        var notes = await _app.Db.Notifications.AsNoTracking().Where(x => x.AppealId == appeal.Id).ToListAsync();
        Assert.Equal([n.Head.Id], notes.Select(x => x.UserId));
    }

    [Fact]
    public async Task Inbox_shows_only_own_in_app_notifications_newest_first()
    {
        var user = await _app.Data.UserAsync();
        var other = await _app.Data.UserAsync();
        var old = await AddAsync(user, at: DateTime.UtcNow.AddHours(-1));
        var read = await AddAsync(user, read: true);
        await AddAsync(user, inApp: false);
        await AddAsync(other);

        var all = await Notifications.GetForUserAsync(user.Id, unreadOnly: false, 1);
        Assert.Equal([read.Id, old.Id], all.Items.Select(x => x.Id));
        Assert.Equal([old.Id], (await Notifications.GetForUserAsync(user.Id, unreadOnly: true, 1)).Items.Select(x => x.Id));
        Assert.Equal(1, await Notifications.CountUnreadAsync(user.Id));
    }

    [Fact]
    public async Task Users_mark_only_their_own_notifications_read()
    {
        var user = await _app.Data.UserAsync();
        var other = await _app.Data.UserAsync();
        var mine = await AddAsync(user);
        var theirs = await AddAsync(other);

        Assert.Null(await Notifications.MarkReadAsync(theirs.Id, user.Id));
        Assert.True((await Notifications.MarkReadAsync(mine.Id, user.Id))!.IsRead);
        await AddAsync(user);
        await Notifications.MarkAllReadAsync(user.Id);

        Assert.Equal(0, await Notifications.CountUnreadAsync(user.Id));
        Assert.Equal(1, await Notifications.CountUnreadAsync(other.Id));
    }

    [Fact]
    public async Task Cleanup_removes_old_notifications_but_keeps_undelivered_mail()
    {
        var user = await _app.Data.UserAsync();
        var oldRead = await AddAsync(user, read: true, at: DateTime.UtcNow.AddDays(-100));
        var oldUnread = await AddAsync(user, at: DateTime.UtcNow.AddDays(-100));
        var ancient = await AddAsync(user, at: DateTime.UtcNow.AddDays(-400));
        var ancientPending = await AddAsync(user, at: DateTime.UtcNow.AddDays(-400), email: DeliveryState.Pending);

        await Notifications.DeleteOldAsync(TimeSpan.FromDays(90), TimeSpan.FromDays(365));

        var left = await _app.Db.Notifications.Where(x => x.UserId == user.Id).Select(x => x.Id).ToListAsync();
        Assert.Equal([oldUnread.Id, ancientPending.Id], left.Order());
        Assert.DoesNotContain(oldRead.Id, left);
        Assert.DoesNotContain(ancient.Id, left);
    }
}

[Trait("Category", "Integration")]
public class AnalyticsServiceTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncLifetime
{
    private ServiceHarness _app = null!;
    private IAnalyticsService Analytics => _app.Get<IAnalyticsService>();
    private DateTime Now => _app.Clock.GetUtcNow().UtcDateTime;

    public Task InitializeAsync()
    {
        _app = new ServiceHarness(database);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Head_analytics_cover_only_the_association()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var outsider = await _app.Data.UserAsync();
        await _app.Data.AppealAsync(n.Resident, n.Category);
        await _app.Data.AppealAsync(n.Resident, n.Category, AppealStatus.InProgress);
        await _app.Data.AppealAsync(n.Neighbour, n.Category, AppealStatus.Done, priority: AppealPriority.Urgent);
        await _app.Data.AppealAsync(outsider, n.Category, AppealStatus.Rejected);

        var data = await Analytics.GetAnalyticsAsync(n.Head);

        Assert.Equal((3, 1, 1, 1, 0), (data.Total, data.New, data.InProgress, data.Done, data.Rejected));
        Assert.Equal(3, data.ByCategory.Values.Single());
        Assert.Equal(30, data.PerDay.Labels.Count);
        Assert.Equal(3, data.PerDay.Values[^1]);
        Assert.Equal(Enum.GetValues<AppealStatus>().Length, data.ByStatus.Values.Count);
        Assert.Equal([0, 2, 1], data.ByPriority.Values);
    }

    [Fact]
    public async Task Management_figures_reflect_deadlines_ratings_and_hot_spots()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var onTime = await _app.Data.AppealAsync(n.Resident, n.Category, AppealStatus.Done, createdAt: Now.AddDays(-4), dueAt: Now.AddDays(1), closedAt: Now.AddDays(-2), address: "Площа 1");
        var late = await _app.Data.AppealAsync(n.Resident, n.Category, AppealStatus.Done, createdAt: Now.AddDays(-10), dueAt: Now.AddDays(-5), closedAt: Now.AddDays(-2), address: " Площа 1 ");
        var overdue = await _app.Data.AppealAsync(n.Neighbour, n.Category, AppealStatus.InProgress, createdAt: Now.AddDays(-9), dueAt: Now.AddDays(-2), address: "Площа 1");
        await _app.Db.Appeals.Where(a => a.Id == onTime.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Rating, 5));
        await _app.Db.Appeals.Where(a => a.Id == late.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Rating, 2).SetProperty(a => a.ReopenCount, 1));
        await _app.Db.Appeals.Where(a => a.Id == overdue.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.SupportCount, 4));

        var m = await Analytics.GetManagementAsync(n.Head);

        Assert.Equal((50, 2, 1), (m.OnTimePercent, m.ClosedWithDeadline, m.OverdueNow));
        Assert.Equal((5d, 3.5, 2, 1, 4), (m.AvgResolutionDays, m.AvgRating, m.RatedCount, m.ReopenedCount, m.SupportTotal));
        Assert.Equal(overdue.Id, Assert.Single(m.Overdue).Id);
        Assert.Equal(2, Assert.Single(m.Overdue).DaysOverdue);
        Assert.Equal((overdue.Id, 4), (Assert.Single(m.MostSupported).Id, m.MostSupported[0].Supporters));
        Assert.Equal(new HotSpot("Площа 1", 3, 1), Assert.Single(m.HotSpots));
        Assert.Equal([0, 1, 0, 0, 1], m.Ratings.Values);
        Assert.Equal(12, m.Flow.Labels.Count);
        Assert.Equal(new ResponsibleLoad(n.Acab.Name, 1, 1, 9), Assert.Single(m.Load));
    }

    [Fact]
    public async Task Empty_scope_gives_empty_figures_instead_of_errors()
    {
        var loner = await _app.Data.UserAsync();
        var m = await Analytics.GetManagementAsync(loner);
        Assert.Equal(((int?)null, (double?)null, (double?)null), (m.OnTimePercent, m.AvgResolutionDays, m.AvgRating));
        var kpis = await Analytics.GetDashboardKpisAsync(loner);
        Assert.Equal(("0", "—"), (kpis.Active.Value, kpis.AvgResponse.Value));
    }

    [Fact]
    public async Task Dashboard_kpis_count_active_closed_and_response_time()
    {
        var resident = await _app.Data.UserAsync();
        var category = await _app.Data.CategoryAsync();
        var a = await _app.Data.AppealAsync(resident, category, AppealStatus.InProgress, createdAt: Now.AddDays(-3));
        var b = await _app.Data.AppealAsync(resident, category, AppealStatus.Done, createdAt: Now.AddDays(-2));
        await _app.Data.AppealAsync(resident, category);
        _app.Db.AppealUpdates.AddRange(
            new AppealUpdate { AppealId = a.Id, OldStatus = AppealStatus.New, NewStatus = AppealStatus.InProgress, ChangedById = resident.Id, ChangedAt = Now.AddDays(-2) },
            new AppealUpdate { AppealId = b.Id, OldStatus = AppealStatus.New, NewStatus = AppealStatus.Done, ChangedById = resident.Id, ChangedAt = Now.AddDays(-1) });
        await _app.Db.SaveChangesAsync();

        var kpis = await Analytics.GetDashboardKpisAsync(resident);

        Assert.Equal("2", kpis.Active.Value);
        Assert.Equal("3", kpis.Total.Value);
        Assert.Equal(7, kpis.Active.Series.Count);
        Assert.Equal(2d, kpis.Active.Series[^1]);
        Assert.Equal("1", kpis.AvgResponse.Value);
    }
}

[Trait("Category", "Integration")]
public class AuditLogTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncLifetime
{
    private ServiceHarness _app = null!;

    public Task InitializeAsync()
    {
        _app = new ServiceHarness(database);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Entries_record_who_did_what_from_where()
    {
        var admin = await _app.Data.AdminAsync();
        _app.Get<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7") } };
        var marker = Guid.NewGuid().ToString("N");

        await _app.Get<IAuditLog>().RecordAsync(admin, "user.block", $"User #5 {marker}", new string('x', 1500));

        var entry = Assert.Single((await _app.Get<IAuditLog>().GetPageAsync(marker, 1)).Items);
        Assert.Equal((admin.Id, "user.block", "203.0.113.7", 1000), (entry.ActorId, entry.Action, entry.IpAddress, entry.Details!.Length));
        Assert.Contains(admin.Email, entry.ActorName);
        Assert.Equal(_app.Clock.GetUtcNow().UtcDateTime, entry.At);
    }

    [Fact]
    public async Task Search_matches_action_target_or_details_and_treats_wildcards_literally()
    {
        var admin = await _app.Data.AdminAsync();
        var marker = Guid.NewGuid().ToString("N");
        await _app.Get<IAuditLog>().RecordAsync(admin, "acab.create", $"Acab {marker}");
        await _app.Get<IAuditLog>().RecordAsync(admin, "acab.delete", "other", $"details {marker}");

        Assert.Equal(2, (await _app.Get<IAuditLog>().GetPageAsync(marker.ToUpperInvariant(), 1)).TotalCount);
        Assert.Empty((await _app.Get<IAuditLog>().GetPageAsync(marker[..5] + "_" + marker[6..], 1)).Items);
    }
}
