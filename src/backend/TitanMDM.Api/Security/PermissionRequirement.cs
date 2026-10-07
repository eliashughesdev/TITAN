using Microsoft.AspNetCore.Authorization;

namespace TitanMDM.Api.Security;

public sealed class PermissionRequirement
    : IAuthorizationRequirement
{
    public PermissionRequirement(
        string permission)
    {
        if (string.IsNullOrWhiteSpace(
                permission))
        {
            throw new ArgumentException(
                "Permission is required.",
                nameof(permission));
        }

        Permission =
            permission
                .Trim()
                .ToLowerInvariant();
    }

    public string Permission
    {
        get;
    }
}