using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using TitanMDM.Application.Interfaces;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Authentication;

public sealed class JwtTokenService
    : ITokenService
{
    private readonly JwtOptions
        _options;

    public JwtTokenService(
        IOptions<JwtOptions> options)
    {
        _options =
            options.Value;
    }

    public string GenerateAccessToken(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        DateTime expiresAtUtc)
    {
        var claims =
            new List<Claim>
            {
                new(
                    JwtRegisteredClaimNames.Sub,
                    user.Id.ToString()),

                new(
                    JwtRegisteredClaimNames.Email,
                    user.Email),

                new(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid()
                        .ToString()),

                new(
                    "organization_id",
                    user.OrganizationId
                        .ToString()),

                new(
                    "security_version",
                    user.SecurityVersion
                        .ToString()),

                new(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),

                new(
                    ClaimTypes.Name,
                    user.FullName)
            };

        claims.AddRange(
            roles.Select(
                role =>
                    new Claim(
                        ClaimTypes.Role,
                        role)));

        claims.AddRange(
            permissions.Select(
                permission =>
                    new Claim(
                        "permission",
                        permission)));

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _options.SigningKey));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms
                    .HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer:
                    _options.Issuer,

                audience:
                    _options.Audience,

                claims:
                    claims,

                notBefore:
                    DateTime.UtcNow,

                expires:
                    expiresAtUtc,

                signingCredentials:
                    credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(
                token);
    }

    public string GenerateRefreshToken()
    {
        var bytes =
            RandomNumberGenerator
                .GetBytes(64);

        return Convert
            .ToBase64String(
                bytes);
    }

    public string HashRefreshToken(
        string token)
    {
        var bytes =
            Encoding.UTF8
                .GetBytes(
                    token);

        var hash =
            SHA256.HashData(
                bytes);

        return Convert
            .ToHexString(
                hash);
    }
}