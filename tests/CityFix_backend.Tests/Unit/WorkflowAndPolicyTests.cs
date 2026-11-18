using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services;

namespace CityFix_backend.Tests.Unit;

[Trait("Category", "Unit")]
public class AppealWorkflowTests
{
    public static TheoryData<AppealStatus> AllStatuses => new(Enum.GetValues<AppealStatus>());

    [Fact]
    public void Open_and_closed_statuses_split_every_status_exactly_once()
    {
        var all = Enum.GetValues<AppealStatus>();
        Assert.Equal(all.Length, AppealWorkflow.Open.Length + AppealWorkflow.Closed.Length);
        Assert.Empty(AppealWorkflow.Open.Intersect(AppealWorkflow.Closed));
        Assert.Equal(all.OrderBy(s => s), AppealWorkflow.Ordered.OrderBy(s => s));
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Final_statuses_are_exactly_the_closed_ones(AppealStatus status)
    {
        Assert.Equal(AppealWorkflow.Closed.Contains(status), AppealWorkflow.IsFinal(status));
        Assert.Equal(AppealWorkflow.Closed.Contains(status), status.IsClosed());
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Staying_in_the_same_status_is_always_allowed(AppealStatus status)
    {
        Assert.True(AppealWorkflow.CanMove(status, status));
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Next_statuses_agree_with_CanMove_and_never_return_to_New(AppealStatus from)
    {
        var next = AppealWorkflow.NextStatuses(from);
        foreach (var to in Enum.GetValues<AppealStatus>().Where(s => s != from))
        {
            Assert.Equal(next.Contains(to), AppealWorkflow.CanMove(from, to));
        }
        Assert.DoesNotContain(AppealStatus.New, next);
    }

    [Fact]
    public void Next_statuses_follow_the_display_order()
    {
        var next = AppealWorkflow.NextStatuses(AppealStatus.New);
        Assert.Equal(next.OrderBy(s => AppealWorkflow.Ordered.ToList().IndexOf(s)), next);
    }

    [Fact]
    public void Every_open_status_can_still_reach_a_closed_one()
    {
        foreach (var start in AppealWorkflow.Open)
        {
            var seen = new HashSet<AppealStatus> { start };
            var queue = new Queue<AppealStatus>([start]);
            while (queue.Count > 0)
            {
                foreach (var next in AppealWorkflow.NextStatuses(queue.Dequeue()).Where(seen.Add)) queue.Enqueue(next);
            }
            Assert.Contains(seen, AppealWorkflow.Closed.Contains);
        }
    }
}

[Trait("Category", "Unit")]
public class AppealVisibilityTests
{
    private static readonly User Admin = new() { Id = 1, Role = UserRole.Administrator };
    private static readonly User Head = new() { Id = 2, Role = UserRole.AcabAdmin, AcabId = 10 };
    private static readonly User HeadWithoutAcab = new() { Id = 3, Role = UserRole.AcabAdmin };
    private static readonly User Resident = new() { Id = 4, Role = UserRole.Resident, AcabId = 10 };

    private static readonly Appeal[] Appeals =
    [
        new() { Id = 1, AuthorId = 4, AcabId = 10 },
        new() { Id = 2, AuthorId = 5, AcabId = 10 },
        new() { Id = 3, AuthorId = 6, AcabId = 20 },
        new() { Id = 4, AuthorId = 2, AcabId = null },
        new() { Id = 5, AuthorId = 3, AcabId = null }
    ];

    private static int[] Visible(User actor) => Appeals.AsQueryable().VisibleTo(actor).Select(a => a.Id).ToArray();

    [Fact]
    public void Administrator_sees_everything() => Assert.Equal([1, 2, 3, 4, 5], Visible(Admin));

    [Fact]
    public void Head_sees_the_association_and_own_appeals() => Assert.Equal([1, 2, 4], Visible(Head));

    [Fact]
    public void Head_without_association_sees_only_own_appeals() => Assert.Equal([5], Visible(HeadWithoutAcab));

    [Fact]
    public void Resident_sees_only_own_appeals_in_lists() => Assert.Equal([1], Visible(Resident));

    [Fact]
    public void Any_signed_in_user_may_open_any_appeal_card()
    {
        Assert.All(Appeals, a => Assert.True(AppealPolicy.CanView(Resident, a)));
    }

    [Fact]
    public void Denied_actions_throw_forbidden()
    {
        var foreign = new Appeal { Id = 9, AuthorId = 99, AcabId = 20, Status = AppealStatus.New };
        Assert.Throws<ForbiddenException>(() => AppealPolicy.EnsureCanEdit(Resident, foreign));
        Assert.Throws<ForbiddenException>(() => AppealPolicy.EnsureCanProcess(Head, foreign));
        AppealPolicy.EnsureCanProcess(Admin, foreign);
    }

    [Fact]
    public void Rating_window_is_inclusive_of_the_last_day()
    {
        var options = new AppealOptions();
        var closed = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var appeal = new Appeal { AuthorId = Resident.Id, Status = AppealStatus.Done, ClosedAt = closed };
        Assert.True(AppealPolicy.CanRate(Resident, appeal, closed.AddDays(options.FeedbackDays), options));
        Assert.False(AppealPolicy.CanRate(Resident, appeal, closed.AddDays(options.FeedbackDays).AddSeconds(1), options));
    }

    [Fact]
    public void Head_can_assign_only_in_own_association()
    {
        Assert.True(AppealPolicy.CanAssign(Head, Appeals[1]));
        Assert.False(AppealPolicy.CanAssign(Head, Appeals[2]));
        Assert.False(AppealPolicy.CanAssign(HeadWithoutAcab, Appeals[4]));
    }
}
