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
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletions.Data.ARCompletionsContext> _dbFactory;

        public LogsController(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletions.Data.ARCompletionsContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        // GET /api/logs/top50
        [HttpGet("top50")]
        public async Task<IActionResult> GetTop50Async()
        {
            using var db = _dbFactory.CreateDbContext();
            var items = await db.AppLogs
                .OrderByDescending(l => l.TimeStamp)
                .Take(55)
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
