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
                    ConversationId = null,
                    MessageId = req.TraceId,
                    Source = req.MessageContext?.SourceType,
                    Payload = JsonSerializer.Serialize(req),
                    MatchedFaqId = req.Analysis?.MatchedFaqId,
                    Confidence = req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    CreatedBy = null
                };

                await _db.MessageResults.AddAsync(mr);
                await _db.SaveChangesAsync();

                resp.Saved.ConversationLog = true;
                resp.ConversationLogId = null; // not using integer PKs; keep null

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
    }
}
