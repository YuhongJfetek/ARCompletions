using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;
using ARCompletions.Dtos;

namespace ARCompletions.Services;

public class MessageEventWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IBackgroundMessageQueue _queue;
    private readonly ILogger<MessageEventWorker> _logger;

    public MessageEventWorker(IServiceProvider services, IBackgroundMessageQueue queue, ILogger<MessageEventWorker> logger)
    {
        _services = services;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageEventWorker started");
        var reader = _queue.Reader;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var eventRowId = await reader.ReadAsync(stoppingToken);
                await ProcessEventAsync(eventRowId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message event");
                await Task.Delay(2000, stoppingToken);
            }
        }

        _logger.LogInformation("MessageEventWorker stopping");
    }

    private async Task ProcessEventAsync(long eventRowId, CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();
            var faqQuery = scope.ServiceProvider.GetRequiredService<ARCompletions.Services.IFaqQueryService>();
            var resultService = scope.ServiceProvider.GetRequiredService<ARCompletions.Services.IMessageResultService>();

            var row = await db.LineEventLogs.FirstOrDefaultAsync(l => l.Id == eventRowId, ct);
            if (row == null) return;

            var analyzeReq = new MessageAnalyzeRequestDto
            {
                TraceId = row.Id.ToString(),
                SourceType = "group",
                EventType = row.EventType ?? "message",
                MessageType = row.MessageType ?? "text",
                LineGroupId = row.LineUserId,
                LineUserId = row.LineUserId,
                Text = row.Text,
                Language = "zh",
                ReceivedAt = row.CreatedAt,
                NodeMeta = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Source", "queued-incoming-event" }
                }
            };

            var analysisResp = await faqQuery.AnalyzeAsync(analyzeReq);

            var msgReq = new MessageResultRequestDto
            {
                TraceId = analyzeReq.TraceId,
                VendorId = null,
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
                    BotReply = analysisResp.Answer
                }
            };

            var routeReq = new MessageRouteCreateDto
            {
                TraceId = analyzeReq.TraceId,
                VendorId = null,
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

            var persisted = await resultService.PersistResultWithRouteAsync(msgReq, routeReq);

            if (persisted != null && persisted.Success)
            {
                try
                {
                    var existingRow = await db.LineEventLogs.FirstOrDefaultAsync(l => l.Id == row.Id, ct);
                    if (existingRow != null)
                    {
                        existingRow.MessageResultId = persisted.MessageResultId;
                        existingRow.ProcessingStatus = persisted.Success ? "analyzed" : "error";
                        existingRow.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update LineEventLog after persist");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing of message event {EventRowId} failed", eventRowId);
        }
    }
}
