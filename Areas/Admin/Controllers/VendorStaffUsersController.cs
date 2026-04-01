using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class VendorStaffUsersController : Controller
{
    private readonly ARCompletionsContext _db;

    public VendorStaffUsersController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _db.VendorStaffUsers.OrderByDescending(v => v.CreatedAt).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.VendorStaffUsers.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VendorStaffUser model)
    {
        if (!ModelState.IsValid) return View(model);
        model.Id = Guid.NewGuid().ToString("N");
        model.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _db.VendorStaffUsers.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "廠商員工已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.VendorStaffUsers.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, VendorStaffUser model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        _db.Entry(model).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        TempData["Success"] = "廠商員工已更新";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.VendorStaffUsers.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var item = await _db.VendorStaffUsers.FindAsync(id);
        if (item != null)
        {
            _db.VendorStaffUsers.Remove(item);
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "廠商員工已刪除";
        return RedirectToAction(nameof(Index));
    }
}
