using System;
using System.Text.Json;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Dtos;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services
{
    public class MessageResultService : IMessageResultService
    {
        private readonly ARCompletionsContext _db;
        private readonly ILogger<MessageResultService> _logger;

        public MessageResultService(ARCompletionsContext db, ILogger<MessageResultService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<MessageResultResponseDto> PersistResultAsync(MessageResultRequestDto req)
        {
            var resp = new MessageResultResponseDto { Success = false, TraceId = req.TraceId };
            try
            {
                var mr = new MessageResult
                {
                    VendorId = req.VendorId,
                    ConversationId = null,
                    MessageId = req.TraceId,
                    Source = req.MessageContext?.SourceType,
                    Payload = JsonSerializer.Serialize(req),
                    MatchedFaqId = req.Analysis?.MatchedFaqId,
                    Confidence = req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null,
                    Route = req.Analysis?.Route,
                    MatchedBy = req.Analysis?.ReasonCode ?? req.Analysis?.PersonaApplied,
                    MatchedScore = req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    CreatedBy = null
                };

                await _db.MessageResults.AddAsync(mr);
                await _db.SaveChangesAsync();

                resp.Saved.ConversationLog = true;
                resp.ConversationLogId = null; // not using integer PKs; keep null
                resp.MessageResultId = mr.Id;
                // populate response with stored metadata
                resp.VendorId = mr.VendorId;
                resp.ConversationId = mr.ConversationId;
                resp.MessageId = mr.MessageId;
                resp.Source = mr.Source;
                resp.Route = mr.Route;
                resp.MatchedBy = mr.MatchedBy;
                resp.MatchedScore = mr.MatchedScore;
                resp.Payload = mr.Payload;
                resp.MatchedFaqId = mr.MatchedFaqId;
                resp.Confidence = mr.Confidence;
                resp.CreatedAt = mr.CreatedAt;
                resp.CreatedBy = mr.CreatedBy;

                // if there are analysis candidates or a matched faq, create faq query log
                if (req.Analysis != null)
                {
                    var q = new FaqQueryLog
                    {
                        MessageResultId = mr.Id,
                        QueryText = req.MessageContext?.UserMessage,
                        MatchedFaqId = req.Analysis.MatchedFaqId,
                        Confidence = req.Analysis.BestScore.HasValue ? (double?)Convert.ToDouble(req.Analysis.BestScore.Value) : null,
                        DetailsJson = JsonSerializer.Serialize(req.Analysis.TopCandidates ?? new System.Collections.Generic.List<FaqCandidateDto>()),
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    await _db.FaqQueryLogs.AddAsync(q);
                    await _db.SaveChangesAsync();
                    resp.Saved.FaqQueryLog = true;
                }

                resp.Success = true;
                resp.TraceId = req.TraceId;
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist message result");
                resp.Success = false;
                resp.TraceId = req.TraceId;
                return resp;
            }
        }

        public async Task<MessageResultResponseDto> PersistResultWithRouteAsync(MessageResultRequestDto req, ARCompletions.Dtos.MessageRouteCreateDto routeReq)
        {
            var resp = new MessageResultResponseDto { Success = false, TraceId = req.TraceId };
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // create MessageResult
                var mr = new MessageResult
                {
                    VendorId = req.VendorId,
                    ConversationId = null,
                    MessageId = req.TraceId,
                    Source = req.MessageContext?.SourceType,
                    Payload = JsonSerializer.Serialize(req),
                    MatchedFaqId = req.Analysis?.MatchedFaqId,
                    Confidence = req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null,
                    Route = req.Analysis?.Route,
                    MatchedBy = req.Analysis?.ReasonCode ?? req.Analysis?.PersonaApplied,
                    MatchedScore = req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    CreatedBy = null
                };

                await _db.MessageResults.AddAsync(mr);

                // create MessageRoute
                var r = new Domain.MessageRoute
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VendorId = routeReq.VendorId ?? string.Empty,
                    InputLogId = routeReq.InputLogId,
                    ConversationId = routeReq.ConversationId,
                    Route = routeReq.Route ?? string.Empty,
                    Reason = routeReq.Reason,
                    MatchedFaqId = routeReq.MatchedFaqId,
                    MatchedScore = routeReq.MatchedScore,
                    MatchedBy = routeReq.MatchedBy,
                    FaqCategory = routeReq.FaqCategory,
                    LlmEnabled = routeReq.LlmEnabled,
                    NeedsHandoff = routeReq.NeedsHandoff,
                    ReplyText = routeReq.ReplyText,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                await _db.MessageRoutes.AddAsync(r);

                await _db.SaveChangesAsync();

                // link
                mr.MessageRouteId = r.Id;
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                resp.Success = true;
                resp.TraceId = req.TraceId;
                resp.Saved.ConversationLog = true;
                resp.MessageResultId = mr.Id;
                resp.MessageRouteId = r.Id;
                // populate response with stored metadata
                resp.VendorId = mr.VendorId;
                resp.ConversationId = mr.ConversationId;
                resp.MessageId = mr.MessageId;
                resp.Source = mr.Source;
                resp.Route = mr.Route;
                resp.MatchedBy = mr.MatchedBy;
                resp.MatchedScore = mr.MatchedScore;
                resp.Payload = mr.Payload;
                resp.MatchedFaqId = mr.MatchedFaqId;
                resp.Confidence = mr.Confidence;
                resp.CreatedAt = mr.CreatedAt;
                resp.CreatedBy = mr.CreatedBy;
                return resp;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Failed to persist message result and route transactionally");
                resp.Success = false;
                resp.TraceId = req.TraceId;
                return resp;
            }
        }
    }
}
