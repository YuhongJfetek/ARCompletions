using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? embeddingsFile)
    {
        if (embeddingsFile == null || embeddingsFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "請選擇 embeddings.json 檔案");
            return View();
        }

        List<EmbeddingImportDto>? items;
        try
        {
            using var stream = embeddingsFile.OpenReadStream();
            items = await JsonSerializer.DeserializeAsync<List<EmbeddingImportDto>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "JSON 解析失敗：" + ex.Message);
            return View();
        }

        if (items == null || items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "匯入資料為空");
            return View();
        }

        var existing = await _db.BotFaqEmbeddings.AsTracking().ToDictionaryAsync(x => x.FaqId);
        var now = DateTimeOffset.UtcNow;

        foreach (var src in items)
        {
            if (string.IsNullOrWhiteSpace(src.id))
            {
                continue;
            }

            if (!existing.TryGetValue(src.id, out var entity))
            {
                entity = new BotFaqEmbedding
                {
                    FaqId = src.id,
                    CreatedAt = now
                };
                _db.BotFaqEmbeddings.Add(entity);
                existing[src.id] = entity;
            }

            entity.Question = src.question;
            entity.SearchText = src.text;
            entity.CategoryKey = src.categoryKey;
            entity.EmbeddingProvider = "local_hash";
            entity.EmbeddingModel = "legacy_hash64";
            entity.VectorDim = src.embedding?.Length ?? 0;
            entity.Embedding = src.embedding ?? Array.Empty<double>();
            entity.IsActive = true;
            entity.RebuiltAt = now;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"已匯入/更新 {items.Count} 筆 Embedding";
        return RedirectToAction(nameof(Index));
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
            var job = await _embeddingRebuildService.RebuildAsync("openai", null, "all", null, triggeredBy, HttpContext.RequestAborted);
            TempData["Success"] = $"已觸發 Embeddings 全量重建：JobId={job.JobId}, Status={job.Status}, Total={job.TotalCount}, Completed={job.CompletedCount}, Failed={job.FailedCount}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Embeddings 全量重建失敗：" + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(System.Guid id)
    {
        var item = await _db.BotFaqEmbeddings.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    private sealed class EmbeddingImportDto
    {
        public string id { get; set; } = string.Empty;
        public string? question { get; set; }
        public string? text { get; set; }
        public string? categoryKey { get; set; }
        public double[]? embedding { get; set; }
    }
}
