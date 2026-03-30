using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Dtos;
using ARCompletions.Services;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("internal/v1/linebot")]
public class LineBotController : ControllerBase
{
    private readonly ARCompletionsContext _db;
    private readonly IFaqQueryService _faqQuery;
    private readonly IMessageResultService _resultService;
    private readonly IMessageRouteService _routeService;

    public LineBotController(ARCompletionsContext db, IFaqQueryService faqQuery, IMessageResultService resultService, IMessageRouteService routeService)
    {
        _db = db;
        _faqQuery = faqQuery;
        _resultService = resultService;
        _routeService = routeService;
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
        var traceId = string.IsNullOrWhiteSpace(request.TraceId) ? Guid.NewGuid().ToString("N") : request.TraceId;

        var analyzeReq = new MessageAnalyzeRequestDto
        {
            TraceId = traceId,
            SourceType = request.Channel ?? "linebot",
            EventType = "message",
            MessageType = request.MessageType,
            LineGroupId = null,
            LineUserId = request.ExternalUserId,
            Text = request.MessageText,
            Language = string.IsNullOrWhiteSpace(request.Language) ? "zh" : request.Language,
            ReceivedAt = request.ReceivedAt == default ? DateTime.UtcNow : request.ReceivedAt,
            NodeMeta = new System.Collections.Generic.Dictionary<string, string>
            {
                { "VendorId", request.VendorId },
                { "Platform", request.Platform }
            }
        };
        // persist an initial input log for immediate auditing/traceability
        var inputPersistReq = new MessageResultRequestDto
        {
            TraceId = traceId,
            VendorId = request.VendorId,
            MessageContext = new MessageContextDto
            {
                LineGroupId = null,
                LineUserId = request.ExternalUserId,
                SourceType = request.Channel ?? "linebot",
                MessageType = request.MessageType,
                UserMessage = request.MessageText,
                MessageTimestamp = request.ReceivedAt == default ? DateTime.UtcNow : request.ReceivedAt,
                Language = request.Language ?? "zh",
                Attachments = request.Attachments?.Select(a => new AttachmentDto { Type = a.Type, Url = a.Url, Name = a.Name }).ToList() ?? new System.Collections.Generic.List<AttachmentDto>()
            }
        };

        var persistResp = await _resultService.PersistResultAsync(inputPersistReq);

        var resp = await _faqQuery.AnalyzeAsync(analyzeReq);
        // 回傳 TraceId、persist 回應與 analysis 供後續 Output 做關聯與儲存
        return Ok(new { traceId, inputSaved = persistResp.Saved, analysis = resp });
    }

