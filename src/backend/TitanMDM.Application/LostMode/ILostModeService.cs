namespace TitanMDM.Application.LostMode;

public interface ILostModeService
{
    Task<LostModeDto> ActivateAsync(
        Guid organizationId,
        Guid userId,
        ActivateLostModeRequest request,
        CancellationToken cancellationToken = default);

    Task<LostModeDto?> GetActiveAsync(
        Guid organizationId,
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid organizationId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}

public sealed record ActivateLostModeRequest(
    Guid DeviceId,
    string Message,
    string? PhoneNumber);

public sealed record LostModeDto(
    Guid Id,
    Guid DeviceId,
    string Message,
    string? PhoneNumber,
    string Status,
    DateTime ActivatedAtUtc,
    DateTime? DeactivatedAtUtc);