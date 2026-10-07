using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Commands;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Commands;

public sealed class DeviceCommandService
    : IDeviceCommandService
{
    private readonly TitanMdmDbContext _dbContext;

    public DeviceCommandService(
        TitanMdmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeviceCommandDto> CreateAsync(
        Guid organizationId,
        Guid createdByUserId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DeviceCommandException(
                "INVALID_ORGANIZATION",
                "La organización no es válida.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DeviceCommandException(
                "INVALID_USER",
                "El usuario no es válido.");
        }

        if (request.DeviceId == Guid.Empty)
        {
            throw new DeviceCommandException(
                "INVALID_DEVICE",
                "El dispositivo es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.CommandType))
        {
            throw new DeviceCommandException(
                "INVALID_COMMAND_TYPE",
                "El tipo de comando es obligatorio.");
        }

        if (request.ExpirationMinutes is < 1 or > 1440)
        {
            throw new DeviceCommandException(
                "INVALID_EXPIRATION",
                "La expiración debe estar entre 1 y 1440 minutos.");
        }

        var deviceExists =
            await _dbContext.Devices
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id == request.DeviceId &&
                        x.OrganizationId == organizationId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (!deviceExists)
        {
            throw new DeviceCommandException(
                "DEVICE_NOT_FOUND",
                "El dispositivo no existe o no pertenece a la organización.");
        }

        var expiresAtUtc =
            DateTime.UtcNow.AddMinutes(
                request.ExpirationMinutes);

        var command =
            new DeviceCommand(
                organizationId,
                request.DeviceId,
                request.CommandType,
                string.IsNullOrWhiteSpace(
                    request.PayloadJson)
                    ? "{}"
                    : request.PayloadJson,
                createdByUserId,
                expiresAtUtc);

        command.Queue();

        _dbContext.DeviceCommands.Add(command);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(command);
    }

    public async Task<DeviceCommandDto?> GetByIdAsync(
        Guid organizationId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var command =
            await _dbContext.DeviceCommands
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == commandId &&
                        x.OrganizationId == organizationId,
                    cancellationToken);

        return command is null
            ? null
            : Map(command);
    }

    public async Task<DeviceCommandListResultDto>
        GetCommandsAsync(
            Guid organizationId,
            Guid? deviceId,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(
            pageSize,
            1,
            100);

        var query =
            _dbContext.DeviceCommands
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId);

        if (deviceId.HasValue)
        {
            query = query.Where(
                x => x.DeviceId ==
                     deviceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DeviceCommandStatus>(
                    status,
                    true,
                    out var parsedStatus))
            {
                throw new DeviceCommandException(
                    "INVALID_STATUS",
                    "El estado del comando no es válido.");
            }

            query = query.Where(
                x => x.Status == parsedStatus);
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var totalPages =
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount /
                    (double)pageSize);

        var commands =
            await query
                .OrderByDescending(
                    x => x.CreatedAtUtc)
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(pageSize)
                .ToListAsync(
                    cancellationToken);

        return new DeviceCommandListResultDto(
            commands.Select(Map).ToArray(),
            totalCount,
            page,
            pageSize,
            totalPages);
    }

    public async Task CancelAsync(
        Guid organizationId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var command =
            await _dbContext.DeviceCommands
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == commandId &&
                        x.OrganizationId ==
                        organizationId,
                    cancellationToken);

        if (command is null)
        {
            throw new DeviceCommandException(
                "COMMAND_NOT_FOUND",
                "El comando no existe.");
        }

        try
        {
            command.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            throw new DeviceCommandException(
                "COMMAND_TERMINAL",
                ex.Message);
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private static DeviceCommandDto Map(
        DeviceCommand command)
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