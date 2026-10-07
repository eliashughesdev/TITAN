using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace TitanMDM.Api.Security;

public sealed class PermissionAuthorizationPolicyProvider
    : DefaultAuthorizationPolicyProvider
{
    public PermissionAuthorizationPolicyProvider(
        IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override Task<
        AuthorizationPolicy?>
        GetPolicyAsync(
            string policyName)
    {
        if (
            !policyName.StartsWith(
                RequirePermissionAttribute.PolicyPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return base.GetPolicyAsync(
                policyName);
        }

        var permission =
            policyName[
                RequirePermissionAttribute
                    .PolicyPrefix
                    .Length..]
                .Trim();

        if (string.IsNullOrWhiteSpace(
                permission))
        {
            return Task.FromResult<
                AuthorizationPolicy?>(null);
        }

        var policy =
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new PermissionRequirement(
                        permission))
                .Build();

        return Task.FromResult<
            AuthorizationPolicy?>(
                policy);
    }
}