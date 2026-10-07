using Microsoft.EntityFrameworkCore;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Services;

/*
 * ================================================================
 * TITANMDM SESSION SECURITY
 * ================================================================
 *
 * Responsable de invalidar sesiones renovables cuando cambia
 * información sensible de identidad o autorización.
 *
 * Casos principales:
 *
 * - cambio de contraseña
 * - desactivación de usuario
 * - asignación/remoción de roles
 * - modificación de permisos de un rol
 *
 * El Access Token actual seguirá siendo válido hasta expirar,
 * pero ya no podrá renovarse mediante RefreshToken.
 *
 * En una fase posterior podemos agregar SecurityVersion para
 * invalidación instantánea de Access Tokens.
 * ================================================================
 */

public sealed class SessionSecurityService
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly ILogger<
        SessionSecurityService> _logger;

    public SessionSecurityService(
        TitanMdmDbContext dbContext,
        ILogger<SessionSecurityService> logger)
    {
        _dbContext =
            dbContext;

        _logger =
            logger;
    }

    /*
     * ============================================================
     * REVOKE ONE USER
     * ============================================================
     */

    public async Task<int> RevokeUserSessionsAsync(
        Guid userId,
        string? revokedByIp = null,
        CancellationToken cancellationToken = default)
    {
        if (
            userId ==
            Guid.Empty)
        {
            return 0;
        }

        var tokens =
            await _dbContext
                .RefreshTokens
                .Where(
                    token =>
                        token.UserId ==
                            userId
                        &&
                        token.RevokedAtUtc ==
                            null
                        &&
                        token.ExpiresAtUtc >
                            DateTime.UtcNow)
                .ToListAsync(
                    cancellationToken);

        if (
            tokens.Count ==
            0)
        {
            return 0;
        }

        foreach (
            var token
            in tokens)
        {
            token.Revoke(
                revokedByIp);
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "TitanMDM invalidó {SessionCount} sesión(es) para UserId={UserId}.",
            tokens.Count,
            userId);

        return tokens.Count;
    }

    /*
     * ============================================================
     * REVOKE MANY USERS
     * ============================================================
     */

    public async Task<int> RevokeUsersSessionsAsync(
        IReadOnlyCollection<Guid> userIds,
        string? revokedByIp = null,
        CancellationToken cancellationToken = default)
    {
        var ids =
            userIds
                .Where(
                    userId =>
                        userId !=
                        Guid.Empty)
                .Distinct()
                .ToArray();

        if (
            ids.Length ==
            0)
        {
            return 0;
        }

        var tokens =
            await _dbContext
                .RefreshTokens
                .Where(
                    token =>
                        ids.Contains(
                            token.UserId)
                        &&
                        token.RevokedAtUtc ==
                            null
                        &&
                        token.ExpiresAtUtc >
                            DateTime.UtcNow)
                .ToListAsync(
                    cancellationToken);

        if (
            tokens.Count ==
            0)
        {
            return 0;
        }

        foreach (
            var token
            in tokens)
        {
            token.Revoke(
                revokedByIp);
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "TitanMDM invalidó {SessionCount} sesión(es) de {UserCount} usuario(s).",
            tokens.Count,
            ids.Length);

        return tokens.Count;
    }

    /*
     * ============================================================
     * REVOKE USERS BY ROLE
     * ============================================================
     */

    public async Task<int> RevokeRoleSessionsAsync(
        Guid roleId,
        string? revokedByIp = null,
        CancellationToken cancellationToken = default)
    {
        if (
            roleId ==
            Guid.Empty)
        {
            return 0;
        }

        var userIds =
            await _dbContext
                .UserRoles
                .Where(
                    assignment =>
                        assignment.RoleId ==
                        roleId)
                .Select(
                    assignment =>
                        assignment.UserId)
                .Distinct()
                .ToArrayAsync(
                    cancellationToken);

        return await RevokeUsersSessionsAsync(
            userIds,
            revokedByIp,
            cancellationToken);
    }
}