    // 2. Line Bot 輸入資料分析結果
    [HttpPost("output")]
    public async Task<IActionResult> Output([FromBody] ARCompletions.Dtos.MessageOutputRequestDto outputReq)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, error = new { code = "BadRequest", message = "輸入格式不正確" } });
        }
        // map to analyze DTO
        var analyzeReq = new MessageAnalyzeRequestDto
        {
            TraceId = string.IsNullOrWhiteSpace(outputReq?.TraceId) ? Guid.NewGuid().ToString("N") : outputReq.TraceId,
            SourceType = outputReq?.SourceType ?? "",
            EventType = "message",
            MessageType = outputReq?.MessageType ?? "",
            LineGroupId = outputReq?.LineGroupId,
            LineUserId = outputReq?.LineUserId,
            Text = outputReq?.Text,
            Language = "zh",
            ReceivedAt = outputReq?.ReceivedAt ?? DateTime.UtcNow,
            NodeMeta = outputReq?.NodeMeta
        };

        // 驗證 NodeMeta 包含 VendorId
        if (analyzeReq.NodeMeta == null || !analyzeReq.NodeMeta.TryGetValue("VendorId", out var vendorId) || string.IsNullOrWhiteSpace(vendorId))
        {
            return BadRequest(new { success = false, error = new { code = "MissingVendorId", message = "缺少 VendorId (請在 NodeMeta.VendorId)" } });
        }

        // If a TraceId is provided, run analysis then create a MessageRoute and link it
        if (!string.IsNullOrWhiteSpace(analyzeReq.TraceId))
        {
            var analysisResp = await _faqQuery.AnalyzeAsync(analyzeReq);

            // prepare route create dto
            var routeReq = new MessageRouteCreateDto
            {
                TraceId = analyzeReq.TraceId,
                VendorId = vendorId,
                InputLogId = analyzeReq.TraceId,
                ConversationId = analyzeReq.LineGroupId,
                Route = analysisResp.Route,
                Reason = analysisResp.ReasonCode,
                MatchedFaqId = analysisResp.MatchedFaqId,
                MatchedScore = analysisResp.BestScore.HasValue ? (double?)analysisResp.BestScore.Value : null,
                MatchedBy = analysisResp.ReasonCode ?? analysisResp.PersonaApplied,
                ReplyText = analysisResp.Answer,
                LlmEnabled = false,
                NeedsHandoff = false
            };

            var routeResp = await _routeService.PersistRouteAsync(routeReq);

            // try to find existing input MessageResult by MessageId == TraceId
            var existing = await _db.MessageResults.FirstOrDefaultAsync(m => m.MessageId == analyzeReq.TraceId);
            if (existing != null && routeResp.Saved && !string.IsNullOrWhiteSpace(routeResp.MessageRouteId))
            {
                existing.MessageRouteId = routeResp.MessageRouteId;
                existing.Route = routeReq.Route;
                existing.MatchedBy = routeReq.MatchedBy;
                existing.MatchedScore = routeReq.MatchedScore;
                await _db.SaveChangesAsync();

                return Ok(new { route = routeResp, linked = true, messageResultId = existing.Id });
            }

            // no existing input record: fallback to persisting a full MessageResult along with analysis
            var msgReq = new MessageResultRequestDto
            {
                TraceId = analyzeReq.TraceId,
                VendorId = vendorId,
                MessageContext = new MessageContextDto
                {
                    LineGroupId = analyzeReq.LineGroupId,
                    LineUserId = analyzeReq.LineUserId,
                    SourceType = analyzeReq.SourceType,
                    MessageType = analyzeReq.MessageType,
                    UserMessage = analyzeReq.Text,
                    MessageTimestamp = analyzeReq.ReceivedAt == default ? DateTime.UtcNow : analyzeReq.ReceivedAt,
                    Language = analyzeReq.Language
                },
                Analysis = new AnalysisResultDto
                {
                    Route = analysisResp.Route,
                    MatchedFaqId = analysisResp.MatchedFaqId,
                    BestScore = analysisResp.BestScore,
                    ReasonCode = analysisResp.ReasonCode,
                    TopCandidates = analysisResp.TopCandidates,
                    PersonaApplied = analysisResp.PersonaApplied
                },
                NodeResult = new NodeExecutionResultDto
                {
                    BotReply = analysisResp.Answer,
                    BotResponseId = null
                }
            };

            var persisted = await _resultService.PersistResultAsync(msgReq);
            // if route was saved, attempt to set link on the newly created message result
            if (persisted.Saved.ConversationLog && routeResp.Saved && !string.IsNullOrWhiteSpace(routeResp.MessageRouteId))
            {
                var newly = await _db.MessageResults.FirstOrDefaultAsync(m => m.Id == persisted.MessageResultId);
                if (newly != null)
                {
                    newly.MessageRouteId = routeResp.MessageRouteId;
                    await _db.SaveChangesAsync();
                }
            }

            return Ok(new { route = routeResp, persisted });
        }

        // No TraceId => analysis-only
        var resp = await _faqQuery.AnalyzeAsync(analyzeReq);
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
    public string? TraceId { get; set; }
    public string? SessionId { get; set; }
    public string? Language { get; set; }
    public System.Collections.Generic.List<LineBotAttachmentItem>? Attachments { get; set; }
}

// Legacy LineBotOutputRequest removed — use Dtos/MessageOutputRequestDto instead

public class LineBotAttachmentItem
{
    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? Name { get; set; }
}
