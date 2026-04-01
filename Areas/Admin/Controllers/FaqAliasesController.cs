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
public class FaqAliasesController : Controller
{
    private readonly ARCompletionsContext _db;

    public FaqAliasesController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _db.FaqAliases.OrderByDescending(f => f.CreatedAt).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.FaqAliases.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FaqAlias model)
    {
        if (!ModelState.IsValid) return View(model);
        model.Id = Guid.NewGuid().ToString("N");
        model.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _db.FaqAliases.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "FAQ Alias 已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.FaqAliases.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, FaqAlias model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        model.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _db.Entry(model).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        TempData["Success"] = "FAQ Alias 已更新";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.FaqAliases.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var item = await _db.FaqAliases.FindAsync(id);
        if (item != null)
        {
            _db.FaqAliases.Remove(item);
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "FAQ Alias 已刪除";
        return RedirectToAction(nameof(Index));
    }
}
