using CityFix_backend.Data;
using CityFix_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public interface IAuditLog
    {
        Task RecordAsync(User actor, string action, string target, string? details = null);

        Task<PagedResult<AuditEntry>> GetPageAsync(string? q, int page);
    }

    public class AuditLog : IAuditLog
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _http;
        private readonly TimeProvider _clock;
        private readonly ILogger<AuditLog> _logger;

        public AuditLog(ApplicationDbContext context, IHttpContextAccessor http, TimeProvider clock, ILogger<AuditLog> logger)
        {
            _context = context;
            _http = http;
            _clock = clock;
            _logger = logger;
        }

        public async Task RecordAsync(User actor, string action, string target, string? details = null)
        {
            var entry = new AuditEntry
            {
                At = _clock.GetUtcNow().UtcDateTime,
                ActorId = actor.Id,
                ActorName = Limit($"{actor.FullName} <{actor.Email}>", 200),
                Action = action,
                Target = Limit(target, 200),
                Details = details == null ? null : Limit(details, 1000),
                IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString()
            };
            // A separate context call so the entry is written even when the action itself used ExecuteUpdate/ExecuteDelete.
            _context.AuditLog.Add(entry);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // The action already happened; losing its audit line must not turn it into an error page.
                _context.Entry(entry).State = EntityState.Detached;
                _logger.LogError(ex, "Could not write audit entry {Action} {Target} by {ActorId}.", action, target, actor.Id);
            }
        }

        public Task<PagedResult<AuditEntry>> GetPageAsync(string? q, int page)
        {
            var query = _context.AuditLog.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = Search.ContainsPattern(q);
                query = query.Where(e => EF.Functions.ILike(e.ActorName, pattern, "\\")
                    || EF.Functions.ILike(e.Action, pattern, "\\")
                    || EF.Functions.ILike(e.Target, pattern, "\\")
                    || (e.Details != null && EF.Functions.ILike(e.Details, pattern, "\\")));
            }
            return query.OrderByDescending(e => e.At).ThenByDescending(e => e.Id).ToPagedResultAsync(page, 25);
        }

        private static string Limit(string text, int max) => text.Length <= max ? text : text[..max];
    }
}
