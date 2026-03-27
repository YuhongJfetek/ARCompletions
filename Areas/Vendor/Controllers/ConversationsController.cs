using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "Vendor")]
public class ConversationsController : Controller
{
    private readonly ARCompletionsContext _db;

    public ConversationsController(ARCompletionsContext db)
    {
        _db = db;
    }

    private string? GetVendorId() => User.FindFirst("VendorId")?.Value;

    public async Task<IActionResult> Index()
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var items = await _db.Conversations
            .Where(c => c.VendorId == vendorId)
            .OrderByDescending(c => c.LastMessageAt ?? c.StartedAt)
            .Take(200)
            .ToListAsync();

        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (string.IsNullOrEmpty(id)) return NotFound();

        var convo = await _db.Conversations
            .Where(c => c.VendorId == vendorId && c.Id == id)
            .FirstOrDefaultAsync();
        if (convo == null) return NotFound();

        var messages = await _db.ConversationMessages
            .Where(m => m.ConversationId == id && m.VendorId == vendorId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var inputLogs = await _db.LineBotInputLogs
            .Where(l => l.VendorId == vendorId && l.ConversationId == id)
            .OrderBy(l => l.ReceivedAt)
            .ToListAsync();

        var outputLogs = await _db.LineBotOutputLogs
            .Where(l => l.VendorId == vendorId && l.ConversationId == id)
            .OrderBy(l => l.SentAt)
            .ToListAsync();

        ViewBag.Messages = messages;
        ViewBag.InputLogs = inputLogs;
        ViewBag.OutputLogs = outputLogs;

        return View(convo);
    }
}