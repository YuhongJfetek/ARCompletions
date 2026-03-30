using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Dtos;
using ARCompletions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("api/linebot")]
public class LineBotController : ControllerBase
{
    private readonly ARCompletionsContext _db;
    private readonly IFaqQueryService _faqQuery;
    private readonly IMessageResultService _resultService;

    public LineBotController(ARCompletionsContext db, IFaqQueryService faqQuery, IMessageResultService resultService)
    {
        _db = db;
        _faqQuery = faqQuery;
        _resultService = resultService;
    }

    // NOTE: /api/message/analyze and /api/message/result endpoints removed.
    // Input and Output endpoints remain and internally call the analysis/result services.

    // 1. 接收 Line Bot 輸入資料
    [HttpPost("input")]
    public async Task<IActionResult> Input([FromBody] LineBotInputRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = new { code = "BadRequest", message = "輸入格式不正確" } });
        }

        if (string.IsNullOrWhiteSpace(request.VendorId))
        {
            return BadRequest(new { success = false, error = new { code = "MissingVendorId", message = "缺少 VendorId" } });
        }

        // 轉換為 analyze DTO 並交由 IFaqQueryService 處理（不做任何寫入）
        var analyzeReq = new MessageAnalyzeRequestDto
        {
            TraceId = Guid.NewGuid().ToString("N"),
            SourceType = request.Channel ?? "linebot",
            EventType = "message",
            MessageType = request.MessageType,
            LineGroupId = null,
            LineUserId = request.ExternalUserId,
            Text = request.MessageText,
            Language = "zh",
            ReceivedAt = request.ReceivedAt == default ? DateTime.UtcNow : request.ReceivedAt,
            NodeMeta = new System.Collections.Generic.Dictionary<string, string>
            {
                { "VendorId", request.VendorId },
                { "Platform", request.Platform }
            }
        };

        var resp = await _faqQuery.AnalyzeAsync(analyzeReq);
        return Ok(resp);
    }

    // 2. 接收 Line Bot 回覆資料
    [HttpPost("output")]
    public async Task<IActionResult> Output([FromBody] LineBotOutputRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = new { code = "BadRequest", message = "輸入格式不正確" } });
        }

        if (string.IsNullOrWhiteSpace(request.VendorId))
        {
            return BadRequest(new { success = false, error = new { code = "MissingVendorId", message = "缺少 VendorId" } });
        }

        // 轉換為 MessageResultRequestDto 並交由 IMessageResultService 處理（不做任何寫入）
        var resultReq = new MessageResultRequestDto
        {
            TraceId = Guid.NewGuid().ToString("N"),
            MessageContext = new MessageContextDto
            {
                LineGroupId = null,
                LineUserId = request.ExternalUserId,
                SourceType = request.SourceType ?? "",
                MessageType = request.MessageType,
                UserMessage = request.MessageText,
                MessageTimestamp = request.SentAt == default ? DateTime.UtcNow : request.SentAt,
                Language = "zh"
            },
            Analysis = new AnalysisResultDto
            {
                Route = request.SourceType ?? "",
                MatchedFaqId = request.SourceFaqId,
                BestScore = request.ConfidenceScore.HasValue ? (decimal?)Convert.ToDecimal(request.ConfidenceScore.Value) : null,
                PersonaApplied = request.ModelName
            },
            NodeResult = new NodeExecutionResultDto
            {
                ReplyStatus = "success",
                BotReply = request.MessageText,
                FallbackUsed = false
            },
            SideEffects = new MessageSideEffectsDto()
        };

        var resp = await _resultService.PersistResultAsync(resultReq);
        return Ok(resp);
    }
}

public class LineBotInputRequest
{
    public string VendorId { get; set; } = string.Empty;
    public string Platform { get; set; } = "LINE";
    public string Channel { get; set; } = "linebot";
    public string ExternalUserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? MessageText { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty;
    public string? ReplyToken { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}

public class LineBotOutputRequest
{
    public string VendorId { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Channel { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? MessageText { get; set; }
    public string? SourceType { get; set; }
    public string? SourceFaqId { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? ModelName { get; set; }
    public string? PromptVersion { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
