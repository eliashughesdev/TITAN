using Microsoft.AspNetCore.Authorization;

namespace TitanMDM.Api.Security;

[AttributeUsage(
    AttributeTargets.Class |
    AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class RequirePermissionAttribute
    : AuthorizeAttribute
{
    public const string PolicyPrefix =
        "permission:";

    public RequirePermissionAttribute(
        string permission)
    {
        if (string.IsNullOrWhiteSpace(
                permission))
        {
            throw new ArgumentException(
                "Permission is required.",
                nameof(permission));
        }

        Policy =
            PolicyPrefix +
            permission
                .Trim()
                .ToLowerInvariant();
    }
}