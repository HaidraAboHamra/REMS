using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using REMS.Authorization;
using REMS.Abstractions;

namespace REMS.Tests;

public class ResultTests
{
    [Fact]
    public void SuccessResult_IsSuccessfulAndHasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void FailureResult_ContainsTheProvidedError()
    {
        var error = new Error("validation.failed");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericSuccessResult_ContainsItsValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFailureResult_DoesNotExposeAValue()
    {
        var result = Result<string>.Failure(new Error("operation.failed"));

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
    }
}

public class AdminPermissionTests
{
    [Fact]
    public async Task Admin1_CanAccessEveryAdminPermission()
    {
        var requirement = new AdminPermissionRequirement(AdminPermissions.TelegramBroadcast);
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Role, "Admin1")
            ])),
            null);

        await new AdminPermissionHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Admin_CannotBroadcastWithoutExplicitPermission()
    {
        var requirement = new AdminPermissionRequirement(AdminPermissions.TelegramBroadcast);
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Role, "Admin")
            ])),
            null);

        await new AdminPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ExplicitPermission_GrantsAccessRegardlessOfLegacyRole()
    {
        var requirement = new AdminPermissionRequirement(AdminPermissions.TelegramBroadcast);
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("permission", AdminPermissions.TelegramBroadcast)
            ])),
            null);

        await new AdminPermissionHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}