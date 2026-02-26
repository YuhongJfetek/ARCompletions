using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ARCompletions.Controllers
{
    [ApiController]
    [Route("api/webhook")]
    public class LineWebhookController : ControllerBase
    {
        private readonly Data.ARCompletionsContext _context;

        public LineWebhookController(Data.ARCompletionsContext context)
        {
            _context = context;
        }

        // POST /webhook/line
        // 接收 LINE webhook 原始 payload，儲存原始 JSON 並嘗試擷取 userId 與 event type
        [HttpPost("line")]
        public async Task<IActionResult> ReceiveLine([FromBody] JsonElement payload)
        {
            try
            {
                var raw = payload.GetRawText();
                string userId = null;
                string eventType = null;
                string lineEventId = null;

                if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
                {
                    var ev = events[0];
                    if (ev.ValueKind == JsonValueKind.Object)
                    {
                        if (ev.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
                            eventType = t.GetString();

                        // prefer LINE event id if present for idempotency
                        if (ev.TryGetProperty("id", out var eid) && eid.ValueKind == JsonValueKind.String)
                            lineEventId = eid.GetString();

                        if (ev.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object && source.TryGetProperty("userId", out var uid) && uid.ValueKind == JsonValueKind.String)
                            userId = uid.GetString();
                    }
                }

                // use event id from payload if available for dedupe
                var lineEvent = new Data.LineEvent
                {
                    EventId = string.IsNullOrEmpty(lineEventId) ? Guid.NewGuid().ToString() : lineEventId,
                    UserId = userId,
                    EventType = eventType,
                    RawEvent = raw,
                    ReceivedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Processed = false
                };

                // Idempotency: skip if EventId already exists
                var exists = _context.LineEvents.Any(e => e.EventId == lineEvent.EventId);
                if (exists)
                {
                    // write audit log for duplicate
                    _context.AuditLogs.Add(new Data.AuditLog { Actor = "system", Action = "webhook_duplicate", TargetId = lineEvent.EventId, Payload = raw });
                    await _context.SaveChangesAsync();
                    return Ok(new { id = (string?)null, duplicate = true });
                }

                _context.LineEvents.Add(lineEvent);
                _context.AuditLogs.Add(new Data.AuditLog { Actor = "system", Action = "webhook_received", TargetId = lineEvent.EventId, Payload = raw });
                await _context.SaveChangesAsync();

                return Ok(new { id = lineEvent.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "failed to store event", detail = ex.Message });
            }
        }
    }
}
