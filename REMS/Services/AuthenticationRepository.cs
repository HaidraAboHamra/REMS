using REMS.Abstractions;
using REMS.Interfaces;
using System.Security.Claims;

namespace REMS.Services;

public class AuthenticationRepository(UserService userService) : IAuthentication
{
    public async Task<Result<ClaimsPrincipal>> Login(string email, string password)
    {
        var result = await userService.LoginAsync(email, password);
        if (result is null)
        {
            return Result<ClaimsPrincipal>.Failure(new Error("Login failed"));
        }

        if (result.IsAdmin)
        {
            var role = "Manager";
            var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, $"{result.FullName}"),
                        new("Id", $"{result.Id}"),
                        new(ClaimTypes.Role, role)
                    };
            var identity = new ClaimsIdentity(claims, "AuthenticationType");
            var user = new ClaimsPrincipal(identity);
            return Result<ClaimsPrincipal>.Success(user);
        }
        else if (result.IsFollowUpAdmin)
        {
            var role = "Admin";
            var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, $"{result.FullName}"),
                        new("Id", $"{result.Id}"),
                        new(ClaimTypes.Role, role)
                    };
            var identity = new ClaimsIdentity(claims, "AuthenticationType");
            var user = new ClaimsPrincipal(identity);
            return Result<ClaimsPrincipal>.Success(user);
        }
        else if (result.IsItAdmin)
        {
            var role = "Admin1";
            var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, $"{result.FullName}"),
                        new("Id", $"{result.Id}"),
                        new(ClaimTypes.Role, role)
                    };
            var identity = new ClaimsIdentity(claims, "AuthenticationType");
            var user = new ClaimsPrincipal(identity);
            return Result<ClaimsPrincipal>.Success(user);
        }
        else if (result.IsFUser)
        {
            var role = "FUser";
            var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, $"{result.FullName}"),
                        new("Id", $"{result.Id}"),
                        new(ClaimTypes.Role, role)
                    };
            var identity = new ClaimsIdentity(claims, "AuthenticationType");
            var user = new ClaimsPrincipal(identity);
            return Result<ClaimsPrincipal>.Success(user);
        }
        else
        {
            var role = "User";
            var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, $"{result.FullName}"),
                        new("Id", $"{result.Id}"),
                        new(ClaimTypes.Role, role)
                    };
            var identity = new ClaimsIdentity(claims, "AuthenticationType");
            var user = new ClaimsPrincipal(identity);
            return Result<ClaimsPrincipal>.Success(user);
        }
    }
}
