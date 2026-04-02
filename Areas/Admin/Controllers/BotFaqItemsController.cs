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
public class BotFaqItemsController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotFaqItemsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q = null, string? categoryKey = null, bool? enabled = null, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var query = _db.BotFaqItems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(f =>
                (f.Question ?? string.Empty).Contains(s) ||
                (f.Answer ?? string.Empty).Contains(s) ||
                (f.Category ?? string.Empty).Contains(s) ||
                (f.CategoryKey ?? string.Empty).Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(categoryKey))
        {
            query = query.Where(f => f.CategoryKey == categoryKey);
        }

        if (enabled.HasValue)
        {
            query = query.Where(f => f.Enabled == enabled.Value);
        }

        var total = await query.LongCountAsync();
        var items = await query
            .OrderBy(f => f.CategoryKey)
            .ThenBy(f => f.FaqId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.Query = q;
        ViewBag.CategoryKey = categoryKey;
        ViewBag.Enabled = enabled;

        return View(items);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    public IActionResult Create()
    {
        return View(new BotFaqItem
        {
            Enabled = true,
            NeedsHumanHandoff = false
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BotFaqItem model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.FaqId))
        {
            ModelState.AddModelError(nameof(model.FaqId), "faq_id 不可為空");
            return View(model);
        }

        var exists = await _db.BotFaqItems.AnyAsync(f => f.FaqId == model.FaqId);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.FaqId), "faq_id 已存在，禁止重複");
            return View(model);
        }

        model.CreatedAt = DateTimeOffset.UtcNow;
        model.UpdatedAt = null;
        model.UpdatedBy = User?.Identity?.Name;

        _db.BotFaqItems.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "FAQ 已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BotFaqItem model)
    {
        if (id != model.FaqId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.BotFaqItems.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Question = model.Question;
        existing.Answer = model.Answer;
        existing.Category = model.Category;
        existing.CategoryKey = model.CategoryKey;
        existing.Subcategory = model.Subcategory;
        existing.Keywords = model.Keywords;
        existing.QueryExamples = model.QueryExamples;
        existing.AliasTerms = model.AliasTerms;
        existing.Sources = model.Sources;
        existing.NeedsHumanHandoff = model.NeedsHumanHandoff;
        existing.Enabled = model.Enabled;
        existing.SearchTextCache = model.SearchTextCache;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.UpdatedBy = User?.Identity?.Name;

        await _db.SaveChangesAsync();

        TempData["Success"] = "FAQ 已更新";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnabled(string id, bool enabled)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.BotFaqItems.FindAsync(id);
        if (item == null) return NotFound();

        item.Enabled = enabled;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = User?.Identity?.Name;
        await _db.SaveChangesAsync();

        TempData["Success"] = enabled ? "FAQ 已啟用" : "FAQ 已停用";
        return RedirectToAction(nameof(Index));
    }
}
