using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class UserScopeGrant
{
    private UserScopeGrant()
    {
    }

    public UserScopeGrant(
        Guid organizationId,
        Guid userId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        Guid? grantedByUserId = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "UserId is required.",
                nameof(userId));
        }

        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException(
                "ScopeId is required.",
                nameof(scopeId));
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        UserId =
            userId;

        ScopeType =
            scopeType;

        ScopeId =
            scopeId;

        GrantedByUserId =
            grantedByUserId;

        CreatedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id
    {
        get;
        private set;
    }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public Guid UserId
    {
        get;
        private set;
    }

    public AuthorizationScopeType ScopeType
    {
        get;
        private set;
    }

    public Guid ScopeId
    {
        get;
        private set;
    }

    public Guid? GrantedByUserId
    {
        get;
        private set;
    }

    public DateTime CreatedAtUtc
    {
        get;
        private set;
    }
}