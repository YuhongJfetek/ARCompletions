using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "Vendor")]
public class FaqsController : Controller
{
    private readonly ARCompletionsContext _db;

    public FaqsController(ARCompletionsContext db)
    {
        _db = db;
    }

    private string? GetVendorId() => User.FindFirst("VendorId")?.Value;

    private async Task PopulateCategoriesAsync(string vendorId, string? selectedCategoryId = null)
    {
        var categories = await _db.FaqCategories
            .Where(c => c.VendorId == vendorId && c.IsActive)
            .OrderBy(c => c.Sort)
            .ToListAsync();

        ViewBag.CategoryId = new SelectList(categories, "Id", "Name", selectedCategoryId);
    }

    public async Task<IActionResult> Index(string? search = null, string? categoryId = null, string? status = null, int page = 1, int pageSize = 25)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
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

        return View(vm);
    }

    public async Task<IActionResult> Create()
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        await PopulateCategoriesAsync(vendorId);
        var faq = new Faq
        {
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
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(vendorId, faq.CategoryId);
            return View(faq);
        }

        faq.Id = Guid.NewGuid().ToString("N");
        faq.VendorId = vendorId;
        faq.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        faq.CreatedByType = "Vendor";
        faq.CreatedById = User.Identity?.Name;

        _db.Faqs.Add(faq);
        await _db.SaveChangesAsync();

        // 寫入審計日誌：FAQ 建立
        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "vendor",
                Action = "Faq.Create",
                TargetId = faq.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { faq.Id, faq.Question }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (string.IsNullOrEmpty(id)) return NotFound();

        var faq = await _db.Faqs
            .Where(f => f.VendorId == vendorId && f.Id == id)
            .FirstOrDefaultAsync();
        if (faq == null) return NotFound();

        await PopulateCategoriesAsync(vendorId, faq.CategoryId);
        return View(faq);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Faq faq)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (id != faq.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(vendorId, faq.CategoryId);
            return View(faq);
        }

        var existing = await _db.Faqs
            .Where(f => f.VendorId == vendorId && f.Id == id)
            .FirstOrDefaultAsync();
        if (existing == null) return NotFound();
        // capture before snapshot for status diff
        var before = new
        {
            existing.Id,
            existing.Status,
            existing.Question,
            existing.Answer,
            existing.CategoryId
        };

        existing.CategoryId = faq.CategoryId;
        existing.Question = faq.Question;
        existing.Answer = faq.Answer;
        existing.Tags = faq.Tags;
        existing.Status = faq.Status;
        existing.Priority = faq.Priority;
        existing.IsActive = faq.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        existing.UpdatedByType = "Vendor";
        existing.UpdatedById = User.Identity?.Name;

        await _db.SaveChangesAsync();

        // 寫入一般編輯審計日誌
        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "vendor",
                Action = "Faq.Edit",
                TargetId = existing.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { existing.Id, existing.Question }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        // 若狀態有變動，額外寫入 before/after 的審計紀錄
        try
        {
            if (!string.Equals(before.Status, existing.Status, StringComparison.OrdinalIgnoreCase))
            {
                var after = new
                {
                    existing.Id,
                    existing.Status,
                    existing.Question,
                    existing.Answer,
                    existing.CategoryId
                };

                _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Actor = User?.Identity?.Name ?? "vendor",
                    Action = "Faq.StatusChange",
                    TargetId = existing.Id,
                    Payload = System.Text.Json.JsonSerializer.Serialize(new { Before = before, After = after }),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
                await _db.SaveChangesAsync();
            }
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }
}
