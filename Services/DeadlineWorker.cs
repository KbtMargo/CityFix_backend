using CityFix_backend.Services.Interfaces;

namespace CityFix_backend.Services
{
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
