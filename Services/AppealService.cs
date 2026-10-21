using CityFix_backend.Data;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace CityFix_backend.Services
{
    public class AppealService : IAppealService
    {
        private const int ExportLimit = 10_000;

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IFileService _files;
        private readonly IGeocodingService _geocoding;
        private readonly AppealOptions _options;
        private readonly TimeProvider _clock;

        public AppealService(ApplicationDbContext context, INotificationService notifications, IFileService files,
            IGeocodingService geocoding, IOptions<AppealOptions> options, TimeProvider clock)
        {
            _context = context;
            _notifications = notifications;
            _files = files;
            _geocoding = geocoding;
            _options = options.Value;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // ───── Reading ─────

        public async Task<AppealPage> GetFilteredAsync(AppealFilter filter, User actor)
        {
            var page = await Filtered(filter, actor)
                .Include(a => a.Author)
                .Include(a => a.Category)
                .Include(a => a.Recipient)
                .Include(a => a.Acab)
                .Include(a => a.Files.OrderBy(f => f.Id))
                .AsSplitQuery()
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .ToPagedResultAsync(filter.Page);

            return new AppealPage(page, await PossibleDuplicatesAsync(page.Items.Select(a => a.Id).ToList()));
        }

        public async Task<AppealDetails> GetDetailsAsync(int id, User actor)
        {
            var appeal = await _context.Appeals.AsNoTracking()
                .Include(a => a.Author)
                .Include(a => a.Category)
                .Include(a => a.Recipient)
                .Include(a => a.Acab)
                .Include(a => a.Files.OrderBy(f => f.Id))
                .Include(a => a.Updates.OrderBy(u => u.ChangedAt)).ThenInclude(u => u.ChangedBy)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new NotFoundException($"Appeal {id} does not exist.");
            AppealPolicy.EnsureCanView(actor, appeal);

            var similar = appeal.Lat != null && appeal.Lng != null
                ? await SimilarAsync((double)appeal.Lat, (double)appeal.Lng, appeal.CategoryId, appeal.Id, openOnly: false, actor)
                : Array.Empty<SimilarAppeal>();

            var canProcess = AppealPolicy.CanProcess(actor, appeal);
            return new AppealDetails(
                appeal,
                similar,
                AppealPolicy.CanEdit(actor, appeal),
                AppealPolicy.CanDelete(actor, appeal),
                canProcess,
                canProcess && !AppealWorkflow.IsFinal(appeal.Status),
                AppealWorkflow.NextStatuses(appeal.Status));
        }

        public async Task<Appeal> GetForEditAsync(int id, User actor)
        {
            var appeal = await _context.Appeals.AsNoTracking()
                .Include(a => a.Files.OrderBy(f => f.Id))
                .FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new NotFoundException($"Appeal {id} does not exist.");
            AppealPolicy.EnsureCanEdit(actor, appeal);
            return appeal;
        }

        public async Task<AppealStats> GetStatsAsync(User actor)
        {
            var counts = await _context.Appeals.VisibleTo(actor)
                .GroupBy(a => AppealWorkflow.Closed.Contains(a.Status))
                .Select(g => new { Closed = g.Key, Count = g.Count() })
                .ToListAsync();
            return new AppealStats(
                counts.Where(c => !c.Closed).Sum(c => c.Count),
                counts.Where(c => c.Closed).Sum(c => c.Count));
        }

        public Task<IReadOnlyList<SimilarAppeal>> FindSimilarAsync(double lat, double lng, int categoryId, int? excludeId, User actor) =>
            SimilarAsync(lat, lng, categoryId, excludeId, openOnly: true, actor);

        public async Task<IReadOnlyList<AppealExportRow>> ExportAsync(AppealFilter filter, User actor)
        {
            var rows = await Filtered(filter, actor)
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .Take(ExportLimit)
                .Select(a => new
                {
                    a.Id, a.Title, a.Status, a.Priority, Category = a.Category!.Name, CategoryEn = a.Category.NameEn, a.ResponsibleKind,
                    Recipient = a.Recipient != null ? a.Recipient.Name : null,
                    RecipientEn = a.Recipient != null ? a.Recipient.NameEn : null,
                    Acab = a.Acab != null ? a.Acab.Name : null,
                    AcabEn = a.Acab != null ? a.Acab.NameEn : null,
                    a.Language, a.TitleTranslated, a.AddressTranslated,
                    a.Author!.FirstName, a.Author.LastName, a.Author.FullNameTranslated, a.Address, a.Lat, a.Lng, a.CreatedAt
                })
                .ToListAsync();

            // Everything in the current UI language, like the list it was exported from.
            return rows.Select(r =>
            {
                var shown = new Appeal { Title = r.Title, Language = r.Language, TitleTranslated = r.TitleTranslated, Address = r.Address, AddressTranslated = r.AddressTranslated };
                var author = new User { FirstName = r.FirstName, LastName = r.LastName, FullNameTranslated = r.FullNameTranslated };
                var acab = r.Acab == null ? null : Localized.Pick(r.Acab, r.AcabEn);
                return new AppealExportRow(
                    r.Id, shown.DisplayTitle(), r.Status.GetDisplayName(), r.Priority.GetDisplayName(), Localized.Pick(r.Category, r.CategoryEn),
                    (r.ResponsibleKind == ResponsibleKind.Association ? acab : r.Recipient == null ? null : Localized.Pick(r.Recipient, r.RecipientEn)) ?? SharedText.Get("Appeal.NoResponsible"),
                    acab, author.DisplayName(), shown.DisplayAddress(), r.Lat, r.Lng,
                    r.CreatedAt.ToKyiv().ToString("yyyy-MM-dd HH:mm"));
            }).ToList();
        }

        // ───── Changing ─────

        public async Task<Appeal> CreateAsync(AppealInput input, IReadOnlyCollection<IFormFile> photos, User actor)
        {
            var appeal = new Appeal
            {
                AuthorId = actor.Id,
                AcabId = actor.AcabId,
                Status = AppealStatus.New,
                Priority = AppealPriority.Normal,
                CreatedAt = Now
            };
            appeal.UpdatedAt = appeal.CreatedAt;
            await ApplyInputAsync(appeal, input);
            _files.ValidatePhotos(photos);

            // The appeal, its photo rows and the heads' notifications are committed together;
            // if anything fails, the photo files already written are removed again.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var written = new List<string>();
            try
            {
                _context.Appeals.Add(appeal);
                await _context.SaveChangesAsync();

                written.AddRange(await _files.WriteAppealPhotosAsync(appeal, photos));
                await _notifications.AddNewAppealAsync(appeal);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                _files.DeleteFiles(written);
                throw;
            }
            return appeal;
        }

        public async Task UpdateAsync(int id, AppealInput input, IReadOnlyCollection<IFormFile> photos, User actor)
        {
            var appeal = await _context.Appeals
                .Include(a => a.Acab)
                .Include(a => a.Recipient)
                .FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new NotFoundException($"Appeal {id} does not exist.");
            AppealPolicy.EnsureCanEdit(actor, appeal);

            var oldResponsible = (appeal.ResponsibleKind, appeal.RecipientId);
            await ApplyInputAsync(appeal, input);
            _files.ValidatePhotos(photos, await _context.AppealFiles.CountAsync(f => f.AppealId == id));
            appeal.UpdatedAt = Now;

            // Staff editing the form can also move the appeal to someone else: that is an assignment.
            if (actor.Id != appeal.AuthorId && oldResponsible != (appeal.ResponsibleKind, appeal.RecipientId))
            {
                await NoteAssignmentAsync(appeal, actor);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var written = new List<string>();
            try
            {
                written.AddRange(await _files.WriteAppealPhotosAsync(appeal, photos));
                await SaveOrConflictAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                _files.DeleteFiles(written);
                throw;
            }
        }

        public async Task ProcessAsync(int id, AppealProcessInput input, User actor)
        {
            var appeal = await _context.Appeals.FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new NotFoundException($"Appeal {id} does not exist.");
            AppealPolicy.EnsureCanProcess(actor, appeal);

            // Someone else changed the status after this staff member opened the card.
            if (appeal.Status != input.ExpectedStatus)
            {
                throw new ServiceValidationException("Status", "Validation.ChangedMeanwhile");
            }
            if (!AppealWorkflow.CanMove(appeal.Status, input.NewStatus))
            {
                throw new ServiceValidationException("Status", "Validation.TransitionNotAllowed",
                    appeal.Status.GetDisplayName(), input.NewStatus.GetDisplayName());
            }

            var comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim();
            var statusChanged = input.NewStatus != appeal.Status;
            var priorityChanged = input.NewPriority != appeal.Priority && Enum.IsDefined(input.NewPriority);

            // Business rule: closing an appeal as Done must say what was done.
            if (statusChanged && input.NewStatus == AppealStatus.Done && comment == null)
            {
                throw new ServiceValidationException("Comment", "Validation.DoneNeedsComment");
            }
            if (!statusChanged && !priorityChanged && comment == null)
            {
                throw new ServiceValidationException("Status", "Validation.NothingChanged");
            }

            var now = Now;
            _context.AppealUpdates.Add(new AppealUpdate
            {
                AppealId = appeal.Id,
                OldStatus = appeal.Status,
                NewStatus = input.NewStatus,
                OldPriority = priorityChanged ? appeal.Priority : null,
                NewPriority = priorityChanged ? input.NewPriority : null,
                ChangedById = actor.Id,
                Comment = comment,
                ChangedAt = now
            });
            appeal.Status = input.NewStatus;
            if (priorityChanged) appeal.Priority = input.NewPriority;
            appeal.UpdatedAt = now;

            if (appeal.AuthorId != actor.Id && (statusChanged || comment != null))
            {
                // A comment without a status change is sent as a comment notification (no new status).
                await _notifications.AddStatusChangedAsync(appeal, statusChanged ? input.NewStatus : null, comment);
            }

            // The new status, its history entry and the notification are saved in one transaction.
            await SaveOrConflictAsync();
        }

        public async Task AssignAsync(int id, ResponsibleKind responsible, int? recipientId, User actor)
        {
            var appeal = await _context.Appeals
                .Include(a => a.Acab)
                .Include(a => a.Recipient)
                .FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new NotFoundException($"Appeal {id} does not exist.");
            AppealPolicy.EnsureCanProcess(actor, appeal);

            if (AppealWorkflow.IsFinal(appeal.Status))
            {
                throw new ServiceValidationException("Responsible", "Validation.AppealClosed");
            }

            var before = (appeal.ResponsibleKind, appeal.RecipientId);
            await SetResponsibleAsync(appeal, responsible, recipientId);
            if (before == (appeal.ResponsibleKind, appeal.RecipientId))
            {
                throw new ServiceValidationException("Responsible", "Validation.NothingChanged");
            }

            appeal.UpdatedAt = Now;
            await NoteAssignmentAsync(appeal, actor);
            await SaveOrConflictAsync();
        }

        public async Task DeleteAsync(int id, User actor)
        {
            var appeal = await _context.Appeals.FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new NotFoundException($"Appeal {id} does not exist.");
            if (!AppealPolicy.CanDelete(actor, appeal))
            {
                throw new ForbiddenException($"User {actor.Id} may not delete appeal {id}.");
            }

            _context.Appeals.Remove(appeal);
            await _context.SaveChangesAsync();
            _files.DeleteAppealFolder(id);
        }

        // ───── Helpers ─────

        private IQueryable<Appeal> Filtered(AppealFilter filter, User actor)
        {
            var query = _context.Appeals.AsNoTracking().VisibleTo(actor);

            if (filter.MineOnly)
            {
                query = query.Where(a => a.AuthorId == actor.Id);
            }
            if (!string.IsNullOrWhiteSpace(filter.Q))
            {
                // Case-insensitive "contains" on title, description and address (original and translation); % and _ typed by the user are literal.
                var pattern = Search.ContainsPattern(filter.Q);
                query = query.Where(a => EF.Functions.ILike(a.Title, pattern, "\\")
                    || (a.TitleTranslated != null && EF.Functions.ILike(a.TitleTranslated, pattern, "\\"))
                    || (a.DescriptionTranslated != null && EF.Functions.ILike(a.DescriptionTranslated, pattern, "\\"))
                    || (a.Description != null && EF.Functions.ILike(a.Description, pattern, "\\"))
                    || (a.Address != null && EF.Functions.ILike(a.Address, pattern, "\\")));
            }
            if (filter.From != null)
            {
                var fromUtc = UiFormat.KyivDayStartUtc(filter.From.Value);
                query = query.Where(a => a.CreatedAt >= fromUtc);
            }
            if (filter.To != null)
            {
                var toUtc = UiFormat.KyivDayStartUtc(filter.To.Value.AddDays(1));
                query = query.Where(a => a.CreatedAt < toUtc);
            }
            if (filter.CategoryId != null)
            {
                query = query.Where(a => a.CategoryId == filter.CategoryId);
            }
            if (filter.ResponsibleIsAcab)
            {
                query = query.Where(a => a.ResponsibleKind == ResponsibleKind.Association);
            }
            else if (filter.ResponsibleRecipientId is int recipientId)
            {
                query = query.Where(a => a.ResponsibleKind == ResponsibleKind.Recipient && a.RecipientId == recipientId);
            }
            if (filter.AcabId != null)
            {
                query = query.Where(a => a.AcabId == filter.AcabId);
            }
            if (filter.Priority != null)
            {
                query = query.Where(a => a.Priority == filter.Priority);
            }

            return filter.CurrentTab switch
            {
                AppealFilter.TabNew => query.Where(a => a.Status == AppealStatus.New),
                AppealFilter.TabInProgress => query.Where(a => AppealWorkflow.Active.Contains(a.Status)),
                AppealFilter.TabClosed => query.Where(a => AppealWorkflow.Closed.Contains(a.Status)),
                _ => query
            };
        }

        // Open appeals on the page that have an earlier open appeal of the same category nearby.
        private async Task<IReadOnlySet<int>> PossibleDuplicatesAsync(List<int> ids)
        {
            if (ids.Count == 0) return new HashSet<int>();

            var radius = _options.SimilarRadiusMeters;
            var found = await _context.Appeals.AsNoTracking()
                .Where(a => ids.Contains(a.Id) && a.Location != null
                    && AppealWorkflow.Open.Contains(a.Status))
                .Where(a => _context.Appeals.Any(o =>
                    o.Id != a.Id && o.CategoryId == a.CategoryId && o.CreatedAt < a.CreatedAt
                    && AppealWorkflow.Open.Contains(o.Status)
                    && o.Location != null && o.Location.IsWithinDistance(a.Location!, radius)))
                .Select(a => a.Id)
                .ToListAsync();
            return found.ToHashSet();
        }

        private async Task<IReadOnlyList<SimilarAppeal>> SimilarAsync(double lat, double lng, int categoryId, int? excludeId, bool openOnly, User actor)
        {
            var point = new Point(lng, lat) { SRID = 4326 };
            var radius = _options.SimilarRadiusMeters;

            var rows = await _context.Appeals.AsNoTracking()
                .Where(a => a.CategoryId == categoryId && a.Location != null && a.Id != (excludeId ?? 0))
                .Where(a => !openOnly || AppealWorkflow.Open.Contains(a.Status))
                .Where(a => a.Location!.IsWithinDistance(point, radius))
                .OrderBy(a => a.Location!.Distance(point))
                .Take(_options.SimilarLimit)
                .Select(a => new { a.Id, a.Title, a.Language, a.TitleTranslated, a.Status, a.CreatedAt, a.AuthorId, a.AcabId, Distance = a.Location!.Distance(point) })
                .ToListAsync();

            return rows.Select(r => new SimilarAppeal(
                r.Id, new Appeal { Title = r.Title, Language = r.Language, TitleTranslated = r.TitleTranslated }.DisplayTitle(),
                r.Status, r.CreatedAt, (int)Math.Round(r.Distance),
                AppealPolicy.CanView(actor, new Appeal { Id = r.Id, AuthorId = r.AuthorId, AcabId = r.AcabId }))).ToList();
        }

        // Copies and checks the form fields. Status, author and dates are never taken from the form.
        private async Task ApplyInputAsync(Appeal appeal, AppealInput input)
        {
            var title = input.Title.Trim();
            var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            if (appeal.Id == 0 || title != appeal.Title || description != appeal.Description)
            {
                // New or changed text: translated again in the background (TranslationWorker); the original shows meanwhile.
                appeal.Language = TextLanguage.Detect($"{title} {description}");
                appeal.TitleTranslated = appeal.DescriptionTranslated = null;
                appeal.TranslationPending = true;
                appeal.TranslationAttempts = 0;
            }
            appeal.Title = title;
            appeal.Description = description;
            appeal.CategoryId = input.CategoryId;

            if (!await _context.Categories.AnyAsync(c => c.Id == input.CategoryId))
            {
                throw new ServiceValidationException("CategoryId", "Validation.Choose");
            }

            // Every appeal has a point on the map: the 500 m search and the duplicate warning need it.
            if (input.Lat == null || input.Lng == null)
            {
                throw new ServiceValidationException("Address", "Validation.PointRequired");
            }
            var lat = Math.Round(input.Lat.Value, 5);
            var lng = Math.Round(input.Lng.Value, 5);
            appeal.Lat = lat;
            appeal.Lng = lng;

            // The map script fills the address from the point; the user may type it when geocoding is
            // unavailable. Still empty (no JS, or the lookup failed in the browser): try once more here.
            var address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();
            address ??= await _geocoding.ReverseAsync((double)lat, (double)lng);
            if (address != appeal.Address)
            {
                // Translated again in the background (TranslationWorker).
                appeal.Address = address;
                appeal.AddressTranslated = null;
            }

            await SetResponsibleAsync(appeal, input.Responsible, input.RecipientId);
        }

        private async Task SetResponsibleAsync(Appeal appeal, ResponsibleKind kind, int? recipientId)
        {
            switch (kind)
            {
                case ResponsibleKind.Association when appeal.AcabId != null:
                    appeal.ResponsibleKind = ResponsibleKind.Association;
                    appeal.RecipientId = null;
                    appeal.Recipient = null;
                    break;
                case ResponsibleKind.Association:
                    throw new ServiceValidationException("Responsible", "Validation.NoAcab");
                case ResponsibleKind.Recipient when recipientId != null && await _context.Recipients.AnyAsync(r => r.Id == recipientId):
                    appeal.ResponsibleKind = ResponsibleKind.Recipient;
                    if (appeal.RecipientId != recipientId)
                    {
                        appeal.RecipientId = recipientId;
                        appeal.Recipient = null;
                    }
                    break;
                default:
                    throw new ServiceValidationException("Responsible", "Validation.Choose");
            }
        }

        // History entry and notifications for a new responsible party.
        private async Task NoteAssignmentAsync(Appeal appeal, User actor)
        {
            var (name, nameEn) = await ResponsibleNameAsync(appeal);
            _context.AppealUpdates.Add(new AppealUpdate
            {
                AppealId = appeal.Id,
                OldStatus = appeal.Status,
                NewStatus = appeal.Status,
                AssignedTo = name,
                AssignedToEn = nameEn,
                ChangedById = actor.Id,
                ChangedAt = Now
            });
            await _notifications.AddAssignedAsync(appeal, name, nameEn, actor.Id);
        }

        // (Ukrainian, English) name of the responsible party.
        private async Task<(string Name, string? NameEn)> ResponsibleNameAsync(Appeal appeal)
        {
            if (appeal.ResponsibleKind == ResponsibleKind.Association)
            {
                var acab = await _context.Acabs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appeal.AcabId);
                return (acab?.Name ?? "", acab?.NameEn);
            }
            var recipient = await _context.Recipients.AsNoTracking().FirstOrDefaultAsync(r => r.Id == appeal.RecipientId);
            return (recipient?.Name ?? "", recipient?.NameEn);
        }

        private async Task SaveOrConflictAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ServiceValidationException("Status", "Validation.ChangedMeanwhile");
            }
        }
    }
}
