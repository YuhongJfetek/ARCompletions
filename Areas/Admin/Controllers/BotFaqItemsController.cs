using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
// using ARCompletions.Services; (duplicate removed)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ARCompletions.Services;
using System.Collections.Generic;
using System.Text.Json;
using ARCompletions.Areas.Admin.Models;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class BotFaqItemsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly IEmbeddingRebuildService _embeddingRebuildService;
    private readonly IDbLogger _dbLogger;

    public BotFaqItemsController(ARCompletionsContext db, IEmbeddingRebuildService embeddingRebuildService, IDbLogger dbLogger)
    {
        _db = db;
        _embeddingRebuildService = embeddingRebuildService;
        _dbLogger = dbLogger;
    }

    // Bulk import removed — use data seeder or admin scripts for bulk operations.

    /// <summary>
    /// 對話分析：從 Bot 訊息與 LLM log 產生 FAQ 建議清單。
    /// 不修改資料庫，只提供管理員挑選後建立/編輯 FAQ。
    /// </summary>
    public async Task<IActionResult> Suggestions(string? route = null, bool onlyWithoutFaq = true, int days = 7, int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 50;
        if (pageSize > 200) pageSize = 200;
        if (days <= 0) days = 7;

        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var routeQuery = _db.BotMessageRoutes
            .AsNoTracking()
            .Where(r => r.CreatedAt >= since);

        if (onlyWithoutFaq)
        {
            routeQuery = routeQuery.Where(r => r.MatchedFaqId == null || r.MatchedFaqId == "");
        }

        if (!string.IsNullOrWhiteSpace(route))
        {
            routeQuery = routeQuery.Where(r => r.Route == route);
        }

        var total = await routeQuery.LongCountAsync();

        var routes = await routeQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var eventIds = routes
            .Where(r => r.EventRowId != null)
            .Select(r => r.EventRowId!.Value)
            .Distinct()
            .ToList();

        var events = await _db.BotIncomingEvents
            .AsNoTracking()
            .Where(e => eventIds.Contains(e.EventRowId))
            .ToListAsync();

        var llmLogs = await _db.BotLlmLogs
            .AsNoTracking()
            .Where(l => l.EventRowId != null && eventIds.Contains(l.EventRowId.Value))
            .ToListAsync();

        var eventDict = events.ToDictionary(e => e.EventRowId, e => e);

        var llmDict = llmLogs
            .GroupBy(l => l.EventRowId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        var suggestions = new List<FaqSuggestionViewModel>();

        foreach (var r in routes)
        {
            if (r.EventRowId == null)
            {
                continue;
            }

            eventDict.TryGetValue(r.EventRowId.Value, out var ev);
            llmDict.TryGetValue(r.EventRowId.Value, out var llm);

            var userText = ev != null ? TryExtractUserText(ev.RawEventJson) : null;

            suggestions.Add(new FaqSuggestionViewModel
            {
                EventRowId = r.EventRowId.Value,
                ReceivedAt = ev?.ReceivedAt ?? r.CreatedAt,
                SourceType = ev?.SourceType,
                ConversationId = ev?.ConversationId,
                LineUserId = ev?.LineUserId,
                UserText = userText,
                SuggestedAnswer = r.ReplyText,
                FaqCategory = r.FaqCategory,
                Route = r.Route,
                Reason = r.Reason,
                MatchedFaqId = r.MatchedFaqId,
                MatchedScore = r.MatchedScore,
                LlmConfidence = llm?.Confidence,
                LlmModel = llm?.Model,
                LlmReason = llm?.Reason
            });
        }

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.Days = days;
        ViewBag.Route = route;
        ViewBag.OnlyWithoutFaq = onlyWithoutFaq;

        return View(suggestions);
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

    public IActionResult Create(string? question = null, string? answer = null, string? categoryKey = null, string? faqId = null)
    {
        var model = new BotFaqItem
        {
            Enabled = true,
            NeedsHumanHandoff = false
        };

        if (!string.IsNullOrWhiteSpace(question))
        {
            model.Question = question;
        }

        if (!string.IsNullOrWhiteSpace(answer))
        {
            model.Answer = answer;
        }

        if (!string.IsNullOrWhiteSpace(categoryKey))
        {
            model.CategoryKey = categoryKey;
        }

        if (!string.IsNullOrWhiteSpace(faqId))
        {
            model.FaqId = faqId;
        }

        return View(model);
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
        catch (Exception ex)
        {
            await _dbLogger.LogAsync("Warning", "Embedding rebuild failed for created FAQ {FaqId}", new { FaqId = model.FaqId }, ex);
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
        existing.MinConfidenceScore = model.MinConfidenceScore;
        existing.SearchTextCache = model.SearchTextCache;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.UpdatedBy = User?.Identity?.Name;

        await _db.SaveChangesAsync();

        // FAQ 編輯完成後，自動觸發單筆 Embedding 重建
        try
        {
            await _embeddingRebuildService.RebuildAsync("openai", null, "single", existing.FaqId, User?.Identity?.Name ?? "admin", HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            await _dbLogger.LogAsync("Warning", "Embedding rebuild failed for updated FAQ {FaqId}", new { FaqId = existing.FaqId }, ex);
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

    // Bulk import DTO and helpers removed together with the BulkImport actions.

    private string? TryExtractUserText(string rawEventJson)
    {
        if (string.IsNullOrWhiteSpace(rawEventJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawEventJson);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("events", out var eventsElement) && eventsElement.ValueKind == JsonValueKind.Array && eventsElement.GetArrayLength() > 0)
            {
                var firstEvent = eventsElement[0];
                if (firstEvent.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.Object)
                {
                    if (messageElement.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                    {
                        return textElement.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _dbLogger.LogSync("Debug", "TryExtractUserText failed to parse raw event", null, ex);
        }

        return null;
    }
}
