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

        // Determine active vector version per vendor from most recent successful job (Status == "done")
        var vendorIds = items.Select(j => j.VendorId).Distinct().ToList();
        var activeDict = new System.Collections.Generic.Dictionary<string, string>();
        if (vendorIds.Any())
        {
            var completed = await _db.EmbeddingJobs
                .Where(j => vendorIds.Contains(j.VendorId) && j.Status == "done")
                .GroupBy(j => j.VendorId)
                .Select(g => g.OrderByDescending(j => j.FinishedAt ?? j.CreatedAt).FirstOrDefault())
                .ToListAsync();

            foreach (var c in completed)
            {
                if (c != null && !string.IsNullOrEmpty(c.VectorVersion)) activeDict[c.VendorId] = c.VectorVersion;
            }

            // fallback: use done_with_errors if no clean 'done' exists
            var missing = vendorIds.Where(v => !activeDict.ContainsKey(v)).ToList();
            if (missing.Any())
            {
                var fallback = await _db.EmbeddingJobs
                    .Where(j => missing.Contains(j.VendorId) && j.Status == "done_with_errors")
                    .GroupBy(j => j.VendorId)
                    .Select(g => g.OrderByDescending(j => j.FinishedAt ?? j.CreatedAt).FirstOrDefault())
                    .ToListAsync();

                foreach (var f in fallback)
                {
                    if (f != null && !string.IsNullOrEmpty(f.VectorVersion)) activeDict[f.VendorId] = f.VectorVersion;
                }
            }
        }

        ViewBag.ActiveVectorVersion = activeDict;

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

    public async Task<IActionResult> ExportCsv(string? vendorId = null, string? status = null, string? vectorVersion = null)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        var query = _db.EmbeddingJobs.AsNoTracking().AsQueryable();

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

        var items = await query.OrderByDescending(j => j.CreatedAt).Take(10000).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,JobNo,VendorId,Status,VectorVersion,TotalFaqCount,SuccessCount,FailCount,CreatedAt,StartedAt,FinishedAt,ErrorMessage");
        foreach (var j in items)
        {
            var line = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",{5},{6},{7},{8},{9},{10},\"{11}\"",
                j.Id,
                (j.JobNo ?? "").Replace("\"","'"),
                (j.VendorId ?? "").Replace("\"","'"),
                (j.Status ?? "").Replace("\"","'"),
                (j.VectorVersion ?? "").Replace("\"","'"),
                j.TotalFaqCount,
                j.SuccessCount,
                j.FailCount,
                j.CreatedAt,
                j.StartedAt ?? 0,
                j.FinishedAt ?? 0,
                (j.ErrorMessage ?? "").Replace("\"","'")
            );
            sb.AppendLine(line);
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", "embedding_jobs.csv");
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

        TempData["Success"] = "Embedding 任務已重新排程";
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

        TempData["Success"] = "已重新排程選取的 Embedding 任務";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TriggerManual(string vendorId, string vectorVersion = "v1", string modelName = "openai-embedding")
    {
        if (string.IsNullOrWhiteSpace(vendorId)) return BadRequest();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(vendorId)) return Forbid();

        var job = new ARCompletions.Domain.EmbeddingJob
        {
            Id = Guid.NewGuid().ToString("N"),
            VendorId = vendorId,
            JobNo = "E-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "pending",
            TriggerType = "manual",
            TriggeredByType = "admin",
            TriggeredById = User?.Identity?.Name ?? "admin",
            VectorVersion = vectorVersion,
            ModelName = modelName,
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
                Actor = User?.Identity?.Name ?? "admin",
                Action = "EmbeddingJob.TriggerManual",
                TargetId = job.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { job.Id, job.JobNo, job.VendorId, job.VectorVersion }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["TriggeredJobId"] = job.Id;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActiveVector(string jobId)
    {
        if (string.IsNullOrEmpty(jobId)) return BadRequest();

        var job = await _db.EmbeddingJobs.FindAsync(jobId);
        if (job == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(job.VendorId)) return Forbid();

        // Upsert EmbeddingSetting for vendor
        var setting = await _db.EmbeddingSettings.FirstOrDefaultAsync(s => s.VendorId == job.VendorId);
        if (setting == null)
        {
            setting = new ARCompletions.Domain.EmbeddingSetting
            {
                Id = System.Guid.NewGuid().ToString("N"),
                VendorId = job.VendorId,
                ActiveVectorVersion = job.VectorVersion,
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            _db.EmbeddingSettings.Add(setting);
        }
        else
        {
            setting.ActiveVectorVersion = job.VectorVersion;
            setting.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _db.EmbeddingSettings.Update(setting);
        }

        await _db.SaveChangesAsync();

        TempData["SetActiveJobId"] = jobId;
        return RedirectToAction(nameof(Index));
    }
}
