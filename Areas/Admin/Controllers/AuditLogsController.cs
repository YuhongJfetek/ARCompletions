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
public class AuditLogsController : Controller
{
    private readonly ARCompletionsContext _db;

    public AuditLogsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? actor = null, string? action = null, long? from = null, long? to = null)
    {
        var query = _db.Set<ARCompletions.Domain.AuditLog>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(actor)) query = query.Where(a => a.Actor.Contains(actor));
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action.Contains(action));
        if (from.HasValue) query = query.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(a => a.Timestamp <= to.Value);

        var items = await query.OrderByDescending(a => a.Timestamp).Take(500).ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var log = await _db.Set<ARCompletions.Domain.AuditLog>().FindAsync(id);
        if (log == null) return NotFound();
        return View(log);
    }
}
