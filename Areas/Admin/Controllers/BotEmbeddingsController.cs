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
public class BotEmbeddingsController : Controller
{
    private readonly ARCompletionsContext _db;

    public BotEmbeddingsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? faqId = null, bool? isActive = null, string? provider = null, string? model = null, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 200) pageSize = 200;

        var query = _db.BotFaqEmbeddings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(faqId))
        {
            query = query.Where(e => e.FaqId == faqId);
        }

        if (isActive.HasValue)
        {
            query = query.Where(e => e.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(e => e.EmbeddingProvider == provider);
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            query = query.Where(e => e.EmbeddingModel == model);
        }

        var total = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(e => e.RebuiltAt ?? e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = total;
        ViewBag.FaqId = faqId;
        ViewBag.IsActive = isActive;
        ViewBag.Provider = provider;
        ViewBag.Model = model;

        return View(items);
    }

    public async Task<IActionResult> Details(System.Guid id)
    {
        var item = await _db.BotFaqEmbeddings.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }
}
