using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,RegulatoryManager,RegulatoryUser,AuthorisedPerson,Auditor,Trainer,ReadOnly")]
    public class PVLiteratureController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public PVLiteratureController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: PVLiterature
        public async Task<IActionResult> Index()
        {
            var searches = await _context.PVLiteratureSearch
                .OrderByDescending(s => s.SearchDate)
                .ToListAsync();
            return View(searches);
        }

        // GET: PVLiterature/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var search = await _context.PVLiteratureSearch
                .Include(s => s.AuditLogs)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (search == null) return NotFound();
            return View(search);
        }

        // GET: PVLiterature/Create
        public IActionResult Create()
        {
            return View(new PVLiteratureSearch
            {
                SearchDate = SiteTime.Today,
                SearchedBy = _userManager.GetUserName(User) ?? ""
            });
        }

        // POST: PVLiterature/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Create(PVLiteratureSearch search)
        {
            if (!ModelState.IsValid) return View(search);

            _context.PVLiteratureSearch.Add(search);
            await _context.SaveChangesAsync();

            AddAuditLog(search.Id, "Created", null, null);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Literature search record saved.";
            return RedirectToAction(nameof(Details), new { id = search.Id });
        }

        // GET: PVLiterature/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var search = await _context.PVLiteratureSearch.FindAsync(id);
            if (search == null) return NotFound();
            return View(search);
        }

        // POST: PVLiterature/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,RegulatoryManager,RegulatoryUser")]
        public async Task<IActionResult> Edit(int id, PVLiteratureSearch search)
        {
            if (id != search.Id) return NotFound();
            if (!ModelState.IsValid) return View(search);

            try
            {
                // Field-level change tracking for the audit log
                var snap = await _context.PVLiteratureSearch.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
                var diff = new List<string>();
                if (snap != null)
                {
                    OpenQMS.Services.AuditDiffHelper.TrackDate(diff, "Search Date",        snap.SearchDate,        search.SearchDate);
                    OpenQMS.Services.AuditDiffHelper.Track(diff,     "Searched By",        snap.SearchedBy,        search.SearchedBy);
                    OpenQMS.Services.AuditDiffHelper.Track(diff,     "Databases Searched", snap.DatabasesSearched, search.DatabasesSearched);
                    OpenQMS.Services.AuditDiffHelper.Track(diff,     "Search Terms",       snap.SearchTerms,       search.SearchTerms);
                    OpenQMS.Services.AuditDiffHelper.TrackDate(diff, "Date Range From",    snap.DateRangeFrom,     search.DateRangeFrom);
                    OpenQMS.Services.AuditDiffHelper.TrackDate(diff, "Date Range To",      snap.DateRangeTo,       search.DateRangeTo);
                    OpenQMS.Services.AuditDiffHelper.Track<int>(diff, "Results Count",      snap.ResultsCount,      search.ResultsCount);
                    OpenQMS.Services.AuditDiffHelper.Track<int>(diff, "Relevant Results",   snap.RelevantResults,   search.RelevantResults);
                    OpenQMS.Services.AuditDiffHelper.Track<int>(diff, "AEs Identified",     snap.AEsIdentified,     search.AEsIdentified);
                    OpenQMS.Services.AuditDiffHelper.Track(diff,     "Notes",              snap.Notes,             search.Notes);
                }

                _context.Update(search);
                await _context.SaveChangesAsync();

                if (diff.Count > 0) AddAuditLog(id, "Edited", null, null, OpenQMS.Services.AuditDiffHelper.Build(diff));
                await _context.SaveChangesAsync();
                TempData["Success"] = "Record updated.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.PVLiteratureSearch.AnyAsync(s => s.Id == id)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.PVLiteratureAuditLog.Add(new PVLiteratureAuditLog
            {
                ParentId = parentId,
                Action = action,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                PerformedBy = User.Identity?.Name ?? "System",
                PerformedOn = DateTime.UtcNow,
                Notes = notes
            });
        }
    }
}
