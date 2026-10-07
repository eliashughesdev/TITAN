using TitanMDM.Application.Authentication;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        DateTime expiresAtUtc);

    string GenerateRefreshToken();

    string HashRefreshToken(string token);
}