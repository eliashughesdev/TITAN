namespace TitanMDM.Application.Commands;

public sealed record DeviceCommandListResultDto(
    IReadOnlyCollection<DeviceCommandDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);