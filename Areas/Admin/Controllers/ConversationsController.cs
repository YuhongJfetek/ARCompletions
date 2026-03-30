using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class ConversationsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public ConversationsController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index(string? vendorId = null, string? filter = null, double? confidenceThreshold = null)
    {
        var vendors = await _db.Vendors
            .OrderBy(v => v.Code)
            .ToListAsync();
        ViewBag.Vendors = new SelectList(vendors, "Id", "Name", vendorId);

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.Conversations.AsQueryable();
        if (!string.IsNullOrEmpty(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
            query = query.Where(c => c.VendorId == vendorId);
        }
        else if (allowed != null)
        {
            query = query.Where(c => allowed.Contains(c.VendorId));
        }

        var items = await query
            .OrderByDescending(c => c.LastMessageAt ?? c.StartedAt)
            .Take(200)
            .ToListAsync();

        // 查出每個 conversation 最近一則 message 的 SourceType/SourceFaqId/ConfidenceScore
        var convoIds = items.Select(i => i.Id).ToList();
        var lastMessages = await _db.ConversationMessages
            .Where(m => convoIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt).FirstOrDefault())
            .ToListAsync();

        var summary = lastMessages
            .Where(m => m != null)
            .ToDictionary(m => m!.ConversationId, m => new {
                Route = m!.SourceType,
                MatchedFaqId = m!.SourceFaqId,
                Confidence = m!.ConfidenceScore
            });

        ViewBag.MessageSummary = summary;

        // Apply filter in-memory (no DB changes)
        if (!string.IsNullOrEmpty(filter))
        {
            if (filter == "matched")
            {
                items = items.Where(i => summary.ContainsKey(i.Id) && !string.IsNullOrEmpty(summary[i.Id].MatchedFaqId)).ToList();
            }
            else if (filter == "low_confidence")
            {
                var thr = confidenceThreshold ?? 0.5;
                items = items.Where(i => summary.ContainsKey(i.Id) && (summary[i.Id].Confidence == null || (double?)summary[i.Id].Confidence < thr)).ToList();
            }
        }

        ViewBag.ActiveFilter = filter;
        ViewBag.ConfidenceThreshold = confidenceThreshold;

        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var convo = await _db.Conversations.FindAsync(id);
        if (convo == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(convo.VendorId)) return Forbid();

        var messages = await _db.ConversationMessages
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var inputLogs = await _db.LineBotInputLogs
            .Where(l => l.ConversationId == id)
            .OrderBy(l => l.ReceivedAt)
            .ToListAsync();

        var outputLogs = await _db.LineBotOutputLogs
            .Where(l => l.ConversationId == id)
            .OrderBy(l => l.SentAt)
            .ToListAsync();

        ViewBag.Messages = messages;
        ViewBag.InputLogs = inputLogs;
        ViewBag.OutputLogs = outputLogs;

        return View(convo);
    }
}