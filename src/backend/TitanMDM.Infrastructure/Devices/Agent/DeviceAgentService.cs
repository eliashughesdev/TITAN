using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Automation;
using TitanMDM.Application.Devices.Agent;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Devices;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Devices.Agent;

public sealed class DeviceAgentService
    : IDeviceAgentService
{
    private const int LowBatteryThreshold =
        20;

    private const int BatteryRecoveryThreshold =
        25;

    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IAutomationEventDispatcher
        _automation;

    private readonly DeviceNamingResolver
        _deviceNamingResolver;

    public DeviceAgentService(
        TitanMdmDbContext dbContext,
        IAutomationEventDispatcher automation,
        DeviceNamingResolver deviceNamingResolver)
    {
        _dbContext =
            dbContext;

        _automation =
            automation;

        _deviceNamingResolver =
            deviceNamingResolver;
    }

    public async Task<DeviceHeartbeatResultDto>
        HeartbeatAsync(
            DeviceHeartbeatRequest request,
            CancellationToken cancellationToken = default)
    {
        // ============================================================
        // REQUEST VALIDATION
        // ============================================================

        if (
            request.DeviceId ==
            Guid.Empty)
        {
            throw new DeviceAuthenticationException(
                "INVALID_DEVICE_ID",
                "DeviceId no es válido.");
        }

        if (
            string.IsNullOrWhiteSpace(
                request.DeviceSecret))
        {
            throw new DeviceAuthenticationException(
                "INVALID_CREDENTIAL",
                "DeviceSecret es obligatorio.");
        }

        // ============================================================
        // DEVICE
        // ============================================================

        var device =
            await _dbContext.Devices
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.DeviceId
                        &&
                        !x.IsDeleted,
                    cancellationToken);

        if (device is null)
        {
            throw new DeviceAuthenticationException(
                "DEVICE_NOT_FOUND",
                "El dispositivo no existe.");
        }

        // ============================================================
        // DEVICE CREDENTIAL
        // ============================================================

        var credential =
            await _dbContext
                .DeviceCredentials
                .SingleOrDefaultAsync(
                    x =>
                        x.DeviceId ==
                            request.DeviceId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (credential is null)
        {
            throw new DeviceAuthenticationException(
                "CREDENTIAL_NOT_FOUND",
                "El dispositivo no posee una credencial activa.");
        }

        // ============================================================
        // CONSTANT-TIME CREDENTIAL VALIDATION
        // ============================================================

        var suppliedSecretHash =
            ComputeSha256(
                request.DeviceSecret.Trim());

        if (
            !FixedTimeEquals(
                credential.SecretHash,
                suppliedSecretHash))
        {
            throw new DeviceAuthenticationException(
                "INVALID_CREDENTIAL",
                "La credencial del dispositivo no es válida.");
        }

        // ============================================================
        // PREVIOUS STATE
        // ============================================================

        /*
         * Guardamos los valores ANTES del heartbeat.
         *
         * RegisterHeartbeat cambia automáticamente Status a Online.
         */

        var previousStatus =
            device.Status;

        var previousBatteryLevel =
            device.BatteryLevel;

        var hadExplicitSite =
            device.SiteId.HasValue;

        // ============================================================
        // DEVICE AUTHENTICATION
        // ============================================================

        credential
            .RegisterAuthentication();

        // ============================================================
        // HEARTBEAT
        // ============================================================

        device.RegisterHeartbeat(
            Normalize(
                request.IpAddress),
            request.BatteryLevel);

        // ============================================================
        // CORPORATE DEVICE NAMING
        // ============================================================

        /*
         * REGLA:
         *
         * Una asignación manual siempre tiene prioridad.
         *
         * Solo intentamos inferir Site / SiteLocation cuando
         * el dispositivo todavía NO tiene Site.
         *
         * Ejemplo:
         *
         * CILSPMCEDI01
         *
         * CI   = Cesar Iglesias
         * L    = Laptop
         * SPM  = San Pedro de Macorís
         * CEDI = área/site configurado
         * 01   = consecutivo
         */

        DeviceNamingResolution?
            namingResolution =
                null;

        if (
            !device.SiteId
                .HasValue)
        {
            namingResolution =
                await _deviceNamingResolver
                    .ResolveAsync(
                        device.OrganizationId,
                        device.DeviceName,
                        cancellationToken);

            if (
                namingResolution.Resolved
                &&
                namingResolution.SiteId
                    .HasValue)
            {
                device.AssignSite(
                    namingResolution.SiteId,
                    namingResolution
                        .SiteLocationId);
            }
        }

        // ============================================================
        // PERSIST
        // ============================================================

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        // ============================================================
        // DEVICE AUTO CLASSIFICATION EVENT
        // ============================================================

        if (
            !hadExplicitSite
            &&
            namingResolution is
            {
                Resolved: true,
                SiteId: not null
            })
        {
            await _automation
                .DispatchAsync(
                    device.OrganizationId,
                    device.Id,
                    "DeviceSiteResolved",
                    new
                    {
                        platform =
                            device.Platform
                                .ToString(),

                        deviceName =
                            device.DeviceName,

                        siteId =
                            namingResolution
                                .SiteId,

                        siteLocationId =
                            namingResolution
                                .SiteLocationId,

                        naming =
                            new
                            {
                                namingResolution
                                    .Parsed
                                    .OrganizationCode,

                                namingResolution
                                    .Parsed
                                    .DeviceTypeCode,

                                namingResolution
                                    .Parsed
                                    .DeviceType,

                                namingResolution
                                    .Parsed
                                    .CityCode,

                                namingResolution
                                    .Parsed
                                    .City,

                                namingResolution
                                    .Parsed
                                    .AreaCode,

                                namingResolution
                                    .Parsed
                                    .Sequence
                            },

                        source =
                            "corporate-device-name"
                    },
                    cancellationToken:
                        cancellationToken);
        }

        // ============================================================
        // DEVICE ONLINE
        // ============================================================

        /*
         * Solo publicamos:
         *
         * Offline -> Online
         *
         * No generamos DeviceOnline cada vez que llega heartbeat.
         */

        if (
            previousStatus ==
                DeviceStatus.Offline
            &&
            device.Status ==
                DeviceStatus.Online)
        {
            await _automation
                .DispatchAsync(
                    device.OrganizationId,
                    device.Id,
                    "DeviceOnline",
                    new
                    {
                        platform =
                            device.Platform
                                .ToString(),

                        deviceName =
                            device.DeviceName,

                        previousStatus =
                            previousStatus
                                .ToString(),

                        currentStatus =
                            device.Status
                                .ToString(),

                        lastSeenAtUtc =
                            device.LastSeenAtUtc,

                        siteId =
                            device.SiteId,

                        siteLocationId =
                            device.SiteLocationId
                    },
                    cancellationToken:
                        cancellationToken);
        }

        // ============================================================
        // LOW BATTERY
        // ============================================================

        /*
         * Solo se dispara al cruzar:
         *
         * >20% -> <=20%
         *
         * Esto evita lanzar una automatización en cada heartbeat.
         */

        if (
            request.BatteryLevel
                .HasValue
            &&
            request.BatteryLevel.Value <=
                LowBatteryThreshold
            &&
            (
                !previousBatteryLevel
                    .HasValue
                ||
                previousBatteryLevel.Value >
                    LowBatteryThreshold
            ))
        {
            await _automation
                .DispatchAsync(
                    device.OrganizationId,
                    device.Id,
                    "LowBattery",
                    new
                    {
                        platform =
                            device.Platform
                                .ToString(),

                        deviceName =
                            device.DeviceName,

                        batteryLevel =
                            request.BatteryLevel
                                .Value,

                        threshold =
                            LowBatteryThreshold,

                        previousBatteryLevel,

                        siteId =
                            device.SiteId,

                        siteLocationId =
                            device.SiteLocationId
                    },
                    cancellationToken:
                        cancellationToken);
        }

        // ============================================================
        // BATTERY RECOVERY HYSTERESIS
        // ============================================================

        /*
         * Actualmente no generamos BatteryRecovered.
         *
         * Pero al persistirse BatteryLevel, cuando vuelva a caer
         * después de haber superado 25%, LowBattery podrá volver
         * a dispararse correctamente.
         */

        _ =
            request.BatteryLevel
                .HasValue
            &&
            request.BatteryLevel.Value >=
                BatteryRecoveryThreshold;

        // ============================================================
        // RESULT
        // ============================================================

        return new DeviceHeartbeatResultDto(
            device.Id,
            device.Status
                .ToString(),
            device.ComplianceStatus
                .ToString(),
            DateTime.UtcNow,
            device.LastSeenAtUtc);
    }

    // ================================================================
    // SHA-256
    // ================================================================

    private static string
        ComputeSha256(
            string value)
    {
        var bytes =
            SHA256.HashData(
                Encoding.UTF8
                    .GetBytes(
                        value));

        return Convert
            .ToHexString(
                bytes);
    }

    // ================================================================
    // CONSTANT TIME COMPARISON
    // ================================================================

    private static bool
        FixedTimeEquals(
            string expectedHash,
            string suppliedHash)
    {
        try
        {
            var expected =
                Convert
                    .FromHexString(
                        expectedHash);

            var supplied =
                Convert
                    .FromHexString(
                        suppliedHash);

            return CryptographicOperations
                .FixedTimeEquals(
                    expected,
                    supplied);
        }
        catch (
            FormatException)
        {
            return false;
        }
    }

    // ================================================================
    // NORMALIZE
    // ================================================================

    private static string?
        Normalize(
            string? value)
    {
        return string
            .IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }
}