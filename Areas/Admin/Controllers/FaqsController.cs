using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class FaqsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.IBackgroundJobQueue _jobQueue;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public FaqsController(ARCompletionsContext db, ARCompletions.Services.IBackgroundJobQueue jobQueue, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _jobQueue = jobQueue;
        _vendorScope = vendorScope;
    }

    private async Task PopulateCategoriesAsync(string vendorId, string? selectedCategoryId = null)
    {
        var categories = await _db.FaqCategories
            .Where(c => c.VendorId == vendorId && c.IsActive)
            .OrderBy(c => c.Sort)
            .ToListAsync();

        ViewBag.CategoryId = new SelectList(categories, "Id", "Name", selectedCategoryId);
    }

    public async Task<IActionResult> Index(string? vendorId, string? search = null, string? categoryId = null, string? status = null, int page = 1, int pageSize = 25)
    {
        if (string.IsNullOrWhiteSpace(vendorId))
        {
            // show list across vendors (or require vendor selection). For now require vendorId param
            return BadRequest("vendorId is required");
        }

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(vendorId)) return Forbid();

        page = page < 1 ? 1 : page;
        pageSize = pageSize <= 0 ? 25 : pageSize;
        if (pageSize > 200) pageSize = 200;

        var query = _db.Faqs.Where(f => f.VendorId == vendorId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(f => (f.Question ?? "").Contains(s) || (f.Answer ?? "").Contains(s) || (f.Tags ?? "").Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(categoryId)) query = query.Where(f => f.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);

        var total = await query.LongCountAsync();

        var items = await query.OrderByDescending(f => f.Priority).ThenBy(f => f.Question)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var categories = await _db.FaqCategories.Where(c => c.VendorId == vendorId && c.IsActive).OrderBy(c => c.Sort).ToListAsync();

        var vm = new ARCompletions.Areas.Vendor.Models.VendorFaqsIndexViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search,
            CategoryId = categoryId,
            Status = status,
            Categories = categories
        };

        ViewBag.VendorId = vendorId;
        return View(vm);
    }

    public async Task<IActionResult> Create(string vendorId)
    {
        if (string.IsNullOrWhiteSpace(vendorId)) return BadRequest();
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(vendorId)) return Forbid();

        await PopulateCategoriesAsync(vendorId);
        var faq = new Faq
        {
            VendorId = vendorId,
            Status = "active",
            Priority = 0,
            Version = 1,
            IsActive = true
        };
        return View(faq);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Faq faq)
    {
        if (string.IsNullOrWhiteSpace(faq.VendorId)) return BadRequest();
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(faq.VendorId)) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(faq.VendorId, faq.CategoryId);
            return View(faq);
        }

        faq.Id = Guid.NewGuid().ToString("N");
        faq.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        faq.CreatedByType = "Admin";
        faq.CreatedById = User.Identity?.Name;

        _db.Faqs.Add(faq);
        await _db.SaveChangesAsync();

        // create embedding job trigger for this vendor
        try
        {
            var job = new ARCompletions.Domain.EmbeddingJob
            {
                Id = Guid.NewGuid().ToString("N"),
                VendorId = faq.VendorId,
                JobNo = "E-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Status = "pending",
                TriggerType = "faq_change",
                TriggeredByType = "admin",
                TriggeredById = User?.Identity?.Name ?? "admin",
                VectorVersion = "v1",
                ModelName = "",
                TotalFaqCount = 0,
                SuccessCount = 0,
                FailCount = 0,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            _db.EmbeddingJobs.Add(job);
            await _db.SaveChangesAsync();
            try { _jobQueue.Enqueue(job.Id); } catch { }
        }
        catch { }

        TempData["Success"] = "FAQ 已建立";
        var vendor = await _db.Vendors.FindAsync(faq.VendorId);
        if (vendor == null)
        {
            TempData["Error"] = "找不到對應的廠商，請先選擇或建立廠商。";
            return RedirectToAction("Index", "Vendors", new { area = "Admin" });
        }
        return RedirectToAction(nameof(Index), new { vendorId = faq.VendorId });
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var faq = await _db.Faqs.FindAsync(id);
        if (faq == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(faq.VendorId)) return Forbid();

        await PopulateCategoriesAsync(faq.VendorId, faq.CategoryId);
        return View(faq);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Faq faq)
    {
        if (id != faq.Id) return BadRequest();
        var existing = await _db.Faqs.FindAsync(id);
        if (existing == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(existing.VendorId)) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(existing.VendorId, faq.CategoryId);
            return View(faq);
        }

        existing.CategoryId = faq.CategoryId;
        existing.Question = faq.Question;
        existing.Answer = faq.Answer;
        existing.Tags = faq.Tags;
        existing.Status = faq.Status;
        existing.Priority = faq.Priority;
        existing.IsActive = faq.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        existing.UpdatedByType = "Admin";
        existing.UpdatedById = User.Identity?.Name;

        await _db.SaveChangesAsync();

        // trigger embedding rebuild
        try
        {
            var job = new ARCompletions.Domain.EmbeddingJob
            {
                Id = Guid.NewGuid().ToString("N"),
                VendorId = existing.VendorId,
                JobNo = "E-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Status = "pending",
                TriggerType = "faq_change",
                TriggeredByType = "admin",
                TriggeredById = User?.Identity?.Name ?? "admin",
                VectorVersion = "v1",
                ModelName = "",
                TotalFaqCount = 0,
                SuccessCount = 0,
                FailCount = 0,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            _db.EmbeddingJobs.Add(job);
            await _db.SaveChangesAsync();
            try { _jobQueue.Enqueue(job.Id); } catch { }
        }
        catch { }

        TempData["Success"] = "FAQ 已更新";
        var vendor2 = await _db.Vendors.FindAsync(existing.VendorId);
        if (vendor2 == null)
        {
            TempData["Error"] = "找不到對應的廠商，請先選擇或建立廠商。";
            return RedirectToAction("Index", "Vendors", new { area = "Admin" });
        }
        return RedirectToAction(nameof(Index), new { vendorId = existing.VendorId });
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var faq = await _db.Faqs.FindAsync(id);
        if (faq == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(faq.VendorId)) return Forbid();

        return View(faq);
    }
}
