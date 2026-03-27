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
public class AiFaqAnalysisJobsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.IBackgroundJobQueue _jobQueue;

    public AiFaqAnalysisJobsController(ARCompletionsContext db, ARCompletions.Services.IBackgroundJobQueue jobQueue)
    {
        _db = db;
        _jobQueue = jobQueue;
    }

    private string? GetVendorId() => User.FindFirst("VendorId")?.Value;

    public async Task<IActionResult> Index()
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var items = await _db.AiFaqAnalysisJobs
            .Where(j => j.VendorId == vendorId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(200)
            .ToListAsync();

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trigger(long? dateFrom = null, long? dateTo = null)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var job = new ARCompletions.Domain.AiFaqAnalysisJob
        {
            Id = Guid.NewGuid().ToString("N"),
            VendorId = vendorId,
            JobNo = "AIFAQ-" + now,
            Status = "pending",
            DateFrom = dateFrom ?? 0,
            DateTo = dateTo ?? now,
            ConversationCount = 0,
            MessageCount = 0,
            CandidateCount = 0,
            TriggeredByType = "vendor",
            TriggeredById = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            CreatedAt = now
        };

        _db.AiFaqAnalysisJobs.Add(job);
        await _db.SaveChangesAsync();

        try { _jobQueue.Enqueue(job.Id); } catch { }

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "vendor",
                Action = "AiFaqAnalysisJob.Trigger",
                TargetId = job.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { job.Id, job.JobNo, job.VendorId, job.DateFrom, job.DateTo }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var job = await _db.AiFaqAnalysisJobs.Where(j => j.Id == id && j.VendorId == vendorId).FirstOrDefaultAsync();
        if (job == null) return NotFound();

        var candidates = await _db.FaqCandidates.Where(c => c.AnalysisJobId == job.Id).OrderByDescending(c => c.ConfidenceScore).Take(500).ToListAsync();
        ViewBag.Candidates = candidates;

        return View(job);
    }
}
