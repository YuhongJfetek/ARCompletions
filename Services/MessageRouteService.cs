using System;
using System.Text.Json;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Dtos;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services
{
    public class MessageRouteService : IMessageRouteService
    {
        private readonly ARCompletionsContext _db;
        private readonly ILogger<MessageRouteService> _logger;

        public MessageRouteService(ARCompletionsContext db, ILogger<MessageRouteService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<MessageRouteResponseDto> PersistRouteAsync(MessageRouteCreateDto req)
        {
            var resp = new MessageRouteResponseDto { Success = false, TraceId = req.TraceId };
            try
            {
                var mr = new MessageRoute
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VendorId = req.VendorId ?? string.Empty,
                    InputLogId = req.InputLogId,
                    ConversationId = req.ConversationId,
                    Route = req.Route ?? string.Empty,
                    Reason = req.Reason,
                    MatchedFaqId = req.MatchedFaqId,
                    MatchedScore = req.MatchedScore,
                    MatchedBy = req.MatchedBy,
                    FaqCategory = req.FaqCategory,
                    LlmEnabled = req.LlmEnabled,
                    LlmModel = req.LlmModel,
                    EmbeddingModel = req.EmbeddingModel,
                    NeedsHandoff = req.NeedsHandoff,
                    ReplyText = req.ReplyText,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                await _db.MessageRoutes.AddAsync(mr);
                await _db.SaveChangesAsync();

                resp.Success = true;
                resp.TraceId = req.TraceId;
                resp.Saved = true;
                resp.MessageRouteId = mr.Id;
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist message route");
                resp.Success = false;
                resp.TraceId = req.TraceId;
                return resp;
            }
        }
    }
}
