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

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, result.FullName ?? string.Empty),
            new("Id", result.Id.ToString()),
            new(ClaimTypes.Role, "User")
        };

        if (result.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Manager"));

        if (result.IsFollowUpAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        if (result.IsItAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin1"));

        if (result.IsFUser)
            claims.Add(new Claim(ClaimTypes.Role, "FUser"));

        var permissions = await userService.GetGrantedAdminPermissionsAsync(result.Id);
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var identity = new ClaimsIdentity(claims, "AuthenticationType");
        return Result<ClaimsPrincipal>.Success(new ClaimsPrincipal(identity));
    }
}
