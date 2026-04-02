using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class BotAuditLogsController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotAuditLogsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? targetTable = null, string? actionType = null, string? changedBy = null, DateTimeOffset? dateFrom = null, DateTimeOffset? dateTo = null)
    {
        var query = _db.BotAuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(targetTable))
        {
            query = query.Where(a => a.TargetTable == targetTable);
        }
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(a => a.ActionType == actionType);
        }
        if (!string.IsNullOrWhiteSpace(changedBy))
        {
            query = query.Where(a => a.ChangedBy == changedBy);
        }
        if (dateFrom.HasValue)
        {
            query = query.Where(a => a.ChangedAt >= dateFrom.Value);
        }
        if (dateTo.HasValue)
        {
            query = query.Where(a => a.ChangedAt <= dateTo.Value);
        }

        var items = await query
            .OrderByDescending(a => a.ChangedAt)
            .Take(500)
            .ToListAsync();

        ViewBag.TargetTable = targetTable;
        ViewBag.ActionType = actionType;
        ViewBag.ChangedBy = changedBy;
        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;

        return View(items);
    }

    public async Task<IActionResult> Details(long id)
    {
        var item = await _db.BotAuditLogs.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }
}
