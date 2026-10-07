using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Enrollment;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Enrollment;

public sealed class EnrollmentService
    : IEnrollmentService
{
    private readonly TitanMdmDbContext _dbContext;

    public EnrollmentService(
        TitanMdmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreatedEnrollmentTokenDto>
        CreateTokenAsync(
            Guid organizationId,
            Guid createdByUserId,
            CreateEnrollmentTokenRequest request,
            CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "CreatedByUserId is required.",
                nameof(createdByUserId));
        }

        if (!Enum.TryParse<DevicePlatform>(
                request.Platform,
                true,
                out var platform))
        {
            throw new ArgumentException(
                "Unsupported device platform.",
                nameof(request));
        }

        if (platform == DevicePlatform.Unknown)
        {
            throw new ArgumentException(
                "Unknown platform cannot be used for enrollment.",
                nameof(request));
        }

        if (request.ExpirationMinutes < 5 ||
            request.ExpirationMinutes > 10080)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "ExpirationMinutes must be between 5 and 10080.");
        }

        if (request.MaxUses < 1 ||
            request.MaxUses > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "MaxUses must be between 1 and 1000.");
        }

        var organizationExists =
            await _dbContext.Organizations
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == organizationId,
                    cancellationToken);

        if (!organizationExists)
        {
            throw new InvalidOperationException(
                "Organization does not exist.");
        }

        var userExists =
            await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id == createdByUserId &&
                        x.OrganizationId == organizationId,
                    cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException(
                "The authenticated user does not belong to the organization.");
        }

        var rawToken = GenerateSecureToken();

        var tokenHash = HashToken(rawToken);

        var expiresAtUtc =
            DateTime.UtcNow.AddMinutes(
                request.ExpirationMinutes);

        var enrollmentToken =
            new EnrollmentToken(
                organizationId,
                platform,
                tokenHash,
                expiresAtUtc,
                request.MaxUses,
                createdByUserId);

        _dbContext.EnrollmentTokens.Add(
            enrollmentToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return new CreatedEnrollmentTokenDto(
            enrollmentToken.Id,
            rawToken,
            enrollmentToken.Platform.ToString(),
            enrollmentToken.Status.ToString(),
            enrollmentToken.MaxUses,
            enrollmentToken.UsedCount,
            enrollmentToken.ExpiresAtUtc,
            enrollmentToken.CreatedAtUtc);
    }

    public async Task<
        IReadOnlyCollection<EnrollmentTokenDto>>
        GetTokensAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        var tokens =
            await _dbContext.EnrollmentTokens
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId)
                .OrderByDescending(
                    x => x.CreatedAtUtc)
                .ToListAsync(
                    cancellationToken);

        return tokens
            .Select(
                x =>
                    new EnrollmentTokenDto(
                        x.Id,
                        x.Platform.ToString(),
                        GetEffectiveStatus(x),
                        x.MaxUses,
                        x.UsedCount,
                        x.ExpiresAtUtc,
                        x.CreatedByUserId,
                        x.CreatedAtUtc,
                        x.UpdatedAtUtc,
                        x.LastUsedAtUtc,
                        x.RevokedAtUtc))
            .ToArray();
    }

    public async Task<bool>
        RevokeTokenAsync(
            Guid organizationId,
            Guid enrollmentTokenId,
            CancellationToken cancellationToken = default)
    {
        var enrollmentToken =
            await _dbContext.EnrollmentTokens
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            enrollmentTokenId &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (enrollmentToken is null)
        {
            return false;
        }

        enrollmentToken.Revoke();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    public async Task<EnrollmentTokenValidationResult>
        ValidateTokenAsync(
            string token,
            string platform,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Invalid(
                "INVALID_TOKEN",
                "Enrollment token is required.");
        }

        if (!Enum.TryParse<DevicePlatform>(
                platform,
                true,
                out var requestedPlatform) ||
            requestedPlatform ==
                DevicePlatform.Unknown)
        {
            return Invalid(
                "PLATFORM_MISMATCH",
                "Unsupported enrollment platform.");
        }

        var tokenHash =
            HashToken(token.Trim());

        var enrollmentToken =
            await _dbContext.EnrollmentTokens
                .FirstOrDefaultAsync(
                    x =>
                        x.TokenHash ==
                        tokenHash,
                    cancellationToken);

        if (enrollmentToken is null)
        {
            return Invalid(
                "INVALID_TOKEN",
                "Enrollment token is invalid.");
        }

        if (enrollmentToken.Status ==
            EnrollmentStatus.Revoked)
        {
            return Invalid(
                "REVOKED_TOKEN",
                "Enrollment token has been revoked.");
        }

        if (enrollmentToken.Status ==
            EnrollmentStatus.Completed ||
            enrollmentToken.UsedCount >=
            enrollmentToken.MaxUses)
        {
            return Invalid(
                "MAX_USES_REACHED",
                "Enrollment token has reached its maximum number of uses.");
        }

        if (DateTime.UtcNow >=
            enrollmentToken.ExpiresAtUtc)
        {
            if (enrollmentToken.Status ==
                EnrollmentStatus.Active)
            {
                enrollmentToken.MarkExpired();

                await _dbContext.SaveChangesAsync(
                    cancellationToken);
            }

            return Invalid(
                "EXPIRED_TOKEN",
                "Enrollment token has expired.");
        }

        if (enrollmentToken.Platform !=
            requestedPlatform)
        {
            return Invalid(
                "PLATFORM_MISMATCH",
                "Enrollment token is not valid for this platform.");
        }

        if (!enrollmentToken.CanBeUsed())
        {
            return Invalid(
                "INVALID_TOKEN",
                "Enrollment token cannot be used.");
        }

        return new EnrollmentTokenValidationResult(
            true,
            enrollmentToken.Id,
            enrollmentToken.OrganizationId,
            enrollmentToken.Platform.ToString(),
            null,
            null);
    }

    private static string GenerateSecureToken()
    {
        var bytes =
            RandomNumberGenerator.GetBytes(32);

        return Convert
            .ToHexString(bytes);
    }

    private static string HashToken(
        string token)
    {
        var bytes =
            Encoding.UTF8.GetBytes(token);

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private static string GetEffectiveStatus(
        EnrollmentToken token)
    {
        if (token.Status ==
                EnrollmentStatus.Active &&
            DateTime.UtcNow >=
                token.ExpiresAtUtc)
        {
            return EnrollmentStatus
                .Expired
                .ToString();
        }

        return token.Status.ToString();
    }

    private static
        EnrollmentTokenValidationResult Invalid(
            string errorCode,
            string message)
    {
        return new EnrollmentTokenValidationResult(
            false,
            null,
            null,
            null,
            errorCode,
            message);
    }
}