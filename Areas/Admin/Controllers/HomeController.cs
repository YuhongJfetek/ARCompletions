using ARCompletions.Areas.Admin.Models;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class HomeController : Controller
{
    private readonly ARCompletionsContext _db;

    public HomeController(ARCompletionsContext db)
    {
        _db = db;
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
        return View(vm);
    }
}
