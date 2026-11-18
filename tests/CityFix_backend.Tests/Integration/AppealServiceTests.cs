using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityFix_backend.Tests.Integration;

[Trait("Category", "Integration")]
public class AppealServiceTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncLifetime
{
    private ServiceHarness _app = null!;
    private Neighbourhood _n = null!;
    private IAppealService Appeals => _app.Get<IAppealService>();
    private DateTime Now => _app.Clock.GetUtcNow().UtcDateTime;

    public async Task InitializeAsync()
    {
        _app = new ServiceHarness(database);
        _n = await _app.Data.NeighbourhoodAsync();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private AppealInput Input(string title = "Яма біля під'їзду", ResponsibleKind responsible = ResponsibleKind.Association, int? recipientId = null,
        decimal? lat = 49.421614m, decimal? lng = 26.996531m, string? address = "вул. Тестова, 1", int? categoryId = null) =>
        new(title, "  Глибока яма  ", lat, lng, address, categoryId ?? _n.Category.Id, responsible, recipientId);

    private Task<Appeal> CreateAsync(User? author = null, AppealInput? input = null, params IFormFile[] photos) =>
        Appeals.CreateAsync(input ?? Input(), photos, author ?? _n.Resident);

    private async Task<Appeal> ReloadAsync(int id)
    {
        _app.NewScope();
        return await _app.Db.Appeals.AsNoTracking().Include(a => a.Updates).Include(a => a.Files).SingleAsync(a => a.Id == id);
    }

    private Task<List<Notification>> NotificationsAsync(int appealId) =>
        _app.Db.Notifications.AsNoTracking().Where(n => n.AppealId == appealId).OrderBy(n => n.Id).ToListAsync();

    private Task ProcessAsync(int id, AppealStatus from, AppealStatus to, string? comment = "Працюємо", User? actor = null, AppealPriority? priority = null) =>
        Appeals.ProcessAsync(id, new AppealProcessInput(from, to, priority ?? AppealPriority.Normal, comment), actor ?? _n.Head);

    [Fact]
    public async Task New_appeal_is_saved_with_a_deadline_and_queued_for_translation()
    {
        var appeal = await CreateAsync();

        var stored = await ReloadAsync(appeal.Id);
        Assert.Equal((AppealStatus.New, AppealPriority.Normal, _n.Acab.Id, ResponsibleKind.Association), (stored.Status, stored.Priority, stored.AcabId, stored.ResponsibleKind));
        Assert.Equal(("Яма біля під'їзду", "Глибока яма", "uk", true), (stored.Title, stored.Description, stored.Language, stored.TranslationPending));
        Assert.Equal((49.42161m, 26.99653m), (stored.Lat, stored.Lng));
        Assert.Equal(stored.CreatedAt.AddDays(7), stored.DueAt);
        Assert.NotNull(stored.Location);
    }

    [Fact]
    public async Task Heads_of_the_association_are_notified_about_a_new_appeal()
    {
        var appeal = await CreateAsync();
        var note = Assert.Single(await NotificationsAsync(appeal.Id));
        Assert.Equal((_n.Head.Id, NotificationType.NewAppeal, DeliveryState.Pending), (note.UserId, note.Type, note.EmailState));
    }

    [Fact]
    public async Task Without_a_head_administrators_are_notified_instead()
    {
        var acab = await _app.Data.AcabAsync();
        var lonely = await _app.Data.UserAsync(acab: acab);
        var admin = await _app.Data.AdminAsync();

        var appeal = await CreateAsync(lonely);

        Assert.Contains(admin.Id, (await NotificationsAsync(appeal.Id)).Select(n => n.UserId));
    }

    [Fact]
    public async Task English_text_is_detected_as_english()
    {
        var appeal = await CreateAsync(input: Input("Broken street light near the school"));
        Assert.Equal("en", (await ReloadAsync(appeal.Id)).Language);
    }

    [Fact]
    public async Task Missing_address_is_filled_by_reverse_geocoding()
    {
        _app.Geocoding.Address = "Проскурівська 1, Хмельницький";
        var appeal = await CreateAsync(input: Input(address: "   "));
        Assert.Equal("Проскурівська 1, Хмельницький", (await ReloadAsync(appeal.Id)).Address);
        Assert.Equal(1, _app.Geocoding.ReverseCalls);
    }

    [Fact]
    public async Task Appeal_needs_a_point_an_existing_category_and_a_valid_responsible()
    {
        Assert.Equal("Validation.PointRequired", (await Assert.ThrowsAsync<ServiceValidationException>(() => CreateAsync(input: Input(lat: null)))).MessageKey);
        Assert.Equal("CategoryId", (await Assert.ThrowsAsync<ServiceValidationException>(() => CreateAsync(input: Input(categoryId: 999_999)))).Field);
        Assert.Equal("Validation.Choose", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            CreateAsync(input: Input(responsible: ResponsibleKind.Recipient, recipientId: 999_999)))).MessageKey);

