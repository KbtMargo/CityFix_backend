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
    [Authorize(Roles = "Administrator")]
    public class RecipientsController : AppController
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResource> _l;

        public RecipientsController(ApplicationDbContext context, IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _l = localizer;
        }

        // GET: Recipients
        public async Task<IActionResult> Index(int page = 1)
        {
            var result = await _context.Recipients.AsNoTracking().OrderBy(r => r.Name).ThenBy(r => r.Id).ToPagedResultAsync(page);
            var ids = result.Items.Select(r => r.Id).ToList();
            var appeals = await _context.Appeals.Where(a => a.RecipientId != null && ids.Contains(a.RecipientId.Value))
                .GroupBy(a => a.RecipientId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            return View(new TablePageViewModel<Recipient> { Page = result, Counts = appeals });
        }

        // GET: Recipients/Create
        public IActionResult Create()
        {
            return View(new Recipient());
        }

        // POST: Recipients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,ContactInfo")] Recipient recipient)
        {
            if (ModelState.IsValid)
            {
                _context.Add(recipient);
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Saved"]);
                return RedirectToAction(nameof(Index));
            }
            return View(recipient);
        }

        // GET: Recipients/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var recipient = await _context.Recipients.FindAsync(id);
            return recipient == null ? NotFound() : View(recipient);
        }

        // POST: Recipients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,ContactInfo")] Recipient recipient)
        {
            if (id != recipient.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existing = await _context.Recipients.FindAsync(id);
                if (existing == null)
                {
                    return NotFound();
                }
                existing.Name = recipient.Name;
                existing.ContactInfo = recipient.ContactInfo;
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Saved"]);
                return RedirectToAction(nameof(Index));
            }
            return View(recipient);
        }

        // GET: Recipients/Delete/5  (blocked while appeals are addressed to it)
        public async Task<IActionResult> Delete(int id)
        {
            var recipient = await _context.Recipients.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (recipient == null)
            {
                return NotFound();
            }
            ViewData["AppealCount"] = await _context.Appeals.CountAsync(a => a.RecipientId == id);
            return View(recipient);
        }

        // POST: Recipients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (await _context.Appeals.AnyAsync(a => a.RecipientId == id))
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            var recipient = await _context.Recipients.FindAsync(id);
            if (recipient != null)
            {
                _context.Recipients.Remove(recipient);
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Deleted"]);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
