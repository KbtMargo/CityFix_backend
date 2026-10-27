using CityFix_backend.Services.Interfaces;

namespace CityFix_backend.Services
{
    /// <summary>Checks for appeals whose deadline has passed and tells whoever processes them, once per deadline.</summary>
    public class DeadlineWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<DeadlineWorker> _logger;

        public DeadlineWorker(IServiceScopeFactory scopes, ILogger<DeadlineWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var appeals = scope.ServiceProvider.GetRequiredService<IAppealService>();
                    int found;
                    // A batch at a time, so a backlog after downtime is cleared in one run.
                    while ((found = await appeals.NotifyOverdueAsync()) > 0)
                    {
                        _logger.LogInformation("Notified about {Count} overdue appeals.", found);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Checking appeal deadlines failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
