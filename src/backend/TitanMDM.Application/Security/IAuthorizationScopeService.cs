using TitanMDM.Domain.Enums;

namespace TitanMDM.Application.Security;

public interface IAuthorizationScopeService
{
    Task<IReadOnlyCollection<
        AuthorizationScopeGrantDto>>
        GetUserScopesAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default);

    Task GrantAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid userId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid organizationId,
        Guid userId,
        Guid grantId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessAsync(
        Guid organizationId,
        Guid userId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default);
}