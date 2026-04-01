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
public class VendorsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public VendorsController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index()
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.Vendors.OrderBy(v => v.Code).AsQueryable();
        if (allowed != null)
        {
            query = query.Where(v => allowed.Contains(v.Id));
        }

        var vendors = await query.ToListAsync();
        return View(vendors);
    }

    public async Task<IActionResult> Create()
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null)
        {
            // vendor-scoped platform users cannot create new vendors
            return Forbid();
        }
        return View(new ARCompletions.Domain.Vendor { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ARCompletions.Domain.Vendor vendor)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null) return Forbid();

        if (!ModelState.IsValid)
        {
            return View(vendor);
        }

        vendor.Id = Guid.NewGuid().ToString("N");
        vendor.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        vendor.CreatedBy = User.Identity?.Name ?? "system";

        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();

        // 寫入審計日誌
        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "Vendor.Create",
                TargetId = vendor.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { vendor.Id, vendor.Code, vendor.Name }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "廠商已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var vendor = await _db.Vendors.FindAsync(id);
        if (vendor == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(vendor.Id)) return Forbid();

        return View(vendor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ARCompletions.Domain.Vendor vendor)
    {
        if (id != vendor.Id) return BadRequest();
        if (!ModelState.IsValid) return View(vendor);

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var existing = await _db.Vendors.FindAsync(id);
        if (existing == null) return NotFound();

        if (allowed != null && !allowed.Contains(existing.Id)) return Forbid();

        existing.Code = vendor.Code;
        existing.Name = vendor.Name;
        existing.ContactName = vendor.ContactName;
        existing.ContactEmail = vendor.ContactEmail;
        existing.ContactPhone = vendor.ContactPhone;
        existing.Address = vendor.Address;
        existing.LineChannelId = vendor.LineChannelId;
        existing.LineChannelSecret = vendor.LineChannelSecret;
        existing.LineAccessToken = vendor.LineAccessToken;
        existing.OpenAiApiKey = vendor.OpenAiApiKey;
        existing.ChatModel = vendor.ChatModel;
        existing.EmbeddingModel = vendor.EmbeddingModel;
        existing.PromptVersion = vendor.PromptVersion;
        existing.AnswerThreshold = vendor.AnswerThreshold;
        existing.IsActive = vendor.IsActive;
        existing.Remark = vendor.Remark;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        existing.UpdatedBy = User.Identity?.Name ?? "system";

        await _db.SaveChangesAsync();

        // 寫入審計日誌
        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "Vendor.Edit",
                TargetId = existing.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { existing.Id, existing.Code, existing.Name }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "廠商已更新";
        return RedirectToAction(nameof(Index));
    }
}
