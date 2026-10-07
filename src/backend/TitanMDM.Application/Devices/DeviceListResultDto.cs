namespace TitanMDM.Application.Devices;

public sealed record DeviceListResultDto(
    IReadOnlyCollection<DeviceListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);