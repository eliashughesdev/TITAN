using Microsoft.AspNetCore.Authorization;

namespace TitanMDM.Api.Security;

/// <summary>
/// Requires the current user to hold AT LEAST ONE of
/// the supplied permission codes (OR semantics).
/// </summary>
[AttributeUsage(
    AttributeTargets.Class |
    AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class RequireAnyPermissionAttribute
    : AuthorizeAttribute
{
    public RequireAnyPermissionAttribute(
        params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(
            permissions);

        var normalized =
            permissions
                .Where(
                    permission =>
                        !string.IsNullOrWhiteSpace(
                            permission))
                .Select(
                    permission =>
                        permission
                            .Trim()
                            .ToLowerInvariant())
                .Distinct()
                .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "At least one permission is required.",
                nameof(permissions));
        }

        Policy =
            RequirePermissionAttribute.PolicyPrefix +
            string.Join(
                "|",
                normalized);
    }
}
