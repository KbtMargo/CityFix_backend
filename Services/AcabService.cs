using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Services
{
    public class AcabService : IAcabService
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _clock;

        public AcabService(ApplicationDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        public async Task<PagedResult<AcabRow>> GetPageAsync(string? q, int page)
        {
            var query = _context.Acabs.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = Search.ContainsPattern(q);
                query = query.Where(a => EF.Functions.ILike(a.Name, pattern, "\\")
                    || (a.NameEn != null && EF.Functions.ILike(a.NameEn, pattern, "\\"))
                    || (a.Address != null && EF.Functions.ILike(a.Address, pattern, "\\"))
                    || (a.AddressEn != null && EF.Functions.ILike(a.AddressEn, pattern, "\\")));
            }

            var acabs = await query.OrderBy(a => a.Name).ThenBy(a => a.Id).ToPagedResultAsync(page);
            var ids = acabs.Items.Select(a => a.Id).ToList();
            var members = await _context.Users.Where(u => u.AcabId != null && ids.Contains(u.AcabId.Value))
                .GroupBy(u => u.AcabId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var appeals = await _context.Appeals.Where(a => a.AcabId != null && ids.Contains(a.AcabId.Value))
                .GroupBy(a => a.AcabId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var heads = await _context.Users.Where(u => u.AcabId != null && ids.Contains(u.AcabId.Value) && u.Role == UserRole.AcabAdmin && !u.IsBlocked)
                .GroupBy(u => u.AcabId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return acabs.Map(a => new AcabRow(a, members.GetValueOrDefault(a.Id), appeals.GetValueOrDefault(a.Id), heads.GetValueOrDefault(a.Id)));
        }

        public async Task<Acab> GetAsync(int id) =>
            await _context.Acabs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException($"Association {id} does not exist.");

        public async Task<Acab> CreateAsync(AcabInput input)
        {
            var now = Now;
            var acab = new Acab
            {
                Name = input.Name.Trim(),
                Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim(),
                NameEn = Clean(input.NameEn),
                AddressEn = Clean(input.AddressEn),
                ResidentCode = await NewCodeAsync(),
                CreatedAt = now,
                UpdatedAt = now
            };
            acab.HeadCode = await NewCodeAsync(acab.ResidentCode);
            _context.Acabs.Add(acab);
            await _context.SaveChangesAsync();
            return acab;
        }

        public async Task UpdateAsync(int id, AcabInput input)
        {
            var acab = await _context.Acabs.FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new NotFoundException($"Association {id} does not exist.");
            acab.Name = input.Name.Trim();
            acab.Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();
            acab.NameEn = Clean(input.NameEn);
            acab.AddressEn = Clean(input.AddressEn);
            acab.UpdatedAt = Now;
            await _context.SaveChangesAsync();
        }

        public async Task<string> RegenerateCodeAsync(int id, AcabCodeKind kind)
        {
            var acab = await _context.Acabs.FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new NotFoundException($"Association {id} does not exist.");
            var code = await NewCodeAsync();
            if (kind == AcabCodeKind.Head) acab.HeadCode = code; else acab.ResidentCode = code;
            acab.UpdatedAt = Now;
            await _context.SaveChangesAsync();
            return code;
        }

        public async Task<DeleteCheck> CheckDeleteAsync(int id)
        {
            var acab = await GetAsync(id);
            var members = await _context.Users.CountAsync(u => u.AcabId == id);
            var appeals = await _context.Appeals.CountAsync(a => a.AcabId == id);
            return members > 0 ? new DeleteCheck(id, acab.Name, members, "Acabs.CannotDeleteText")
                : appeals > 0 ? new DeleteCheck(id, acab.Name, appeals, "Common.HasAppealsText")
                : new DeleteCheck(id, acab.Name, 0, null);
        }

        public async Task DeleteAsync(int id)
        {
            var check = await CheckDeleteAsync(id);
            if (check.Blocked)
            {
                throw new ServiceValidationException("", check.BlockedKey!, check.UsageCount);
            }
            await _context.Acabs.Where(a => a.Id == id).ExecuteDeleteAsync();
        }

        public async Task<(Acab Acab, UserRole Role)?> FindByCodeAsync(string code)
        {
            var normalized = RandomCodes.NormalizeCode(code);
            if (normalized.Length == 0) return null;

            var acab = await _context.Acabs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ResidentCode == normalized || a.HeadCode == normalized);
            return acab == null ? null : (acab, acab.HeadCode == normalized ? UserRole.AcabAdmin : UserRole.Resident);
        }

        public async Task<bool> ConsumeHeadCodeAsync(int acabId, string code)
        {
            var normalized = RandomCodes.NormalizeCode(code);
            var replacement = await NewCodeAsync();
            var now = Now;
            var updated = await _context.Acabs
                .Where(a => a.Id == acabId && a.HeadCode == normalized)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.HeadCode, replacement)
                    .SetProperty(a => a.UpdatedAt, now));
            return updated == 1;
        }

        private async Task<string> NewCodeAsync(string? alsoAvoid = null)
        {
            while (true)
            {
                var code = RandomCodes.InvitationCode();
                if (code != alsoAvoid && !await _context.Acabs.AnyAsync(a => a.ResidentCode == code || a.HeadCode == code))
                {
                    return code;
                }
            }
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
