using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    // Condominium associations (ОСББ). The entity stays "Acab" in code and in the database.
    [Authorize(Roles = "Administrator")]
    public class AcabsController : AppController
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResource> _l;

        public AcabsController(ApplicationDbContext context, IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _l = localizer;
        }

        // GET: Acabs
        public async Task<IActionResult> Index(int page = 1)
        {
            var result = await _context.Acabs.AsNoTracking().OrderBy(a => a.Name).ThenBy(a => a.Id).ToPagedResultAsync(page);
            var ids = result.Items.Select(a => a.Id).ToList();
            var members = await _context.Users.Where(u => u.AcabId != null && ids.Contains(u.AcabId.Value))
                .GroupBy(u => u.AcabId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            return View(new TablePageViewModel<Acab> { Page = result, Counts = members });
        }

        // GET: Acabs/Create
        public IActionResult Create()
        {
            return View(new Acab());
        }

        // POST: Acabs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Address,InvitationCode")] Acab acab)
        {
            if (ModelState.IsValid)
            {
                acab.CreatedAt = DateTime.UtcNow;
                acab.UpdatedAt = acab.CreatedAt;
                _context.Add(acab);
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Saved"]);
                return RedirectToAction(nameof(Index));
            }
            return View(acab);
        }

        // GET: Acabs/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var acab = await _context.Acabs.FindAsync(id);
            return acab == null ? NotFound() : View(acab);
        }

        // POST: Acabs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,InvitationCode")] Acab acab)
        {
            if (id != acab.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existing = await _context.Acabs.FindAsync(id);
                if (existing == null)
                {
                    return NotFound();
                }

                // Copy the editable fields so CreatedAt keeps its stored value.
                existing.Name = acab.Name;
                existing.Address = acab.Address;
                existing.InvitationCode = acab.InvitationCode;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Saved"]);
                return RedirectToAction(nameof(Index));
            }
            return View(acab);
        }

        // GET: Acabs/Delete/5  (blocked while users are linked: Users.AcabId has no ON DELETE rule)
        public async Task<IActionResult> Delete(int id)
        {
            var acab = await _context.Acabs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (acab == null)
            {
                return NotFound();
            }
            ViewData["MemberCount"] = await _context.Users.CountAsync(u => u.AcabId == id);
            return View(acab);
        }

        // POST: Acabs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (await _context.Users.AnyAsync(u => u.AcabId == id))
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            var acab = await _context.Acabs.FindAsync(id);
            if (acab != null)
            {
                _context.Acabs.Remove(acab);
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Deleted"]);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
