using CityFix_backend.Data;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
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
                // The two stages fail independently: a name the translator chokes on must not stall appeal translation.
                var namesOk = await RunStageAsync("names", TranslateNamesAsync, stoppingToken);
                var appealsOk = await RunStageAsync("appeals", TranslateAppealsAsync, stoppingToken);
                await Task.Delay(namesOk && appealsOk ? _options.Interval : _options.RetryAfterFailure, stoppingToken);
            }
        }

        private async Task<bool> RunStageAsync(string stage, Func<CancellationToken, Task> run, CancellationToken stoppingToken)
        {
            try
            {
                await run(stoppingToken);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Machine translation of {Stage} failed; retrying in {Delay}.", stage, _options.RetryAfterFailure);
                return false;
            }
        }

        // Names and addresses have no attempt counter in the database, so items that keep failing are remembered here
        // and skipped until the next restart; otherwise one bad text would be retried forever.
        private readonly Dictionary<(string Kind, int Id), int> _failures = new();

        private List<int> GivenUp(string kind) =>
            _failures.Where(f => f.Key.Kind == kind && f.Value >= _options.MaxAttempts).Select(f => f.Key.Id).ToList();

        private async Task AttemptAsync(string kind, int id, Func<Task> translate)
        {
            try
            {
                await translate();
                _failures.Remove((kind, id));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var attempts = _failures[(kind, id)] = _failures.GetValueOrDefault((kind, id)) + 1;
                if (attempts >= _options.MaxAttempts)
                {
                    _logger.LogWarning("Gave up translating {Kind} {Id} after {Attempts} attempts; it is skipped until restart.", kind, id, attempts);
                }
                throw;
            }
        }

        private async Task TranslateNamesAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var translator = scope.ServiceProvider.GetRequiredService<ITranslator>();

            var skip = GivenUp(nameof(Category));
            foreach (var category in await db.Categories.Where(c => (c.NameEn == null || c.NameEn == "") && !skip.Contains(c.Id)).Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                await AttemptAsync(nameof(Category), category.Id, async () =>
                {
                    (category.Name, category.NameEn) = await BilingualAsync(translator, category.Name, 100, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                });
            }

            skip = GivenUp(nameof(Recipient));
            foreach (var recipient in await db.Recipients.Where(r => (r.NameEn == null || r.NameEn == "") && !skip.Contains(r.Id)).Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                await AttemptAsync(nameof(Recipient), recipient.Id, async () =>
                {
                    (recipient.Name, recipient.NameEn) = await BilingualAsync(translator, recipient.Name, 200, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                });
            }

            skip = GivenUp(nameof(Acab));
            foreach (var acab in await db.Acabs.Where(a => (a.NameEn == null || a.NameEn == "" || (a.Address != null && (a.AddressEn == null || a.AddressEn == ""))) && !skip.Contains(a.Id)).Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                await AttemptAsync(nameof(Acab), acab.Id, async () =>
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
                });
            }

            skip = GivenUp(nameof(User));
            foreach (var user in await db.Users.Where(u => u.FullNameTranslated == null && u.FirstName != "" && !skip.Contains(u.Id)).Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                await AttemptAsync(nameof(User), user.Id, async () =>
                {
                    user.FullNameTranslated = TextLanguage.Detect(user.FullName) == "uk"
                        ? Transliteration.ToLatin(user.FullName)
                        : Limit(await translator.TranslateAsync(user.FullName, "en", "uk", cancellationToken), 200);
                    await db.SaveChangesAsync(cancellationToken);
                });
            }

            skip = GivenUp(nameof(Appeal));
            foreach (var appeal in await db.Appeals.Where(a => a.Address != null && a.AddressTranslated == null && !skip.Contains(a.Id)).Take(_options.BatchSize).ToListAsync(cancellationToken))
            {
                await AttemptAsync(nameof(Appeal), appeal.Id, async () =>
                {
                    var from = TextLanguage.Detect(appeal.Address);
                    appeal.AddressTranslated = Limit(await translator.TranslateAsync(appeal.Address!, from, TextLanguage.Other(from), cancellationToken), 600);
                    await SaveAsync(db, cancellationToken);
                });
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
