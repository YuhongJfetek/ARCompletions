using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ARCompletions.Data;
using ARCompletions.Dto;
using ARCompletions.Domain;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("internal/v1")]
public class InternalBotController : ControllerBase
{
    private readonly ARCompletionsContext _db;

    public InternalBotController(ARCompletionsContext db)
    {
        _db = db;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok" });
    }

    [HttpPost("bot/query")]
    public async Task<IActionResult> Query([FromBody] InternalBotQueryDto req)
    {
        if (req == null) return BadRequest();

        // 1) Check conversation state for handoff
        var conv = await _db.ConversationStates
            .Where(c => c.VendorId == req.VendorId && c.ConversationId == req.ConversationId)
            .FirstOrDefaultAsync();

        if (conv != null && conv.HandoffUntil.HasValue)
        {
            var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (conv.HandoffUntil.Value > now)
            {
                return Ok(new InternalBotResponseDto { Action = "handoff", NeedsHandoff = true, Message = "handoff active" });
            }
        }

        // 2) Exact alias match
        var alias = await _db.FaqAliases
            .Where(a => a.VendorId == req.VendorId && a.Term == req.Input)
            .FirstOrDefaultAsync();

        if (alias != null)
        {
            return Ok(new InternalBotResponseDto { Action = "faq", Message = alias.Term, MatchedFaqId = alias.FaqIds });
        }

        // 3) Try message routes (simple equality match on Route)
        var route = await _db.MessageRoutes
            .Where(m => m.VendorId == req.VendorId && (m.Route == req.Input))
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        if (route != null)
        {
            return Ok(new InternalBotResponseDto { Action = "route", Route = route.Route, Message = route.Reason, MatchedFaqId = route.MatchedFaqId, Score = route.MatchedScore });
        }

        // 4) fallback: no match
        return Ok(new InternalBotResponseDto { Action = "none", Message = "no match" });
    }
}
