using ARCompletions.Areas.Admin.Models;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class HomeController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly IAuthorizationService _authz;

    public HomeController(ARCompletionsContext db, IAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
    }

    // 總部後台首頁：顯示全系統統計
    public IActionResult Index()
    {
        // Aggregate basic KPIs and recent lists
        var faqCount = _db.BotFaqItems.Count();
        var convoStateCount = _db.BotConversationStates.Count();
        var routeCount = _db.BotMessageRoutes.Count();
        var pendingEmb = _db.BotEmbeddingJobs.Count(j => j.Status != "finished");

        // recent conversations (last 24h)
        var since = DateTimeOffset.UtcNow.AddDays(-1);
        var convos = _db.BotIncomingEvents
            .AsNoTracking()
            .Where(e => e.ReceivedAt >= since)
            .GroupBy(e => new { e.SourceType, e.ConversationId })
            .Select(g => new ConversationSummaryViewModel
            {
                SourceType = g.Key.SourceType,
                ConversationId = g.Key.ConversationId,
                LastReceivedAt = g.Max(x => x.ReceivedAt),
                MessageCount = g.Count(),
                LastUserText = g.OrderByDescending(x => x.ReceivedAt).Select(x => x.Text).FirstOrDefault()
            })
            .OrderByDescending(c => c.LastReceivedAt)
            .Take(8)
            .ToList();

        // recent errors (24h)
        var recentErrors = _db.AppLogs
            .AsNoTracking()
            .Where(l => l.Level != null && l.Level.ToLower() == "error" && l.TimeStamp >= since)
            .OrderByDescending(l => l.TimeStamp)
            .Take(6)
            .Select(l => l.Message ?? l.Exception ?? l.MessageTemplate ?? "(no message)")
            .ToList();

        var vm = new AdminDashboardViewModel
        {
            BotFaqItemCount = faqCount,
            BotConversationStateCount = convoStateCount,
            BotMessageRouteCount = routeCount,
            PendingEmbeddingJobs = pendingEmb,
            RecentConversations = convos,
            RecentErrors = recentErrors,
            RecentErrorsCount = _db.AppLogs.Count(l => l.Level != null && l.Level.ToLower() == "error" && l.TimeStamp >= since)
        };

        ViewData["Title"] = "總部後台首頁";

        // Debug helpers: surface authentication + claims info to the view during troubleshooting
        ViewBag.IsAuthenticated = User?.Identity?.IsAuthenticated ?? false;
        ViewBag.UserName = User?.Identity?.Name ?? "(none)";
        ViewBag.Claims = User?.Claims.Select(c => new { c.Type, c.Value }).ToList();
        var platformAuth = _authz.AuthorizeAsync(User ?? new ClaimsPrincipal(), null, "Platform").GetAwaiter().GetResult();
        ViewBag.IsPlatformAuthorized = platformAuth.Succeeded;

        return View(vm);
    }
}
