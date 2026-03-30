using ARCompletions.Data;
using ARCompletions.Dtos;
using ARCompletions.Domain;
using ARCompletions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("internal/v1/logs")]
public class LogsController : ControllerBase
{
    private readonly ARCompletionsContext _db;
    private readonly IFaqQueryService _faqQuery;
    private readonly IMessageRouteService _routeService;
    private readonly IMessageResultService _resultService;

    public LogsController(ARCompletionsContext db, IFaqQueryService faqQuery, IMessageRouteService routeService, IMessageResultService resultService)
    {
        _db = db;
        _faqQuery = faqQuery;
        _routeService = routeService;
        _resultService = resultService;
    }

    // POST /logs/incoming-event
    [HttpPost("incoming-event")]
    public async Task<IActionResult> IncomingEvent([FromBody] IncomingEventRequestDto req)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = "BadRequest" });

        // persist to LineEventLogs (Id is BIGSERIAL)
        var row = new LineEventLog
        {
            LineUserId = req.UserId,
            EventType = "message",
            MessageType = "text",
            Text = req.MessageText,
            RawJson = req.RawEvent?.ToJsonString() ?? "{}",
            CreatedAt = req.ReceivedAt.ToUniversalTime()
        };

        await _db.LineEventLogs.AddAsync(row);
        await _db.SaveChangesAsync();

        // enqueue for background processing by MessageEventWorker
        try
        {
            var msgQueue = HttpContext.RequestServices.GetService<ARCompletions.Services.IBackgroundMessageQueue>();
            msgQueue?.Enqueue(row.Id);
        }
        catch
        {
            // fallback: swallow - still return accepted
        }

        return Accepted(new { eventRowId = row.Id, receivedAt = req.ReceivedAt, queued = true });
    }

    // POST /logs/reply
    [HttpPost("reply")]
    public async Task<IActionResult> Reply([FromBody] ReplyLogRequestDto req)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = "BadRequest" });

        // create message route record
        var routeReq = new MessageRouteCreateDto
        {
            TraceId = req.EventRowId.ToString(),
            VendorId = null,
            InputLogId = req.EventRowId.ToString(),
            ConversationId = req.SourceGroupId,
            Route = req.Route ?? string.Empty,
            Reason = req.Reason,
            MatchedFaqId = req.MatchedFaqId,
            MatchedScore = req.MatchedScore,
            MatchedBy = req.MatchedBy,
            FaqCategory = req.FaqCategory,
            ReplyText = req.ReplyText,
            LlmModel = req.LlmModel,
            EmbeddingModel = req.EmbeddingModel,
            LlmEnabled = req.LlmEnabled,
            NeedsHandoff = req.NeedsHumanHandoff
        };

        var routeResp = await _routeService.PersistRouteAsync(routeReq);

        return Created(string.Empty, new { routeRowId = routeResp.MessageRouteId, pushedAt = req.PushedAt });
    }
}
