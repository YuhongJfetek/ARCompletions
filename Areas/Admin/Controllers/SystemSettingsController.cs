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
public class SystemSettingsController : Controller
{
    private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletions.Data.ARCompletionsContext> _dbFactory;

    public SystemSettingsController(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletions.Data.ARCompletionsContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IActionResult> Index()
    {
        using var db = _dbFactory.CreateDbContext();
        var items = await db.SystemSettings
            .OrderBy(s => s.SettingKey)
            .ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        using var db = _dbFactory.CreateDbContext();
        var item = await db.SystemSettings.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SystemSetting model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        using var db = _dbFactory.CreateDbContext();
        var existing = await db.SystemSettings.FindAsync(id);
        if (existing == null) return NotFound();

        existing.SettingValue = model.SettingValue;
        existing.Description = model.Description;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        existing.UpdatedBy = User.Identity?.Name ?? "system";

        await db.SaveChangesAsync();

        try
        {
            db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "SystemSetting.Edit",
                TargetId = existing.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { existing.SettingKey, existing.SettingValue }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "系統設定已更新";
        TempData["Success"] = "設定已更新";
        return RedirectToAction(nameof(Index));
    }
}
