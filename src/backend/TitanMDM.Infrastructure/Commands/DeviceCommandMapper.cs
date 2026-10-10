using TitanMDM.Application.Commands;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Commands;

internal static class DeviceCommandMapper
{
    public static DeviceCommandDto Map(DeviceCommand command)
    {
        return new DeviceCommandDto(
            command.Id,
            command.OrganizationId,
            command.DeviceId,
            command.CommandType,
            command.PayloadJson,
            command.Status.ToString(),
            command.CreatedByUserId,
            command.CreatedAtUtc,
            command.UpdatedAtUtc,
            command.ExpiresAtUtc,
            command.QueuedAtUtc,
            command.SentAtUtc,
            command.DeliveredAtUtc,
            command.StartedAtUtc,
            command.CompletedAtUtc,
            command.ResultJson,
            command.ErrorCode,
            command.ErrorMessage,
            command.DeliveryAttempts);
    }
}
