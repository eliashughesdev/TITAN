using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Applications;
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

    public DeviceCommandAgentService(
        TitanMdmDbContext dbContext,
        IApplicationInventoryService applicationInventoryService,
        ISecurityPostureService securityPostureService,
        IDeviceLocationService deviceLocationService,
        WindowsInventoryResultProcessor windowsInventoryProcessor)
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

        var expiredCommands =
            await _dbContext
                .DeviceCommands
                .Where(
                    x =>
                        x.DeviceId ==
                            deviceId
                        &&
                        (
                            x.Status ==
                                DeviceCommandStatus.Pending
                            ||
                            x.Status ==
                                DeviceCommandStatus.Queued
                            ||
                            x.Status ==
                                DeviceCommandStatus.Dispatching
                            ||
                            x.Status ==
                                DeviceCommandStatus.Sent
                        )
                        &&
                        x.ExpiresAtUtc <=
                            now)
                .ToListAsync(
                    cancellationToken);

        foreach (
            var expiredCommand
            in expiredCommands)
        {
            expiredCommand
                .MarkTimeout();
        }

        var commands =
            await _dbContext
                .DeviceCommands
                .Where(
                    x =>
                        x.DeviceId ==
                            deviceId
                        &&
                        (
                            x.Status ==
                                DeviceCommandStatus.Pending
                            ||
                            x.Status ==
                                DeviceCommandStatus.Queued
                        )
                        &&
                        x.ExpiresAtUtc >
                            now)
                .OrderBy(
                    x =>
                        x.CreatedAtUtc)
                .Take(
                    20)
                .ToListAsync(
                    cancellationToken);

        var result =
            new List<
                AgentCommandDto>();

        foreach (
            var command
            in commands)
        {
            command
                .MarkDispatching();

            command
                .MarkSent();

            result.Add(
                new AgentCommandDto(
                    command.Id,
                    command.CommandType,
                    command.PayloadJson,
                    command.CreatedAtUtc,
                    command.ExpiresAtUtc));
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

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

        command
            .CompleteFailure(
                errorCode,
                errorMessage,
                resultJson);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
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
}