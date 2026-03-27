using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class FaqLogsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public FaqLogsController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index(string? vendorId = null, string? faqId = null, string? actionType = null)
    {
        var vendors = await _db.Vendors
            .OrderBy(v => v.Code)
            .ToListAsync();
        ViewBag.Vendors = new SelectList(vendors, "Id", "Name", vendorId);

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.FaqLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
            query = query.Where(l => l.VendorId == vendorId);
        }
        else if (allowed != null)
        {
            query = query.Where(l => allowed.Contains(l.VendorId));
        }

        if (!string.IsNullOrWhiteSpace(faqId))
        {
            query = query.Where(l => l.FaqId == faqId);
        }

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(l => l.ActionType == actionType);
        }

        var items = await query
            .OrderByDescending(l => l.OperatedAt)
            .Take(500)
            .ToListAsync();

        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var log = await _db.FaqLogs.FindAsync(id);
        if (log == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(log.VendorId)) return Forbid();

        var faq = await _db.Faqs.FindAsync(log.FaqId);
        ViewBag.Faq = faq;

        return View(log);
    }
}
