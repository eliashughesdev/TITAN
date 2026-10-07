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

        var hasPermission =
            context.User.Claims.Any(
                claim =>
                    claim.Type ==
                        "permission"
                    &&
                    string.Equals(
                        claim.Value,
                        requirement.Permission,
                        StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(
                requirement);
        }

        return Task.CompletedTask;
    }
}