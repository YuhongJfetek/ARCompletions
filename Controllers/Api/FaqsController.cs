using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("api/faqs")]
public class FaqsController : ControllerBase
{
    private readonly ARCompletionsContext _db;

    public FaqsController(ARCompletionsContext db)
    {
        _db = db;
    }

    // 3. 回傳 FAQ 資料
    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] FaqQueryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = new { code = "BadRequest", message = "輸入格式不正確" } });
        }

        var query = _db.Faqs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.VendorId))
        {
            query = query.Where(f => f.VendorId == request.VendorId);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(f => f.Question.Contains(keyword) ||
                                     f.Answer.Contains(keyword) ||
                                     (f.Tags != null && f.Tags.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(f => f.Status == request.Status);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(f => f.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CategoryId))
        {
            query = query.Where(f => f.CategoryId == request.CategoryId);
        }

        var total = await query.CountAsync();

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 50 : Math.Min(request.PageSize, 200);

        var items = await query
            .OrderByDescending(f => f.Priority)
            .ThenBy(f => f.Question)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var categoryIds = items.Select(f => f.CategoryId).Distinct().ToList();
        var categories = await _db.FaqCategories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var dtoItems = items.Select(f => new FaqItemDto
        {
            Id = f.Id,
            VendorId = f.VendorId,
            CategoryId = f.CategoryId,
            CategoryName = categories.TryGetValue(f.CategoryId, out var name) ? name : string.Empty,
            Question = f.Question,
            Answer = f.Answer,
            Tags = f.Tags,
            Status = f.Status,
            Priority = f.Priority,
            Version = f.Version,
            IsActive = f.IsActive,
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(f.UpdatedAt ?? f.CreatedAt).UtcDateTime
        }).ToList();

        return Ok(new FaqQueryResponse
        {
            Success = true,
            Total = total,
            Items = dtoItems
        });
    }
}

public class FaqQueryRequest
{
    public string? VendorId { get; set; }
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public bool? IsActive { get; set; }
    public string? CategoryId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class FaqQueryResponse
{
    public bool Success { get; set; }
    public int Total { get; set; }
    public List<FaqItemDto> Items { get; set; } = new();
}

public class FaqItemDto
{
    public string Id { get; set; } = string.Empty;
    public string VendorId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
