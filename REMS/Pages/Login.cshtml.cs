using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using REMS.Interfaces;
using System.Security.Claims;

namespace REMS.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public bool LoginFailed { get; set; }
        public IAuthentication Authentication { get; set; }

        public LoginModel(IAuthentication _authentication)
        {
            Authentication = _authentication;
        }

        public async Task<IActionResult> OnGetAsync()
        {
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

            return Redirect("~/home");
        }
    }
}
