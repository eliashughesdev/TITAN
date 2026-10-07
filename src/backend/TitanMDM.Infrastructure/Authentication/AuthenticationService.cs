using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TitanMDM.Application.Authentication;
using TitanMDM.Application.Interfaces;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Authentication;

public sealed class AuthenticationService
    : IAuthenticationService
{
    private readonly TitanMdmDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthenticationService(
        TitanMdmDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail =
            request.Email.Trim().ToLowerInvariant();

        var user =
            await _dbContext.Users
                .SingleOrDefaultAsync(
                    x => x.Email == normalizedEmail,
                    cancellationToken);

        if (user is null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return null;
        }

        var passwordResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

        if (passwordResult ==
            PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (passwordResult ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(
                _passwordHasher.HashPassword(
                    user,
                    request.Password));
        }

        user.RegisterLogin();

        var authorization =
            await GetAuthorizationAsync(
                user.Id,
                cancellationToken);

        return await CreateSessionAsync(
            user,
            authorization.Roles,
            authorization.Permissions,
            ipAddress,
            cancellationToken);
    }

    public async Task<LoginResponse?> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var tokenHash =
            _tokenService.HashRefreshToken(
                refreshToken);

        var storedToken =
            await _dbContext.RefreshTokens
                .SingleOrDefaultAsync(
                    x => x.TokenHash == tokenHash,
                    cancellationToken);

        if (storedToken is null ||
            !storedToken.IsActive)
        {
            return null;
        }

        var user =
            await _dbContext.Users
                .SingleOrDefaultAsync(
                    x => x.Id == storedToken.UserId,
                    cancellationToken);

        if (user is null ||
            !user.IsActive)
        {
            return null;
        }

        var authorization =
            await GetAuthorizationAsync(
                user.Id,
                cancellationToken);

        var newRefreshToken =
            _tokenService.GenerateRefreshToken();

        var newRefreshTokenHash =
            _tokenService.HashRefreshToken(
                newRefreshToken);

        storedToken.Revoke(
            ipAddress,
            newRefreshTokenHash);

        var refreshExpiresAtUtc =
            DateTime.UtcNow.AddDays(
                _jwtOptions.RefreshTokenDays);

        var newStoredToken =
            new RefreshToken(
                user.Id,
                newRefreshTokenHash,
                refreshExpiresAtUtc,
                ipAddress);

        _dbContext.RefreshTokens.Add(
            newStoredToken);

        var accessExpiresAtUtc =
            DateTime.UtcNow.AddMinutes(
                _jwtOptions.AccessTokenMinutes);

        var accessToken =
            _tokenService.GenerateAccessToken(
                user,
                authorization.Roles,
                authorization.Permissions,
                accessExpiresAtUtc);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return new LoginResponse(
            accessToken,
            accessExpiresAtUtc,
            newRefreshToken,
            refreshExpiresAtUtc,
            MapUser(
                user,
                authorization.Roles,
                authorization.Permissions));
    }

    public async Task<bool> LogoutAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var tokenHash =
            _tokenService.HashRefreshToken(
                refreshToken);

        var storedToken =
            await _dbContext.RefreshTokens
                .SingleOrDefaultAsync(
                    x => x.TokenHash == tokenHash,
                    cancellationToken);

        if (storedToken is null)
            return false;

        storedToken.Revoke(ipAddress);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    public async Task<AuthenticatedUserDto?>
        GetCurrentUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        var user =
            await _dbContext.Users
                .SingleOrDefaultAsync(
                    x => x.Id == userId &&
                         x.IsActive,
                    cancellationToken);

        if (user is null)
            return null;

        var authorization =
            await GetAuthorizationAsync(
                user.Id,
                cancellationToken);

        return MapUser(
            user,
            authorization.Roles,
            authorization.Permissions);
    }

    private async Task<LoginResponse>
        CreateSessionAsync(
            User user,
            IReadOnlyCollection<string> roles,
            IReadOnlyCollection<string> permissions,
            string? ipAddress,
            CancellationToken cancellationToken)
    {
        var accessExpiresAtUtc =
            DateTime.UtcNow.AddMinutes(
                _jwtOptions.AccessTokenMinutes);

        var refreshExpiresAtUtc =
            DateTime.UtcNow.AddDays(
                _jwtOptions.RefreshTokenDays);

        var accessToken =
            _tokenService.GenerateAccessToken(
                user,
                roles,
                permissions,
                accessExpiresAtUtc);

        var refreshToken =
            _tokenService.GenerateRefreshToken();

        var refreshTokenHash =
            _tokenService.HashRefreshToken(
                refreshToken);

        _dbContext.RefreshTokens.Add(
            new RefreshToken(
                user.Id,
                refreshTokenHash,
                refreshExpiresAtUtc,
                ipAddress));

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return new LoginResponse(
            accessToken,
            accessExpiresAtUtc,
            refreshToken,
            refreshExpiresAtUtc,
            MapUser(
                user,
                roles,
                permissions));
    }

    private async Task<AuthorizationData>
        GetAuthorizationAsync(
            Guid userId,
            CancellationToken cancellationToken)
    {
        var roles =
            await (
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles
                    on userRole.RoleId equals role.Id
                where userRole.UserId == userId &&
                      role.IsActive
                select role.Name
            )
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var permissions =
            await (
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles
                    on userRole.RoleId equals role.Id
                join rolePermission
                    in _dbContext.RolePermissions
                    on role.Id equals
                    rolePermission.RoleId
                join permission
                    in _dbContext.Permissions
                    on rolePermission.PermissionId
                    equals permission.Id
                where userRole.UserId == userId &&
                      role.IsActive &&
                      permission.IsActive
                select permission.Code
            )
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return new AuthorizationData(
            roles,
            permissions);
    }

    private static AuthenticatedUserDto MapUser(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions)
    {
        return new AuthenticatedUserDto(
            user.Id,
            user.OrganizationId,
            user.FirstName,
            user.LastName,
            user.Email,
            roles.ToArray(),
            permissions.ToArray());
    }

    private sealed record AuthorizationData(
        string[] Roles,
        string[] Permissions);
}