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
public class FaqCandidatesController : Controller
{
    private readonly ARCompletionsContext _db;

    public FaqCandidatesController(ARCompletionsContext db)
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

    public async Task<IActionResult> Index(string? search = null, string? status = null, int page = 1, int pageSize = 25)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var query = _db.FaqCandidates.Where(c => c.VendorId == vendorId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c => c.Question.Contains(s) || c.Answer.Contains(s));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var total = await query.LongCountAsync();

        var items = await query.OrderByDescending(c => c.GeneratedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var vm = new ARCompletions.Areas.Vendor.Models.VendorFaqCandidatesIndexViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Search = search,
            Status = status
        };

        // populate categories for inline batch create
        await PopulateCategoriesAsync(vendorId, vm.CategoryId);

        return View(vm);
    }

    public async Task<IActionResult> Edit(string id)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (string.IsNullOrEmpty(id)) return NotFound();

        var item = await _db.FaqCandidates
            .Where(c => c.VendorId == vendorId && c.Id == id)
            .FirstOrDefaultAsync();
        if (item == null) return NotFound();

        await PopulateCategoriesAsync(vendorId);

        // 狀態與分類目前直接在 View 中用文字/下拉處理
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, FaqCandidate candidate, string? newStatus, string? categoryId)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (id != candidate.Id) return BadRequest();

        var existing = await _db.FaqCandidates
            .Where(c => c.VendorId == vendorId && c.Id == id)
            .FirstOrDefaultAsync();
        if (existing == null) return NotFound();

        existing.Status = string.IsNullOrEmpty(newStatus) ? existing.Status : newStatus;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 若指定分類且希望立刻產生正式 FAQ，可在這裡擴充
        if (!string.IsNullOrEmpty(categoryId))
        {
            // 僅示意：目前只更新建議分類欄位，未自動產生 FAQ
            existing.SuggestedCategory = categoryId;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "候選 FAQ 已更新";
        return RedirectToAction(nameof(Index));
    }
}
