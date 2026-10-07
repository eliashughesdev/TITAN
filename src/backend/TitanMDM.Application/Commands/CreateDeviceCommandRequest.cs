namespace TitanMDM.Application.Commands;

public sealed record CreateDeviceCommandRequest(
    Guid DeviceId,
    string CommandType,
    string? PayloadJson,
    int ExpirationMinutes = 30);