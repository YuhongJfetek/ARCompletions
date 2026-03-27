using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class VendorAccountsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public VendorAccountsController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index()
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.VendorAccounts.AsQueryable();
        if (allowed != null)
        {
            query = query.Where(a => allowed.Contains(a.VendorId));
        }

        var accounts = await query
            .OrderBy(a => a.LoginEmail)
            .ToListAsync();
        return View(accounts);
    }

    public async Task<IActionResult> Create()
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null)
        {
            // platform user with vendor-scoped access cannot create new vendor accounts outside their scope
            await PopulateVendorsDropDownList();
        }

        await PopulateVendorsDropDownList();
        return View(new ARCompletions.Domain.VendorAccount { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ARCompletions.Domain.VendorAccount account)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !string.IsNullOrEmpty(account.VendorId) && !allowed.Contains(account.VendorId)) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateVendorsDropDownList(account.VendorId);
            return View(account);
        }

        account.Id = Guid.NewGuid().ToString("N");
        account.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        account.CreatedBy = User.Identity?.Name ?? "system";

        _db.VendorAccounts.Add(account);
        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "VendorAccount.Create",
                TargetId = account.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { account.Id, account.LoginEmail, account.AccountName }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var account = await _db.VendorAccounts.FindAsync(id);
        if (account == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(account.VendorId)) return Forbid();

        await PopulateVendorsDropDownList(account.VendorId);
        return View(account);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ARCompletions.Domain.VendorAccount account)
    {
        if (id != account.Id) return BadRequest();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        if (!ModelState.IsValid)
        {
            await PopulateVendorsDropDownList(account.VendorId);
            return View(account);
        }

        var existing = await _db.VendorAccounts.FindAsync(id);
        if (existing == null) return NotFound();

        if (allowed != null && !allowed.Contains(existing.VendorId)) return Forbid();

        existing.VendorId = account.VendorId;
        existing.LoginEmail = account.LoginEmail;
        existing.PasswordHash = account.PasswordHash;
        existing.AccountName = account.AccountName;
        existing.Phone = account.Phone;
        existing.IsActive = account.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        existing.UpdatedBy = User.Identity?.Name ?? "system";

        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "VendorAccount.Edit",
                TargetId = existing.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { existing.Id, existing.LoginEmail, existing.AccountName }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateVendorsDropDownList(string? selectedVendorId = null)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.Vendors
            .OrderBy(v => v.Code)
            .Select(v => new { v.Id, Display = v.Code + " - " + v.Name })
            .AsQueryable();

        if (allowed != null)
        {
            query = query.Where(v => allowed.Contains(v.Id));
        }

        var vendors = await query.ToListAsync();

        ViewBag.VendorId = new SelectList(vendors, "Id", "Display", selectedVendorId);
    }
}
