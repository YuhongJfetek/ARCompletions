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
public class BotMessagesController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotMessagesController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Routes(string? conversationId = null, string? route = null, string? reason = null, string? faqCategory = null, string? logPriority = null, int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _db.BotMessageRoutes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            query = query.Where(r => r.ConversationId == conversationId);
        }
        if (!string.IsNullOrWhiteSpace(route))
        {
            query = query.Where(r => r.Route == route);
        }
        if (!string.IsNullOrWhiteSpace(reason))
        {
            query = query.Where(r => r.Reason == reason);
        }
        if (!string.IsNullOrWhiteSpace(faqCategory))
        {
            query = query.Where(r => r.FaqCategory == faqCategory);
        }
        if (!string.IsNullOrWhiteSpace(logPriority))
        {
            query = query.Where(r => r.LogPriority == logPriority);
        }

        var total = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.ConversationId = conversationId;
        ViewBag.Route = route;
        ViewBag.Reason = reason;
        ViewBag.FaqCategory = faqCategory;
        ViewBag.LogPriority = logPriority;

        return View(items);
    }

    public async Task<IActionResult> Events(string? conversationId = null, string? userId = null, string? messageType = null, int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var query = _db.BotIncomingEvents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            query = query.Where(e => e.ConversationId == conversationId);
        }
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(e => e.LineUserId == userId);
        }
        if (!string.IsNullOrWhiteSpace(messageType))
        {
            query = query.Where(e => e.MessageType == messageType);
        }

        var total = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(e => e.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.ConversationId = conversationId;
        ViewBag.UserId = userId;
        ViewBag.MessageType = messageType;

        return View(items);
    }
}
