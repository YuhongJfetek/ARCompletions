using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ARCompletions.Areas.Admin.Models;
using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class BotEmbeddingsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly IEmbeddingRebuildService _embeddingRebuildService;

    public BotEmbeddingsController(ARCompletionsContext db, IEmbeddingRebuildService embeddingRebuildService)
    {
        _db = db;
        _embeddingRebuildService = embeddingRebuildService;
    }


    public async Task<IActionResult> Index(string? faqId = null, bool? isActive = null, string? provider = null, string? model = null, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var query = _db.BotFaqEmbeddings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(faqId))
        {
            query = query.Where(e => e.FaqId == faqId);
        }

        if (isActive.HasValue)
        {
            query = query.Where(e => e.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(e => e.EmbeddingProvider == provider);
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            query = query.Where(e => e.EmbeddingModel == model);
        }

        var total = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(e => e.RebuiltAt ?? e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.FaqId = faqId;
        ViewBag.IsActive = isActive;
        ViewBag.Provider = provider;
        ViewBag.Model = model;

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildAll()
    {
        var triggeredBy = User?.Identity?.Name ?? "admin";

        try
        {
            var job = await _embeddingRebuildService.StartRebuildAsync("openai", null, "all", null, triggeredBy);
            // set TempData so the Index view knows this redirect came from pressing Rebuild
            TempData["RebuildTriggered"] = job.JobId.ToString();
            // redirect to Index with jobId so the UI can poll and display results
            return RedirectToAction(nameof(Index), new { jobId = job.JobId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Embeddings 全量重建失敗：" + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildFaq(string faqId)
    {
        if (string.IsNullOrWhiteSpace(faqId)) return BadRequest(new { error = "faqId required" });

        var triggeredBy = User?.Identity?.Name ?? "admin";
        try
        {
            var job = await _embeddingRebuildService.StartRebuildAsync("openai", null, "single", faqId, triggeredBy);
            TempData["RebuildTriggered"] = job.JobId.ToString();
            return Json(new { JobId = job.JobId.ToString() });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public async Task<IActionResult> Details(System.Guid id)
    {
        var item = await _db.BotFaqEmbeddings.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpGet]
    public async Task<IActionResult> RebuildStatus(Guid jobId)
    {
        var job = await _embeddingRebuildService.GetJobAsync(jobId);
        if (job == null) return NotFound();

        // Always return job + any embeddings produced since job.StartedAt (allows progressive updates while running)
        var q = _db.BotFaqEmbeddings.AsNoTracking().Where(e => e.EmbeddingProvider == job.Provider && e.EmbeddingModel == job.Model);
        if (!string.IsNullOrWhiteSpace(job.TargetFaqId)) q = q.Where(e => e.FaqId == job.TargetFaqId);
        if (job.StartedAt.HasValue)
        {
            var started = job.StartedAt.Value;
            q = q.Where(e => (e.RebuiltAt ?? e.CreatedAt) >= started);
        }

        var embeddingsList = await q.OrderByDescending(e => e.RebuiltAt ?? e.CreatedAt).Take(2000).ToListAsync();
        // readiness: job completed and at least one embedding present, or job failed (show what we have)
        var status = (job.Status ?? string.Empty).ToLowerInvariant();
        bool ready = false;
        if (status == "failed") ready = true;
        if (status == "completed" && embeddingsList != null && embeddingsList.Count > 0) ready = true;

        return Json(new { Job = job, Embeddings = embeddingsList, Ready = ready });
    }

    // Import functionality removed — embeddings should be managed via rebuilds or direct DB operations.
}
