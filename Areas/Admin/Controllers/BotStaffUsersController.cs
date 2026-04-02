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
public class BotStaffUsersController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotStaffUsersController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q = null, string? role = null, bool? enabled = null)
    {
        var query = _db.BotStaffUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(u => (u.UserId ?? string.Empty).Contains(s) || (u.Name ?? string.Empty).Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (enabled.HasValue)
        {
            query = query.Where(u => u.Enabled == enabled.Value);
        }

        var items = await query.OrderBy(u => u.UserId).Take(500).ToListAsync();

        ViewBag.Query = q;
        ViewBag.Role = role;
        ViewBag.Enabled = enabled;
        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotStaffUsers.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    public IActionResult Create()
    {
        return View(new BotStaffUser { Enabled = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BotStaffUser model)
    {
        if (!ModelState.IsValid) return View(model);
        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            ModelState.AddModelError(nameof(model.UserId), "UserId 不可為空");
            return View(model);
        }

        var exists = await _db.BotStaffUsers.AnyAsync(u => u.UserId == model.UserId);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.UserId), "UserId 已存在");
            return View(model);
        }

        model.CreatedAt = DateTimeOffset.UtcNow;
        model.UpdatedAt = null;
        model.UpdatedBy = User?.Identity?.Name;

        _db.BotStaffUsers.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Staff 使用者已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotStaffUsers.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BotStaffUser model)
    {
        if (id != model.UserId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.BotStaffUsers.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = model.Name;
        existing.Role = model.Role;
        existing.Enabled = model.Enabled;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.UpdatedBy = User?.Identity?.Name;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Staff 使用者已更新";
        return RedirectToAction(nameof(Index));
    }
}
