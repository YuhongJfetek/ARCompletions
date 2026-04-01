using System;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Areas.Admin.Models;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class PlatformRolesController : Controller
{
    private readonly ARCompletionsContext _db;

    public PlatformRolesController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _db.PlatformRoles
            .OrderBy(r => r.Code)
            .ToListAsync();
        return View(roles);
    }

    public IActionResult Create()
    {
        return View(new PlatformRole { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlatformRole role)
    {
        if (!ModelState.IsValid)
        {
            return View(role);
        }

        role.Id = Guid.NewGuid().ToString("N");
        role.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        role.CreatedBy = User.Identity?.Name ?? "system";

        _db.PlatformRoles.Add(role);
        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "PlatformRole.Create",
                TargetId = role.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { role.Id, role.Code, role.Name }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "角色已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var role = await _db.PlatformRoles.FindAsync(id);
        if (role == null) return NotFound();

        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, PlatformRole role)
    {
        if (id != role.Id) return BadRequest();
        if (!ModelState.IsValid) return View(role);

        var existing = await _db.PlatformRoles.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = role.Name;
        existing.Code = role.Code;
        existing.Description = role.Description;
        existing.IsActive = role.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        existing.UpdatedBy = User.Identity?.Name ?? "system";

        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "PlatformRole.Edit",
                TargetId = existing.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { existing.Id, existing.Code, existing.Name }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "角色已更新";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Permissions(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var role = await _db.PlatformRoles.FindAsync(id);
        if (role == null) return NotFound();

        var allPermissions = await _db.PlatformPermissions
            .OrderBy(p => p.Code)
            .ToListAsync();

        var selectedIds = await _db.PlatformRolePermissions
            .Where(rp => rp.PlatformRoleId == id)
            .Select(rp => rp.PlatformPermissionId)
            .ToArrayAsync();

        var vm = new PlatformRolePermissionsViewModel
        {
            RoleId = role.Id,
            RoleName = role.Name,
            SelectedPermissionIds = selectedIds
        };

        ViewBag.AllPermissions = new SelectList(allPermissions, "Id", "Name");
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(PlatformRolePermissionsViewModel vm)
    {
        var role = await _db.PlatformRoles.FindAsync(vm.RoleId);
        if (role == null) return NotFound();

        var existing = await _db.PlatformRolePermissions
            .Where(rp => rp.PlatformRoleId == vm.RoleId)
            .ToListAsync();

        _db.PlatformRolePermissions.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var createdBy = User.Identity?.Name ?? "system";

        foreach (var pid in vm.SelectedPermissionIds ?? Array.Empty<string>())
        {
            _db.PlatformRolePermissions.Add(new PlatformRolePermission
            {
                Id = Guid.NewGuid().ToString("N"),
                PlatformRoleId = vm.RoleId,
                PlatformPermissionId = pid,
                CreatedAt = now,
                CreatedBy = createdBy
            });
        }

        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "PlatformRole.Permissions.Update",
                TargetId = vm.RoleId,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { Selected = vm.SelectedPermissionIds }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "角色權限已更新";
        return RedirectToAction(nameof(Index));
    }
}