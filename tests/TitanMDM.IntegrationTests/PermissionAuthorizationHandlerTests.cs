using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

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

    [Fact]
    public async Task AnyOfRequirement_WithOneGrantedPermission_Succeeds()
    {
        var requirement =
            new PermissionRequirement(
                new[]
                {
                    "android.enterprise.view",
                    "workspace.android.view"
                });

        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        "permission",
                        "workspace.android.view")
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

        Assert.True(
            context.HasSucceeded);
    }

    [Fact]
    public async Task AnyOfRequirement_WithoutAnyGrantedPermission_IsDenied()
    {
        var requirement =
            new PermissionRequirement(
                new[]
                {
                    "android.enterprise.view",
                    "workspace.android.view"
                });

        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        "permission",
                        "devices.view")
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
    public void RequireAnyPermissionAttribute_BuildsJoinedPolicy()
    {
        var attribute =
            new RequireAnyPermissionAttribute(
                "Android.Enterprise.View",
                "workspace.android.view");

        Assert.Equal(
            "permission:android.enterprise.view|workspace.android.view",
            attribute.Policy);
    }

    [Fact]
    public async Task PolicyProvider_SplitsJoinedPermissions()
    {
        var provider =
            new PermissionAuthorizationPolicyProvider(
                Options.Create(
                    new AuthorizationOptions()));

        var policy =
            await provider.GetPolicyAsync(
                "permission:android.enterprise.view|workspace.android.view");

        Assert.NotNull(policy);

        var requirement =
            policy!.Requirements
                .OfType<PermissionRequirement>()
                .SingleOrDefault();

        Assert.NotNull(requirement);

        Assert.Equal(
            2,
            requirement!.Permissions.Count);

        Assert.Contains(
            "android.enterprise.view",
            requirement.Permissions);

        Assert.Contains(
            "workspace.android.view",
            requirement.Permissions);
    }
}