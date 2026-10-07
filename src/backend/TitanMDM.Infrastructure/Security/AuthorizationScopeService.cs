using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Security;

using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Security;

public sealed class AuthorizationScopeService
    : IAuthorizationScopeService
{
    private readonly TitanMdmDbContext
        _dbContext;

    public AuthorizationScopeService(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<
        IReadOnlyCollection<
            AuthorizationScopeGrantDto>>
        GetUserScopesAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext
            .UserScopeGrants
            .AsNoTracking()
            .Where(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.UserId ==
                        userId)
            .OrderBy(
                x =>
                    x.ScopeType)
            .ThenBy(
                x =>
                    x.ScopeId)
            .Select(
                x =>
                    new AuthorizationScopeGrantDto(
                        x.Id,
                        x.ScopeType,
                        x.ScopeId,
                        x.CreatedAtUtc))
            .ToArrayAsync(
                cancellationToken);
    }

    public async Task GrantAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid userId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        if (
            organizationId == Guid.Empty
            ||
            userId == Guid.Empty
            ||
            scopeId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "El scope solicitado no es válido.");
        }

        var userExists =
            await _dbContext
                .Users
                .AnyAsync(
                    x =>
                        x.Id ==
                            userId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException(
                "El usuario no existe o no pertenece a la organización.");
        }

        await ValidateScopeAsync(
            organizationId,
            scopeType,
            scopeId,
            cancellationToken);

        var exists =
            await _dbContext
                .UserScopeGrants
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.UserId ==
                            userId
                        &&
                        x.ScopeType ==
                            scopeType
                        &&
                        x.ScopeId ==
                            scopeId,
                    cancellationToken);

        if (exists)
        {
            return;
        }

        _dbContext
            .UserScopeGrants
            .Add(
                new UserScopeGrant(
                    organizationId,
                    userId,
                    scopeType,
                    scopeId,
                    actorUserId));

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    public async Task RevokeAsync(
        Guid organizationId,
        Guid userId,
        Guid grantId,
        CancellationToken cancellationToken = default)
    {
        var grant =
            await _dbContext
                .UserScopeGrants
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            grantId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.UserId ==
                            userId,
                    cancellationToken);

        if (grant is null)
        {
            return;
        }

        _dbContext
            .UserScopeGrants
            .Remove(
                grant);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    public async Task<bool> CanAccessAsync(
        Guid organizationId,
        Guid userId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        var grants =
            await _dbContext
                .UserScopeGrants
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.UserId ==
                            userId)
                .Select(
                    x =>
                        new AuthorizationScopeDescriptor(
                            x.OrganizationId,
                            x.ScopeType,
                            x.ScopeId))
                .ToArrayAsync(
                    cancellationToken);

        return AuthorizationScopeEvaluator
            .CanAccess(
                organizationId,
                grants,
                scopeType,
                scopeId);
    }

    private async Task ValidateScopeAsync(
        Guid organizationId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        switch (scopeType)
        {
            case AuthorizationScopeType.Organization:

                if (scopeId != organizationId)
                {
                    throw new InvalidOperationException(
                        "El Organization scope debe corresponder a la organización actual.");
                }

                break;

            case AuthorizationScopeType.Department:

                var departmentExists =
                    await _dbContext
                        .Departments
                        .AnyAsync(
                            x =>
                                x.Id ==
                                    scopeId
                                &&
                                x.OrganizationId ==
                                    organizationId,
                            cancellationToken);

                if (!departmentExists)
                {
                    throw new InvalidOperationException(
                        "El departamento no existe en la organización.");
                }

                break;

            case AuthorizationScopeType.Group:

                var groupExists =
                    await _dbContext
                        .DeviceGroups
                        .AnyAsync(
                            x =>
                                x.Id ==
                                    scopeId
                                &&
                                x.OrganizationId ==
                                    organizationId,
                            cancellationToken);

                if (!groupExists)
                {
                    throw new InvalidOperationException(
                        "El grupo no existe en la organización.");
                }

                break;

            case AuthorizationScopeType.Site:

                /*
                 * Site será una entidad formal en Fase 5.
                 *
                 * No inferimos Site usando Department ni cadenas
                 * de texto. Guardamos el identificador de scope
                 * y Fase 5 agregará integridad referencial.
                 */
                break;

            default:

                throw new InvalidOperationException(
                    "Tipo de scope no soportado.");
        }
    }
}