using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ARCompletions.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AnalysisController : Controller
    {
        private readonly Data.ARCompletionsContext _context;

        public AnalysisController(Data.ARCompletionsContext context)
        {
            _context = context;
        }

        // GET /Admin/Analysis
        [HttpGet]
        public IActionResult Index(int page = 1, int pageSize = 50, string? searchQuery = null, string? filterType = null, string? filterStatus = null, System.DateTime? startDate = null, System.DateTime? endDate = null, string? jsonKey = null, string? jsonValue = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var q = _context.AnalysisJobs.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                var sq = searchQuery.Trim();
                q = q.Where(j => j.Id.Contains(sq) || (j.Params != null && j.Params.Contains(sq)) || (j.ResultSummary != null && j.ResultSummary.Contains(sq)));
            }

            if (!string.IsNullOrEmpty(filterType)) q = q.Where(j => j.Type == filterType);
            if (!string.IsNullOrEmpty(filterStatus)) q = q.Where(j => j.Status == filterStatus);

            if (startDate.HasValue)
            {
                var startSec = ((DateTimeOffset)startDate.Value.Date).ToUnixTimeSeconds();
                q = q.Where(j => j.CreatedAt >= startSec);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1);
                var endSec = ((DateTimeOffset)endOfDay).ToUnixTimeSeconds() - 1;
                q = q.Where(j => j.CreatedAt <= endSec);
            }

            // If PostgreSQL and JSON key/value provided, use JSON extraction for precise matching
            var isPostgres = _context.Database.ProviderName != null && _context.Database.ProviderName.Contains("Npgsql");

            if (isPostgres && (!string.IsNullOrEmpty(jsonKey) || !string.IsNullOrEmpty(jsonValue)))
            {
                // Build parameterized SQL using Postgres JSON operators
                var sql = "SELECT * FROM \"AnalysisJobs\" WHERE 1=1";
                var parameters = new System.Collections.Generic.List<object>();

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    sql += " AND (\"Id\" ILIKE '%' || {0} || '%' OR \"Params\"::text ILIKE '%' || {0} || '%' OR \"ResultSummary\" ILIKE '%' || {0} || '%')";
                    parameters.Add(searchQuery.Trim());
                }

                if (!string.IsNullOrEmpty(filterType))
                {
                    sql += " AND \"Type\" = {" + parameters.Count + "}";
                    parameters.Add(filterType);
                }

                if (!string.IsNullOrEmpty(filterStatus))
                {
                    sql += " AND \"Status\" = {" + parameters.Count + "}";
                    parameters.Add(filterStatus);
                }

                if (startDate.HasValue)
                {
                    var startSec = ((DateTimeOffset)startDate.Value.Date).ToUnixTimeSeconds();
                    sql += " AND \"CreatedAt\" >= {" + parameters.Count + "}";
                    parameters.Add(startSec);
                }

                if (endDate.HasValue)
                {
                    var endOfDay = endDate.Value.Date.AddDays(1);
                    var endSec = ((DateTimeOffset)endOfDay).ToUnixTimeSeconds() - 1;
                    sql += " AND \"CreatedAt\" <= {" + parameters.Count + "}";
                    parameters.Add(endSec);
                }

                if (!string.IsNullOrEmpty(jsonKey) && !string.IsNullOrEmpty(jsonValue))
                {
                    // Params::jsonb ->> key = value
                    sql += " AND (\"Params\"::jsonb ->> {" + parameters.Count + "}) = {" + (parameters.Count + 1) + "}";
                    parameters.Add(jsonKey);
                    parameters.Add(jsonValue);
                }
                else if (!string.IsNullOrEmpty(jsonKey))
                {
                    sql += " AND (\"Params\"::jsonb ? {" + parameters.Count + "})";
                    parameters.Add(jsonKey);
                }
                else if (!string.IsNullOrEmpty(jsonValue))
                {
                    // fallback: value substring match in Params text
                    sql += " AND \"Params\"::text ILIKE '%' || {" + parameters.Count + "} || '%'";
                    parameters.Add(jsonValue);
                }

                // Compose final query and execute via FromSqlRaw with parameters
                var qRaw = _context.AnalysisJobs.FromSqlRaw(sql, parameters.ToArray()).AsQueryable();
                var totalRaw = qRaw.Count();
                var jobsRaw = qRaw.OrderByDescending(j => j.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

                var typesRaw = _context.AnalysisJobs.Select(j => j.Type).Distinct().Where(t => t != null).ToList();
                var statusesRaw = _context.AnalysisJobs.Select(j => j.Status).Distinct().Where(s => s != null).ToList();

                var vmRaw = new ARCompletions.Areas.Admin.Models.AnalysisIndexViewModel
                {
                    Jobs = jobsRaw,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalRaw,
                    SearchQuery = searchQuery,
                    FilterType = filterType,
                    FilterStatus = filterStatus,
                    StartDate = startDate,
                    EndDate = endDate,
                    JsonKey = jsonKey,
                    JsonValue = jsonValue,
                    Types = typesRaw,
                    Statuses = statusesRaw
                };

                return View(vmRaw);
            }

            var total = q.Count();

            var jobs = q
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var types = _context.AnalysisJobs.Select(j => j.Type).Distinct().Where(t => t != null).ToList();
            var statuses = _context.AnalysisJobs.Select(j => j.Status).Distinct().Where(s => s != null).ToList();

            var vm = new ARCompletions.Areas.Admin.Models.AnalysisIndexViewModel
            {
                Jobs = jobs,
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                SearchQuery = searchQuery,
                FilterType = filterType,
                FilterStatus = filterStatus,
                StartDate = startDate,
                EndDate = endDate,
                JsonKey = jsonKey,
                JsonValue = jsonValue,
                Types = types,
                Statuses = statuses
            };

            // 為每筆 job 計算可直接顯示的中文敘述，放入 ViewData 供視圖使用
            try
            {
                var human = jobs.ToDictionary(j => j.Id, j => ToChineseSummary(j.ResultSummary));
                ViewData["HumanSummaries"] = human;
            }
            catch
            {
                // 若發生任何錯誤則不影響主要流程
                ViewData["HumanSummaries"] = new System.Collections.Generic.Dictionary<string, string>();
            }

            return View(vm);
        }

        // GET /Admin/Analysis/Details/{id}
        [HttpGet]
        public IActionResult Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();
            var job = _context.AnalysisJobs.FirstOrDefault(j => j.Id == id);
            if (job == null) return NotFound();
            // 計算並傳遞中文敘述供視圖顯示
            ViewData["HumanSummary"] = ToChineseSummary(job.ResultSummary);
            return View(job);
        }

        private static string ToChineseSummary(string? summary)
        {
            if (string.IsNullOrEmpty(summary)) return string.Empty;
            try
            {
                var parts = summary.Split(';', StringSplitOptions.RemoveEmptyEntries);
                var dict = parts.Select(p => p.Split('=', 2)).Where(a => a.Length == 2).ToDictionary(a => a[0], a => a[1]);

                dict.TryGetValue("successRate", out var sr);
                dict.TryGetValue("high", out var high);
                dict.TryGetValue("med", out var med);
                dict.TryGetValue("low", out var low);
                dict.TryGetValue("docs", out var docs);

                double successRate = 0;
                if (!string.IsNullOrEmpty(sr)) double.TryParse(sr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out successRate);
                int ihigh = 0, imed = 0, ilow = 0, idocs = 0;
                int.TryParse(high ?? "0", out ihigh);
                int.TryParse(med ?? "0", out imed);
                int.TryParse(low ?? "0", out ilow);
                int.TryParse(docs ?? "0", out idocs);

                var pieces = new System.Collections.Generic.List<string>();
                pieces.Add($"共分析 {idocs} 筆文件");
                pieces.Add($"成功率 {(successRate * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%");
                pieces.Add($"風險分佈：高 {ihigh}、中 {imed}、低 {ilow}");

                return string.Join("；", pieces);
            }
            catch
            {
                return summary ?? string.Empty;
            }
        }

        // GET /Admin/Analysis/WebhookLogs
        [HttpGet]
        public IActionResult WebhookLogs(string? userId, string? eventType, int skip = 0, int take = 50)
        {
            try
            {
                take = Math.Clamp(take, 1, 500);
                var q = _context.LineEventLogs.AsQueryable();
                if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(l => l.LineUserId == userId);
                if (!string.IsNullOrWhiteSpace(eventType)) q = q.Where(l => l.EventType == eventType);

                var items = q.OrderByDescending(l => l.CreatedAt).Skip(skip).Take(take).ToList();
                ViewData["userId"] = userId;
                ViewData["eventType"] = eventType;
                ViewData["skip"] = skip;
                ViewData["take"] = take;
                return View(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "failed to query webhook logs", detail = ex.Message });
            }
        }

        // POST /Admin/Analysis/Jobs
        [HttpPost]
        public IActionResult Jobs([FromForm] string? body)
        {
            // accept raw form body or form field 'body' containing JSON
            string paramJson = body ?? HttpContext.Request.Form["body"].ToString() ?? "{}";
            try
            {
                var job = new Data.AnalysisJob
                {
                    Type = "manual",
                    Params = paramJson
                };
                _context.AnalysisJobs.Add(job);
                _context.AuditLogs.Add(new Data.AuditLog { Actor = User?.Identity?.Name ?? "admin", Action = "create_analysis_job", TargetId = job.Id, Payload = paramJson });
                _context.SaveChanges();
                return Ok(new { jobId = job.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "db_error", detail = ex.Message });
            }
        }

        // POST /Admin/Analysis/Reanalyze
        [HttpPost]
        public IActionResult Reanalyze([FromForm] string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();
            var existing = _context.AnalysisJobs.FirstOrDefault(j => j.Id == id);
            if (existing == null) return NotFound();

            try
            {
                var newJob = new Data.AnalysisJob
                {
                    Type = "reanalyze",
                    Params = existing.Params
                };
                _context.AnalysisJobs.Add(newJob);
                _context.AuditLogs.Add(new Data.AuditLog { Actor = User?.Identity?.Name ?? "admin", Action = "reanalyze", TargetId = newJob.Id, Payload = existing.Params });
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST /Admin/Analysis/Unmatched
        [HttpPost]
        public IActionResult Unmatched([FromForm] string query, [FromForm] string? source)
        {
            if (string.IsNullOrEmpty(query)) return BadRequest();
            try
            {
                var u = new Data.UnmatchedQuery
                {
                    Query = query,
                    Source = string.IsNullOrEmpty(source) ? "unknown" : source
                };
                _context.UnmatchedQueries.Add(u);
                _context.SaveChanges();
                return Ok(new { id = u.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "db_error", detail = ex.Message });
            }
        }

        // POST /Admin/Analysis/Feedback
        [HttpPost]
        public IActionResult Feedback([FromForm] string? userId, [FromForm] int rating = 0, [FromForm] string? comment = null)
        {
            if (rating < 0) return BadRequest();
            try
            {
                var f = new Data.Feedback
                {
                    UserId = userId,
                    Rating = rating,
                    Comment = comment
                };
                _context.Feedbacks.Add(f);
                _context.SaveChanges();
                return Ok(new { id = f.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "db_error", detail = ex.Message });
            }
        }

        // GET /Admin/Analysis/Stats?days=7
        [HttpGet]
        public IActionResult Stats(int days = 7)
        {
            if (days < 1) days = 7;
            var labels = new System.Collections.Generic.List<string>();
            var messages = new System.Collections.Generic.List<int>();
            var unmatched = new System.Collections.Generic.List<int>();
            var feedbackCount = new System.Collections.Generic.List<int>();
            var satisfaction = new System.Collections.Generic.List<double>();

            var today = DateTime.UtcNow.Date;
            for (int i = days - 1; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                var startSec = ((DateTimeOffset)d).ToUnixTimeSeconds();
                var endSec = ((DateTimeOffset)d.AddDays(1)).ToUnixTimeSeconds() - 1;
                labels.Add(d.ToString("yyyy-MM-dd"));

                var m = _context.ChatMessages.Count(c => c.CreatedAt >= startSec && c.CreatedAt <= endSec);
                var u = _context.UnmatchedQueries.Count(c => c.CreatedAt >= startSec && c.CreatedAt <= endSec);
                var fList = _context.Feedbacks.Where(f => f.CreatedAt >= startSec && f.CreatedAt <= endSec).ToList();
                var fc = fList.Count;
                double avg = fc > 0 ? fList.Average(f => f.Rating) : 0.0;

                messages.Add(m);
                unmatched.Add(u);
                feedbackCount.Add(fc);
                satisfaction.Add(Math.Round(avg, 2));
            }

            var totalMessages = messages.Sum();
            var totalUnmatched = unmatched.Sum();
            double unmatchedRatio = totalMessages > 0 ? (double)totalUnmatched / totalMessages : 0.0;

            return Ok(new
            {
                labels,
                messages,
                unmatched,
                feedbackCount,
                satisfaction,
                totalMessages,
                totalUnmatched,
                unmatchedRatio
            });
        }

        // GET /Admin/Analysis/Unmatched
        [HttpGet]
        public IActionResult Unmatched(int page = 1, int pageSize = 50, string? q = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var qset = _context.UnmatchedQueries.AsQueryable();
            if (!string.IsNullOrEmpty(q))
            {
                var tq = q.Trim();
                qset = qset.Where(u => u.Query != null && u.Query.Contains(tq));
            }

            var total = qset.Count();
            var items = qset.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var vm = new ARCompletions.Areas.Admin.Models.UnmatchedIndexViewModel
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Query = q
            };
            return View(vm);
        }

        // GET /Admin/Analysis/UnmatchedExport?q=...  => CSV
        [HttpGet]
        public IActionResult UnmatchedExport(string? q = null)
        {
            var qset = _context.UnmatchedQueries.AsQueryable();
            if (!string.IsNullOrEmpty(q))
            {
                var tq = q.Trim();
                qset = qset.Where(u => u.Query != null && u.Query.Contains(tq));
            }
            var items = qset.OrderByDescending(u => u.CreatedAt).ToList();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Id,Query,Source,CreatedAt");
            foreach (var it in items)
            {
                var created = System.DateTimeOffset.FromUnixTimeSeconds(it.CreatedAt).ToString("o");
                // escape quotes by doubling
                var qesc = (it.Query ?? string.Empty).Replace("\"", "\"\"");
                var sourceEsc = (it.Source ?? string.Empty).Replace("\"", "\"\"");
                csv.AppendLine($"\"{it.Id}\",\"{qesc}\",\"{sourceEsc}\",\"{created}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var filename = $"unmatched_{System.DateTime.UtcNow:yyyyMMdd}.csv";
            return File(bytes, "text/csv", filename);
        }
    }
}
