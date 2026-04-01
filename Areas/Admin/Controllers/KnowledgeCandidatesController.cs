using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class KnowledgeCandidatesController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public KnowledgeCandidatesController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index(string? vendorId = null, string? status = null, double? minConfidence = null, int page = 1, int pageSize = 25)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        var query = _db.FaqCandidates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
            query = query.Where(c => c.VendorId == vendorId);
        }
        else if (allowed != null)
        {
            query = query.Where(c => allowed.Contains(c.VendorId));
        }

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
        if (minConfidence != null) query = query.Where(c => c.ConfidenceScore != null && c.ConfidenceScore >= minConfidence);

        var total = await query.LongCountAsync();
        var items = await query.OrderByDescending(c => c.ConfidenceScore).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalCount = total;
        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.FaqCandidates.FindAsync(id);
        if (item == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(item.VendorId)) return Forbid();

        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReviewed(string id, string reviewerId)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();
        var item = await _db.FaqCandidates.FindAsync(id);
        if (item == null) return NotFound();

        item.Status = "reviewed";
        item.ReviewedByType = "admin";
        item.ReviewedById = reviewerId ?? User?.Identity?.Name;
        item.ReviewedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        item.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        _db.FaqCandidates.Update(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "已標記為已審核";
        return RedirectToAction(nameof(Details), new { id });
    }
}
