using Microsoft.AspNetCore.Authorization;

namespace TitanMDM.Api.Security;

public sealed class PermissionRequirement
    : IAuthorizationRequirement
{
    private static readonly string[] Separators =
    [
        "|"
    ];

    public PermissionRequirement(
        string permission)
        : this(
        [
            permission
        ])
    {
    }

    public PermissionRequirement(
        IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(
            permissions);

        var normalized =
            new List<string>();

        foreach (var permission
            in permissions)
        {
            if (string.IsNullOrWhiteSpace(
                    permission))
            {
                continue;
            }

            foreach (var candidate
                in permission.Split(
                    Separators,
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions
                        .TrimEntries))
            {
                var value =
                    candidate
                        .Trim()
                        .ToLowerInvariant();

                if (value.Length == 0)
                {
                    continue;
                }

                if (!normalized.Contains(
                        value,
                        StringComparer.Ordinal))
                {
                    normalized.Add(value);
                }
            }
        }

        if (normalized.Count == 0)
        {
            throw new ArgumentException(
                "At least one permission is required.",
                nameof(permissions));
        }

        Permissions = normalized;
    }

    /// <summary>
    /// Any of these permissions grants access.
    /// </summary>
    public IReadOnlyList<string> Permissions
    {
        get;
    }

    /// <summary>
    /// Backward-compatible single-permission accessor.
    /// Returns the first (and, for single-permission
    /// requirements, the only) permission.
    /// </summary>
    public string Permission =>
        Permissions[0];
}
