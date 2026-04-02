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
public class BotFaqAliasesController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotFaqAliasesController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q = null, string? mode = null, bool? enabled = null, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var query = _db.BotFaqAliases.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(a => (a.Term ?? string.Empty).Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            query = query.Where(a => a.Mode == mode);
        }

        if (enabled.HasValue)
        {
            query = query.Where(a => a.Enabled == enabled.Value);
        }

        var total = await query.LongCountAsync();
        var items = await query
            .OrderBy(a => a.Term)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.Query = q;
        ViewBag.Mode = mode;
        ViewBag.Enabled = enabled;

        return View(items);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var item = await _db.BotFaqAliases.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    public IActionResult Create()
    {
        return View(new BotFaqAlias
        {
            Mode = "disambiguation",
            Enabled = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BotFaqAlias model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.Term))
        {
            ModelState.AddModelError(nameof(model.Term), "term 不可為空");
            return View(model);
        }

        model.AliasId = Guid.NewGuid();
        model.CreatedAt = DateTimeOffset.UtcNow;
        model.UpdatedAt = null;

        _db.BotFaqAliases.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Alias 已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var item = await _db.BotFaqAliases.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, BotFaqAlias model)
    {
        if (id != model.AliasId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.BotFaqAliases.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Term = model.Term;
        existing.Synonyms = model.Synonyms;
        existing.Mode = model.Mode;
        existing.FaqIds = model.FaqIds;
        existing.Enabled = model.Enabled;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Alias 已更新";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnabled(Guid id, bool enabled)
    {
        var item = await _db.BotFaqAliases.FindAsync(id);
        if (item == null) return NotFound();

        item.Enabled = enabled;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = enabled ? "Alias 已啟用" : "Alias 已停用";
        return RedirectToAction(nameof(Index));
    }
}
