using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ARCompletions.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ChatController : Controller
    {
        private readonly Data.ARCompletionsContext _context;

        public ChatController(Data.ARCompletionsContext context)
        {
            _context = context;
        }

        // GET /Admin/Chat
        [HttpGet]
        public IActionResult Index(int page = 1, int pageSize = 50)
        {
            if (page < 1) page = 1;
            pageSize = Math.Clamp(pageSize, 1, 500);
            var q = _context.ChatMessages.OrderByDescending(c => c.CreatedAt);
            var total = q.Count();
            var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            ViewData["TotalCount"] = total;
            ViewData["Page"] = page;
            ViewData["PageSize"] = pageSize;
            return View(items);
        }

        // POST /Admin/Chat/PostMessage
        [HttpPost]
        public async Task<IActionResult> PostMessage([FromForm] Dto.ChatMessageRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.userId) || string.IsNullOrEmpty(req.role) || string.IsNullOrEmpty(req.content))
                return BadRequest("Invalid request");

            var msg = new Data.ChatMessage
            {
                UserId = req.userId,
                Role = req.role,
                Content = req.content,
                Meta = req.meta
            };

            try
            {
                _context.Add(msg);
                await _context.SaveChangesAsync();
                return Ok(new { id = msg.Id, createdAt = msg.CreatedAt });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "db_error", detail = ex.Message });
            }
        }
    }
}
