using CityFix_backend.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CityFix_backend.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ApplicationDbContext _context;

        public DatabaseHealthCheck(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The database is unreachable.");
    }
}
