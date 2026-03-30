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
public class FaqQueryLogsController : Controller
{
    private readonly ARCompletionsContext _db;

    public FaqQueryLogsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 25, long? dateFrom = null, long? dateTo = null, string? vendorId = null, string? matched = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize <= 0 ? 25 : pageSize;
        var query = _db.FaqQueryLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Join(_db.MessageResults, f => f.MessageResultId, m => m.Id, (f, m) => new { f, m })
                         .Join(_db.Conversations, fm => fm.m.ConversationId, c => c.Id, (fm, c) => new { fm.f, fm.m, c })
                         .Where(x => x.c.VendorId == vendorId)
                         .Select(x => x.f);
        }
        if (dateFrom.HasValue) query = query.Where(f => f.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(f => f.CreatedAt <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(matched))
        {
            if (matched == "matched") query = query.Where(f => f.MatchedFaqId != null && f.MatchedFaqId != "");
            else if (matched == "unmatched") query = query.Where(f => f.MatchedFaqId == null || f.MatchedFaqId == "");
        }

        query = query.OrderByDescending(f => f.CreatedAt);
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
        var query = _db.FaqQueryLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Join(_db.MessageResults, f => f.MessageResultId, m => m.Id, (f, m) => new { f, m })
                         .Join(_db.Conversations, fm => fm.m.ConversationId, c => c.Id, (fm, c) => new { fm.f, fm.m, c })
                         .Where(x => x.c.VendorId == vendorId)
                         .Select(x => x.f);
        }
        if (dateFrom.HasValue) query = query.Where(f => f.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(f => f.CreatedAt <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(matched))
        {
            if (matched == "matched") query = query.Where(f => f.MatchedFaqId != null && f.MatchedFaqId != "");
            else if (matched == "unmatched") query = query.Where(f => f.MatchedFaqId == null || f.MatchedFaqId == "");
        }
        var items = await query.OrderByDescending(f => f.CreatedAt).Take(10000).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,CreatedAt,MessageResultId,QueryText,MatchedFaqId,Confidence,DetailsJson");
        foreach (var m in items)
        {
            var line = string.Format("\"{0}\",{1},\"{2}\",\"{3}\",\"{4}\",{5},\"{6}\"",
                m.Id,
                m.CreatedAt,
                (m.MessageResultId ?? "").Replace("\"","'"),
                (m.QueryText ?? "").Replace("\"","'"),
                (m.MatchedFaqId ?? "").Replace("\"","'"),
                m.Confidence?.ToString() ?? "",
                (m.DetailsJson ?? "").Replace("\"","'")
            );
            sb.AppendLine(line);
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", "faq_query_logs.csv");
    }
}
