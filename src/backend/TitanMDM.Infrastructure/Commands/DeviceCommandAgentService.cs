using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Applications;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Commands.Agent;
using TitanMDM.Application.Location;
using TitanMDM.Application.Security;

using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;

using TitanMDM.Infrastructure.Devices.Agent;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Commands;

public sealed class DeviceCommandAgentService
    : IDeviceCommandAgentService
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IApplicationInventoryService
        _applicationInventoryService;

    private readonly ISecurityPostureService
        _securityPostureService;

    private readonly IDeviceLocationService
        _deviceLocationService;

    private readonly WindowsInventoryResultProcessor
        _windowsInventoryProcessor;

    private readonly IDeviceCommandNotifier
        _notifier;

    public DeviceCommandAgentService(
        TitanMdmDbContext dbContext,
        IApplicationInventoryService applicationInventoryService,
        ISecurityPostureService securityPostureService,
        IDeviceLocationService deviceLocationService,
        WindowsInventoryResultProcessor windowsInventoryProcessor,
        IDeviceCommandNotifier notifier)
    {
        _dbContext =
            dbContext;

        _applicationInventoryService =
            applicationInventoryService;

        _securityPostureService =
            securityPostureService;

        _deviceLocationService =
            deviceLocationService;

        _windowsInventoryProcessor =
            windowsInventoryProcessor;

        _notifier =
            notifier;
    }

    public async Task ExpireStaleCommandsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var staleCommands = await _dbContext.DeviceCommands
            .Where(command =>
                (command.Status == DeviceCommandStatus.Pending ||
                 command.Status == DeviceCommandStatus.Queued ||
                 command.Status == DeviceCommandStatus.Dispatching ||
                 command.Status == DeviceCommandStatus.Sent ||
                 command.Status == DeviceCommandStatus.Delivered ||
                 command.Status == DeviceCommandStatus.Executing) &&
                (command.ExpiresAtUtc <= now ||
                 (command.Status == DeviceCommandStatus.Sent &&
                  command.DeliveryAttempts >= 5 &&
                  command.SentAtUtc <= now.AddSeconds(-30))))
            .Take(500)
            .ToListAsync(cancellationToken);

        foreach (var command in staleCommands)
        {
            if (command.ExpiresAtUtc <= now)
            {
                command.MarkTimeout();
            }
            else
            {
                command.CompleteFailure(
                    "COMMAND_DELIVERY_FAILED",
                    "El agente no confirmó la entrega después de cinco intentos.");
            }
        }

        if (staleCommands.Count == 0)
        {
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var command in staleCommands)
        {
            await NotifyUpdatedAsync(command, cancellationToken);
        }
    }

    // ============================================================
    // PENDING COMMANDS
    // ============================================================

    public async Task<IReadOnlyCollection<AgentCommandDto>>
        GetPendingCommandsAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        if (
            deviceId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "DeviceId no es válido.");
        }

        var now =
            DateTime.UtcNow;

        var expiredCommands = await _dbContext.DeviceCommands
            .Where(command =>
                command.DeviceId == deviceId &&
                (command.Status == DeviceCommandStatus.Pending ||
                 command.Status == DeviceCommandStatus.Queued ||
                 command.Status == DeviceCommandStatus.Dispatching ||
                 command.Status == DeviceCommandStatus.Sent ||
                 command.Status == DeviceCommandStatus.Delivered ||
                 command.Status == DeviceCommandStatus.Executing) &&
                command.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (
            var expiredCommand
            in expiredCommands)
        {
            expiredCommand
                .MarkTimeout();
        }

        if (expiredCommands.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var expiredCommand in expiredCommands)
            {
                await NotifyUpdatedAsync(expiredCommand, cancellationToken);
            }
        }

        var redeliveryBefore = now.AddSeconds(-30);

        var claimable = _dbContext.DeviceCommands
            .AsNoTracking()
            .Where(command =>
                command.DeviceId == deviceId &&
                command.ExpiresAtUtc > now &&
                (command.Status == DeviceCommandStatus.Pending ||
                 command.Status == DeviceCommandStatus.Queued ||
                 (command.Status == DeviceCommandStatus.Sent &&
                  command.DeliveryAttempts < 5 &&
                  command.SentAtUtc <= redeliveryBefore)));

        const string urgentType = "LOCK_DEVICE";

        var hasUrgentInFlight = await _dbContext.DeviceCommands
            .AsNoTracking()
            .AnyAsync(command =>
                command.DeviceId == deviceId &&
                (command.Status == DeviceCommandStatus.Delivered ||
                 command.Status == DeviceCommandStatus.Executing ||
                 (command.Status == DeviceCommandStatus.Sent &&
                  command.SentAtUtc > redeliveryBefore)) &&
                command.CommandType == urgentType,
                cancellationToken);

        var hasStandardInFlight = await _dbContext.DeviceCommands
            .AsNoTracking()
            .AnyAsync(command =>
                command.DeviceId == deviceId &&
                (command.Status == DeviceCommandStatus.Delivered ||
                 command.Status == DeviceCommandStatus.Executing ||
                 (command.Status == DeviceCommandStatus.Sent &&
                  command.SentAtUtc > redeliveryBefore)) &&
                command.CommandType != urgentType,
                cancellationToken);

        var urgentIds = hasUrgentInFlight
            ? []
            : await claimable
            .Where(command => command.CommandType == urgentType)
            .OrderBy(command => command.CreatedAtUtc)
            .Select(command => command.Id)
            .Take(1)
            .ToListAsync(cancellationToken);

        var regularIds = hasStandardInFlight
            ? []
            : await claimable
            .Where(command => command.CommandType != urgentType)
            .OrderBy(command => command.CreatedAtUtc)
            .Select(command => command.Id)
            .Take(1)
            .ToListAsync(cancellationToken);

        var candidateIds = urgentIds
            .Concat(regularIds)
            .ToList();

        var claimedIds = new List<Guid>(candidateIds.Count);

        foreach (var commandId in candidateIds)
        {
            var affected = await _dbContext.DeviceCommands
                .Where(command =>
                    command.Id == commandId &&
                    command.DeviceId == deviceId &&
                    command.ExpiresAtUtc > now &&
                    (command.Status == DeviceCommandStatus.Pending ||
                     command.Status == DeviceCommandStatus.Queued ||
                     (command.Status == DeviceCommandStatus.Sent &&
                      command.DeliveryAttempts < 5 &&
                      command.SentAtUtc <= redeliveryBefore)))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            command => command.Status,
                            DeviceCommandStatus.Sent)
                        .SetProperty(
                            command => command.DeliveryAttempts,
                            command => command.DeliveryAttempts + 1)
                        .SetProperty(
                            command => command.SentAtUtc,
                            now)
                        .SetProperty(
                            command => command.UpdatedAtUtc,
                            now),
                    cancellationToken);

            if (affected == 1)
            {
                claimedIds.Add(commandId);
            }
        }

        var commands = claimedIds.Count == 0
            ? []
            : await _dbContext.DeviceCommands
                .AsNoTracking()
                .Where(command => claimedIds.Contains(command.Id))
                .ToListAsync(cancellationToken);

        commands = commands
            .OrderBy(command => candidateIds.IndexOf(command.Id))
            .ToList();

        var result =
            new List<
                AgentCommandDto>();

        foreach (
            var command
            in commands)
        {
            result.Add(
                new AgentCommandDto(
                    command.Id,
                    command.CommandType,
                    command.PayloadJson,
                    command.CreatedAtUtc,
                    command.ExpiresAtUtc));

            await NotifyUpdatedAsync(command, cancellationToken);
        }

        return result;
    }

    // ============================================================
    // DELIVERED
    // ============================================================

    public async Task MarkDeliveredAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var command =
            await GetCommandAsync(
                deviceId,
                commandId,
                cancellationToken);

        command
            .MarkDelivered();

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        await NotifyUpdatedAsync(command, cancellationToken);
    }

    // ============================================================
    // EXECUTING
    // ============================================================

    public async Task MarkExecutingAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var command =
            await GetCommandAsync(
                deviceId,
                commandId,
                cancellationToken);

        command
            .MarkExecuting();

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        await NotifyUpdatedAsync(command, cancellationToken);
    }

    // ============================================================
    // SUCCESS
    // ============================================================

    public async Task MarkSuccessAsync(
        Guid deviceId,
        Guid commandId,
        string? resultJson,
        CancellationToken cancellationToken = default)
    {
        var command =
            await GetCommandAsync(
                deviceId,
                commandId,
                cancellationToken);

        if (command.Status == DeviceCommandStatus.Success)
        {
            return;
        }

        if (IsTerminal(command.Status))
        {
            throw new InvalidOperationException(
                $"El comando ya terminó con estado {command.Status}.");
        }

        var commandType =
            command.CommandType
                .Trim()
                .ToUpperInvariant();

        switch (
            commandType)
        {
            // =====================================================
            // WINDOWS DEVICE INVENTORY
            // =====================================================

            case "DEVICE_INFO":
                {
                    RequireResult(
                        resultJson,
                        commandType);

                    await _windowsInventoryProcessor
                        .ProcessDeviceInfoAsync(
                            deviceId,
                            resultJson!,
                            cancellationToken);

                    break;
                }

            case "DEVICE_INVENTORY":
                {
                    RequireResult(
                        resultJson,
                        commandType);

                    await _windowsInventoryProcessor
                        .ProcessInventoryAsync(
                            deviceId,
                            resultJson!,
                            cancellationToken);

                    break;
                }

            case "NETWORK_INFO":
                {
                    RequireResult(
                        resultJson,
                        commandType);

                    await _windowsInventoryProcessor
                        .ProcessNetworkAsync(
                            deviceId,
                            resultJson!,
                            cancellationToken);

                    break;
                }

            // =====================================================
            // APPLICATIONS
            // =====================================================

            case "APP_INVENTORY":
                {
                    RequireResult(
                        resultJson,
                        commandType);

                    await _applicationInventoryService
                        .ProcessInventoryAsync(
                            deviceId,
                            resultJson!,
                            cancellationToken);

                    break;
                }

            // =====================================================
            // SECURITY
            // =====================================================

            case "SECURITY_STATUS":
                {
                    RequireResult(
                        resultJson,
                        commandType);

                    await _securityPostureService
                        .ProcessSecurityStatusAsync(
                            deviceId,
                            resultJson!,
                            cancellationToken);

                    break;
                }

            case "COMPLIANCE_CHECK":
                {
                    RequireResult(
                        resultJson,
                        commandType);

                    await _securityPostureService
                        .ProcessComplianceAsync(
                            deviceId,
                            resultJson!,
                            cancellationToken);

                    break;
                }

            // =====================================================
            // LOCATION
            // =====================================================

            case "LOCATION_REQUEST":
                {
                    RequireResult(
                        resultJson,
                        commandType);

                    await _deviceLocationService
                        .ProcessLocationAsync(
                            deviceId,
                            resultJson!,
                            cancellationToken);

                    break;
                }
        }

        command
            .CompleteSuccess(
                resultJson);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        await NotifyUpdatedAsync(command, cancellationToken);
    }

    // ============================================================
    // FAILED
    // ============================================================

    public async Task MarkFailedAsync(
        Guid deviceId,
        Guid commandId,
        string errorCode,
        string errorMessage,
        string? resultJson,
        CancellationToken cancellationToken = default)
    {
        if (
            string.IsNullOrWhiteSpace(
                errorCode))
        {
            errorCode =
                "COMMAND_FAILED";
        }

        if (
            string.IsNullOrWhiteSpace(
                errorMessage))
        {
            errorMessage =
                "El agente informó que el comando falló.";
        }

        var command =
            await GetCommandAsync(
                deviceId,
                commandId,
                cancellationToken);

        if (command.Status == DeviceCommandStatus.Failed)
        {
            return;
        }

        if (IsTerminal(command.Status))
        {
            throw new InvalidOperationException(
                $"El comando ya terminó con estado {command.Status}.");
        }

        command
            .CompleteFailure(
                errorCode,
                errorMessage,
                resultJson);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        await NotifyUpdatedAsync(command, cancellationToken);
    }

    // ============================================================
    // GET COMMAND
    // ============================================================

    private async Task<DeviceCommand>
        GetCommandAsync(
            Guid deviceId,
            Guid commandId,
            CancellationToken cancellationToken)
    {
        if (
            deviceId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "DeviceId no es válido.");
        }

        if (
            commandId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "CommandId no es válido.");
        }

        var command =
            await _dbContext
                .DeviceCommands
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            commandId
                        &&
                        x.DeviceId ==
                            deviceId,
                    cancellationToken);

        if (
            command is null)
        {
            throw new InvalidOperationException(
                "El comando no existe o no pertenece al dispositivo.");
        }

        return command;
    }

    // ============================================================
    // RESULT VALIDATION
    // ============================================================

    private static void RequireResult(
        string? resultJson,
        string commandType)
    {
        if (
            string.IsNullOrWhiteSpace(
                resultJson))
        {
            throw new InvalidOperationException(
                $"{commandType} no devolvió información.");
        }
    }

    private Task NotifyUpdatedAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        return _notifier.NotifyUpdatedAsync(
            DeviceCommandMapper.Map(command),
            cancellationToken);
    }

    private static bool IsTerminal(DeviceCommandStatus status)
    {
        return status is DeviceCommandStatus.Success or
            DeviceCommandStatus.Failed or
            DeviceCommandStatus.Timeout or
            DeviceCommandStatus.Cancelled;
    }
}
