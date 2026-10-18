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
    public class CategoriesController : AppController
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResource> _l;

        public CategoriesController(ApplicationDbContext context, IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _l = localizer;
        }

        // GET: Categories
        public async Task<IActionResult> Index(int page = 1)
        {
            var result = await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ThenBy(c => c.Id).ToPagedResultAsync(page);
            var ids = result.Items.Select(c => c.Id).ToList();
            var appeals = await _context.Appeals.Where(a => ids.Contains(a.CategoryId))
                .GroupBy(a => a.CategoryId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            return View(new TablePageViewModel<Category> { Page = result, Counts = appeals });
        }

        // GET: Categories/Create
        public IActionResult Create()
        {
            return View(new Category());
        }

        // POST: Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Saved"]);
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            return category == null ? NotFound() : View(category);
        }

        // POST: Categories/Edit/5  (Icon isn't shown in this design and keeps its stored value)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existing = await _context.Categories.FindAsync(id);
                if (existing == null)
                {
                    return NotFound();
                }
                existing.Name = category.Name;
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Saved"]);
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Delete/5  (blocked while appeals use it: the FK would delete them too)
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
            {
                return NotFound();
            }
            ViewData["AppealCount"] = await _context.Appeals.CountAsync(a => a.CategoryId == id);
            return View(category);
        }

        // POST: Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (await _context.Appeals.AnyAsync(a => a.CategoryId == id))
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Deleted"]);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
