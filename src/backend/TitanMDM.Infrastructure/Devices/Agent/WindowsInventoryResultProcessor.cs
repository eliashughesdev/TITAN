using System.Net;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace TitanMDM.Infrastructure.Devices.Agent;

public sealed class WindowsInventoryResultProcessor
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly ILogger<
        WindowsInventoryResultProcessor>
        _logger;

    public WindowsInventoryResultProcessor(
        TitanMdmDbContext dbContext,
        ILogger<WindowsInventoryResultProcessor> logger)
    {
        _dbContext =
            dbContext;

        _logger =
            logger;
    }

    // ============================================================
    // DEVICE INFO
    // ============================================================

    public async Task ProcessDeviceInfoAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        var payload =
            Deserialize<DeviceInfoPayload>(
                resultJson,
                "DEVICE_INFO");

        var device =
            await GetDeviceAsync(
                deviceId,
                cancellationToken);

        device.UpdateInventory(
            manufacturer:
                Normalize(
                    payload.Manufacturer),

            model:
                Normalize(
                    payload.Model),

            operatingSystem:
                Normalize(
                    payload.ProductName)
                ??
                Normalize(
                    payload.OperatingSystem),

            operatingSystemVersion:
                BuildOperatingSystemVersion(
                    payload.DisplayVersion,
                    payload.CurrentBuild,
                    payload.OperatingSystemVersion),

            agentVersion:
                Normalize(
                    payload.AgentVersion),

            imei:
                device.Imei,

            macAddress:
                device.MacAddress);

        device.AssignUser(
            Normalize(
                payload.UserName),

            device.Department);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "Inventario DEVICE_INFO actualizado para DeviceId {DeviceId}.",
            deviceId);
    }

    // ============================================================
    // FULL INVENTORY
    // ============================================================

    public async Task ProcessInventoryAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        var payload =
            Deserialize<InventoryPayload>(
                resultJson,
                "DEVICE_INVENTORY");

        var device =
            await GetDeviceAsync(
                deviceId,
                cancellationToken);

        var preferredAdapter =
            SelectPreferredNetworkAdapter(
                payload.Network);

        var ipAddress =
            preferredAdapter is null
                ? device.IpAddress
                : SelectPreferredIpAddress(
                    preferredAdapter.IpAddresses)
                  ??
                  device.IpAddress;

        var macAddress =
            preferredAdapter is null
                ? device.MacAddress
                : Normalize(
                    preferredAdapter.MacAddress)
                  ??
                  device.MacAddress;

        var operatingSystem =
            Normalize(
                payload.Device.ProductName)
            ??
            Normalize(
                payload.Device.OperatingSystem);

        var operatingSystemVersion =
            BuildOperatingSystemVersion(
                payload.Device.DisplayVersion,
                payload.Device.CurrentBuild,
                payload.Device.OperatingSystemVersion);

        device.UpdateInventory(
            manufacturer:
                Normalize(
                    payload.Device.Manufacturer),

            model:
                Normalize(
                    payload.Device.Model),

            operatingSystem:
                operatingSystem,

            operatingSystemVersion:
                operatingSystemVersion,

            agentVersion:
                Normalize(
                    payload.Device.AgentVersion),

            imei:
                device.Imei,

            macAddress:
                macAddress);

        device.RegisterHeartbeat(
            ipAddress,
            device.BatteryLevel);

        device.AssignUser(
            Normalize(
                payload.Device.UserName),

            device.Department);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "Inventario completo actualizado para DeviceId {DeviceId}.",
            deviceId);
    }

    // ============================================================
    // NETWORK
    // ============================================================

    public async Task ProcessNetworkAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        var payload =
            Deserialize<
                IReadOnlyCollection<NetworkAdapterPayload>>(
                    resultJson,
                    "NETWORK_INFO");

        var device =
            await GetDeviceAsync(
                deviceId,
                cancellationToken);

        var preferredAdapter =
            SelectPreferredNetworkAdapter(
                payload);

        if (
            preferredAdapter is null)
        {
            return;
        }

        var ipAddress =
            SelectPreferredIpAddress(
                preferredAdapter.IpAddresses);

        var macAddress =
            Normalize(
                preferredAdapter.MacAddress);

        device.UpdateInventory(
            device.Manufacturer,
            device.Model,
            device.OperatingSystem,
            device.OperatingSystemVersion,
            device.AgentVersion,
            device.Imei,
            macAddress
            ??
            device.MacAddress);

        device.RegisterHeartbeat(
            ipAddress
            ??
            device.IpAddress,
            device.BatteryLevel);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        _logger.LogInformation(
            "Información de red actualizada para DeviceId {DeviceId}.",
            deviceId);
    }

    // ============================================================
    // DEVICE LOOKUP
    // ============================================================

    private async Task<
        TitanMDM.Domain.Entities.Device>
        GetDeviceAsync(
            Guid deviceId,
            CancellationToken cancellationToken)
    {
        if (
            deviceId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "DeviceId no puede estar vacío.");
        }

        var device =
            await _dbContext
                .Devices
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            deviceId
                        &&
                        !x.IsDeleted,
                    cancellationToken);

        if (
            device is null)
        {
            throw new InvalidOperationException(
                "El dispositivo no existe.");
        }

        return device;
    }

    // ============================================================
    // NETWORK SELECTION
    // ============================================================

    private static NetworkAdapterPayload?
        SelectPreferredNetworkAdapter(
            IEnumerable<
                NetworkAdapterPayload> adapters)
    {
        return adapters
            .Where(
                x =>
                    string.Equals(
                        x.OperationalStatus,
                        "Up",
                        StringComparison.OrdinalIgnoreCase))
            .Where(
                x =>
                    !string.IsNullOrWhiteSpace(
                        x.MacAddress))
            .OrderByDescending(
                x =>
                    x.IpAddresses.Any(
                        IsUsableIpv4Address))
            .ThenByDescending(
                x =>
                    x.Gateways.Count >
                    0)
            .ThenByDescending(
                x =>
                    x.Speed)
            .FirstOrDefault();
    }

    private static string?
        SelectPreferredIpAddress(
            IEnumerable<string> addresses)
    {
        return addresses
            .FirstOrDefault(
                IsUsableIpv4Address);
    }

    private static bool
        IsUsableIpv4Address(
            string value)
    {
        if (
            !IPAddress.TryParse(
                value,
                out var address))
        {
            return false;
        }

        if (
            address.AddressFamily !=
            System.Net.Sockets
                .AddressFamily
                .InterNetwork)
        {
            return false;
        }

        if (
            IPAddress.IsLoopback(
                address))
        {
            return false;
        }

        return !address.Equals(
            IPAddress.Any);
    }

    // ============================================================
    // OS VERSION
    // ============================================================

    private static string?
        BuildOperatingSystemVersion(
            string? displayVersion,
            string? currentBuild,
            string? fallback)
    {
        var display =
            Normalize(
                displayVersion);

        var build =
            Normalize(
                currentBuild);

        if (
            display is not null
            &&
            build is not null)
        {
            return $"{display} (Build {build})";
        }

        if (
            build is not null)
        {
            return $"Build {build}";
        }

        return Normalize(
            fallback);
    }

    // ============================================================
    // JSON
    // ============================================================

    private static T Deserialize<T>(
        string resultJson,
        string commandType)
    {
        if (
            string.IsNullOrWhiteSpace(
                resultJson))
        {
            throw new InvalidOperationException(
                $"{commandType} devolvió un resultado vacío.");
        }

        try
        {
            return JsonSerializer
                .Deserialize<T>(
                    resultJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive =
                            true
                    })
                ??
                throw new InvalidOperationException(
                    $"{commandType} devolvió un resultado inválido.");
        }
        catch (
            JsonException ex)
        {
            throw new InvalidOperationException(
                $"{commandType} devolvió JSON inválido.",
                ex);
        }
    }

    // ============================================================
    // NORMALIZE
    // ============================================================

    private static string?
        Normalize(
            string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }

    // ============================================================
    // INTERNAL CONTRACTS
    // ============================================================

    private sealed record InventoryPayload(
        DeviceInfoPayload Device,
        IReadOnlyCollection<
            NetworkAdapterPayload> Network,
        JsonElement Applications,
        JsonElement Processes,
        JsonElement Services,
        DateTime CollectedAtUtc);

    private sealed record DeviceInfoPayload(
        string? ComputerName,
        string? UserName,
        string? DomainName,
        string? OperatingSystem,
        string? OperatingSystemVersion,
        string? OsArchitecture,
        string? ProcessArchitecture,
        string? Framework,
        int ProcessorCount,
        bool Is64BitOperatingSystem,
        string? MachineGuid,
        string? ProductName,
        string? DisplayVersion,
        string? CurrentBuild,
        DateTime? InstallDateUtc,
        string? SystemDrive,
        long? SystemDriveTotalBytes,
        long? SystemDriveFreeBytes,
        string? AgentVersion,
        string? Manufacturer = null,
        string? Model = null);

    private sealed record NetworkAdapterPayload(
        string? Name,
        string? Description,
        string? InterfaceType,
        string? OperationalStatus,
        string? MacAddress,
        long Speed,
        IReadOnlyCollection<string> IpAddresses,
        IReadOnlyCollection<string> Gateways,
        IReadOnlyCollection<string> DnsServers);
}