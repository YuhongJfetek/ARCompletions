using ARCompletions.Areas.Admin.Models;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
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
        var vm = new AdminDashboardViewModel
        {
            VendorCount = _db.Vendors.Count(),
            ActiveVendorCount = _db.Vendors.Count(v => v.IsActive),
            ConversationCount = _db.Conversations.Count(),
            FaqCount = _db.Faqs.Count(),
            FaqCandidateCount = _db.FaqCandidates.Count(),
            EmbeddingJobCount = _db.EmbeddingJobs.Count(),
            AiFaqAnalysisJobCount = _db.AiFaqAnalysisJobs.Count()
        };

        ViewData["Title"] = "總部後台首頁";

        // Debug helpers: surface authentication + claims info to the view during troubleshooting
        ViewBag.IsAuthenticated = User?.Identity?.IsAuthenticated ?? false;
        ViewBag.UserName = User?.Identity?.Name ?? "(none)";
        ViewBag.Claims = User?.Claims.Select(c => new { c.Type, c.Value }).ToList();
        var platformAuth = _authz.AuthorizeAsync(User, null, "Platform").GetAwaiter().GetResult();
        ViewBag.IsPlatformAuthorized = platformAuth.Succeeded;

        return View(vm);
    }
}
