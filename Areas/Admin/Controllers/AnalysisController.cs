using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ARCompletions.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class AnalysisController : Controller
    {
        private readonly Data.ARCompletionsContext _context;

        public AnalysisController(Data.ARCompletionsContext context)
        {
            _context = context;
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
    }
}
