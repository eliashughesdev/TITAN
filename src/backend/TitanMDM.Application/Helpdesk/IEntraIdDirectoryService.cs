
namespace TitanMDM.Application.Helpdesk;

public interface IEntraIdDirectoryService
{
    Task<EntraIdSettingsDto> GetSettingsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<EntraIdSettingsDto> SaveSettingsAsync(
        Guid organizationId,
        SaveEntraIdSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<EntraSyncResultDto> SyncDirectoryAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntraDirectoryUserDto>> SearchDirectoryAsync(
        Guid organizationId,
        string? search,
        CancellationToken cancellationToken = default);
}
