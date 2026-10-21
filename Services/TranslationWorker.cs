using CityFix_backend.Data;
using CityFix_backend.Helpers;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Services
{
    public class TranslationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly TranslationOptions _options;
        private readonly ILogger<TranslationWorker> _logger;

        public TranslationWorker(IServiceScopeFactory scopes, IOptions<TranslationOptions> options, ILogger<TranslationWorker> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled) return;

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = _options.Interval;
                try
                {
                    await TranslateNamesAsync(stoppingToken);
                    await TranslateAppealsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Machine translation failed; retrying in {Delay}.", _options.RetryAfterFailure);
                    delay = _options.RetryAfterFailure;
                }
                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task TranslateNamesAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var translator = scope.ServiceProvider.GetRequiredService<ITranslator>();

            foreach (var category in await db.Categories.Where(c => c.NameEn == null || c.NameEn == "").Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                (category.Name, category.NameEn) = await BilingualAsync(translator, category.Name, 100, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            foreach (var recipient in await db.Recipients.Where(r => r.NameEn == null || r.NameEn == "").Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                (recipient.Name, recipient.NameEn) = await BilingualAsync(translator, recipient.Name, 200, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            foreach (var acab in await db.Acabs.Where(a => a.NameEn == null || a.NameEn == "" || (a.Address != null && (a.AddressEn == null || a.AddressEn == ""))).Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(acab.NameEn))
                {
                    (acab.Name, acab.NameEn) = await BilingualAsync(translator, acab.Name, 200, cancellationToken);
                }
                if (acab.Address != null && string.IsNullOrWhiteSpace(acab.AddressEn))
                {
                    (acab.Address, acab.AddressEn) = await BilingualAsync(translator, acab.Address, 300, cancellationToken);
                }
                await db.SaveChangesAsync(cancellationToken);
            }

            foreach (var user in await db.Users.Where(u => u.FullNameTranslated == null && u.FirstName != "").Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                user.FullNameTranslated = TextLanguage.Detect(user.FullName) == "uk"
                    ? Transliteration.ToLatin(user.FullName)
                    : Limit(await translator.TranslateAsync(user.FullName, "en", "uk", cancellationToken), 200);
                await db.SaveChangesAsync(cancellationToken);
            }

            foreach (var appeal in await db.Appeals.Where(a => a.Address != null && a.AddressTranslated == null).Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                var from = TextLanguage.Detect(appeal.Address);
                appeal.AddressTranslated = Limit(await translator.TranslateAsync(appeal.Address!, from, TextLanguage.Other(from), cancellationToken), 600);
                await SaveAsync(db, cancellationToken);
            }
        }

        private static async Task<(string Uk, string En)> BilingualAsync(ITranslator translator, string name, int max, CancellationToken cancellationToken) =>
            TextLanguage.Detect(name) == "en"
                ? (Limit(await translator.TranslateAsync(name, "en", "uk", cancellationToken), max), name)
                : (name, Limit(await translator.TranslateAsync(name, "uk", "en", cancellationToken), max));

        private async Task TranslateAppealsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var translator = scope.ServiceProvider.GetRequiredService<ITranslator>();

            var appeals = await db.Appeals
                .Where(a => a.TranslationPending)
                .OrderBy(a => a.Id)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var appeal in appeals)
            {
                var from = TextLanguage.Detect($"{appeal.Title} {appeal.Description}");
                var to = TextLanguage.Other(from);
                try
                {
                    appeal.Language = from;
                    appeal.TitleTranslated = Limit(await translator.TranslateAsync(appeal.Title, from, to, cancellationToken), 300);
                    appeal.DescriptionTranslated = appeal.Description == null
                        ? null
                        : Limit(await translator.TranslateAsync(appeal.Description, from, to, cancellationToken), 3000);
                    appeal.TranslationPending = false;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    appeal.TitleTranslated = appeal.DescriptionTranslated = null;
                    appeal.TranslationPending = ++appeal.TranslationAttempts < _options.MaxAttempts;
                    await SaveAsync(db, cancellationToken);
                    throw;
                }
                await SaveAsync(db, cancellationToken);
            }
        }

        private static async Task SaveAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
            }
        }

        private static string Limit(string text, int max) => text.Length <= max ? text : text[..max];
    }
}
