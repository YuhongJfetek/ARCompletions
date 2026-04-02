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
public class EmbeddingSettingsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public EmbeddingSettingsController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index(string? vendorId = null)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var vendors = await _db.Vendors.OrderBy(v => v.Code).ToListAsync();
        ViewBag.Vendors = vendors;

        var query = _db.Vendors.AsQueryable();
        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
            query = query.Where(v => v.Id == vendorId);
        }
        else if (allowed != null)
        {
            query = query.Where(v => allowed.Contains(v.Id));
        }

        var list = await query.Select(v => new
        {
            v.Id,
            v.Code,
            Setting = _db.EmbeddingSettings.FirstOrDefault(s => s.VendorId == v.Id)
        }).ToListAsync();

        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string vendorId, string? activeVectorVersion)
    {
        if (string.IsNullOrWhiteSpace(vendorId)) return BadRequest();
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(vendorId)) return Forbid();

        var setting = await _db.EmbeddingSettings.FirstOrDefaultAsync(s => s.VendorId == vendorId);
        if (setting == null)
        {
            setting = new ARCompletions.Domain.EmbeddingSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                VendorId = vendorId,
                ActiveVectorVersion = activeVectorVersion,
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            _db.EmbeddingSettings.Add(setting);
        }
        else
        {
            setting.ActiveVectorVersion = activeVectorVersion;
            setting.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _db.EmbeddingSettings.Update(setting);
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "已更新 Embedding 設定";
        var vendor = await _db.Vendors.FindAsync(vendorId);
        if (vendor == null)
        {
            TempData["Error"] = "找不到對應的廠商，無法顯示設定。";
            return RedirectToAction("Index", "Vendors", new { area = "Admin" });
        }
        return RedirectToAction(nameof(Index), new { vendorId });
    }
}
