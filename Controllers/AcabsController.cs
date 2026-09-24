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
    public class AcabsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AcabsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Acabs
        public async Task<IActionResult> Index()
        {
            return View(await _context.Acabs.ToListAsync());
        }

        // GET: Acabs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var acab = await _context.Acabs
                .FirstOrDefaultAsync(m => m.Id == id);
            if (acab == null)
            {
                return NotFound();
            }

            return View(acab);
        }

        // GET: Acabs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Acabs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,InvitationCode,CreatedAt,UpdatedAt")] Acab acab)
        {
            if (ModelState.IsValid)
            {
                _context.Add(acab);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(acab);
        }

        // GET: Acabs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var acab = await _context.Acabs.FindAsync(id);
            if (acab == null)
            {
                return NotFound();
            }
            return View(acab);
        }

        // POST: Acabs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,InvitationCode,CreatedAt,UpdatedAt")] Acab acab)
        {
            if (id != acab.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    acab.UpdatedAt = DateTime.UtcNow;
                    _context.Update(acab);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AcabExists(acab.Id))
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
            return View(acab);
        }

        // GET: Acabs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var acab = await _context.Acabs
                .FirstOrDefaultAsync(m => m.Id == id);
            if (acab == null)
            {
                return NotFound();
            }

            return View(acab);
        }

        // POST: Acabs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var acab = await _context.Acabs.FindAsync(id);
            if (acab != null)
            {
                _context.Acabs.Remove(acab);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AcabExists(int id)
        {
            return _context.Acabs.Any(e => e.Id == id);
        }
    }
}
