using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class MessageResultsController : Controller
{
    private readonly ARCompletionsContext _db;

    public MessageResultsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 25, long? dateFrom = null, long? dateTo = null, string? vendorId = null, string? matched = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize <= 0 ? 25 : pageSize;
        var query = _db.MessageResults.AsNoTracking().AsQueryable();

        // joins for vendor filter
        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Join(_db.Conversations, m => m.ConversationId, c => c.Id, (m, c) => new { m, c })
                         .Where(x => x.c.VendorId == vendorId)
                         .Select(x => x.m);
        }

        if (dateFrom.HasValue) query = query.Where(m => m.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(m => m.CreatedAt <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(matched))
        {
            if (matched == "matched") query = query.Where(m => m.MatchedFaqId != null && m.MatchedFaqId != "");
            else if (matched == "unmatched") query = query.Where(m => m.MatchedFaqId == null || m.MatchedFaqId == "");
        }

        query = query.OrderByDescending(m => m.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.FilterDateFrom = dateFrom;
        ViewBag.FilterDateTo = dateTo;
        ViewBag.FilterVendor = vendorId;
        ViewBag.FilterMatched = matched;
        return View(items);
    }

    public async Task<IActionResult> ExportCsv(long? dateFrom = null, long? dateTo = null, string? vendorId = null, string? matched = null)
    {
        var query = _db.MessageResults.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Join(_db.Conversations, m => m.ConversationId, c => c.Id, (m, c) => new { m, c })
                         .Where(x => x.c.VendorId == vendorId)
                         .Select(x => x.m);
        }
        if (dateFrom.HasValue) query = query.Where(m => m.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(m => m.CreatedAt <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(matched))
        {
            if (matched == "matched") query = query.Where(m => m.MatchedFaqId != null && m.MatchedFaqId != "");
            else if (matched == "unmatched") query = query.Where(m => m.MatchedFaqId == null || m.MatchedFaqId == "");
        }
        var items = await query.OrderByDescending(m => m.CreatedAt).Take(10000).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,CreatedAt,VendorId,ConversationId,MessageId,Source,Route,MessageRouteId,MatchedFaqId,MatchedBy,MatchedScore,Confidence,CreatedBy,Payload");
        foreach (var m in items)
        {
            var line = string.Format("\"{0}\",{1},\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",{10},{11},\"{12}\",\"{13}\"",
                m.Id,
                m.CreatedAt,
                (m.VendorId ?? "").Replace("\"","'"),
                (m.ConversationId ?? "" ).Replace("\"","'"),
                (m.MessageId ?? "").Replace("\"","'"),
                (m.Source ?? "").Replace("\"","'"),
                (m.Route ?? "").Replace("\"","'"),
                (m.MessageRouteId ?? "").Replace("\"","'"),
                (m.MatchedFaqId ?? "").Replace("\"","'"),
                (m.MatchedBy ?? "").Replace("\"","'"),
                m.MatchedScore?.ToString() ?? "",
                m.Confidence?.ToString() ?? "",
                (m.CreatedBy ?? "").Replace("\"","'"),
                (m.Payload ?? "").Replace("\"","'")
            );
            sb.AppendLine(line);
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", "message_results.csv");
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var item = await _db.MessageResults.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (item == null) return NotFound();
        return View(item);
    }
}
