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
    public class RecipientsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecipientsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Recipients
        public async Task<IActionResult> Index()
        {
            return View(await _context.Recipients.ToListAsync());
        }

        // GET: Recipients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipient = await _context.Recipients
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recipient == null)
            {
                return NotFound();
            }

            return View(recipient);
        }

        // GET: Recipients/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Recipients/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,ContactInfo")] Recipient recipient)
        {
            if (ModelState.IsValid)
            {
                _context.Add(recipient);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(recipient);
        }

        // GET: Recipients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipient = await _context.Recipients.FindAsync(id);
            if (recipient == null)
            {
                return NotFound();
            }
            return View(recipient);
        }

        // POST: Recipients/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
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
                try
                {
                    _context.Update(recipient);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecipientExists(recipient.Id))
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
            return View(recipient);
        }

        // GET: Recipients/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipient = await _context.Recipients
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recipient == null)
            {
                return NotFound();
            }

            return View(recipient);
        }

        // POST: Recipients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recipient = await _context.Recipients.FindAsync(id);
            if (recipient != null)
            {
                _context.Recipients.Remove(recipient);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RecipientExists(int id)
        {
            return _context.Recipients.Any(e => e.Id == id);
        }
    }
}
