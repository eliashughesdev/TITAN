using TitanMDM.Application.Authentication;

namespace TitanMDM.Application.Interfaces;

public interface IAuthenticationService
{
    Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<LoginResponse?> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<bool> LogoutAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedUserDto?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}