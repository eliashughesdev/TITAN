
using System.Diagnostics;

using Microsoft.AspNetCore.SignalR;
using TitanMDM.Api.Hubs;
using TitanMDM.Application.Commands;

namespace TitanMDM.Api.Services;

public sealed class DeviceCommandSignalRNotifier
    : IDeviceCommandNotifier
{
    private readonly IHubContext<DeviceCommandHub> _hubContext;
    private readonly ILogger<DeviceCommandSignalRNotifier> _logger;

    public DeviceCommandSignalRNotifier(
        IHubContext<DeviceCommandHub> hubContext,
        ILogger<DeviceCommandSignalRNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task NotifyAvailableAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty || commandId == Guid.Empty)
        {
            throw new ArgumentException(
                "La notificación requiere DeviceId y CommandId válidos.");
        }

        return SendSafelyAsync(
            _hubContext.Clients.Group(
                DeviceCommandHub.AgentGroup(deviceId)),
            "CommandAvailable",
            commandId,
            deviceId,
            commandId,
            cancellationToken);
    }

    public Task NotifyUpdatedAsync(
        DeviceCommandDto command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return SendSafelyAsync(
            _hubContext.Clients.Group(
                DeviceCommandHub.ViewerGroup(
                    command.OrganizationId,
                    command.DeviceId)),
            "DeviceCommandUpdated",
            command,
            command.DeviceId,
            command.Id,
            cancellationToken);
    }

    private async Task SendSafelyAsync(
        IClientProxy client,
        string method,
        object payload,
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var started = Stopwatch.GetTimestamp();

        try
        {
            await client.SendCoreAsync(
                method,
                [payload],
                cancellationToken);

            var elapsed = Stopwatch.GetElapsedTime(started);

            _logger.LogInformation(
                "WIN-C SignalR published. " +
                "Method={Method}, DeviceId={DeviceId}, " +
                "CommandId={CommandId}, PublishMs={PublishMs:F1}",
                method,
                deviceId,
                commandId,
                elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var elapsed = Stopwatch.GetElapsedTime(started);

            /*
             * La orden ya fue persistida por DeviceCommandService.
             *
             * Un fallo de SignalR NO equivale a fallo de ejecución.
             * El agente podrá recuperarla mediante sondeo HTTP.
             */
            _logger.LogWarning(
                exception,
                "WIN-C SignalR publication failed. " +
                "Method={Method}, DeviceId={DeviceId}, " +
                "CommandId={CommandId}, PublishMs={PublishMs:F1}. " +
                "HTTP polling remains the fallback.",
                method,
                deviceId,
                commandId,
                elapsed.TotalMilliseconds);
        }
    }
}
