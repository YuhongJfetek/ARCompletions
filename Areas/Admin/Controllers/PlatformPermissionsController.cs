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
public class PlatformPermissionsController : Controller
{
    private readonly ARCompletionsContext _db;

    public PlatformPermissionsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _db.PlatformPermissions
            .OrderBy(p => p.GroupName)
            .ThenBy(p => p.Code)
            .ToListAsync();
        return View(items);
    }

    public IActionResult Create()
    {
        return View(new PlatformPermission());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlatformPermission perm)
    {
        if (!ModelState.IsValid) return View(perm);

        perm.Id = Guid.NewGuid().ToString("N");
        perm.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        perm.CreatedBy = User.Identity?.Name ?? "system";

        _db.PlatformPermissions.Add(perm);
        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "PlatformPermission.Create",
                TargetId = perm.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { perm.Id, perm.Code, perm.Name }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "權限已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var perm = await _db.PlatformPermissions.FindAsync(id);
        if (perm == null) return NotFound();
        return View(perm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, PlatformPermission perm)
    {
        if (id != perm.Id) return BadRequest();
        if (!ModelState.IsValid) return View(perm);

        var existing = await _db.PlatformPermissions.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Code = perm.Code;
        existing.Name = perm.Name;
        existing.GroupName = perm.GroupName;
        existing.Description = perm.Description;

        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = User?.Identity?.Name ?? "system",
                Action = "PlatformPermission.Edit",
                TargetId = existing.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { existing.Id, existing.Code, existing.Name }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "權限已更新";
        return RedirectToAction(nameof(Index));
    }

    // Matrix view: roles x permissions editable grid
    public async Task<IActionResult> Matrix()
    {
        var permissions = await _db.PlatformPermissions
            .OrderBy(p => p.GroupName)
            .ThenBy(p => p.Code)
            .ToListAsync();

        var roles = await _db.PlatformRoles
            .OrderBy(r => r.Code)
            .ToListAsync();

        var existing = await _db.PlatformRolePermissions
            .ToListAsync();

        ViewBag.Permissions = permissions;
        ViewBag.Roles = roles;
        ViewBag.Existing = existing;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Matrix(string[] assignments)
    {
        // assignments: array of strings formatted as "{roleId}|{permId}"
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var actor = User?.Identity?.Name ?? "system";

        // Parse desired mapping grouped by role
        var desired = new Dictionary<string, HashSet<string>>();
        if (assignments != null)
        {
            foreach (var a in assignments)
            {
                var parts = a.Split('|');
                if (parts.Length != 2) continue;
                var roleId = parts[0];
                var permId = parts[1];
                if (!desired.ContainsKey(roleId)) desired[roleId] = new HashSet<string>();
                desired[roleId].Add(permId);
            }
        }

        // Load existing mappings
        var existing = await _db.PlatformRolePermissions.ToListAsync();
        var existingByRole = existing.GroupBy(e => e.PlatformRoleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PlatformPermissionId).ToHashSet());

        // Compute diffs per role
        var rolesToProcess = new HashSet<string>(existingByRole.Keys.Concat(desired.Keys));

        var adds = new List<Domain.PlatformRolePermission>();
        var removes = new List<Domain.PlatformRolePermission>();

        foreach (var roleId in rolesToProcess)
        {
            existingByRole.TryGetValue(roleId, out var exSet);
            exSet ??= new HashSet<string>();
            desired.TryGetValue(roleId, out var deSet);
            deSet ??= new HashSet<string>();

            var (toAdd, toRemove) = ARCompletions.Services.PlatformPermissionHelper.ComputeDiffs(exSet, deSet);

            foreach (var pid in toAdd)
            {
                adds.Add(new Domain.PlatformRolePermission
                {
                    Id = Guid.NewGuid().ToString("N"),
                    PlatformRoleId = roleId,
                    PlatformPermissionId = pid,
                    CreatedAt = now,
                    CreatedBy = actor
                });
            }

            foreach (var pid in toRemove)
            {
                var toRemoveEntity = existing.FirstOrDefault(e => e.PlatformRoleId == roleId && e.PlatformPermissionId == pid);
                if (toRemoveEntity != null) removes.Add(toRemoveEntity);
            }
        }

        if (removes.Any()) _db.PlatformRolePermissions.RemoveRange(removes);
        if (adds.Any()) _db.PlatformRolePermissions.AddRange(adds);

        await _db.SaveChangesAsync();

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = actor,
                Action = "PlatformRole.Permissions.MatrixUpdate",
                TargetId = null,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { Added = adds.Count, Removed = removes.Count }),
                Timestamp = now
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        TempData["Success"] = "角色權限矩陣已更新";
        return RedirectToAction(nameof(Matrix));
    }
}