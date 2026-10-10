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
    private readonly IDeviceCommandNotifier _notifier;

    public DeviceCommandService(
        TitanMdmDbContext dbContext,
        IDeviceCommandNotifier notifier)
    {
        _dbContext = dbContext;
        _notifier = notifier;
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

        var expirationMinutes =
            GetEffectiveExpirationMinutes(
                request.CommandType,
                request.ExpirationMinutes);

        var expiresAtUtc =
            DateTime.UtcNow.AddMinutes(
                expirationMinutes);

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

        var dto = DeviceCommandMapper.Map(command);

        await _notifier.NotifyAvailableAsync(
            command.DeviceId,
            command.Id,
            cancellationToken);

        await _notifier.NotifyUpdatedAsync(
            dto,
            cancellationToken);

        return dto;
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
            : DeviceCommandMapper.Map(command);
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
            commands.Select(DeviceCommandMapper.Map).ToArray(),
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

        await _notifier.NotifyUpdatedAsync(
            DeviceCommandMapper.Map(command),
            cancellationToken);
    }

    private static int GetEffectiveExpirationMinutes(
        string commandType,
        int requestedMinutes)
    {
        var maximumMinutes = commandType.Trim().ToUpperInvariant() switch
        {
            "LOCK_DEVICE" => 2,
            "RESTART_DEVICE" or "SHUTDOWN_DEVICE" => 5,
            "PROCESS_KILL" or "SERVICE_START" or
                "SERVICE_STOP" or "SERVICE_RESTART" => 5,
            _ => requestedMinutes
        };

        return Math.Min(requestedMinutes, maximumMinutes);
    }
}
