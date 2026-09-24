using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CityFix_backend.Data;
using CityFix_backend.Models;

namespace CityFix_backend.Controllers
{
    public class AppealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AppealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Appeals
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Appeals.Include(a => a.Author).Include(a => a.Category).Include(a => a.Recipient);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Appeals/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appeal = await _context.Appeals
                .Include(a => a.Author)
                .Include(a => a.Category)
                .Include(a => a.Recipient)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (appeal == null)
            {
                return NotFound();
            }

            return View(appeal);
        }

        // GET: Appeals/Create
        public IActionResult Create()
        {
            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Email");
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            ViewData["RecipientId"] = new SelectList(_context.Recipients, "Id", "Name");
            return View();
        }

        // POST: Appeals/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Description,Lat,Lng,Address,Status,Priority,AuthorId,CategoryId,RecipientId,CreatedAt,UpdatedAt")] Appeal appeal)
        {
            if (ModelState.IsValid)
            {
                _context.Add(appeal);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Email", appeal.AuthorId);
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", appeal.CategoryId);
            ViewData["RecipientId"] = new SelectList(_context.Recipients, "Id", "Name", appeal.RecipientId);
            return View(appeal);
        }

        // GET: Appeals/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appeal = await _context.Appeals.FindAsync(id);
            if (appeal == null)
            {
                return NotFound();
            }
            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Email", appeal.AuthorId);
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", appeal.CategoryId);
            ViewData["RecipientId"] = new SelectList(_context.Recipients, "Id", "Name", appeal.RecipientId);
            return View(appeal);
        }

        // POST: Appeals/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Lat,Lng,Address,Status,Priority,AuthorId,CategoryId,RecipientId,CreatedAt,UpdatedAt")] Appeal appeal)
        {
            if (id != appeal.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    appeal.UpdatedAt = DateTime.UtcNow;
                    _context.Update(appeal);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppealExists(appeal.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["AuthorId"] = new SelectList(_context.Users, "Id", "Email", appeal.AuthorId);
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", appeal.CategoryId);
            ViewData["RecipientId"] = new SelectList(_context.Recipients, "Id", "Name", appeal.RecipientId);
            return View(appeal);
        }

        // GET: Appeals/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appeal = await _context.Appeals
                .Include(a => a.Author)
                .Include(a => a.Category)
                .Include(a => a.Recipient)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (appeal == null)
            {
                return NotFound();
            }

            return View(appeal);
        }

        // POST: Appeals/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appeal = await _context.Appeals.FindAsync(id);
            if (appeal != null)
            {
                _context.Appeals.Remove(appeal);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AppealExists(int id)
        {
            return _context.Appeals.Any(e => e.Id == id);
        }
    }
}
