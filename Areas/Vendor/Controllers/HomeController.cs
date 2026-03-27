using System.Security.Claims;
using ARCompletions.Areas.Vendor.Models;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ARCompletions.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "Vendor")]
public class HomeController : Controller
{
    private readonly ARCompletionsContext _db;

    public HomeController(ARCompletionsContext db)
    {
        _db = db;
    }

    // 廠商後台首頁：顯示自己廠商的使用概況
    public IActionResult Index()
    {
        var vendorId = User.FindFirst("VendorId")?.Value;
        if (string.IsNullOrEmpty(vendorId))
        {
            return Forbid();
        }

        var vm = new VendorDashboardViewModel
        {
            VendorId = vendorId,
            ConversationCount = _db.Conversations.Count(c => c.VendorId == vendorId),
            FaqCount = _db.Faqs.Count(f => f.VendorId == vendorId),
            FaqCandidateCount = _db.FaqCandidates.Count(c => c.VendorId == vendorId),
            EmbeddingJobCount = _db.EmbeddingJobs.Count(j => j.VendorId == vendorId)
        };

        ViewData["Title"] = "廠商後台首頁";
        return View(vm);
    }
}
