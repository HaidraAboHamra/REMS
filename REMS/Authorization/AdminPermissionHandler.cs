using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace REMS.Authorization;

public sealed class AdminPermissionHandler : AuthorizationHandler<AdminPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Permission)
            || HasLegacyAdministratorAccess(context.User, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasLegacyAdministratorAccess(ClaimsPrincipal user, string permission)
    {
        if (user.IsInRole("Admin1"))
        {
            return true;
        }

        return user.IsInRole("Admin")
            && permission is not AdminPermissions.TelegramBroadcast;
    }
}
