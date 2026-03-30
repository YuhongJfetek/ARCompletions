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
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public AuditController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    // List audit logs with optional filters and pagination
    public async Task<IActionResult> Index(string? vendorId = null, string? action = null, string? dateFrom = null, string? dateTo = null, int page = 1, int pageSize = 50)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.Set<ARCompletions.Domain.AuditLog>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
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

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && log.Payload != null && !allowed.Any(v => log.Payload.Contains(v))) return Forbid();

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
        return RedirectToAction(nameof(Governance));
    }
}
