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
public class MessageRoutesController : Controller
{
    private readonly ARCompletionsContext _db;

    public MessageRoutesController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 25, long? dateFrom = null, long? dateTo = null, string? vendorId = null, string? route = null, string? matchedFaqId = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize <= 0 ? 25 : pageSize;
        var query = _db.MessageRoutes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Join(_db.Conversations, m => m.ConversationId, c => c.Id, (m, c) => new { m, c })
                         .Where(x => x.c.VendorId == vendorId)
                         .Select(x => x.m);
        }
        if (dateFrom.HasValue) query = query.Where(m => m.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(m => m.CreatedAt <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(route)) query = query.Where(m => m.Route == route || m.Route.Contains(route));
        if (!string.IsNullOrWhiteSpace(matchedFaqId)) query = query.Where(m => m.MatchedFaqId == matchedFaqId);

        query = query.OrderByDescending(m => m.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.FilterDateFrom = dateFrom;
        ViewBag.FilterDateTo = dateTo;
        ViewBag.FilterVendor = vendorId;
        ViewBag.FilterRoute = route;
        ViewBag.FilterMatchedFaq = matchedFaqId;
        return View(items);
    }

    public async Task<IActionResult> ExportCsv(long? dateFrom = null, long? dateTo = null, string? vendorId = null, string? route = null, string? matchedFaqId = null)
    {
        var query = _db.MessageRoutes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Join(_db.Conversations, m => m.ConversationId, c => c.Id, (m, c) => new { m, c })
                         .Where(x => x.c.VendorId == vendorId)
                         .Select(x => x.m);
        }
        if (dateFrom.HasValue) query = query.Where(m => m.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(m => m.CreatedAt <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(route)) query = query.Where(m => m.Route == route || m.Route.Contains(route));
        if (!string.IsNullOrWhiteSpace(matchedFaqId)) query = query.Where(m => m.MatchedFaqId == matchedFaqId);

        var items = await query.OrderByDescending(m => m.CreatedAt).Take(10000).ToListAsync();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,CreatedAt,VendorId,ConversationId,Route,MatchedFaqId,MatchedScore,MatchedBy,ReplyText");
        foreach (var m in items)
        {
            var line = string.Format("\"{0}\",{1},\"{2}\",\"{3}\",\"{4}\",\"{5}\",{6},\"{7}\",\"{8}\",\"{9}\"",
                m.Id,
                m.CreatedAt,
                (m.VendorId ?? "").Replace("\"","'"),
                (m.ConversationId ?? "").Replace("\"","'"),
                (m.Route ?? "").Replace("\"","'"),
                (m.MatchedFaqId ?? "").Replace("\"","'"),
                m.MatchedScore?.ToString() ?? "",
                (m.MatchedBy ?? "").Replace("\"","'"),
                (m.ReplyText ?? "").Replace("\"","'")
            );
            sb.AppendLine(line);
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", "message_routes.csv");
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.MessageRoutes.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MessageRoute model)
    {
        if (!ModelState.IsValid) return View(model);
        model.Id = Guid.NewGuid().ToString("N");
        model.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _db.MessageRoutes.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Message Route 已建立";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.MessageRoutes.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, MessageRoute model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        _db.Entry(model).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Message Route 已更新";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var item = await _db.MessageRoutes.FindAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var item = await _db.MessageRoutes.FindAsync(id);
        if (item != null)
        {
            _db.MessageRoutes.Remove(item);
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "Message Route 已刪除";
        return RedirectToAction(nameof(Index));
    }
}
