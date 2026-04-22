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
using ARCompletions.Areas.Admin.Models;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class BotFaqItemsController : Controller
{
    private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletions.Data.ARCompletionsContext> _dbFactory;
    private readonly IEmbeddingRebuildService _embeddingRebuildService;
    private readonly IDbLogger _dbLogger;
    private readonly IQueryHintsService _queryHints;
    private readonly ITextProcessingService _textProcessing;
    private readonly ARCompletions.Services.IBotConstantsService _botConstants;

    public BotFaqItemsController(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletions.Data.ARCompletionsContext> dbFactory, IEmbeddingRebuildService embeddingRebuildService, IDbLogger dbLogger, IQueryHintsService queryHints, ITextProcessingService textProcessing, ARCompletions.Services.IBotConstantsService botConstants)
    {
        _dbFactory = dbFactory;
        _embeddingRebuildService = embeddingRebuildService;
        _dbLogger = dbLogger;
        _queryHints = queryHints;
        _textProcessing = textProcessing;
        _botConstants = botConstants;
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

















        using var db = _dbFactory.CreateDbContext();
        var convoQuery = db.BotIncomingEvents
            .AsNoTracking()
            .Where(e => e.ReceivedAt >= since)
            .GroupBy(e => new { e.SourceType, e.ConversationId })
            .Select(g => new
            {
                SourceType = g.Key.SourceType,
                ConversationId = g.Key.ConversationId,
                LastReceivedAt = g.Max(x => x.ReceivedAt),
                MessageCount = g.Count(),
                LastEventId = g.OrderByDescending(x => x.ReceivedAt).Select(x => (long?)x.EventRowId).FirstOrDefault()
            });

        var total = await convoQuery.LongCountAsync();

        var convos = await convoQuery
            .OrderByDescending(c => c.LastReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var lastEventIds = convos.Where(c => c.LastEventId.HasValue).Select(c => c.LastEventId!.Value).ToList();

        var lastEvents = await db.BotIncomingEvents
            .AsNoTracking()
            .Where(e => lastEventIds.Contains(e.EventRowId))
            .ToListAsync();

        var eventDict = lastEvents.ToDictionary(e => e.EventRowId, e => e);

        var summaries = convos.Select(c =>
        {
            var lastId = c.LastEventId ?? 0L;
            eventDict.TryGetValue(lastId, out var ev);
            // Prefer explicit Text column.
            var lastUserText = ev?.Text;
            return new Areas.Admin.Models.ConversationSummaryViewModel
            {
                SourceType = c.SourceType,
                ConversationId = c.ConversationId,
                LastReceivedAt = c.LastReceivedAt,
                MessageCount = c.MessageCount,
                LastUserText = lastUserText
            };
        }).ToList();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.Days = days;

        return View(summaries);
    }

    public async Task<IActionResult> Index(string? q = null, string? categoryKey = null, bool? enabled = null, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        using var db = _dbFactory.CreateDbContext();
        var query = db.BotFaqItems.AsQueryable();

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
        using var db = _dbFactory.CreateDbContext();
        var item = await db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    public async Task<IActionResult> Create(string? question = null, string? answer = null, string? categoryKey = null, string? faqId = null)
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

        // If FaqId isn't provided but we have a question, generate a safe default FaqId
        if (string.IsNullOrWhiteSpace(model.FaqId) && !string.IsNullOrWhiteSpace(model.Question))
        {
            model.FaqId = GenerateFaqIdFromQuestion(model.Question);
        }

        // Provide a sensible default QueryExamples and SearchTextCache to help admins
        if (string.IsNullOrWhiteSpace(model.QueryExamples) && !string.IsNullOrWhiteSpace(model.Question))
        {
            // store as a JSON array string so the UI shows an example search
            model.QueryExamples = System.Text.Json.JsonSerializer.Serialize(new[] { model.Question });
        }

        if (string.IsNullOrWhiteSpace(model.SearchTextCache) && !string.IsNullOrWhiteSpace(model.Question))
        {
            model.SearchTextCache = BuildSearchTextFromModel(model);
        }

        // Populate additional fields from lightweight analysis of the Question
        if (!string.IsNullOrWhiteSpace(model.Question))
        {
            var normalized = _textProcessing.Normalize(model.Question);
            // Category hints
            try
            {
                var hints = _queryHints.DetectPreferredCategoryKeys(normalized);
                if (hints != null && hints.Length > 0)
                {
                    model.CategoryKey ??= hints[0];
                    model.Category ??= hints[0];
                }
            }
            catch (Exception ex)
            {
                try
                {
                    using var dbLog = _dbFactory.CreateDbContext();
                    await _dbLogger.LogAsync(dbLog, "Warning", "Category hint detection failed", new { Question = model.Question }, ex);
                }
                catch { }
            }

            // Keywords: top distinct tokens
            try
            {
                var toks = _textProcessing.Tokenize(normalized) ?? Array.Empty<string>();
                var top = toks.GroupBy(t => t).OrderByDescending(g => g.Count()).Select(g => g.Key).Distinct().Take(8).ToArray();
                if (top.Length > 0)
                {
                    model.Keywords = System.Text.Json.JsonSerializer.Serialize(top);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    using var dbLog = _dbFactory.CreateDbContext();
                    await _dbLogger.LogAsync(dbLog, "Warning", "Keyword tokenization failed", new { Question = model.Question }, ex);
                }
                catch { }
            }

            // AliasTerms and Sources as simple JSON arrays
            if (string.IsNullOrWhiteSpace(model.AliasTerms)) model.AliasTerms = System.Text.Json.JsonSerializer.Serialize(Array.Empty<string>());
            if (string.IsNullOrWhiteSpace(model.Sources)) model.Sources = System.Text.Json.JsonSerializer.Serialize(new[] { "conversation_suggestion" });

            // Min confidence sensible default if not set
            if (!model.MinConfidenceScore.HasValue) model.MinConfidenceScore = 0.3;

            // Try to prefill Answer from recent bot replies using token-overlap scoring
            try
            {
                using var db = _dbFactory.CreateDbContext();
                var normalizedQ = _textProcessing.Normalize(model.Question) ?? string.Empty;

                // Read configurable parameters from bot_constants (fallback to sensible defaults)
                var configs = _botConstants != null ? await _botConstants.GetAllConfigsAsync().ConfigureAwait(false) : new System.Collections.Generic.List<ARCompletions.Domain.BotConstantsConfig>();
                int GetIntCfg(string key, int def)
                {
                    var c = configs.Find(x => string.Equals(x.ConfigKey, key, StringComparison.OrdinalIgnoreCase));
                    if (c == null || string.IsNullOrWhiteSpace(c.ConfigValue)) return def;
                    return int.TryParse(c.ConfigValue, out var v) ? v : def;
                }
                double GetDoubleCfg(string key, double def)
                {
                    var c = configs.Find(x => string.Equals(x.ConfigKey, key, StringComparison.OrdinalIgnoreCase));
                    if (c == null || string.IsNullOrWhiteSpace(c.ConfigValue)) return def;
                    return double.TryParse(c.ConfigValue, out var v) ? v : def;
                }

                var RECENT_MAX = GetIntCfg("bot.faq.prefillRecentMax", 200);
                var OVERLAP_THRESHOLD = GetDoubleCfg("bot.faq.prefillOverlapThreshold", 0.5);

                var candidates = await (from r in db.BotMessageRoutes.AsNoTracking()
                                        join e in db.BotIncomingEvents.AsNoTracking() on r.EventRowId equals e.EventRowId
                                        where !string.IsNullOrWhiteSpace(r.ReplyText) && !string.IsNullOrWhiteSpace(e.Text)
                                        orderby r.CreatedAt descending
                                        select new { r.ReplyText, UserText = e.Text })
                                        .Take(RECENT_MAX)
                                        .ToListAsync();

                double bestScore = 0.0;
                string? bestReply = null;
                foreach (var c in candidates)
                {
                    var userText = c.UserText ?? string.Empty;
                    var normalizedUser = _textProcessing.Normalize(userText) ?? string.Empty;
                    var ov = _textProcessing.TokenOverlapScore(normalizedQ, normalizedUser);
                    if (ov > bestScore)
                    {
                        bestScore = ov;
                        bestReply = c.ReplyText;
                    }
                }

                if (bestScore >= OVERLAP_THRESHOLD && !string.IsNullOrWhiteSpace(bestReply))
                {
                    ViewBag.AnswerDraft = bestReply;
                    if (string.IsNullOrWhiteSpace(model.Answer)) model.Answer = bestReply;
                }
                else
                {
                    var draft = GenerateDraftAnswer(model.Question);
                    ViewBag.AnswerDraft = draft;
                    if (string.IsNullOrWhiteSpace(model.Answer)) model.Answer = draft;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    using var dbLog = _dbFactory.CreateDbContext();
                    await _dbLogger.LogAsync(dbLog, "Warning", "Prefill answer (token-overlap) lookup failed", new { Question = model.Question }, ex);
                }
                catch { }

                var draft = GenerateDraftAnswer(model.Question);
                ViewBag.AnswerDraft = draft;
                if (string.IsNullOrWhiteSpace(model.Answer)) model.Answer = draft;
            }
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

        // Validate JSON-string fields and auto-correct if malformed.
        var jsonWarnings = new System.Collections.Generic.List<string>();
        string NormalizeJsonArrayField(string? raw, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "[]";
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(raw);
                if (arr == null) return "[]";
                return System.Text.Json.JsonSerializer.Serialize(arr);
            }
            catch
            {
                // fallback: wrap the raw input as single-element array to preserve data
                jsonWarnings.Add($"{fieldName}: JSON 解析失敗，已自動封裝為單元素陣列。");
                try
                {
                    return System.Text.Json.JsonSerializer.Serialize(new[] { raw });
                }
                catch
                {
                    // worst-case: return empty array
                    return "[]";
                }
            }
        }

        model.Keywords = NormalizeJsonArrayField(model.Keywords, "Keywords");
        model.QueryExamples = NormalizeJsonArrayField(model.QueryExamples, "QueryExamples");
        model.AliasTerms = NormalizeJsonArrayField(model.AliasTerms, "AliasTerms");
        model.Sources = NormalizeJsonArrayField(model.Sources, "Sources");

        // Add warnings to ModelState so the UI can show them (non-blocking)
        foreach (var w in jsonWarnings)
        {
            ModelState.AddModelError("JsonWarning", w);
        }

        if (string.IsNullOrWhiteSpace(model.FaqId))
        {
            ModelState.AddModelError(nameof(model.FaqId), "faq_id 不可為空");
            return View(model);
        }

        using var db = _dbFactory.CreateDbContext();
        var exists = await db.BotFaqItems.AnyAsync(f => f.FaqId == model.FaqId);
        if (exists)
        {
            // try to auto-resolve by appending suffixes
            var baseId = model.FaqId;
            var resolved = string.Empty;
            for (int i = 1; i <= 20; i++)
            {
                var candidate = $"{baseId}-{i}";
                var any = await db.BotFaqItems.AnyAsync(f => f.FaqId == candidate);
                if (!any)
                {
                    resolved = candidate;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(resolved))
            {
                model.FaqId = resolved;
                // inform user via ModelState (non-blocking)
                ModelState.AddModelError(nameof(model.FaqId), $"faq_id 已存在，已自動改為 '{resolved}'。");
            }
            else
            {
                ModelState.AddModelError(nameof(model.FaqId), "faq_id 已存在，請提供不同的 faq_id");
                return View(model);
            }
        }

        model.CreatedAt = DateTimeOffset.UtcNow;
        model.UpdatedAt = null;
        model.UpdatedBy = User?.Identity?.Name;

        db.BotFaqItems.Add(model);
        await db.SaveChangesAsync();

        // FAQ 建立完成後，排程單筆 Embedding 重建（非同步排程，避免阻塞管理介面）
        try
        {
            var job = await _embeddingRebuildService.StartRebuildAsync("openai", null, "single", model.FaqId, User?.Identity?.Name ?? "admin");
            if (job != null)
            {
                TempData["EmbeddingJobId"] = job.JobId.ToString();
            }
        }
        catch (Exception ex)
        {
            await _dbLogger.LogAsync(db, "Warning", "Embedding rebuild (start) failed for created FAQ {FaqId}", new { FaqId = model.FaqId }, ex, true);
            // 忽略 Embedding 排程失敗，避免影響 FAQ CRUD。詳細錯誤可從 bot_embedding_jobs 查詢。
        }

        TempData["Success"] = "FAQ 已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        using var db = _dbFactory.CreateDbContext();
        var item = await db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BotFaqItem model)
    {
        if (id != model.FaqId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        using var db = _dbFactory.CreateDbContext();
        var existing = await db.BotFaqItems.FindAsync(id);
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

        await db.SaveChangesAsync();

        // FAQ 編輯完成後，自動觸發單筆 Embedding 重建
        try
        {
            await _embeddingRebuildService.RebuildAsync("openai", null, "single", existing.FaqId, User?.Identity?.Name ?? "admin", HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            await _dbLogger.LogAsync(db, "Warning", "Embedding rebuild failed for updated FAQ {FaqId}", new { FaqId = existing.FaqId }, ex, true);
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
        using var db = _dbFactory.CreateDbContext();
        var item = await db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();

        item.Enabled = enabled;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = User?.Identity?.Name;
        await db.SaveChangesAsync();

        TempData["Success"] = enabled ? "FAQ 已啟用" : "FAQ 已停用";
        return RedirectToAction(nameof(Index));
    }

    // Bulk import DTO and helpers removed together with the BulkImport actions.

    // Raw vendor payload parsing removed — use stored `Text` on events instead.

    private static string GenerateFaqIdFromQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return Guid.NewGuid().ToString("N");
        var s = question.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_') sb.Append('-');
            // else skip punctuation
        }
        var outStr = sb.ToString().Trim('-');
        if (outStr.Length == 0) return Guid.NewGuid().ToString("N");
        if (outStr.Length > 60) outStr = outStr.Substring(0, 60);
        return outStr;
    }

    private static string GenerateDraftAnswer(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return string.Empty;
        var q = question.Trim();
        return $"建議回覆：\n針對「{q}」的查詢，可說明背景、可行解法與注意事項；請補充具體步驟與範例。";
    }

    private static string BuildSearchTextFromModel(BotFaqItem model)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(model.Question)) parts.Add(model.Question);
        if (!string.IsNullOrWhiteSpace(model.Answer)) parts.Add(model.Answer);

        // Try to parse JSON array fields and add elements; fall back to raw string when parse fails
        void TryAddJsonArray(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(raw) ?? Array.Empty<string>();
                parts.AddRange(arr.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            catch
            {
                // fallback: include raw
                parts.Add(raw);
            }
        }

        TryAddJsonArray(model.Keywords);
        TryAddJsonArray(model.QueryExamples);
        TryAddJsonArray(model.AliasTerms);

        return parts.Count == 0 ? (model.Question ?? string.Empty) : string.Join(" ", parts);
    }

}

