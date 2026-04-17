using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ARCompletions.Data;

namespace ARCompletions.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        private readonly ARCompletionsContext _db;

        public LogsController(ARCompletionsContext db)
        {
            _db = db;
        }

        // GET /api/logs/top30
        [HttpGet("top30")]
        public async Task<IActionResult> GetTop30Async()
        {
            var items = await _db.AppLogs
                .OrderByDescending(l => l.TimeStamp)
                .Take(30)
                .ToListAsync();

            var result = items.Select(l => new
            {
                l.Id,
                l.TimeStamp,
                l.Level,
                l.Message,
                l.MessageTemplate,
                l.Exception,
                Properties = l.Properties != null ? (object)l.Properties.RootElement : null,
                LogEvent = l.LogEvent
            });

            return Ok(result);
        }
    }
}
