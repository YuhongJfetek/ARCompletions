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
public class EmbeddingJobsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.IBackgroundJobQueue _jobQueue;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public EmbeddingJobsController(ARCompletionsContext db, ARCompletions.Services.IBackgroundJobQueue jobQueue, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _jobQueue = jobQueue;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index(string? vendorId = null, string? status = null, string? vectorVersion = null, int page = 1, int pageSize = 25)
    {
        var vendors = await _db.Vendors
            .OrderBy(v => v.Code)
            .ToListAsync();
        ViewBag.Vendors = vendors;

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.EmbeddingJobs.AsQueryable();

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

        if (!string.IsNullOrWhiteSpace(vectorVersion))
        {
            query = query.Where(j => j.VectorVersion == vectorVersion);
        }

        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var total = await query.LongCountAsync();

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new ARCompletions.Areas.Admin.Models.EmbeddingJobsIndexViewModel
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            VendorId = vendorId,
            Status = status,
            VectorVersion = vectorVersion
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var job = await _db.EmbeddingJobs.FindAsync(id);
        if (job == null) return NotFound();

        var vendor = await _db.Vendors.FindAsync(job.VendorId);
        ViewBag.Vendor = vendor;

        var logs = await _db.EmbeddingLogs
            .Where(l => l.EmbeddingJobId == job.Id)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();
        ViewBag.Logs = logs;

        return View(job);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var job = await _db.EmbeddingJobs.FindAsync(id);
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
                Action = "EmbeddingJob.Retry",
                TargetId = job.Id,
                Payload = null,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch
        {
            // 忽略審計寫入錯誤
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRetry(string selectedIds, bool selectAllMatched = false, string? vendorId = null, string? status = null, string? vectorVersion = null)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        List<string> ids = new List<string>();

        if (selectAllMatched)
        {
            // create BulkJob and enqueue for background processing
            var filter = new { vendorId, status, vectorVersion };
            var filterJson = System.Text.Json.JsonSerializer.Serialize(filter);

            var query = _db.EmbeddingJobs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(vendorId))
            {
                if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
                query = query.Where(j => j.VendorId == vendorId);
            }
            else if (allowed != null)
            {
                query = query.Where(j => allowed.Contains(j.VendorId));
            }

            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(j => j.Status == status);
            if (!string.IsNullOrWhiteSpace(vectorVersion)) query = query.Where(j => j.VectorVersion == vectorVersion);

            var total = await query.LongCountAsync();

            var bulk = new ARCompletions.Domain.BulkJob
            {
                Id = Guid.NewGuid().ToString("N"),
                Initiator = User?.Identity?.Name ?? "system",
                Action = "EmbeddingJob.BulkRetry",
                FilterJson = filterJson,
                Status = "queued",
                TotalCount = total,
                ProcessedCount = 0,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            _db.BulkJobs.Add(bulk);
            await _db.SaveChangesAsync();

            // enqueue bulk job id
            try
            {
                var bulkQueue = HttpContext.RequestServices.GetService(typeof(ARCompletions.Services.IBulkJobQueue)) as ARCompletions.Services.IBulkJobQueue;
                bulkQueue?.Enqueue(bulk.Id);
            }
            catch { }

            try
            {
                _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Actor = User?.Identity?.Name ?? "system",
                    Action = "BulkJob.Create",
                    TargetId = bulk.Id,
                    Payload = System.Text.Json.JsonSerializer.Serialize(new { bulk.Action, bulk.TotalCount }),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
                await _db.SaveChangesAsync();
            }
            catch { }

            TempData["BulkJobCreated"] = bulk.Id;
            return RedirectToAction(nameof(Index));
        }

        // else fall back to immediate small-batch processing
        if (string.IsNullOrWhiteSpace(selectedIds)) return BadRequest();
        ids = selectedIds.Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        if (!ids.Any()) return BadRequest();

        var jobs = await _db.EmbeddingJobs.Where(j => ids.Contains(j.Id)).ToListAsync();

        foreach (var job in jobs)
        {
            if (allowed != null && !allowed.Contains(job.VendorId)) continue; // skip not allowed

            job.Status = "pending";
            job.ErrorMessage = null;
            job.StartedAt = null;
            job.FinishedAt = null;

            try { _jobQueue.Enqueue(job.Id); } catch { }

            try
            {
                _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Actor = User?.Identity?.Name ?? "system",
                    Action = "EmbeddingJob.BulkRetry",
                    TargetId = job.Id,
                    Payload = null,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }
            catch { }
        }

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
