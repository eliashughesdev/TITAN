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
            dbContext
            ??
            throw new ArgumentNullException(
                nameof(
                    dbContext));
    }

    // ============================================================
    // GET
    // ============================================================

    public async Task<
        IReadOnlyCollection<
            AuthorizationScopeGrantDto>>
        GetUserScopesAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(
            organizationId,
            userId);

        await EnsureUserExistsAsync(
            organizationId,
            userId,
            cancellationToken);

        return await _dbContext
            .UserScopeGrants
            .AsNoTracking()
            .Where(
                grant =>
                    grant.OrganizationId ==
                        organizationId
                    &&
                    grant.UserId ==
                        userId)
            .OrderBy(
                grant =>
                    grant.ScopeType)
            .ThenBy(
                grant =>
                    grant.ScopeId)
            .Select(
                grant =>
                    new AuthorizationScopeGrantDto(
                        grant.Id,
                        grant.ScopeType,
                        grant.ScopeId,
                        grant.CreatedAtUtc))
            .ToArrayAsync(
                cancellationToken);
    }

    // ============================================================
    // GRANT
    // ============================================================

    public async Task GrantAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid userId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(
            organizationId,
            userId);

        if (
            actorUserId ==
                Guid.Empty)
        {
            throw new InvalidOperationException(
                "ActorUserId no es válido.");
        }

        if (
            scopeId ==
                Guid.Empty)
        {
            throw new InvalidOperationException(
                "ScopeId no es válido.");
        }

        await EnsureUserExistsAsync(
            organizationId,
            userId,
            cancellationToken);

        await ValidateScopeAsync(
            organizationId,
            scopeType,
            scopeId,
            cancellationToken);

        /*
         * ========================================================
         * ORGANIZATION SCOPE
         * ========================================================
         *
         * Organization domina sobre todos los scopes inferiores.
         *
         * Si lo otorgamos:
         *
         * - eliminamos Site;
         * - eliminamos Department;
         * - eliminamos Group.
         *
         * Así evitamos configuraciones redundantes/ambiguas.
         * ========================================================
         */

        if (
            scopeType ==
            AuthorizationScopeType.Organization)
        {
            var current =
                await _dbContext
                    .UserScopeGrants
                    .Where(
                        grant =>
                            grant.OrganizationId ==
                                organizationId
                            &&
                            grant.UserId ==
                                userId)
                    .ToListAsync(
                        cancellationToken);

            var organizationGrant =
                current
                    .FirstOrDefault(
                        grant =>
                            grant.ScopeType ==
                                AuthorizationScopeType.Organization
                            &&
                            grant.ScopeId ==
                                organizationId);

            if (
                organizationGrant is null)
            {
                _dbContext
                    .UserScopeGrants
                    .RemoveRange(
                        current);

                _dbContext
                    .UserScopeGrants
                    .Add(
                        new UserScopeGrant(
                            organizationId,
                            userId,
                            AuthorizationScopeType.Organization,
                            organizationId,
                            actorUserId));
            }
            else
            {
                var redundant =
                    current
                        .Where(
                            grant =>
                                grant.Id !=
                                    organizationGrant.Id)
                        .ToArray();

                _dbContext
                    .UserScopeGrants
                    .RemoveRange(
                        redundant);
            }

            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);

            return;
        }

        /*
         * Si ya posee Organization Scope,
         * agregar Sites/Groups/Departments no cambia nada.
         */

        var organizationWide =
            await _dbContext
                .UserScopeGrants
                .AsNoTracking()
                .AnyAsync(
                    grant =>
                        grant.OrganizationId ==
                            organizationId
                    &&
                        grant.UserId ==
                            userId
                    &&
                        grant.ScopeType ==
                            AuthorizationScopeType.Organization
                    &&
                        grant.ScopeId ==
                            organizationId,
                    cancellationToken);

        if (organizationWide)
        {
            return;
        }

        var exists =
            await _dbContext
                .UserScopeGrants
                .AnyAsync(
                    grant =>
                        grant.OrganizationId ==
                            organizationId
                    &&
                        grant.UserId ==
                            userId
                    &&
                        grant.ScopeType ==
                            scopeType
                    &&
                        grant.ScopeId ==
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

    // ============================================================
    // REPLACE
    // ============================================================

    public async Task ReplaceAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid userId,
        IReadOnlyCollection<
            AuthorizationScopeAssignment>
            scopes,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(
            organizationId,
            userId);

        if (
            actorUserId ==
                Guid.Empty)
        {
            throw new InvalidOperationException(
                "ActorUserId no es válido.");
        }

        if (
            scopes is null)
        {
            throw new InvalidOperationException(
                "La colección de scopes es obligatoria.");
        }

        await EnsureUserExistsAsync(
            organizationId,
            userId,
            cancellationToken);

        var normalized =
            scopes
                .Where(
                    scope =>
                        scope.ScopeId !=
                            Guid.Empty)
                .Distinct()
                .ToArray();

        /*
         * Organization Scope domina.
         *
         * No aceptamos una mezcla:
         *
         * Organization + Site
         * Organization + Group
         * Organization + Department
         */

        var organizationScopes =
            normalized
                .Where(
                    scope =>
                        scope.ScopeType ==
                            AuthorizationScopeType.Organization)
                .ToArray();

        if (
            organizationScopes.Length >
                1)
        {
            throw new InvalidOperationException(
                "Solamente puede existir un Organization scope.");
        }

        if (
            organizationScopes.Length ==
                1)
        {
            if (
                organizationScopes[0]
                    .ScopeId !=
                    organizationId)
            {
                throw new InvalidOperationException(
                    "El Organization scope debe corresponder a la organización actual.");
            }

            normalized =
            [
                new AuthorizationScopeAssignment(
                    AuthorizationScopeType.Organization,
                    organizationId)
            ];
        }

        foreach (
            var scope
            in normalized)
        {
            await ValidateScopeAsync(
                organizationId,
                scope.ScopeType,
                scope.ScopeId,
                cancellationToken);
        }

        /*
         * Cargamos una sola vez y reemplazamos el conjunto
         * completo de grants.
         */

        var current =
            await _dbContext
                .UserScopeGrants
                .Where(
                    grant =>
                        grant.OrganizationId ==
                            organizationId
                    &&
                        grant.UserId ==
                            userId)
                .ToListAsync(
                    cancellationToken);

        _dbContext
            .UserScopeGrants
            .RemoveRange(
                current);

        foreach (
            var scope
            in normalized)
        {
            _dbContext
                .UserScopeGrants
                .Add(
                    new UserScopeGrant(
                        organizationId,
                        userId,
                        scope.ScopeType,
                        scope.ScopeId,
                        actorUserId));
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    // ============================================================
    // REVOKE
    // ============================================================

    public async Task RevokeAsync(
        Guid organizationId,
        Guid userId,
        Guid grantId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(
            organizationId,
            userId);

        if (
            grantId ==
                Guid.Empty)
        {
            return;
        }

        var grant =
            await _dbContext
                .UserScopeGrants
                .SingleOrDefaultAsync(
                    item =>
                        item.Id ==
                            grantId
                        &&
                        item.OrganizationId ==
                            organizationId
                        &&
                        item.UserId ==
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

    // ============================================================
    // CAN ACCESS
    // ============================================================

    public async Task<bool> CanAccessAsync(
        Guid organizationId,
        Guid userId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        if (
            organizationId ==
                Guid.Empty
            ||
            userId ==
                Guid.Empty
            ||
            scopeId ==
                Guid.Empty)
        {
            return false;
        }

        var grants =
            await _dbContext
                .UserScopeGrants
                .AsNoTracking()
                .Where(
                    grant =>
                        grant.OrganizationId ==
                            organizationId
                        &&
                        grant.UserId ==
                            userId)
                .Select(
                    grant =>
                        new AuthorizationScopeDescriptor(
                            grant.OrganizationId,
                            grant.ScopeType,
                            grant.ScopeId))
                .ToArrayAsync(
                    cancellationToken);

        return AuthorizationScopeEvaluator
            .CanAccess(
                organizationId,
                grants,
                scopeType,
                scopeId);
    }

    // ============================================================
    // USER VALIDATION
    // ============================================================

    private async Task EnsureUserExistsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exists =
            await _dbContext
                .Users
                .AsNoTracking()
                .AnyAsync(
                    user =>
                        user.Id ==
                            userId
                        &&
                        user.OrganizationId ==
                            organizationId
                        &&
                        user.IsActive,
                    cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException(
                "El usuario no existe, está inactivo o pertenece a otra organización.");
        }
    }

    // ============================================================
    // SCOPE VALIDATION
    // ============================================================

    private async Task ValidateScopeAsync(
        Guid organizationId,
        AuthorizationScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        switch (scopeType)
        {
            case AuthorizationScopeType.Organization:
            {
                if (
                    scopeId !=
                    organizationId)
                {
                    throw new InvalidOperationException(
                        "El Organization scope debe corresponder a la organización actual.");
                }

                var organizationExists =
                    await _dbContext
                        .Organizations
                        .AsNoTracking()
                        .AnyAsync(
                            organization =>
                                organization.Id ==
                                    organizationId,
                            cancellationToken);

                if (!organizationExists)
                {
                    throw new InvalidOperationException(
                        "La organización no existe.");
                }

                break;
            }

            case AuthorizationScopeType.Site:
            {
                var siteExists =
                    await _dbContext
                        .Sites
                        .AsNoTracking()
                        .AnyAsync(
                            site =>
                                site.Id ==
                                    scopeId
                                &&
                                site.OrganizationId ==
                                    organizationId
                                &&
                                site.IsActive,
                            cancellationToken);

                if (!siteExists)
                {
                    throw new InvalidOperationException(
                        "La localidad no existe, está inactiva o pertenece a otra organización.");
                }

                break;
            }

            case AuthorizationScopeType.Department:
            {
                var departmentExists =
                    await _dbContext
                        .Departments
                        .AsNoTracking()
                        .AnyAsync(
                            department =>
                                department.Id ==
                                    scopeId
                                &&
                                department.OrganizationId ==
                                    organizationId,
                            cancellationToken);

                if (!departmentExists)
                {
                    throw new InvalidOperationException(
                        "El departamento no existe en la organización.");
                }

                break;
            }

            case AuthorizationScopeType.Group:
            {
                var groupExists =
                    await _dbContext
                        .DeviceGroups
                        .AsNoTracking()
                        .AnyAsync(
                            group =>
                                group.Id ==
                                    scopeId
                                &&
                                group.OrganizationId ==
                                    organizationId,
                            cancellationToken);

                if (!groupExists)
                {
                    throw new InvalidOperationException(
                        "El grupo no existe en la organización.");
                }

                break;
            }

            default:
            {
                throw new InvalidOperationException(
                    "Tipo de scope no soportado.");
            }
        }
    }

    private static void ValidateIdentifiers(
        Guid organizationId,
        Guid userId)
    {
        if (
            organizationId ==
                Guid.Empty)
        {
            throw new InvalidOperationException(
                "OrganizationId no es válido.");
        }

        if (
            userId ==
                Guid.Empty)
        {
            throw new InvalidOperationException(
                "UserId no es válido.");
        }
    }
}