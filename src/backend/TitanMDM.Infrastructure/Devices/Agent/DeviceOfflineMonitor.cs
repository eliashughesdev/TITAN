using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TitanMDM.Application.Automation;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Devices.Agent;

public sealed class DeviceOfflineMonitor
    : BackgroundService
{
    /*
     * Si un dispositivo no reporta durante
     * cinco minutos, TitanMDM lo considera
     * offline.
     *
     * Más adelante este valor podrá moverse
     * a Global Settings.
     */
    private static readonly TimeSpan
        OfflineThreshold =
            TimeSpan.FromMinutes(5);

    /*
     * El monitor revisa la flota cada minuto.
     */
    private static readonly TimeSpan
        ScanInterval =
            TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        DeviceOfflineMonitor>
        _logger;

    public DeviceOfflineMonitor(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceOfflineMonitor> logger)
    {
        _scopeFactory =
            scopeFactory;

        _logger =
            logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TitanMDM Device Offline Monitor started.");

        /*
         * Dejamos arrancar completamente la API
         * antes del primer análisis.
         */
        await Task.Delay(
            TimeSpan.FromSeconds(15),
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (
                    stoppingToken
                        .IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error while detecting offline devices.");
            }

            await Task.Delay(
                ScanInterval,
                stoppingToken);
        }
    }

    private async Task ScanAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<
                    TitanMdmDbContext>();

        var automation =
            scope.ServiceProvider
                .GetRequiredService<
                    IAutomationEventDispatcher>();

        var cutoff =
            DateTime.UtcNow -
            OfflineThreshold;

        /*
         * Solo analizamos dispositivos que
         * actualmente están Online.
         *
         * Esto evita publicar DeviceOffline
         * repetidamente.
         */
        var devices =
            await db.Devices
                .Where(
                    x =>
                        !x.IsDeleted &&
                        x.IsManaged &&
                        x.Status ==
                            DeviceStatus.Online &&
                        x.LastSeenAtUtc.HasValue &&
                        x.LastSeenAtUtc.Value <
                            cutoff)
                .ToListAsync(
                    cancellationToken);

        if (devices.Count == 0)
            return;

        foreach (var device in devices)
        {
            /*
             * El dominio actualmente no expone
             * un método genérico MarkOffline().
             *
             * Para mantener la transición dentro
             * del agregado Device, añadiremos
             * ese método en el siguiente paso.
             */
            device.MarkOffline();
        }

        /*
         * Persistimos TODOS los estados primero.
         */
        await db.SaveChangesAsync(
            cancellationToken);

        /*
         * Después publicamos Automation Events.
         */
        foreach (var device in devices)
        {
            await automation.DispatchAsync(
                device.OrganizationId,
                device.Id,
                "DeviceOffline",
                new
                {
                    platform =
                        device.Platform.ToString(),

                    deviceName =
                        device.DeviceName,

                    status =
                        device.Status.ToString(),

                    lastSeenAtUtc =
                        device.LastSeenAtUtc,

                    offlineThresholdMinutes =
                        OfflineThreshold
                            .TotalMinutes
                },
                cancellationToken:
                    cancellationToken);

            _logger.LogInformation(
                "Device {DeviceId} marked Offline.",
                device.Id);
        }
    }
}