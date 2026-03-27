using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "Vendor")]
public class FaqLogsController : Controller
{
    private readonly ARCompletionsContext _db;

    public FaqLogsController(ARCompletionsContext db)
    {
        _db = db;
    }

    private string? GetVendorId() => User.FindFirst("VendorId")?.Value;

    public async Task<IActionResult> Index(string? faqId = null, string? actionType = null)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var query = _db.FaqLogs.AsQueryable().Where(l => l.VendorId == vendorId);

        if (!string.IsNullOrWhiteSpace(faqId)) query = query.Where(l => l.FaqId == faqId);
        if (!string.IsNullOrWhiteSpace(actionType)) query = query.Where(l => l.ActionType == actionType);

        var items = await query.OrderByDescending(l => l.OperatedAt).Take(500).ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var log = await _db.FaqLogs.Where(l => l.Id == id && l.VendorId == vendorId).FirstOrDefaultAsync();
        if (log == null) return NotFound();

        var faq = await _db.Faqs.FindAsync(log.FaqId);
        ViewBag.Faq = faq;

        return View(log);
    }
}
