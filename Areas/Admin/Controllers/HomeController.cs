using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

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

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl ?? "/Admin/Home";
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            var cfgUser = _config["Admin:Username"] ?? "admin";
            var cfgPass = _config["Admin:Password"] ?? "changeme";
            if (username == cfgUser && password == cfgPass)
            {
                var claims = new[] { new Claim(ClaimTypes.Name, username) };
                var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
                return LocalRedirect(returnUrl ?? "/Admin/Home");
            }
            ModelState.AddModelError("", "Invalid credentials");
            ViewData["ReturnUrl"] = returnUrl ?? "/Admin/Home";
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
