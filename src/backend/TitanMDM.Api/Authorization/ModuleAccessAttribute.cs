using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TitanMDM.Api.Authorization;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ModuleAccessAttribute(
    string readPermission,
    string writePermission,
    string? exportPermission = null)
    : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        bool Has(string permission) =>
            user.Claims.Any(claim =>
                claim.Type == "permission" &&
                string.Equals(
                    claim.Value,
                    permission,
                    StringComparison.OrdinalIgnoreCase));

        var request = context.HttpContext.Request;

        var isRead =
            HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method);

        var needed = isRead
            ? readPermission
            : writePermission;

        if (
            exportPermission is not null &&
            request.Path.Value?.Contains(
                "/export/",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            needed = exportPermission;
        }

        if (!Has(needed))
        {
            context.Result = new ForbidResult();
        }

        return Task.CompletedTask;
    }
}