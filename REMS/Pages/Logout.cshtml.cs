using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using REMS.Services;
namespace REMS.Pages;

public class LogoutModel : PageModel
{
    private readonly AuditLogService _audit;

    public LogoutModel(AuditLogService audit) => _audit = audit;

    public async Task<IActionResult> OnGetAsync()
    {
        if (int.TryParse(User.FindFirstValue("Id"), out var userId))
            await _audit.WriteAsync("Logout", "تم تسجيل الخروج.", userId);
        await HttpContext.SignOutAsync();
        return RedirectToPage("/login");
    }
}
