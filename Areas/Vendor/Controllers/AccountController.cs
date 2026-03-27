using System.Security.Claims;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Vendor.Controllers;

[Area("Vendor")]
public class AccountController : Controller
{
    private readonly ARCompletionsContext _db;

    public AccountController(ARCompletionsContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(VendorLoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await _db.VendorAccounts
            .Include(a => _db.Vendors.FirstOrDefault(v => v.Id == a.VendorId))
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.LoginEmail == model.Email && a.IsActive);

        if (account == null || account.PasswordHash != model.Password)
        {
            ModelState.AddModelError(string.Empty, "帳號或密碼錯誤");
            return View(model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id),
            new Claim(ClaimTypes.Name, account.AccountName ?? account.LoginEmail),
            new Claim("Role", "Vendor"),
            new Claim("VendorId", account.VendorId)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // 寫入審計日誌：廠商帳號登入
        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = account.LoginEmail,
                Action = "VendorAccount.Login",
                TargetId = account.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { account.Id, account.VendorId }),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        return RedirectToAction("Index", "Home", new { area = "Vendor" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var actor = User?.Identity?.Name;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        try
        {
            _db.Set<ARCompletions.Domain.AuditLog>().Add(new ARCompletions.Domain.AuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                Actor = actor ?? "unknown",
                Action = "VendorAccount.Logout",
                TargetId = null,
                Payload = null,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await _db.SaveChangesAsync();
        }
        catch { }

        return RedirectToAction("Login", "Account", new { area = "Vendor" });
    }
}

public class VendorLoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
