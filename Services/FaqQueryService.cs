using System.Threading.Tasks;
using ARCompletions.Dtos;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ARCompletions.Services
{
    public class FaqQueryService : IFaqQueryService
    {
        private readonly ARCompletionsContext _db;

        public FaqQueryService(ARCompletionsContext db)
        {
            _db = db;
        }

        public async Task<MessageAnalyzeResponseDto> AnalyzeAsync(MessageAnalyzeRequestDto req)
        {
            var resp = new MessageAnalyzeResponseDto
            {
                Success = true,
                TraceId = req.TraceId,
                Route = "none",
                FeedbackEnabled = false
            };

            // 1) Check conversation state for handoff
            if (req.NodeMeta != null && req.NodeMeta.TryGetValue("VendorId", out var vendorId))
            {
                var conv = await _db.ConversationStates
                    .Where(c => c.VendorId == vendorId && c.ConversationId == (req.LineGroupId ?? string.Empty))
                    .FirstOrDefaultAsync();

                if (conv != null && conv.HandoffUntil.HasValue)
                {
                    var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (conv.HandoffUntil.Value > now)
                    {
                        resp.Route = "handoff";
                        resp.NodeAction = "handoff";
                        resp.FallbackSuggested = false;
                        resp.ReasonCode = "handoff_active";
                        return resp;
                    }
                }
            }

            // 2) Exact alias match (simple)
            if (req.NodeMeta != null && req.NodeMeta.TryGetValue("VendorId", out var vId) && !string.IsNullOrWhiteSpace(req.Text))
            {
                var alias = await _db.FaqAliases
                    .Where(a => a.VendorId == vId && a.Term == req.Text)
                    .FirstOrDefaultAsync();

                if (alias != null)
                {
                    resp.Route = "faq";
                    resp.NodeAction = "reply_text";
                    resp.MatchedFaqId = alias.FaqIds;
                    resp.Answer = alias.Term;
                    resp.ReasonCode = "alias_exact";
                    return resp;
                }
            }

            // 3) Try message routes (simple equality match on Route)
            if (req.NodeMeta != null && req.NodeMeta.TryGetValue("VendorId", out var v2) && !string.IsNullOrWhiteSpace(req.Text))
            {
                var route = await _db.MessageRoutes
                    .Where(m => m.VendorId == v2 && (m.Route == req.Text))
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                if (route != null)
                {
                    resp.Route = "route";
                    resp.NodeAction = "reply_text";
                    resp.Answer = route.Reason;
                    resp.MatchedFaqId = route.MatchedFaqId;
                    resp.BestScore = route.MatchedScore.HasValue ? (decimal?)System.Convert.ToDecimal(route.MatchedScore.Value) : null;
                    resp.ReasonCode = "route_match";
                    return resp;
                }
            }

            // fallback
            resp.Route = "none";
            resp.NodeAction = "reply_text";
            resp.ReasonCode = "no_match";
            return resp;
        }
    }
}
