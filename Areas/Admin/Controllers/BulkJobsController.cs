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
public class BulkJobsController : Controller
{
    private readonly ARCompletionsContext _db;

    public BulkJobsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var total = await _db.BulkJobs.LongCountAsync();
        var items = await _db.BulkJobs.OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var vm = new ARCompletions.Areas.Admin.Models.BulkJobsIndexViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var job = await _db.BulkJobs.FindAsync(id);
        if (job == null) return NotFound();

        var logs = await _db.Set<ARCompletions.Domain.AuditLog>()
            .Where(l => l.TargetId == job.Id)
            .OrderByDescending(l => l.Timestamp)
            .Take(200)
            .ToListAsync();

        ViewBag.Logs = logs;
        return View(job);
    }
}
