using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Areas.Admin.Models;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class BotConversationsController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotConversationsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? sourceType = null, bool? enabled = null, bool? hasHandoff = null, int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        // 以 settings 為主清單
        var query = _db.BotConversationSettings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(sourceType))
        {
            query = query.Where(c => c.SourceType == sourceType);
        }

        if (enabled.HasValue)
        {
            query = query.Where(c => c.Enabled == enabled.Value);
        }

        var total = await query.LongCountAsync();
        var settings = await query
            .OrderBy(c => c.SourceType).ThenBy(c => c.ConversationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 對應狀態
        var keys = settings.Select(s => new { s.SourceType, s.ConversationId }).ToList();
        var statesQuery = _db.BotConversationStates.AsQueryable();
        if (!string.IsNullOrWhiteSpace(sourceType))
        {
            statesQuery = statesQuery.Where(s => s.SourceType == sourceType);
        }
        var states = await statesQuery.ToListAsync();
        var stateDict = states.ToDictionary(s => (s.SourceType, s.ConversationId));

        // 若需要 hasHandoff 篩選，在記憶體上處理
        if (hasHandoff.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            settings = settings.Where(s =>
            {
                if (!stateDict.TryGetValue((s.SourceType, s.ConversationId), out var st)) return !hasHandoff.Value;
                var active = st.HandoffUntil.HasValue && st.HandoffUntil.Value > now;
                return hasHandoff.Value ? active : !active;
            }).ToList();
        }

        ViewBag.States = stateDict;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.SourceType = sourceType;
        ViewBag.Enabled = enabled;
        ViewBag.HasHandoff = hasHandoff;

        return View(settings);
    }

    public async Task<IActionResult> Edit(string sourceType, string conversationId)
    {
        if (string.IsNullOrEmpty(sourceType) || string.IsNullOrEmpty(conversationId)) return NotFound();
        var setting = await _db.BotConversationSettings.FindAsync(sourceType, conversationId);
        if (setting == null)
        {
            setting = new Domain.BotConversationSetting
            {
                SourceType = sourceType,
                ConversationId = conversationId,
                Enabled = true
            };
        }
        return View(setting);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string sourceType, string conversationId, Domain.BotConversationSetting model)
    {
        if (sourceType != model.SourceType || conversationId != model.ConversationId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.BotConversationSettings.FindAsync(sourceType, conversationId);
        if (existing == null)
        {
            model.UpdatedAt = DateTimeOffset.UtcNow;
            model.UpdatedBy = User?.Identity?.Name;
            _db.BotConversationSettings.Add(model);
        }
        else
        {
            existing.Enabled = model.Enabled;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = User?.Identity?.Name;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "群組設定已更新";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearHandoff(string sourceType, string conversationId)
    {
        if (string.IsNullOrEmpty(sourceType) || string.IsNullOrEmpty(conversationId)) return NotFound();
        var state = await _db.BotConversationStates.FindAsync(sourceType, conversationId);
        if (state != null)
        {
            state.HandoffStartedAt = null;
            state.HandoffUntil = null;
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "已清除 handoff 暫停";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> History(string sourceType, string conversationId)
    {
        if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(conversationId))
        {
            return NotFound();
        }

        var events = await _db.BotIncomingEvents
            .Where(e => e.SourceType == sourceType && e.ConversationId == conversationId)
            .OrderBy(e => e.ReceivedAt)
            .ToListAsync();

        if (events.Count == 0)
        {
            return View(new ConversationLogViewModel
            {
                SourceType = sourceType,
                ConversationId = conversationId,
                Messages = Array.Empty<ConversationLogMessageViewModel>()
            });
        }

        var eventIds = events.Select(e => e.EventRowId).ToList();

        var routes = await _db.BotMessageRoutes
            .Where(r => r.EventRowId != null && eventIds.Contains(r.EventRowId.Value))
            .ToListAsync();

        var llmLogs = await _db.BotLlmLogs
            .Where(l => l.EventRowId != null && eventIds.Contains(l.EventRowId.Value))
            .ToListAsync();

        var routeDict = routes
            .GroupBy(r => r.EventRowId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        var llmDict = llmLogs
            .GroupBy(l => l.EventRowId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        var first = events.First();

        var messages = events.Select(e =>
        {
            routeDict.TryGetValue(e.EventRowId, out var route);
            llmDict.TryGetValue(e.EventRowId, out var llm);
            // Use explicitly stored Text (from API request).
            string? userText = string.IsNullOrWhiteSpace(e.Text) ? null : e.Text;

            return new ConversationLogMessageViewModel
            {
                EventRowId = e.EventRowId,
                ReceivedAt = e.ReceivedAt,
                MessageType = e.MessageType,
                EventType = e.EventType,
                // raw payload removed for privacy
                UserText = userText,
                Route = route?.Route,
                Reason = route?.Reason,
                FaqCategory = route?.FaqCategory,
                MatchedFaqId = route?.MatchedFaqId,
                MatchedScore = route?.MatchedScore,
                ReplyText = route?.ReplyText,
                Confidence = llm?.Confidence,
                LlmModel = llm?.Model,
                LlmReason = llm?.Reason
            };
        }).ToList();

        var vm = new ConversationLogViewModel
        {
            SourceType = sourceType,
            ConversationId = conversationId,
            LineUserId = first.LineUserId,
            Messages = messages
        };

        return View(vm);
    }
}
