using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "Vendor")]
public class EmbeddingJobsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.IBackgroundJobQueue _jobQueue;

    public EmbeddingJobsController(ARCompletionsContext db, ARCompletions.Services.IBackgroundJobQueue jobQueue)
    {
        _db = db;
        _jobQueue = jobQueue;
    }

    private string? GetVendorId() => User.FindFirst("VendorId")?.Value;

    public async Task<IActionResult> Index(string? status = null)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var query = _db.EmbeddingJobs.Where(j => j.VendorId == vendorId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(j => j.Status == status);

        var items = await query.OrderByDescending(j => j.CreatedAt).Take(200).ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var job = await _db.EmbeddingJobs.Where(j => j.Id == id && j.VendorId == vendorId).FirstOrDefaultAsync();
        if (job == null) return NotFound();

        var logs = await _db.EmbeddingLogs.Where(l => l.EmbeddingJobId == job.Id && l.VendorId == vendorId).OrderBy(l => l.CreatedAt).ToListAsync();
        ViewBag.Logs = logs;
        return View(job);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trigger()
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var job = new ARCompletions.Domain.EmbeddingJob
        {
            Id = Guid.NewGuid().ToString("N"),
            VendorId = vendorId,
            JobNo = "E-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "pending",
            TriggerType = "manual",
            TriggeredByType = "vendor",
            TriggeredById = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            VectorVersion = "v1",
            ModelName = "openai-embedding",
            TotalFaqCount = 0,
            SuccessCount = 0,
            FailCount = 0,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _db.EmbeddingJobs.Add(job);
        await _db.SaveChangesAsync();

        try { _jobQueue.Enqueue(job.Id); } catch { }

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "vendor",
                Action = "EmbeddingJob.Trigger",
                TargetId = job.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { job.Id, job.JobNo, job.VendorId }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "Embedding 任務已建立";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var job = await _db.EmbeddingJobs.Where(j => j.Id == id && j.VendorId == vendorId).FirstOrDefaultAsync();
        if (job == null) return NotFound();

        job.Status = "pending";
        job.ErrorMessage = null;
        job.StartedAt = null;
        job.FinishedAt = null;
        await _db.SaveChangesAsync();

        try { _jobQueue.Enqueue(job.Id); } catch { }

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "vendor",
                Action = "EmbeddingJob.Retry",
                TargetId = job.Id,
                Payload = null,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "Embedding 任務已重新排程";
        return RedirectToAction(nameof(Details), new { id });
    }
}
