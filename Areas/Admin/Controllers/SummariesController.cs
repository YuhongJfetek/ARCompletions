using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class SummariesController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public SummariesController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    public async Task<IActionResult> Index(string? vendorId = null, string? status = null, string? dateFrom = null, string? dateTo = null, int page = 1, int pageSize = 25)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var query = _db.AiFaqAnalysisJobs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
            query = query.Where(j => j.VendorId == vendorId);
        }
        else if (allowed != null)
        {
            query = query.Where(j => allowed.Contains(j.VendorId));
        }

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(j => j.Status == status);

        // date filters (expects yyyy-MM-dd or other parseable date)
        long? fromSec = null, toSec = null;
        if (!string.IsNullOrWhiteSpace(dateFrom) && System.DateTime.TryParse(dateFrom, out var dfrom))
        {
            fromSec = new System.DateTimeOffset(dfrom.Date).ToUnixTimeSeconds();
            query = query.Where(j => j.CreatedAt >= fromSec.Value);
        }
        if (!string.IsNullOrWhiteSpace(dateTo) && System.DateTime.TryParse(dateTo, out var dto))
        {
            // include entire day
            toSec = new System.DateTimeOffset(dto.Date.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();
            query = query.Where(j => j.CreatedAt <= toSec.Value);
        }

        var total = await query.LongCountAsync();
        var items = await query.OrderByDescending(j => j.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalCount = total;
        ViewBag.DateFrom = dateFrom; ViewBag.DateTo = dateTo;
        return View(items);
    }

    public async Task<IActionResult> ExportSummary(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();
        var job = await _db.AiFaqAnalysisJobs.FindAsync(id);
        if (job == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(job.VendorId)) return Forbid();

        var candidates = await _db.FaqCandidates.Where(c => c.AnalysisJobId == job.Id).OrderByDescending(c => c.ConfidenceScore).Take(2000).ToListAsync();

        var sb = new System.Text.StringBuilder();
        var utf8Bom = System.Text.Encoding.UTF8.GetPreamble();
        sb.AppendLine($"JobNo:,{job.JobNo}");
        sb.AppendLine($"Vendor:,{job.VendorId}");
        sb.AppendLine($"Status:,{job.Status}");
        sb.AppendLine($"CreatedAt:,{System.DateTimeOffset.FromUnixTimeSeconds(job.CreatedAt).UtcDateTime:o}");
        sb.AppendLine();
        sb.AppendLine("Question,Answer,Confidence,SuggestedCategory,SourceConversationIds");

        string csvEscape(string s)
        {
            if (s == null) return string.Empty;
            var outS = s.Replace("\"", "\"\"");
            if (outS.Contains(",") || outS.Contains("\n") || outS.Contains("\r") || outS.Contains("\"")) return "\"" + outS + "\"";
            return outS;
        }

        foreach (var c in candidates)
        {
            sb.Append(csvEscape(c.Question)); sb.Append(',');
            sb.Append(csvEscape(c.Answer)); sb.Append(',');
            sb.Append(c.ConfidenceScore?.ToString("0.00") ?? ""); sb.Append(',');
            sb.Append(csvEscape(c.SuggestedCategory)); sb.Append(',');
            sb.Append(csvEscape(c.SourceConversationIds)); sb.AppendLine();
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var data = new byte[utf8Bom.Length + bytes.Length];
        System.Array.Copy(utf8Bom, 0, data, 0, utf8Bom.Length);
        System.Array.Copy(bytes, 0, data, utf8Bom.Length, bytes.Length);

        var fileName = $"summary_{job.JobNo}_{System.DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(data, "text/csv; charset=utf-8", fileName);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var job = await _db.AiFaqAnalysisJobs.FindAsync(id);
        if (job == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(job.VendorId)) return Forbid();

        var candidates = await _db.FaqCandidates.Where(c => c.AnalysisJobId == job.Id).OrderByDescending(c => c.ConfidenceScore).Take(500).ToListAsync();
        ViewBag.Candidates = candidates;
        return View(job);
    }
}
