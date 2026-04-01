using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Vendor.Controllers;

[Area("Vendor")]
[Authorize(Policy = "Vendor")]
public class VendorSettingsController : Controller
{
    private readonly ARCompletionsContext _db;

    public VendorSettingsController(ARCompletionsContext db)
    {
        _db = db;
    }

    private string? GetVendorId() => User.FindFirst("VendorId")?.Value;

    public async Task<IActionResult> Index()
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var vendor = await _db.Vendors.FindAsync(vendorId);
        if (vendor == null) return NotFound();

        return View(vendor);
    }

    public async Task<IActionResult> Edit()
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();

        var vendor = await _db.Vendors.FindAsync(vendorId);
        if (vendor == null) return NotFound();

        return View(vendor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ARCompletions.Domain.Vendor model)
    {
        var vendorId = GetVendorId();
        if (vendorId == null) return Forbid();
        if (id != model.Id) return BadRequest();

        var existing = await _db.Vendors.FindAsync(id);
        if (existing == null) return NotFound();
        if (existing.Id != vendorId) return Forbid();

        if (!ModelState.IsValid) return View(model);

        // Allow vendors to update non-sensitive fields only
        existing.ContactName = model.ContactName;
        existing.ContactEmail = model.ContactEmail;
        existing.ContactPhone = model.ContactPhone;
        existing.Address = model.Address;
        existing.Remark = model.Remark;
        existing.UpdatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        existing.UpdatedBy = User.Identity?.Name ?? "vendor";

        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = System.Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "vendor",
                Action = "VendorSettings.Edit",
                TargetId = existing.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { existing.Id, existing.ContactEmail, existing.ContactName }),
                Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "設定已儲存";
        return RedirectToAction(nameof(Index));
    }
}
