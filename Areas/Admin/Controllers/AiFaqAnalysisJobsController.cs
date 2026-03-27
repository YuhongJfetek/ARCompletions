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
public class AiFaqAnalysisJobsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.IBackgroundJobQueue _jobQueue;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public AiFaqAnalysisJobsController(ARCompletionsContext db, ARCompletions.Services.IBackgroundJobQueue jobQueue, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _jobQueue = jobQueue;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index(string? vendorId = null, string? status = null)
    {
        var vendors = await _db.Vendors
            .OrderBy(v => v.Code)
            .ToListAsync();
        ViewBag.Vendors = vendors;

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.AiFaqAnalysisJobs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
            query = query.Where(j => j.VendorId == vendorId);
        }
        else if (allowed != null)
        {
            query = query.Where(j => allowed.Contains(j.VendorId));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(j => j.Status == status);
        }

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(200)
            .ToListAsync();

        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var job = await _db.AiFaqAnalysisJobs.FindAsync(id);
        if (job == null) return NotFound();

        var vendor = await _db.Vendors.FindAsync(job.VendorId);
        ViewBag.Vendor = vendor;

        var candidates = await _db.FaqCandidates
            .Where(c => c.AnalysisJobId == job.Id)
            .OrderByDescending(c => c.ConfidenceScore)
            .Take(500)
            .ToListAsync();
        ViewBag.Candidates = candidates;

        return View(job);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var job = await _db.AiFaqAnalysisJobs.FindAsync(id);
        if (job == null) return NotFound();

        job.Status = "pending";
        job.ErrorMessage = null;
        job.StartedAt = null;
        job.FinishedAt = null;

        await _db.SaveChangesAsync();

        // 將 jobId 放入 in-memory 隊列，讓 worker 可以立即處理
        try
        {
            _jobQueue.Enqueue(job.Id);
        }
        catch
        {
            // 若隊列不可用也沒關係，worker 仍會從 DB poll 到 pending job
        }

        // 記錄審計日誌
        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "AiFaqAnalysisJob.Retry",
                TargetId = job.Id,
                Payload = null,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        return RedirectToAction(nameof(Details), new { id });
    }
}
