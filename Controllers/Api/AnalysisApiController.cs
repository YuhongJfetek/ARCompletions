using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using ARCompletions.Dto;

namespace ARCompletions.Controllers.Api
{
    [ApiController]
    [Route("api/analysis")]
    public class AnalysisApiController : ControllerBase
    {
        private readonly Data.ARCompletionsContext _db;
        private readonly IConfiguration _config;

        public AnalysisApiController(Data.ARCompletionsContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        private bool ValidateApiKey()
        {
            var header = HttpContext.Request.Headers["X-Api-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(header))
            {
                var cfg = _config["Lintbot:ApiKey"] ?? System.Environment.GetEnvironmentVariable("LINTBOT_API_KEY");
                return !string.IsNullOrEmpty(cfg) && header == cfg;
            }
            return User?.Identity?.IsAuthenticated ?? false;
        }

        // GET api/analysis?page=1&pageSize=50
        [HttpGet]
        public IActionResult List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (!ValidateApiKey()) return Unauthorized();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var q = _db.AnalysisJobs.AsNoTracking().OrderByDescending(j => j.CreatedAt);
            var total = q.Count();
            var jobs = q.Skip((page - 1) * pageSize).Take(pageSize).Select(j => new AnalysisJobDto
            {
                Id = j.Id,
                Type = j.Type,
                Status = j.Status,
                Progress = j.Progress,
                CreatedAt = j.CreatedAt,
                StartedAt = j.StartedAt,
                FinishedAt = j.FinishedAt,
                ResultSummary = j.ResultSummary,
                Error = j.Error
            }).ToList();

            return Ok(new { total, page, pageSize, items = jobs });
        }

        // GET api/analysis/{id}
        [HttpGet("{id}")]
        public IActionResult Get(string id)
        {
            if (!ValidateApiKey()) return Unauthorized();
            var job = _db.AnalysisJobs.AsNoTracking().FirstOrDefault(j => j.Id == id);
            if (job == null) return NotFound();

            var details = _db.AnalysisDetails.AsNoTracking().Where(d => d.JobId == id).Select(d => new AnalysisDetailDto
            {
                Id = d.Id,
                JobId = d.JobId,
                CompletionId = d.CompletionId,
                Similarity = d.Similarity,
                RiskLevel = d.RiskLevel,
                Flags = d.Flags
            }).ToList();

            var dto = new AnalysisJobDto
            {
                Id = job.Id,
                Type = job.Type,
                Status = job.Status,
                Progress = job.Progress,
                CreatedAt = job.CreatedAt,
                StartedAt = job.StartedAt,
                FinishedAt = job.FinishedAt,
                ResultSummary = job.ResultSummary,
                Error = job.Error,
                Details = details
            };

            return Ok(dto);
        }
    }
}
