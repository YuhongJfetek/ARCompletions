using System.Threading.Tasks;
using ARCompletions.Dtos;

namespace ARCompletions.Services
{
    // Stub that simulates persisting result without touching DB.
    public class MessageResultServiceStub : IMessageResultService
    {
        public Task<MessageResultResponseDto> PersistResultAsync(MessageResultRequestDto req)
        {
            var resp = new MessageResultResponseDto
            {
                Success = true,
                TraceId = req.TraceId,
                ConversationLogId = null,
                FaqQueryLogId = null,
                MessageResultId = "stub-" + req.TraceId,
                VendorId = req.VendorId,
                MessageId = req.TraceId,
                Source = req.MessageContext?.SourceType,
                Route = req.Analysis?.Route,
                MatchedBy = req.Analysis?.ReasonCode ?? req.Analysis?.PersonaApplied,
                MatchedScore = req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null,
                Payload = System.Text.Json.JsonSerializer.Serialize(req),
                MatchedFaqId = req.Analysis?.MatchedFaqId,
                Confidence = req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null,
                CreatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedBy = null,
                Saved = new SaveStateDto
                {
                    ConversationLog = false,
                    FaqQueryLog = false,
                    GroupState = false,
                    FileState = false,
                    Feedback = false,
                    AuditLog = false
                }
            };
            return Task.FromResult(resp);
        }

        public Task<MessageResultResponseDto> PersistResultWithRouteAsync(MessageResultRequestDto req, ARCompletions.Dtos.MessageRouteCreateDto routeReq)
        {
            var resp = new MessageResultResponseDto
            {
                Success = true,
                TraceId = req.TraceId,
                MessageResultId = Guid.NewGuid().ToString("N"),
                MessageRouteId = Guid.NewGuid().ToString("N"),
                ConversationLogId = null,
                FaqQueryLogId = null,
                VendorId = req.VendorId ?? routeReq.VendorId,
                MessageId = req.TraceId,
                Source = req.MessageContext?.SourceType,
                Route = routeReq.Route ?? req.Analysis?.Route,
                MatchedBy = routeReq.MatchedBy ?? req.Analysis?.ReasonCode ?? req.Analysis?.PersonaApplied,
                MatchedScore = routeReq.MatchedScore ?? (req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null),
                Payload = System.Text.Json.JsonSerializer.Serialize(req),
                MatchedFaqId = routeReq.MatchedFaqId ?? req.Analysis?.MatchedFaqId,
                Confidence = req.Analysis?.BestScore.HasValue == true ? (double?)req.Analysis.BestScore.Value : null,
                CreatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedBy = null,
                Saved = new SaveStateDto
                {
                    ConversationLog = false,
                    FaqQueryLog = false,
                    GroupState = false,
                    FileState = false,
                    Feedback = false,
                    AuditLog = false
                }
            };
            return Task.FromResult(resp);
        }
    }
}
