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
public class BotConstantsConfigController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotConstantsConfigController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _db.BotConstantsConfigs
            .OrderBy(c => c.ConfigKey)
            .ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotConstantsConfigs.FindAsync(id);
        if (item == null)
        {
            item = new BotConstantsConfig { ConfigKey = id };
        }
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BotConstantsConfig model)
    {
        if (id != model.ConfigKey) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.BotConstantsConfigs.FindAsync(id);
        if (existing == null)
        {
            model.UpdatedAt = DateTimeOffset.UtcNow;
            model.UpdatedBy = User?.Identity?.Name;
            _db.BotConstantsConfigs.Add(model);
        }
        else
        {
            existing.ConfigValue = model.ConfigValue;
            existing.ValueType = model.ValueType;
            existing.Description = model.Description;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = User?.Identity?.Name;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "常數設定已更新";
        return RedirectToAction(nameof(Index));
    }
}
