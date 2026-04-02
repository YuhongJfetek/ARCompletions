using System;
using System.Threading;
using System.Threading.Tasks;
using ARCompletions.Dto;
using ARCompletions.Services;
using Microsoft.AspNetCore.Mvc;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("internal/v1/embeddings")] // 由 Program.cs 的 middleware 保護 X-Internal-API-Key
public class EmbeddingsController : ControllerBase
{
    private readonly IEmbeddingRebuildService _rebuildService;

    public EmbeddingsController(IEmbeddingRebuildService rebuildService)
    {
        _rebuildService = rebuildService;
    }

    /// <summary>
    /// 觸發 Embeddings 重建任務。
    /// scope = "all" 時重建全部 FAQ，scope = "single" 則只重建指定 faqId。
    /// </summary>
    [HttpPost("rebuild")]
    public async Task<IActionResult> Rebuild([FromBody] EmbeddingRebuildRequestDto request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { error = "request body is required" });
        }

        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "all" : request.Scope;
        scope = scope.ToLowerInvariant();
        if (scope != "all" && scope != "single")
        {
            return BadRequest(new { error = "scope must be 'all' or 'single'" });
        }

        if (scope == "single" && string.IsNullOrWhiteSpace(request.FaqId))
        {
            return BadRequest(new { error = "faqId is required when scope = 'single'" });
        }

        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "openai" : request.Provider!;
        var model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model;

        var triggeredBy = "system"; // 未來可從 Header/Claim 帶入，例如 "admin"

        try
        {
            var job = await _rebuildService.RebuildAsync(provider, model, scope, request.FaqId, triggeredBy, cancellationToken);

            return Ok(new
            {
                jobId = job.JobId,
                status = job.Status,
                total = job.TotalCount,
                completed = job.CompletedCount,
                failed = job.FailedCount,
                startedAt = job.StartedAt,
                finishedAt = job.FinishedAt,
                provider = job.Provider,
                model = job.Model,
                scope = job.Scope,
                faqId = job.TargetFaqId,
                triggeredBy = job.TriggeredBy,
                errorMessage = job.ErrorMessage
            });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 查詢 Embedding 重建任務狀態。
    /// </summary>
    [HttpGet("jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJob(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _rebuildService.GetJobAsync(jobId, cancellationToken);
        if (job == null)
        {
            return NotFound(new { error = "job not found" });
        }

        return Ok(new
        {
            jobId = job.JobId,
            status = job.Status,
            total = job.TotalCount,
            completed = job.CompletedCount,
            failed = job.FailedCount,
            startedAt = job.StartedAt,
            finishedAt = job.FinishedAt,
            provider = job.Provider,
            model = job.Model,
            scope = job.Scope,
            faqId = job.TargetFaqId,
            triggeredBy = job.TriggeredBy,
            errorMessage = job.ErrorMessage
        });
    }
}
