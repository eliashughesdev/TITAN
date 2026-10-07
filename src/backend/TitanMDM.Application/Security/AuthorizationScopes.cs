using TitanMDM.Domain.Enums;

namespace TitanMDM.Application.Security;

public sealed record AuthorizationScopeGrantDto(
    Guid Id,
    AuthorizationScopeType ScopeType,
    Guid ScopeId,
    DateTime CreatedAtUtc);

public sealed record AuthorizationScopeDescriptor(
    Guid OrganizationId,
    AuthorizationScopeType ScopeType,
    Guid ScopeId);

public static class AuthorizationScopeEvaluator
{
    public static bool CanAccess(
        Guid organizationId,
        IReadOnlyCollection<
            AuthorizationScopeDescriptor> grants,
        AuthorizationScopeType requiredScopeType,
        Guid requiredScopeId)
    {
        if (
            organizationId == Guid.Empty
            ||
            requiredScopeId == Guid.Empty)
        {
            return false;
        }

        /*
         * Organization scope domina todos los demás
         * scopes de ESA organización.
         */
        var organizationAccess =
            grants.Any(
                grant =>
                    grant.OrganizationId ==
                        organizationId
                    &&
                    grant.ScopeType ==
                        AuthorizationScopeType.Organization
                    &&
                    grant.ScopeId ==
                        organizationId);

        if (organizationAccess)
        {
            return true;
        }

        /*
         * Nunca inferimos jerarquías que no conocemos.
         * Department != Site, Group != Department, etc.
         */
        return grants.Any(
            grant =>
                grant.OrganizationId ==
                    organizationId
                &&
                grant.ScopeType ==
                    requiredScopeType
                &&
                grant.ScopeId ==
                    requiredScopeId);
    }
}