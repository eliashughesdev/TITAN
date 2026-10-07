using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace TitanMDM.WindowsAgent.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsInventoryProvider
{
    private readonly WindowsDeviceInfoProvider
        _deviceInfoProvider;

    public WindowsInventoryProvider(
        WindowsDeviceInfoProvider deviceInfoProvider)
    {
        _deviceInfoProvider =
            deviceInfoProvider;
    }

    // ============================================================
    // FULL INVENTORY
    // ============================================================

    public WindowsInventorySnapshot
        Collect()
    {
        return new WindowsInventorySnapshot(
            Device:
                CollectDevice(),

            Network:
                CollectNetwork(),

            Disks:
                CollectDisks(),

            Applications:
                CollectApplications(),

            Processes:
                CollectProcesses(),

            Services:
                CollectServices(),

            CollectedAtUtc:
                DateTime.UtcNow);
    }

    // ============================================================
    // DEVICE
    // ============================================================

    public WindowsDeviceSnapshot
        CollectDevice()
    {
        var info =
            _deviceInfoProvider
                .GetDeviceInformation();

        var systemDrive =
            Path.GetPathRoot(
                Environment.SystemDirectory)
            ??
            @"C:\";

        DriveInfo?
            drive =
                null;

        try
        {
            drive =
                new DriveInfo(
                    systemDrive);
        }
        catch
        {
            // La recolección continúa.
        }

        return new WindowsDeviceSnapshot(
            ComputerName:
                info.DeviceName,

            UserName:
                info.UserName,

            DomainName:
                info.DomainName,

            Manufacturer:
                info.Manufacturer,

            Model:
                info.Model,

            SerialNumber:
                info.SerialNumber,

            BiosVersion:
                info.BiosVersion,

            CpuName:
                info.CpuName,

            TotalMemoryBytes:
                info.TotalMemoryBytes,

            OperatingSystem:
                info.OperatingSystem,

            OperatingSystemVersion:
                info.OperatingSystemVersion,

            OsArchitecture:
                RuntimeInformation
                    .OSArchitecture
                    .ToString(),

            ProcessArchitecture:
                RuntimeInformation
                    .ProcessArchitecture
                    .ToString(),

            Framework:
                RuntimeInformation
                    .FrameworkDescription,

            ProcessorCount:
                Environment
                    .ProcessorCount,

            Is64BitOperatingSystem:
                Environment
                    .Is64BitOperatingSystem,

            MachineGuid:
                info.MachineGuid,

            ProductName:
                ReadRegistryString(
                    Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "ProductName"),

            DisplayVersion:
                ReadRegistryString(
                    Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "DisplayVersion"),

            CurrentBuild:
                ReadRegistryString(
                    Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "CurrentBuildNumber")
                ??
                ReadRegistryString(
                    Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "CurrentBuild"),

            InstallDateUtc:
                ReadInstallDate(),

            SystemDrive:
                drive?
                    .Name
                ??
                systemDrive,

            SystemDriveTotalBytes:
                SafeDriveValue(
                    () =>
                        drive?
                            .TotalSize),

            SystemDriveFreeBytes:
                SafeDriveValue(
                    () =>
                        drive?
                            .AvailableFreeSpace),

            AgentVersion:
                info.AgentVersion);
    }

    // ============================================================
    // NETWORK
    // ============================================================

    public IReadOnlyCollection<
        WindowsNetworkAdapterSnapshot>
        CollectNetwork()
    {
        var adapters =
            new List<
                WindowsNetworkAdapterSnapshot>();

        foreach (
            var networkInterface
            in NetworkInterface
                .GetAllNetworkInterfaces())
        {
            try
            {
                var properties =
                    networkInterface
                        .GetIPProperties();

                var addresses =
                    properties
                        .UnicastAddresses
                        .Where(
                            x =>
                                x.Address
                                    .AddressFamily ==
                                    AddressFamily
                                        .InterNetwork
                                ||
                                x.Address
                                    .AddressFamily ==
                                    AddressFamily
                                        .InterNetworkV6)
                        .Select(
                            x =>
                                x.Address
                                    .ToString())
                        .Distinct()
                        .ToArray();

                var gateways =
                    properties
                        .GatewayAddresses
                        .Select(
                            x =>
                                x.Address
                                    .ToString())
                        .Where(
                            x =>
                                !string.IsNullOrWhiteSpace(
                                    x))
                        .Distinct()
                        .ToArray();

                var dns =
                    properties
                        .DnsAddresses
                        .Select(
                            x =>
                                x.ToString())
                        .Distinct()
                        .ToArray();

                adapters.Add(
                    new WindowsNetworkAdapterSnapshot(
                        Name:
                            networkInterface
                                .Name,

                        Description:
                            networkInterface
                                .Description,

                        InterfaceType:
                            networkInterface
                                .NetworkInterfaceType
                                .ToString(),

                        OperationalStatus:
                            networkInterface
                                .OperationalStatus
                                .ToString(),

                        MacAddress:
                            FormatMacAddress(
                                networkInterface
                                    .GetPhysicalAddress()),

                        Speed:
                            networkInterface
                                .Speed,

                        IpAddresses:
                            addresses,

                        Gateways:
                            gateways,

                        DnsServers:
                            dns));
            }
            catch
            {
                // Un adaptador defectuoso no invalida inventario.
            }
        }

        return adapters;
    }

    // ============================================================
    // DISKS
    // ============================================================

    public IReadOnlyCollection<
        WindowsDiskSnapshot>
        CollectDisks()
    {
        var disks =
            new List<
                WindowsDiskSnapshot>();

        foreach (
            var drive
            in DriveInfo
                .GetDrives())
        {
            try
            {
                if (
                    !drive.IsReady)
                {
                    continue;
                }

                disks.Add(
                    new WindowsDiskSnapshot(
                        Name:
                            drive.Name,

                        DriveType:
                            drive.DriveType
                                .ToString(),

                        FileSystem:
                            Normalize(
                                drive.DriveFormat),

                        VolumeLabel:
                            Normalize(
                                drive.VolumeLabel),

                        TotalBytes:
                            drive.TotalSize,

                        FreeBytes:
                            drive.AvailableFreeSpace));
            }
            catch
            {
                // Continuar.
            }
        }

        return disks;
    }

    // ============================================================
    // APPLICATIONS
    // ============================================================

    public IReadOnlyCollection<
        WindowsApplicationSnapshot>
        CollectApplications()
    {
        var applications =
            new Dictionary<
                string,
                WindowsApplicationSnapshot>(
                    StringComparer
                        .OrdinalIgnoreCase);

        ReadApplications(
            RegistryHive.LocalMachine,
            RegistryView.Registry64,
            applications);

        ReadApplications(
            RegistryHive.LocalMachine,
            RegistryView.Registry32,
            applications);

        ReadApplications(
            RegistryHive.CurrentUser,
            RegistryView.Default,
            applications);

        return applications
            .Values
            .OrderBy(
                x =>
                    x.Name)
            .ToArray();
    }

    // ============================================================
    // PROCESSES
    // ============================================================

    public IReadOnlyCollection<
        WindowsProcessSnapshot>
        CollectProcesses()
    {
        var processes =
            new List<
                WindowsProcessSnapshot>();

        foreach (
            var process
            in Process
                .GetProcesses())
        {
            try
            {
                processes.Add(
                    new WindowsProcessSnapshot(
                        ProcessId:
                            process.Id,

                        Name:
                            process.ProcessName,

                        WorkingSetBytes:
                            process.WorkingSet64,

                        StartTimeUtc:
                            TryGetProcessStartTime(
                                process)));
            }
            catch
            {
                // Algunos procesos del sistema niegan acceso.
            }
            finally
            {
                process.Dispose();
            }
        }

        return processes
            .OrderBy(
                x =>
                    x.Name)
            .ToArray();
    }

    // ============================================================
    // SERVICES
    // ============================================================

    public IReadOnlyCollection<
        WindowsServiceSnapshot>
        CollectServices()
    {
        var services =
            new List<
                WindowsServiceSnapshot>();

        using var servicesKey =
            Registry.LocalMachine
                .OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services");

        if (
            servicesKey is null)
        {
            return services;
        }

        foreach (
            var serviceName
            in servicesKey
                .GetSubKeyNames())
        {
            try
            {
                using var serviceKey =
                    servicesKey
                        .OpenSubKey(
                            serviceName);

                if (
                    serviceKey is null)
                {
                    continue;
                }

                services.Add(
                    new WindowsServiceSnapshot(
                        Name:
                            serviceName,

                        DisplayName:
                            Normalize(
                                serviceKey
                                    .GetValue(
                                        "DisplayName")
                                    ?.ToString()),

                        ImagePath:
                            Normalize(
                                serviceKey
                                    .GetValue(
                                        "ImagePath")
                                    ?.ToString()),

                        StartType:
                            serviceKey
                                .GetValue(
                                    "Start")
                                ?.ToString(),

                        ServiceType:
                            serviceKey
                                .GetValue(
                                    "Type")
                                ?.ToString()));
            }
            catch
            {
                // Continuar.
            }
        }

        return services
            .OrderBy(
                x =>
                    x.Name)
            .ToArray();
    }

    // ============================================================
    // INSTALLED APPLICATIONS
    // ============================================================

    private static void
        ReadApplications(
            RegistryHive hive,
            RegistryView view,
            IDictionary<
                string,
                WindowsApplicationSnapshot>
                applications)
    {
        const string uninstallPath =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        try
        {
            using var baseKey =
                RegistryKey
                    .OpenBaseKey(
                        hive,
                        view);

            using var uninstallKey =
                baseKey
                    .OpenSubKey(
                        uninstallPath);

            if (
                uninstallKey is null)
            {
                return;
            }

            foreach (
                var subKeyName
                in uninstallKey
                    .GetSubKeyNames())
            {
                try
                {
                    using var applicationKey =
                        uninstallKey
                            .OpenSubKey(
                                subKeyName);

                    if (
                        applicationKey is null)
                    {
                        continue;
                    }

                    var name =
                        Normalize(
                            applicationKey
                                .GetValue(
                                    "DisplayName")
                                ?.ToString());

                    if (
                        name is null)
                    {
                        continue;
                    }

                    var version =
                        Normalize(
                            applicationKey
                                .GetValue(
                                    "DisplayVersion")
                                ?.ToString());

                    var publisher =
                        Normalize(
                            applicationKey
                                .GetValue(
                                    "Publisher")
                                ?.ToString());

                    var installLocation =
                        Normalize(
                            applicationKey
                                .GetValue(
                                    "InstallLocation")
                                ?.ToString());

                    var uninstallString =
                        Normalize(
                            applicationKey
                                .GetValue(
                                    "UninstallString")
                                ?.ToString());

                    var key =
                        $"{name}|{version}|{publisher}";

                    applications[key] =
                        new WindowsApplicationSnapshot(
                            Name:
                                name,

                            Version:
                                version,

                            Publisher:
                                publisher,

                            InstallLocation:
                                installLocation,

                            UninstallString:
                                uninstallString);
                }
                catch
                {
                    // Saltar aplicación defectuosa.
                }
            }
        }
        catch
        {
            // Continuar.
        }
    }

    // ============================================================
    // REGISTRY
    // ============================================================

    private static string?
        ReadRegistryString(
            RegistryKey root,
            string path,
            string valueName)
    {
        try
        {
            using var key =
                root
                    .OpenSubKey(
                        path);

            return Normalize(
                key?
                    .GetValue(
                        valueName)
                    ?.ToString());
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // INSTALL DATE
    // ============================================================

    private static DateTime?
        ReadInstallDate()
    {
        var raw =
            ReadRegistryString(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "InstallDate");

        if (
            !long.TryParse(
                raw,
                out var unixSeconds))
        {
            return null;
        }

        try
        {
            return DateTimeOffset
                .FromUnixTimeSeconds(
                    unixSeconds)
                .UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // DRIVE SAFE
    // ============================================================

    private static long?
        SafeDriveValue(
            Func<long?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // PROCESS
    // ============================================================

    private static DateTime?
        TryGetProcessStartTime(
            Process process)
    {
        try
        {
            return process
                .StartTime
                .ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // MAC
    // ============================================================

    private static string
        FormatMacAddress(
            PhysicalAddress address)
    {
        var bytes =
            address
                .GetAddressBytes();

        if (
            bytes.Length ==
                0)
        {
            return string.Empty;
        }

        return string.Join(
            ":",
            bytes.Select(
                x =>
                    x.ToString(
                        "X2")));
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
}

// ================================================================
// CONTRACTS
// ================================================================

public sealed record WindowsInventorySnapshot(
    WindowsDeviceSnapshot Device,
    IReadOnlyCollection<
        WindowsNetworkAdapterSnapshot> Network,
    IReadOnlyCollection<
        WindowsDiskSnapshot> Disks,
    IReadOnlyCollection<
        WindowsApplicationSnapshot> Applications,
    IReadOnlyCollection<
        WindowsProcessSnapshot> Processes,
    IReadOnlyCollection<
        WindowsServiceSnapshot> Services,
    DateTime CollectedAtUtc);

public sealed record WindowsDeviceSnapshot(
    string ComputerName,
    string? UserName,
    string? DomainName,
    string? Manufacturer,
    string? Model,
    string SerialNumber,
    string? BiosVersion,
    string? CpuName,
    long? TotalMemoryBytes,
    string OperatingSystem,
    string OperatingSystemVersion,
    string OsArchitecture,
    string ProcessArchitecture,
    string Framework,
    int ProcessorCount,
    bool Is64BitOperatingSystem,
    string? MachineGuid,
    string? ProductName,
    string? DisplayVersion,
    string? CurrentBuild,
    DateTime? InstallDateUtc,
    string SystemDrive,
    long? SystemDriveTotalBytes,
    long? SystemDriveFreeBytes,
    string AgentVersion);

public sealed record WindowsNetworkAdapterSnapshot(
    string Name,
    string Description,
    string InterfaceType,
    string OperationalStatus,
    string MacAddress,
    long Speed,
    IReadOnlyCollection<string> IpAddresses,
    IReadOnlyCollection<string> Gateways,
    IReadOnlyCollection<string> DnsServers);

public sealed record WindowsDiskSnapshot(
    string Name,
    string DriveType,
    string? FileSystem,
    string? VolumeLabel,
    long TotalBytes,
    long FreeBytes);

public sealed record WindowsApplicationSnapshot(
    string Name,
    string? Version,
    string? Publisher,
    string? InstallLocation,
    string? UninstallString);

public sealed record WindowsProcessSnapshot(
    int ProcessId,
    string Name,
    long WorkingSetBytes,
    DateTime? StartTimeUtc);

public sealed record WindowsServiceSnapshot(
    string Name,
    string? DisplayName,
    string? ImagePath,
    string? StartType,
    string? ServiceType);