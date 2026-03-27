using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("api/embeddings")]
public class EmbeddingsController : ControllerBase
{
    private readonly ARCompletionsContext _db;

    public EmbeddingsController(ARCompletionsContext db)
    {
        _db = db;
    }

    // 4. 回傳 Embedding 資料
    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] EmbeddingQueryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = new { code = "BadRequest", message = "輸入格式不正確" } });
        }

        var query = _db.EmbeddingItems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.VendorId))
        {
            query = query.Where(e => e.VendorId == request.VendorId);
        }

        if (!string.IsNullOrWhiteSpace(request.FaqId))
        {
            query = query.Where(e => e.FaqId == request.FaqId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(e => e.Status == request.Status);
        }

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 50 : Math.Min(request.PageSize, 200);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var jobIds = items.Select(i => i.EmbeddingJobId).Distinct().ToList();
        var jobs = await _db.EmbeddingJobs
            .Where(j => jobIds.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id, j => j);

        if (!string.IsNullOrWhiteSpace(request.VectorVersion))
        {
            items = items
                .Where(i => jobs.TryGetValue(i.EmbeddingJobId, out var job) && job.VectorVersion == request.VectorVersion)
                .ToList();
            total = items.Count;
        }

        var dtoItems = items.Select(i =>
        {
            jobs.TryGetValue(i.EmbeddingJobId, out var job);
            return new EmbeddingItemDto
            {
                Id = i.Id,
                EmbeddingJobId = i.EmbeddingJobId,
                VendorId = i.VendorId,
                FaqId = i.FaqId,
                ChunkText = i.ChunkText,
                EmbeddingJson = i.EmbeddingJson,
                TokenCount = i.TokenCount,
                Status = i.Status,
                ModelName = job?.ModelName,
                VectorVersion = job?.VectorVersion ?? string.Empty,
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(i.CreatedAt).UtcDateTime
            };
        }).ToList();

        return Ok(new EmbeddingQueryResponse
        {
            Success = true,
            Total = total,
            Items = dtoItems
        });
    }
}

public class EmbeddingQueryRequest
{
    public string? VendorId { get; set; }
    public string? VectorVersion { get; set; }
    public string? FaqId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class EmbeddingQueryResponse
{
    public bool Success { get; set; }
    public int Total { get; set; }
    public List<EmbeddingItemDto> Items { get; set; } = new();
}

public class EmbeddingItemDto
{
    public string Id { get; set; } = string.Empty;
    public string EmbeddingJobId { get; set; } = string.Empty;
    public string VendorId { get; set; } = string.Empty;
    public string FaqId { get; set; } = string.Empty;
    public string ChunkText { get; set; } = string.Empty;
    public string EmbeddingJson { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public string VectorVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
