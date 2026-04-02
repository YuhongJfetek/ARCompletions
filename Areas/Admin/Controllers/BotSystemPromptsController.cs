using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class BotSystemPromptsController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotSystemPromptsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _db.BotSystemPrompts
            .OrderBy(p => p.PromptKey)
            .ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotSystemPrompts.FindAsync(id);
        if (item == null)
        {
            item = new BotSystemPrompt { PromptKey = id };
        }
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BotSystemPrompt model)
    {
        if (id != model.PromptKey) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.BotSystemPrompts.FindAsync(id);
        if (existing == null)
        {
            model.UpdatedAt = DateTimeOffset.UtcNow;
            model.UpdatedBy = User?.Identity?.Name;
            _db.BotSystemPrompts.Add(model);
        }
        else
        {
            existing.PromptText = model.PromptText;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = User?.Identity?.Name;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Prompt 已更新";
        return RedirectToAction(nameof(Index));
    }
}
