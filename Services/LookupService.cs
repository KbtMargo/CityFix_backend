using CityFix_backend.Data;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class LookupService : ILookupService
    {
        private readonly ApplicationDbContext _context;

        public LookupService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AppealLookups> GetAsync() => new(
            await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            await _context.Recipients.AsNoTracking().OrderBy(r => r.Name).ToListAsync(),
            await _context.Acabs.AsNoTracking().OrderBy(a => a.Name).ToListAsync());
    }
}
