using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class FaqAnalysisController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public FaqAnalysisController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    // Lists conversation messages that are low-confidence or unmatched with server-side pagination
    public async Task<IActionResult> Index(string? vendorId = null, string? filter = null, double? confidenceThreshold = null, int page = 1, int pageSize = 50)
    {
        var vendors = await _db.Vendors.OrderBy(v => v.Code).ToListAsync();
        ViewBag.Vendors = new SelectList(vendors, "Id", "Name", vendorId);

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var msgQuery = _db.ConversationMessages.AsQueryable();
        if (!string.IsNullOrEmpty(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
            msgQuery = msgQuery.Where(m => m.VendorId == vendorId);
        }
        else if (allowed != null)
        {
            msgQuery = msgQuery.Where(m => allowed.Contains(m.VendorId));
        }

        // Apply filters at DB level when possible
        if (!string.IsNullOrEmpty(filter))
        {
            if (filter == "unmatched")
            {
                msgQuery = msgQuery.Where(m => string.IsNullOrEmpty(m.SourceFaqId));
            }
            else if (filter == "low_confidence")
            {
                var thr = confidenceThreshold ?? 0.5;
                msgQuery = msgQuery.Where(m => m.ConfidenceScore == null || m.ConfidenceScore < thr);
            }
        }

        var totalCount = await msgQuery.CountAsync();
        var totalPages = (int)System.Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages < 1) totalPages = 1;
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var items = await msgQuery
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.ActiveFilter = filter;
        ViewBag.ConfidenceThreshold = confidenceThreshold;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;

        return View(items);
    }

    // Export low-confidence / unmatched items as CSV (no DB changes)
    public async Task<IActionResult> ExportCsv(string? vendorId = null, string? filter = null, double? confidenceThreshold = null, int max = 5000)
    {
        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);

        var msgQuery = _db.ConversationMessages.AsQueryable();
        if (!string.IsNullOrEmpty(vendorId))
        {
            if (allowed != null && !allowed.Contains(vendorId)) return Forbid();
            msgQuery = msgQuery.Where(m => m.VendorId == vendorId);
        }
        else if (allowed != null)
        {
            msgQuery = msgQuery.Where(m => allowed.Contains(m.VendorId));
        }

        if (!string.IsNullOrEmpty(filter))
        {
            if (filter == "unmatched") msgQuery = msgQuery.Where(m => string.IsNullOrEmpty(m.SourceFaqId));
            else if (filter == "low_confidence")
            {
                var thr = confidenceThreshold ?? 0.5;
                msgQuery = msgQuery.Where(m => m.ConfidenceScore == null || m.ConfidenceScore < thr);
            }
        }

        var items = await msgQuery.OrderByDescending(m => m.CreatedAt).Take(max).ToListAsync();

        var sb = new System.Text.StringBuilder();
        // UTF-8 BOM for Excel compatibility
        var utf8Bom = System.Text.Encoding.UTF8.GetPreamble();

        sb.AppendLine("CreatedAt,VendorId,ConversationId,MessageId,Direction,MessageText,Route,MatchedFaq,Confidence,Model,PromptVersion");
        foreach (var m in items)
        {
            string csvEscape(string? s)
            {
                if (s == null) return "";
                var outS = s.Replace("\"", "\"\"");
                if (outS.Contains(",") || outS.Contains("\n") || outS.Contains("\r") || outS.Contains("\"")) return "\"" + outS + "\"";
                return outS;
            }

            var created = DateTimeOffset.FromUnixTimeSeconds(m.CreatedAt).UtcDateTime.ToString("o");
            sb.Append(created);
            sb.Append(','); sb.Append(csvEscape(m.VendorId));
            sb.Append(','); sb.Append(csvEscape(m.ConversationId));
            sb.Append(','); sb.Append(csvEscape(m.Id));
            sb.Append(','); sb.Append(csvEscape(m.Direction));
            sb.Append(','); sb.Append(csvEscape(m.MessageText));
            sb.Append(','); sb.Append(csvEscape(m.SourceType));
            sb.Append(','); sb.Append(csvEscape(m.SourceFaqId));
            sb.Append(','); sb.Append(m.ConfidenceScore?.ToString("0.00") ?? "");
            sb.Append(','); sb.Append(csvEscape(m.ModelName));
            sb.Append(','); sb.Append(csvEscape(m.PromptVersion));
            sb.AppendLine();
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var data = new byte[utf8Bom.Length + bytes.Length];
        System.Array.Copy(utf8Bom, 0, data, 0, utf8Bom.Length);
        System.Array.Copy(bytes, 0, data, utf8Bom.Length, bytes.Length);

        var fileName = $"faq_analysis_{(filter ?? "all")}_{System.DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(data, "text/csv; charset=utf-8", fileName);
    }

    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var msg = await _db.ConversationMessages.FindAsync(id);
        if (msg == null) return NotFound();

        var allowed = await _vendorScope.GetAllowedVendorIdsAsync(User);
        if (allowed != null && !allowed.Contains(msg.VendorId)) return Forbid();

        return View(msg);
    }
}
