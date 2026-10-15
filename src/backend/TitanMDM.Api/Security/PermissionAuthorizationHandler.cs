using Microsoft.AspNetCore.Authorization;

namespace TitanMDM.Api.Security;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (
            context.User.Identity?.IsAuthenticated
            != true)
        {
            return Task.CompletedTask;
        }

        var granted =
            context.User
                .Claims
                .Where(
                    claim =>
                        claim.Type ==
                            "permission")
                .Select(
                    claim =>
                        claim.Value)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var hasPermission =
            requirement
                .Permissions
                .Any(
                    granted.Contains);

        if (hasPermission)
        {
            context.Succeed(
                requirement);
        }

        return Task.CompletedTask;
    }
}
