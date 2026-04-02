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
public class PlatformUsersController : Controller
{
    private readonly ARCompletionsContext _db;

    public PlatformUsersController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _db.PlatformUsers
            .OrderBy(u => u.Email)
            .ToListAsync();
        return View(users);
    }

    private async Task PopulateLookupsAsync(string[]? selectedRoleIds = null)
    {
        var roles = await _db.PlatformRoles
            .Where(r => r.IsActive)
            .OrderBy(r => r.Code)
            .ToListAsync();

        ViewBag.Roles = new MultiSelectList(roles, "Id", "Name", selectedRoleIds);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateLookupsAsync();
        var vm = new PlatformUserEditViewModel
        {
            IsActive = true
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlatformUserEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm.SelectedRoleIds);
            return View(vm);
        }

        var user = new PlatformUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = vm.Name,
            Email = vm.Email,
            PasswordHash = vm.Password,
            Department = vm.Department,
            JobTitle = vm.JobTitle,
            Phone = vm.Phone,
            IsActive = vm.IsActive,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedBy = User.Identity?.Name ?? "system"
        };

        _db.PlatformUsers.Add(user);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var createdBy = user.CreatedBy;

        foreach (var rid in vm.SelectedRoleIds ?? Array.Empty<string>())
        {
            _db.PlatformUserRoles.Add(new PlatformUserRole
            {
                Id = Guid.NewGuid().ToString("N"),
                PlatformUserId = user.Id,
                PlatformRoleId = rid,
                CreatedAt = now,
                CreatedBy = createdBy
            });
        }

        await _db.SaveChangesAsync();

        // 寫入審計日誌
        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "PlatformUser.Create",
                TargetId = user.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { user.Id, user.Email, user.Name }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "後台帳號已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var user = await _db.PlatformUsers.FindAsync(id);
        if (user == null) return NotFound();

        var roleIds = await _db.PlatformUserRoles
            .Where(ur => ur.PlatformUserId == id)
            .Select(ur => ur.PlatformRoleId)
            .ToArrayAsync();

        var vm = new PlatformUserEditViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Department = user.Department,
            JobTitle = user.JobTitle,
            Phone = user.Phone,
            IsActive = user.IsActive,
            SelectedRoleIds = roleIds
        };

        await PopulateLookupsAsync(roleIds);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, PlatformUserEditViewModel vm)
    {
        if (id != vm.Id) return BadRequest();

        var user = await _db.PlatformUsers.FindAsync(id);
        if (user == null) return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(vm.SelectedRoleIds);
            return View(vm);
        }

        user.Name = vm.Name;
        user.Email = vm.Email;
        if (!string.IsNullOrEmpty(vm.Password))
        {
            user.PasswordHash = vm.Password;
        }
        user.Department = vm.Department;
        user.JobTitle = vm.JobTitle;
        user.Phone = vm.Phone;
        user.IsActive = vm.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        user.UpdatedBy = User.Identity?.Name ?? "system";

        var existingRoles = await _db.PlatformUserRoles
            .Where(ur => ur.PlatformUserId == id)
            .ToListAsync();
        _db.PlatformUserRoles.RemoveRange(existingRoles);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var updatedBy = user.UpdatedBy;

        foreach (var rid in vm.SelectedRoleIds ?? Array.Empty<string>())
        {
            _db.PlatformUserRoles.Add(new PlatformUserRole
            {
                Id = Guid.NewGuid().ToString("N"),
                PlatformUserId = user.Id,
                PlatformRoleId = rid,
                CreatedAt = now,
                CreatedBy = updatedBy
            });
        }

        await _db.SaveChangesAsync();

        // 寫入審計日誌
        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "PlatformUser.Edit",
                TargetId = user.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { user.Id, user.Email, user.Name }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "後台帳號已更新";
        return RedirectToAction(nameof(Index));
    }
}