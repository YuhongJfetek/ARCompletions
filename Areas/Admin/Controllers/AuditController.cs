using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class AuditController : Controller
{
    private readonly ARCompletionsContext _db;

    public AuditController(ARCompletionsContext db)
    {
        _db = db;
    }

    // List audit logs with optional filters and pagination
    public async Task<IActionResult> Index(string? vendorId = null, string? action = null, string? dateFrom = null, string? dateTo = null, int page = 1, int pageSize = 50)
    {
        var query = _db.Set<ARCompletions.Domain.AuditLog>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Where(a => a.Payload != null && a.Payload.Contains(vendorId));
        }

        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(dateFrom) && System.DateTime.TryParse(dateFrom, out var df))
        {
            var from = new System.DateTimeOffset(df.Date).ToUnixTimeSeconds();
            query = query.Where(a => a.Timestamp >= from);
        }
        if (!string.IsNullOrWhiteSpace(dateTo) && System.DateTime.TryParse(dateTo, out var dt))
        {
            var to = new System.DateTimeOffset(dt.Date.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();
            query = query.Where(a => a.Timestamp <= to);
        }

        var total = await query.LongCountAsync();
        var items = await query.OrderByDescending(a => a.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalCount = total;
        ViewBag.ActiveAction = action; ViewBag.DateFrom = dateFrom; ViewBag.DateTo = dateTo; ViewBag.VendorId = vendorId;

        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var log = await _db.Set<ARCompletions.Domain.AuditLog>().FindAsync(id);
        if (log == null) return NotFound();

        return View(log);
    }

    // Governance: list system settings and allow update (use SystemSettings table)
    public async Task<IActionResult> Governance()
    {
        var settings = await _db.SystemSettings.OrderBy(s => s.SettingKey).ToListAsync();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSetting(string id, string value)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();
        var s = await _db.SystemSettings.FindAsync(id);
        if (s == null) return NotFound();
        s.SettingValue = value ?? string.Empty;
        s.UpdatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        s.UpdatedBy = User?.Identity?.Name;
        _db.SystemSettings.Update(s);
        await _db.SaveChangesAsync();
        TempData["Success"] = "設定已更新";
        return RedirectToAction(nameof(Governance));
    }
}