        var outsider = await _app.Data.UserAsync();
        Assert.Equal("Validation.NoAcab", (await Assert.ThrowsAsync<ServiceValidationException>(() => CreateAsync(outsider))).MessageKey);
        Assert.False(await _app.Db.Appeals.AnyAsync(a => a.AuthorId == outsider.Id));
    }

    [Fact]
    public async Task Residents_without_association_send_appeals_to_city_services()
    {
        var outsider = await _app.Data.UserAsync();
        var appeal = await CreateAsync(outsider, Input(responsible: ResponsibleKind.Recipient, recipientId: _n.Recipient.Id));
        var stored = await ReloadAsync(appeal.Id);
        Assert.Equal((ResponsibleKind.Recipient, _n.Recipient.Id, (int?)null), (stored.ResponsibleKind, stored.RecipientId, stored.AcabId));
    }

    [Fact]
    public async Task Photos_are_stored_without_metadata_and_get_thumbnails()
    {
        var appeal = await CreateAsync(photos: TestFiles.FormFile(TestFiles.JpegWithSecrets(), "../../evil.jpg", "image/jpeg"));

        var file = Assert.Single((await ReloadAsync(appeal.Id)).Files);
        Assert.Equal(("evil.jpg", "image/jpeg"), (file.FileName, file.ContentType));
        Assert.StartsWith($"uploads/appeals/{appeal.Id}/", file.FilePath);
        var files = _app.Get<IFileService>();
        var bytes = await File.ReadAllBytesAsync(files.PhysicalPathOf(file));
        Assert.False(TestFiles.Contains(bytes, "SECRETCAM"));
        Assert.False(TestFiles.Contains(bytes, "GPS 49.42"));
        Assert.True(File.Exists(files.ThumbnailPathOf(file)));
    }

    [Fact]
    public async Task Invalid_photo_rejects_the_whole_appeal()
    {
        var before = await _app.Db.Appeals.CountAsync();
        await Assert.ThrowsAsync<ServiceValidationException>(() => CreateAsync(photos: TestFiles.FormFile("not an image"u8.ToArray(), "x.png")));
        Assert.Equal(before, await _app.Db.Appeals.CountAsync());
    }

    [Fact]
    public async Task Author_edits_while_new_but_not_after_work_started()
    {
        var appeal = await CreateAsync();
        await Appeals.UpdateAsync(appeal.Id, Input("Оновлена назва ями"), [], _n.Resident);
        var stored = await ReloadAsync(appeal.Id);
        Assert.Equal(("Оновлена назва ями", true), (stored.Title, stored.TranslationPending));

        await ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.InProgress);
        _app.NewScope();
        await Assert.ThrowsAsync<ForbiddenException>(() => Appeals.UpdateAsync(appeal.Id, Input(), [], _n.Resident));
        await Assert.ThrowsAsync<ForbiddenException>(() => Appeals.UpdateAsync(appeal.Id, Input(), [], _n.Neighbour));
    }

    [Fact]
    public async Task Editing_only_the_location_keeps_the_translation()
    {
        var appeal = await CreateAsync();
        await _app.Db.Appeals.Where(a => a.Id == appeal.Id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.TranslationPending, false).SetProperty(a => a.TitleTranslated, "Pothole"));
        _app.NewScope();

        await Appeals.UpdateAsync(appeal.Id, Input(lat: 49.43m, address: "Інша адреса"), [], _n.Resident);

        var stored = await ReloadAsync(appeal.Id);
        Assert.Equal(("Pothole", false, "Інша адреса", (string?)null), (stored.TitleTranslated, stored.TranslationPending, stored.Address, stored.AddressTranslated));
    }

    [Fact]
    public async Task Head_changing_the_responsible_while_editing_records_the_assignment()
    {
        var appeal = await CreateAsync();
        await Appeals.UpdateAsync(appeal.Id, Input(responsible: ResponsibleKind.Recipient, recipientId: _n.Recipient.Id), [], _n.Head);
        var update = Assert.Single((await ReloadAsync(appeal.Id)).Updates);
        Assert.Equal(_n.Recipient.Name, update.AssignedTo);
        Assert.Contains(await NotificationsAsync(appeal.Id), n => n.Type == NotificationType.Assigned && n.UserId == _n.Resident.Id);
    }

    [Fact]
    public async Task Processing_follows_the_workflow_and_writes_history()
    {
        var appeal = await CreateAsync();
        await ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.InProgress, "Бригада виїхала");
        _app.NewScope();
        await ProcessAsync(appeal.Id, AppealStatus.InProgress, AppealStatus.Done, "Яму залатано");

        var stored = await ReloadAsync(appeal.Id);
        Assert.Equal(AppealStatus.Done, stored.Status);
        Assert.Equal(Now, stored.ClosedAt);
        Assert.Equal([(AppealStatus.New, AppealStatus.InProgress, "Бригада виїхала"), (AppealStatus.InProgress, AppealStatus.Done, "Яму залатано")],
            stored.Updates.OrderBy(u => u.Id).Select(u => (u.OldStatus, u.NewStatus, u.Comment)));
        var toAuthor = (await NotificationsAsync(appeal.Id)).Where(n => n.UserId == _n.Resident.Id).ToList();
        Assert.Equal([AppealStatus.InProgress, AppealStatus.Done], toAuthor.Select(n => n.NewStatus!.Value));
    }

    [Fact]
    public async Task Disallowed_transitions_and_stale_forms_are_rejected()
    {
        var appeal = await CreateAsync();
        Assert.Equal("Validation.TransitionNotAllowed", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.Done))).MessageKey);
        Assert.Equal("Validation.ChangedMeanwhile", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            ProcessAsync(appeal.Id, AppealStatus.InProgress, AppealStatus.OnHold))).MessageKey);
    }

    [Fact]
    public async Task Closing_as_done_needs_a_comment_and_empty_changes_are_refused()
    {
        var appeal = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.InProgress);
        Assert.Equal("Validation.DoneNeedsComment", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            ProcessAsync(appeal.Id, AppealStatus.InProgress, AppealStatus.Done, "  "))).MessageKey);
        Assert.Equal("Validation.NothingChanged", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            ProcessAsync(appeal.Id, AppealStatus.InProgress, AppealStatus.InProgress, null))).MessageKey);
        Assert.Equal("Validation.MaxLength", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            ProcessAsync(appeal.Id, AppealStatus.InProgress, AppealStatus.OnHold, new string('x', 1001)))).MessageKey);
    }

    [Fact]
    public async Task Comment_without_status_change_reaches_the_author()
    {
        var appeal = await CreateAsync();
        await ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.New, "Уточніть, будь ласка, під'їзд");
        var note = (await NotificationsAsync(appeal.Id)).Single(n => n.UserId == _n.Resident.Id);
        Assert.Equal((NotificationType.StatusChanged, (AppealStatus?)null, "Уточніть, будь ласка, під'їзд"), (note.Type, note.NewStatus, note.Message));
    }

    [Fact]
    public async Task Raising_the_priority_moves_the_deadline_from_the_original_start()
    {
        var appeal = await CreateAsync();
        _app.Clock.Advance(TimeSpan.FromDays(1));
        await ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.New, null, priority: AppealPriority.Urgent);

        var stored = await ReloadAsync(appeal.Id);
        Assert.Equal((AppealPriority.Urgent, appeal.CreatedAt.AddDays(2)), (stored.Priority, stored.DueAt));
        var update = Assert.Single(stored.Updates);
        Assert.Equal((AppealPriority.Normal, AppealPriority.Urgent), (update.OldPriority, update.NewPriority));
    }

    [Fact]
    public async Task Only_heads_of_the_association_and_administrators_process()
    {
        var appeal = await CreateAsync();
        var otherHead = await _app.Data.HeadAsync(await _app.Data.AcabAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.InProgress, actor: _n.Resident));
        await Assert.ThrowsAsync<ForbiddenException>(() => ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.InProgress, actor: otherHead));
        await ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.InProgress, actor: await _app.Data.AdminAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => ProcessAsync(999_999, AppealStatus.New, AppealStatus.InProgress));
    }

    [Fact]
    public async Task Concurrent_processing_of_the_same_appeal_lets_only_one_win()
    {
        var appeal = await CreateAsync();
        var admin = await _app.Data.AdminAsync();

        using var first = _app.Root.CreateScope();
        using var second = _app.Root.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<IAppealService>();
        var b = second.ServiceProvider.GetRequiredService<IAppealService>();
        var firstUser = await first.ServiceProvider.GetRequiredService<IUserService>().GetActorAsync(_n.Head.Id);
        var secondUser = await second.ServiceProvider.GetRequiredService<IUserService>().GetActorAsync(admin.Id);

        var results = await Task.WhenAll(
            Capture(() => a.ProcessAsync(appeal.Id, new AppealProcessInput(AppealStatus.New, AppealStatus.InProgress, AppealPriority.Normal, "A"), firstUser)),
            Capture(() => b.ProcessAsync(appeal.Id, new AppealProcessInput(AppealStatus.New, AppealStatus.Rejected, AppealPriority.Normal, "B"), secondUser)));

        Assert.Single(results, r => r == null);
        Assert.Equal("Validation.ChangedMeanwhile", Assert.Single(results, r => r != null)!.MessageKey);
        Assert.Single((await ReloadAsync(appeal.Id)).Updates);

        static async Task<ServiceValidationException?> Capture(Func<Task> run)
        {
            try { await run(); return null; }
            catch (ServiceValidationException ex) { return ex; }
        }
    }

    [Fact]
    public async Task Author_rates_a_done_appeal_once_and_heads_hear_about_it()
    {
        var appeal = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.Done);
        await Appeals.RateAsync(appeal.Id, new AppealRatingInput(5, " Дякую! "), _n.Resident);

        var stored = await ReloadAsync(appeal.Id);
        Assert.Equal((5, "Дякую!"), (stored.Rating, stored.RatingComment));
        Assert.Equal(5, Assert.Single(stored.Updates).Rating);
        Assert.Contains(await NotificationsAsync(appeal.Id), n => n.Type == NotificationType.Rated && n.UserId == _n.Head.Id);

        _app.NewScope();
        Assert.Equal("Validation.RatingClosed", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Appeals.RateAsync(appeal.Id, new AppealRatingInput(1, null), _n.Resident))).MessageKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rating_must_be_between_one_and_five(int rating)
    {
        var appeal = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.Done);
        Assert.Equal("Validation.Choose", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Appeals.RateAsync(appeal.Id, new AppealRatingInput(rating, null), _n.Resident))).MessageKey);
    }

    [Fact]
    public async Task Others_cannot_rate_and_late_ratings_are_refused()
    {
        var appeal = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.Done);
        await Assert.ThrowsAsync<ServiceValidationException>(() => Appeals.RateAsync(appeal.Id, new AppealRatingInput(1, null), _n.Neighbour));
        _app.Clock.Advance(TimeSpan.FromDays(15));
        await Assert.ThrowsAsync<ServiceValidationException>(() => Appeals.RateAsync(appeal.Id, new AppealRatingInput(1, null), _n.Resident));
    }

    [Fact]
    public async Task Reopening_restarts_the_work_with_a_new_deadline()
    {
        var appeal = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.Done, priority: AppealPriority.Urgent);
        _app.Clock.Advance(TimeSpan.FromDays(1));

        await Appeals.ReopenAsync(appeal.Id, "Яма знову з'явилась", _n.Resident);

        var stored = await ReloadAsync(appeal.Id);
        Assert.Equal((AppealStatus.InProgress, 1, (DateTime?)null, Now.AddDays(2)), (stored.Status, stored.ReopenCount, stored.ClosedAt, stored.DueAt));
        Assert.Contains(await NotificationsAsync(appeal.Id), n => n.Type == NotificationType.Reopened && n.UserId == _n.Head.Id);
    }

    [Fact]
    public async Task Reopening_needs_a_reason_and_is_limited()
    {
        var appeal = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.Done);
        Assert.Equal("Validation.ReopenNeedsComment", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Appeals.ReopenAsync(appeal.Id, " ", _n.Resident))).MessageKey);

        await _app.Db.Appeals.Where(a => a.Id == appeal.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.ReopenCount, 3));
        _app.NewScope();
        Assert.Equal("Validation.ReopenClosed", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Appeals.ReopenAsync(appeal.Id, "Знову", _n.Resident))).MessageKey);
    }

    [Fact]
    public async Task Neighbours_toggle_support_and_get_status_updates()
    {
        var appeal = await CreateAsync();

        Assert.Equal(new SupportResult(true, 1), await Appeals.ToggleSupportAsync(appeal.Id, _n.Neighbour));
        Assert.Equal(1, (await ReloadAsync(appeal.Id)).SupportCount);

        await ProcessAsync(appeal.Id, AppealStatus.New, AppealStatus.InProgress);
        Assert.Contains(await NotificationsAsync(appeal.Id), n => n.UserId == _n.Neighbour.Id && n.NewStatus == AppealStatus.InProgress);

        _app.NewScope();
        Assert.Equal(new SupportResult(false, 0), await Appeals.ToggleSupportAsync(appeal.Id, _n.Neighbour));
    }

    [Fact]
    public async Task Authors_staff_and_closed_appeals_cannot_be_supported()
    {
        var appeal = await CreateAsync();
        await Assert.ThrowsAsync<ServiceValidationException>(() => Appeals.ToggleSupportAsync(appeal.Id, _n.Resident));
        await Assert.ThrowsAsync<ServiceValidationException>(() => Appeals.ToggleSupportAsync(appeal.Id, _n.Head));
        var closed = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.Rejected);
        await Assert.ThrowsAsync<ServiceValidationException>(() => Appeals.ToggleSupportAsync(closed.Id, _n.Neighbour));
    }

    [Fact]
    public async Task Support_count_stays_right_under_parallel_clicks()
    {
        var appeal = await CreateAsync();
        var supporters = new List<User>();
        for (var i = 0; i < 8; i++) supporters.Add(await _app.Data.UserAsync(acab: _n.Acab));

        await Task.WhenAll(supporters.Select(u => _app.InScopeAsync(sp => sp.GetRequiredService<IAppealService>().ToggleSupportAsync(appeal.Id, u))));

        Assert.Equal(8, (await ReloadAsync(appeal.Id)).SupportCount);
        Assert.Equal(8, await _app.Db.AppealSupports.CountAsync(s => s.AppealId == appeal.Id));
    }

    [Fact]
    public async Task Similar_appeals_are_found_nearby_in_the_same_category()
    {
        var category = await _app.Data.CategoryAsync();
        var near = await _app.Data.AppealAsync(_n.Neighbour, category, lat: TestData.Lat + 0.001);
        var far = await _app.Data.AppealAsync(_n.Neighbour, category, lat: TestData.Lat + 0.01);
        var otherCategory = await _app.Data.AppealAsync(_n.Neighbour, await _app.Data.CategoryAsync(), lat: TestData.Lat);
        var closed = await _app.Data.AppealAsync(_n.Neighbour, category, AppealStatus.Done, lat: TestData.Lat);

        var similar = await Appeals.FindSimilarAsync(TestData.Lat, TestData.Lng, category.Id, null, _n.Resident);

        var hit = Assert.Single(similar);
        Assert.Equal(near.Id, hit.Id);
        Assert.InRange(hit.DistanceMeters, 100, 120);
        Assert.True(hit.CanSupport);
        Assert.DoesNotContain(similar, s => s.Id == far.Id || s.Id == otherCategory.Id || s.Id == closed.Id);
    }

    [Fact]
    public async Task Appeal_card_shows_similar_appeals_and_what_the_viewer_may_do()
    {
        var category = await _app.Data.CategoryAsync();
        var older = await _app.Data.AppealAsync(_n.Neighbour, category, createdAt: Now.AddDays(-1));
        var appeal = await _app.Data.AppealAsync(_n.Resident, category);

        var mine = await Appeals.GetDetailsAsync(appeal.Id, _n.Resident);
        Assert.Equal(older.Id, Assert.Single(mine.Similar).Id);
        Assert.Equal((true, true, false, false), (mine.CanEdit, mine.CanDelete, mine.CanProcess, mine.CanSupport));

        var head = await Appeals.GetDetailsAsync(appeal.Id, _n.Head);
        Assert.Equal((true, true), (head.CanProcess, head.CanAssign));
        Assert.Contains(AppealStatus.InProgress, head.NextStatuses);

        var neighbour = await Appeals.GetDetailsAsync(appeal.Id, _n.Neighbour);
        Assert.Equal((false, true), (neighbour.CanEdit, neighbour.CanSupport));
        await Assert.ThrowsAsync<NotFoundException>(() => Appeals.GetDetailsAsync(999_999, _n.Resident));
    }

    [Fact]
    public async Task List_respects_visibility_tabs_search_and_filters()
    {
        var category = await _app.Data.CategoryAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var mineNew = await _app.Data.AppealAsync(_n.Resident, category, title: $"Ліхтар {marker}");
        var mineDone = await _app.Data.AppealAsync(_n.Resident, category, AppealStatus.Done, title: $"Сміття {marker}");
        var neighbours = await _app.Data.AppealAsync(_n.Neighbour, category, AppealStatus.InProgress, title: $"Лавка {marker}");

        async Task<int[]> Ids(AppealFilter filter, User actor) =>
            (await Appeals.GetFilteredAsync(filter, actor)).Page.Items.Select(a => a.Id).Order().ToArray();

        Assert.Equal([mineNew.Id, mineDone.Id], await Ids(new AppealFilter { CategoryId = category.Id }, _n.Resident));
        Assert.Equal([mineNew.Id, mineDone.Id, neighbours.Id], await Ids(new AppealFilter { CategoryId = category.Id }, _n.Head));
        Assert.Equal([mineNew.Id, mineDone.Id, neighbours.Id], await Ids(new AppealFilter { CategoryId = category.Id, Community = true }, _n.Resident));
        Assert.Equal([mineNew.Id], await Ids(new AppealFilter { CategoryId = category.Id, Tab = AppealFilter.TabNew }, _n.Head));
        Assert.Equal([neighbours.Id], await Ids(new AppealFilter { CategoryId = category.Id, Tab = AppealFilter.TabInProgress }, _n.Head));
        Assert.Equal([mineDone.Id], await Ids(new AppealFilter { CategoryId = category.Id, Tab = AppealFilter.TabClosed }, _n.Head));
        Assert.Equal([mineDone.Id], await Ids(new AppealFilter { Q = $"сМІТТЯ {marker}" }, _n.Head));
        Assert.Empty(await Ids(new AppealFilter { CategoryId = category.Id, MineOnly = true }, _n.Head));
        Assert.Empty(await Ids(new AppealFilter { Q = "%" + marker }, _n.Head));
        Assert.Empty(await Ids(new AppealFilter { CategoryId = category.Id, From = Now.AddDays(2) }, _n.Head));
        Assert.Equal(3, (await Ids(new AppealFilter { CategoryId = category.Id, From = Now.AddDays(-1), To = Now, Responsible = "acab" }, _n.Head)).Length);
    }

    [Fact]
    public async Task List_flags_newer_appeals_at_the_same_place_as_possible_duplicates()
    {
        var category = await _app.Data.CategoryAsync();
        var original = await _app.Data.AppealAsync(_n.Neighbour, category, createdAt: Now.AddHours(-2));
        var copy = await _app.Data.AppealAsync(_n.Resident, category, lat: TestData.Lat + 0.0005);

        var page = await Appeals.GetFilteredAsync(new AppealFilter { CategoryId = category.Id }, _n.Head);

        Assert.Contains(copy.Id, page.PossibleDuplicates);
        Assert.DoesNotContain(original.Id, page.PossibleDuplicates);
    }

    [Fact]
    public async Task Export_returns_readable_rows_for_staff()
    {
        using var _ = new UiCulture("uk");
        var category = await _app.Data.CategoryAsync("Освітлення", "Lighting");
        var appeal = await _app.Data.AppealAsync(_n.Resident, category, title: "Не світить ліхтар");

        var row = Assert.Single(await Appeals.ExportAsync(new AppealFilter { CategoryId = category.Id }, _n.Head));
        Assert.Equal((appeal.Id, "Не світить ліхтар", "Освітлення", _n.Acab.Name), (row.Id, row.Title, row.Category, row.Responsible));
        Assert.Equal("Олена Шевченко", row.Author);
    }

    [Fact]
    public async Task Overdue_appeals_are_reported_to_heads_once()
    {
        var late = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.InProgress, dueAt: Now.AddHours(-1));
        var closed = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.Done, dueAt: Now.AddHours(-1));

        Assert.True(await Appeals.NotifyOverdueAsync() >= 1);
        _app.NewScope();
        await Appeals.NotifyOverdueAsync();

        Assert.NotNull((await ReloadAsync(late.Id)).OverdueNotifiedAt);
        Assert.Single(await NotificationsAsync(late.Id), n => n.Type == NotificationType.Overdue && n.UserId == _n.Head.Id);
        Assert.Empty(await NotificationsAsync(closed.Id));
    }

    [Fact]
    public async Task Reassigning_notifies_the_author_and_refuses_closed_or_unchanged_appeals()
    {
        var appeal = await CreateAsync();
        await Appeals.AssignAsync(appeal.Id, ResponsibleKind.Recipient, _n.Recipient.Id, _n.Head);
        Assert.Contains(await NotificationsAsync(appeal.Id), n => n.Type == NotificationType.Assigned && n.UserId == _n.Resident.Id && n.Message == _n.Recipient.Name);

        _app.NewScope();
        Assert.Equal("Validation.NothingChanged", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Appeals.AssignAsync(appeal.Id, ResponsibleKind.Recipient, _n.Recipient.Id, _n.Head))).MessageKey);

        var closed = await _app.Data.AppealAsync(_n.Resident, _n.Category, AppealStatus.Done);
        Assert.Equal("Validation.AppealClosed", (await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Appeals.AssignAsync(closed.Id, ResponsibleKind.Recipient, _n.Recipient.Id, _n.Head))).MessageKey);
    }

    [Fact]
    public async Task Deleting_removes_the_appeal_and_its_photos()
    {
        var appeal = await CreateAsync(photos: TestFiles.FormFile(TestFiles.Image()));
        var file = Assert.Single((await ReloadAsync(appeal.Id)).Files);
        var path = _app.Get<IFileService>().PhysicalPathOf(file);
        Assert.True(File.Exists(path));

        await Assert.ThrowsAsync<ForbiddenException>(() => Appeals.DeleteAsync(appeal.Id, _n.Neighbour));
        await Appeals.DeleteAsync(appeal.Id, _n.Resident);

        Assert.False(await _app.Db.Appeals.AnyAsync(a => a.Id == appeal.Id));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Single_photo_can_be_removed_by_the_editor_only()
    {
        var appeal = await CreateAsync(photos: [TestFiles.FormFile(TestFiles.Image()), TestFiles.FormFile(TestFiles.Image(30, 30))]);
        var files = (await ReloadAsync(appeal.Id)).Files.OrderBy(f => f.Id).ToList();

        await Assert.ThrowsAsync<ForbiddenException>(() => Appeals.DeleteFileAsync(files[0].Id, _n.Neighbour));
        Assert.Equal(appeal.Id, await Appeals.DeleteFileAsync(files[0].Id, _n.Resident));

        Assert.Equal(files[1].Id, Assert.Single((await ReloadAsync(appeal.Id)).Files).Id);
        Assert.False(File.Exists(_app.Get<IFileService>().PhysicalPathOf(files[0])));
        Assert.Equal(files[1].Id, (await Appeals.GetFileAsync(files[1].Id, _n.Neighbour)).Id);
    }

    [Fact]
    public async Task Photo_limit_counts_photos_already_attached()
    {
        var appeal = await CreateAsync(photos: Enumerable.Range(0, 5).Select(_ => TestFiles.FormFile(TestFiles.Image())).ToArray());
        await Assert.ThrowsAsync<ServiceValidationException>(() => Appeals.UpdateAsync(appeal.Id, Input(), [TestFiles.FormFile(TestFiles.Image())], _n.Resident));
    }

    [Fact]
    public async Task Missing_thumbnails_are_rebuilt_for_old_photos()
    {
        var appeal = await CreateAsync(photos: TestFiles.FormFile(TestFiles.Image(800, 600)));
        var file = Assert.Single((await ReloadAsync(appeal.Id)).Files);
        var files = _app.Get<IFileService>();
        File.Delete(files.ThumbnailPathOf(file)!);
        await _app.Db.AppealFiles.Where(f => f.Id == file.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.ThumbnailPath, (string?)null));
        _app.NewScope();

        Assert.True(await _app.Get<IFileService>().CreateMissingThumbnailsAsync() >= 1);

        var rebuilt = Assert.Single((await ReloadAsync(appeal.Id)).Files);
        Assert.True(File.Exists(_app.Get<IFileService>().ThumbnailPathOf(rebuilt)));
    }

    [Fact]
    public async Task Stats_split_open_and_closed_appeals_visible_to_the_actor()
    {
        var author = await _app.Data.UserAsync();
        var category = await _app.Data.CategoryAsync();
        await _app.Data.AppealAsync(author, category);
        await _app.Data.AppealAsync(author, category, AppealStatus.OnHold);
        await _app.Data.AppealAsync(author, category, AppealStatus.Duplicate);
        Assert.Equal(new AppealStats(2, 1), await Appeals.GetStatsAsync(author));
    }
}
