using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

using TitanMDM.Api.Security;

namespace TitanMDM.IntegrationTests;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task MatchingPermission_Succeeds()
    {
        var requirement =
            new PermissionRequirement(
                "devices.view");

        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        Guid.NewGuid()
                            .ToString()),

                    new Claim(
                        "permission",
                        "devices.view")
                },
                "Test");

        var principal =
            new ClaimsPrincipal(
                identity);

        var context =
            new AuthorizationHandlerContext(
                new[]
                {
                    requirement
                },
                principal,
                null);

        var handler =
            new PermissionAuthorizationHandler();

        await handler.HandleAsync(
            context);

        Assert.True(
            context.HasSucceeded);
    }

    [Fact]
    public async Task MissingPermission_IsDenied()
    {
        var requirement =
            new PermissionRequirement(
                "security.manage");

        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        "permission",
                        "security.view")
                },
                "Test");

        var context =
            new AuthorizationHandlerContext(
                new[]
                {
                    requirement
                },
                new ClaimsPrincipal(
                    identity),
                null);

        var handler =
            new PermissionAuthorizationHandler();

        await handler.HandleAsync(
            context);

        Assert.False(
            context.HasSucceeded);
    }

    [Fact]
    public async Task UnauthenticatedIdentity_IsDenied()
    {
        var requirement =
            new PermissionRequirement(
                "devices.view");

        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        "permission",
                        "devices.view")
                });

        var context =
            new AuthorizationHandlerContext(
                new[]
                {
                    requirement
                },
                new ClaimsPrincipal(
                    identity),
                null);

        var handler =
            new PermissionAuthorizationHandler();

        await handler.HandleAsync(
            context);

        Assert.False(
            context.HasSucceeded);
    }
}