using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "Vendor")]
public class FaqCategoriesController : Controller
{
    private readonly ARCompletionsContext _db;

    public FaqCategoriesController(ARCompletionsContext db)
    {
        _db = db;
    }

    private string? GetVendorId()
    {
        return User.FindFirst("VendorId")?.Value;
    }

    public async Task<IActionResult> Index()
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var items = await _db.FaqCategories
            .Where(c => c.VendorId == vendorId)
            .OrderBy(c => c.Sort)
            .ToListAsync();
        return View(items);
    }

    public IActionResult Create()
    {
        return View(new FaqCategory { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FaqCategory category)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        if (!ModelState.IsValid) return View(category);

        category.Id = Guid.NewGuid().ToString("N");
        category.VendorId = vendorId;
        category.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        category.CreatedBy = User.Identity?.Name ?? "vendor";

        _db.FaqCategories.Add(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "分類已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (string.IsNullOrEmpty(id)) return NotFound();

        var category = await _db.FaqCategories
            .Where(c => c.VendorId == vendorId && c.Id == id)
            .FirstOrDefaultAsync();
        if (category == null) return NotFound();

        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, FaqCategory category)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (id != category.Id) return BadRequest();

        if (!ModelState.IsValid) return View(category);

        var existing = await _db.FaqCategories
            .Where(c => c.VendorId == vendorId && c.Id == id)
            .FirstOrDefaultAsync();
        if (existing == null) return NotFound();

        existing.Name = category.Name;
        existing.Description = category.Description;
        existing.Sort = category.Sort;
        existing.IsActive = category.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        existing.UpdatedBy = User.Identity?.Name ?? "vendor";

        await _db.SaveChangesAsync();
        TempData["Success"] = "分類已更新";
        return RedirectToAction(nameof(Index));
    }
}