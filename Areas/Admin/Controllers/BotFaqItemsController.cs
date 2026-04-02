using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Text.Json;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class BotFaqItemsController : Controller
{
    private readonly ARCompletionsContext _db;

    private readonly IEmbeddingRebuildService _embeddingRebuildService;

    public BotFaqItemsController(ARCompletionsContext db, IEmbeddingRebuildService embeddingRebuildService)
    {
        _db = db;
        _embeddingRebuildService = embeddingRebuildService;
    }

    public IActionResult BulkImport()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkImport(IFormFile? faqFile)
    {
        if (faqFile == null || faqFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "請選擇 faq.json 檔案");
            return View();
        }

        List<FaqImportDto>? items;
        try
        {
            using var stream = faqFile.OpenReadStream();
            items = await JsonSerializer.DeserializeAsync<List<FaqImportDto>>(stream, new JsonSerializerOptions
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

        var existing = await _db.BotFaqItems.AsTracking().ToDictionaryAsync(x => x.FaqId);
        var now = DateTimeOffset.UtcNow;
        var user = User?.Identity?.Name ?? "import";

        foreach (var src in items)
        {
            if (string.IsNullOrWhiteSpace(src.id))
            {
                continue;
            }

            if (!existing.TryGetValue(src.id, out var entity))
            {
                entity = new BotFaqItem
                {
                    FaqId = src.id,
                    CreatedAt = now
                };
                _db.BotFaqItems.Add(entity);
                existing[src.id] = entity;
            }

            entity.Question = src.question ?? string.Empty;
            entity.Answer = src.answer ?? string.Empty;
            entity.Category = src.category;
            entity.CategoryKey = src.categoryKey;
            entity.Subcategory = src.subcategory;
            entity.Keywords = SerializeJsonArray(src.keywords);
            entity.QueryExamples = SerializeJsonArray(src.queryExamples);
            entity.AliasTerms = SerializeJsonArray(src.aliasTerms);
            entity.Sources = SerializeJsonArray(src.sources);
            entity.NeedsHumanHandoff = src.needsHumanHandoff;
            entity.Enabled = src.enabled;
            entity.SearchTextCache = BuildSearchText(src);
            entity.UpdatedAt = now;
            entity.UpdatedBy = user;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"已匯入/更新 {items.Count} 筆 FAQ";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Index(string? q = null, string? categoryKey = null, bool? enabled = null, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var query = _db.BotFaqItems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(f =>
                (f.Question ?? string.Empty).Contains(s) ||
                (f.Answer ?? string.Empty).Contains(s) ||
                (f.Category ?? string.Empty).Contains(s) ||
                (f.CategoryKey ?? string.Empty).Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(categoryKey))
        {
            query = query.Where(f => f.CategoryKey == categoryKey);
        }

        if (enabled.HasValue)
        {
            query = query.Where(f => f.Enabled == enabled.Value);
        }

        var total = await query.LongCountAsync();
        var items = await query
            .OrderBy(f => f.CategoryKey)
            .ThenBy(f => f.FaqId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.Query = q;
        ViewBag.CategoryKey = categoryKey;
        ViewBag.Enabled = enabled;

        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    public IActionResult Create()
    {
        return View(new BotFaqItem
        {
            Enabled = true,
            NeedsHumanHandoff = false
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BotFaqItem model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.FaqId))
        {
            ModelState.AddModelError(nameof(model.FaqId), "faq_id 不可為空");
            return View(model);
        }

        var exists = await _db.BotFaqItems.AnyAsync(f => f.FaqId == model.FaqId);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.FaqId), "faq_id 已存在，禁止重複");
            return View(model);
        }

        model.CreatedAt = DateTimeOffset.UtcNow;
        model.UpdatedAt = null;
        model.UpdatedBy = User?.Identity?.Name;

        _db.BotFaqItems.Add(model);
        await _db.SaveChangesAsync();

        // FAQ 建立完成後，自動觸發單筆 Embedding 重建（不中斷 FAQ 建立流程，錯誤記錄在 job 中）
        try
        {
            await _embeddingRebuildService.RebuildAsync("openai", null, "single", model.FaqId, User?.Identity?.Name ?? "admin", HttpContext.RequestAborted);
        }
        catch
        {
            // 忽略 Embedding 失敗，避免影響 FAQ CRUD。詳細錯誤可從 bot_embedding_jobs 查詢。
        }

        TempData["Success"] = "FAQ 已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BotFaqItem model)
    {
        if (id != model.FaqId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.BotFaqItems.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Question = model.Question;
        existing.Answer = model.Answer;
        existing.Category = model.Category;
        existing.CategoryKey = model.CategoryKey;
        existing.Subcategory = model.Subcategory;
        existing.Keywords = model.Keywords;
        existing.QueryExamples = model.QueryExamples;
        existing.AliasTerms = model.AliasTerms;
        existing.Sources = model.Sources;
        existing.NeedsHumanHandoff = model.NeedsHumanHandoff;
        existing.Enabled = model.Enabled;
        existing.SearchTextCache = model.SearchTextCache;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.UpdatedBy = User?.Identity?.Name;

        await _db.SaveChangesAsync();

        // FAQ 編輯完成後，自動觸發單筆 Embedding 重建
        try
        {
            await _embeddingRebuildService.RebuildAsync("openai", null, "single", existing.FaqId, User?.Identity?.Name ?? "admin", HttpContext.RequestAborted);
        }
        catch
        {
            // 忽略 Embedding 失敗，避免影響 FAQ CRUD。詳細錯誤可從 bot_embedding_jobs 查詢。
        }

        TempData["Success"] = "FAQ 已更新";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnabled(string id, bool enabled)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();

        item.Enabled = enabled;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = User?.Identity?.Name;
        await _db.SaveChangesAsync();

        TempData["Success"] = enabled ? "FAQ 已啟用" : "FAQ 已停用";
        return RedirectToAction(nameof(Index));
    }

    private static string? SerializeJsonArray(string[]? values)
    {
        if (values == null || values.Length == 0)
        {
            return "[]";
        }

        return JsonSerializer.Serialize(values);
    }

    private static string? BuildSearchText(FaqImportDto src)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(src.question)) parts.Add(src.question);
        if (!string.IsNullOrWhiteSpace(src.answer)) parts.Add(src.answer);
        if (src.keywords != null) parts.AddRange(src.keywords);
        if (src.queryExamples != null) parts.AddRange(src.queryExamples);
        if (src.aliasTerms != null) parts.AddRange(src.aliasTerms);
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private sealed class FaqImportDto
    {
        public string id { get; set; } = string.Empty;
        public string? question { get; set; }
        public string? answer { get; set; }
        public string? category { get; set; }
        public string? categoryKey { get; set; }
        public string? subcategory { get; set; }
        public string[]? keywords { get; set; }
        public string[]? queryExamples { get; set; }
        public string[]? aliasTerms { get; set; }
        public string[]? sources { get; set; }
        public bool needsHumanHandoff { get; set; }
        public bool enabled { get; set; } = true;
    }
}
