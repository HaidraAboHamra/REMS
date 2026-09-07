using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using REMS.Interfaces;
using System.Security.Claims;
using REMS.Services;

namespace REMS.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public bool LoginFailed { get; set; }
        public bool IsDevelopment { get; private set; }
        public string BootstrapAdminEmail { get; private set; } = string.Empty;
        public string BootstrapAdminPassword { get; private set; } = string.Empty;
        public IAuthentication Authentication { get; set; }

        private readonly AuditLogService _audit;

        private readonly IConfiguration _configuration;

        public LoginModel(IAuthentication _authentication, AuditLogService audit, IConfiguration configuration)
        {
            Authentication = _authentication;
            _audit = audit;
            _configuration = configuration;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            LoadDevelopmentCredentials();
            var authenticate = await HttpContext.AuthenticateAsync();
            if (authenticate.Succeeded)
            { 
                return Redirect("~/home");
            }
            else
            {
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            LoadDevelopmentCredentials();
            var result = await Authentication.Login(Email, Password);

            if (result.IsFailure)
            {
                LoginFailed = true;
                return Page();
            }

            var claimsPrincipal = result.Value;
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);
            await _audit.WriteAsync("Login", "تم تسجيل الدخول بنجاح.", int.Parse(claimsPrincipal.FindFirstValue("Id")!));

            return Redirect("~/home");
        }

        private void LoadDevelopmentCredentials()
        {
            IsDevelopment = HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            if (!IsDevelopment)
                return;

            BootstrapAdminEmail = _configuration["BootstrapAdminEmail"] ?? string.Empty;
            BootstrapAdminPassword = _configuration["BootstrapAdminPassword"] ?? string.Empty;
        }
    }
}
