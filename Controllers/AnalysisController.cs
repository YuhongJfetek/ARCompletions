using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ARCompletions.Controllers
{
    [ApiController]
    [Route("api/analysis")]
    public class AnalysisController : ControllerBase
    {
        private readonly Data.ARCompletionsContext _context;

        public AnalysisController(Data.ARCompletionsContext context)
        {
            _context = context;
        }

        // NOTE: job creation/management moved to Admin area (protected).

        // GET /api/analysis/jobs/{id}
        [HttpGet("jobs/{id}")]
        public IActionResult GetJob(string id)
        {
            var job = _context.AnalysisJobs.FirstOrDefault(j => j.Id == id);
            if (job == null) return NotFound();
            return Ok(new { job.Id, job.Type, job.Status, job.Progress, job.CreatedAt, job.StartedAt, job.FinishedAt, job.Error, job.ResultSummary });
        }

        // GET /api/analysis/jobs
        [HttpGet("jobs")]
        public IActionResult ListJobs([FromQuery] string status = null)
        {
            var q = _context.AnalysisJobs.AsQueryable();
            if (!string.IsNullOrEmpty(status)) q = q.Where(j => j.Status == status);
            var list = q.OrderByDescending(j => j.CreatedAt).Take(50).Select(j => new { j.Id, j.Type, j.Status, j.Progress, j.CreatedAt });
            return Ok(list);
        }
    }
}
