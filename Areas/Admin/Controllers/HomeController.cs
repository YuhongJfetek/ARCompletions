using Microsoft.AspNetCore.Mvc;

namespace ARCompletions.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        private readonly IConfiguration _config;

        public HomeController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index()
        {
            return View();
        }


        // Logout removed — authentication handled externally (no login/logout UI)
    }
}
