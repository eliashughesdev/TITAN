using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TitanMDM.WindowsAgent.Configuration;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class AgentDiagnosticsService
{
    private readonly DeviceIdentityStore
        _identityStore;

    private readonly AgentRuntimeSettingsStore
        _settingsStore;

    private readonly AgentOptions
        _options;

    private readonly ILogger<AgentDiagnosticsService>
        _logger;

    private static readonly JsonSerializerOptions
        JsonOptions =
            new()
            {
                WriteIndented = true,
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase
            };

    public AgentDiagnosticsService(
        DeviceIdentityStore identityStore,
        AgentRuntimeSettingsStore settingsStore,
        IOptions<AgentOptions> options,
        ILogger<AgentDiagnosticsService> logger)
    {
        _identityStore =
            identityStore;

        _settingsStore =
            settingsStore;

        _options =
            options.Value;

        _logger =
            logger;
    }

    public async Task<AgentDiagnosticSnapshot>
        CaptureAsync(
            CancellationToken cancellationToken =
                default)
    {
        var identity =
            await _identityStore
                .LoadAsync(
                    cancellationToken);

        var settings =
            await _settingsStore
                .LoadAsync(
                    cancellationToken);

        var serverUrl =
            settings?.ServerUrl
            ??
            _options.ServerUrl;

        var programData =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .CommonApplicationData),
                "TitanMDM");

        var agentDirectory =
            AppContext.BaseDirectory;

        var remoteHostPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .ProgramFiles),
                "TitanMDM",
                "RemoteHost",
                "TitanMDM.RemoteHost.exe");

        var serviceStatus =
            GetServiceStatus(
                "TitanMDMWindowsAgent");

        var connectivity =
            await TestServerConnectivityAsync(
                serverUrl,
                cancellationToken);

        var disk =
            GetDiskInfo();

        var snapshot =
            new AgentDiagnosticSnapshot(
                CapturedAtUtc:
                    DateTime.UtcNow,

                MachineName:
                    Environment.MachineName,

                UserName:
                    Environment.UserName,

                OperatingSystem:
                    Environment.OSVersion
                        .VersionString,

                Is64BitOperatingSystem:
                    Environment
                        .Is64BitOperatingSystem,

                AgentVersion:
                    typeof(
                        AgentDiagnosticsService)
                        .Assembly
                        .GetName()
                        .Version?
                        .ToString()
                    ??
                    "unknown",

                AgentDirectory:
                    agentDirectory,

                ProgramDataDirectory:
                    programData,

                SettingsFile:
                    _settingsStore
                        .GetSettingsFilePath(),

                SettingsExists:
                    _settingsStore.Exists(),

                IdentityFile:
                    _identityStore
                        .GetIdentityFilePath(),

                IdentityExists:
                    _identityStore.Exists(),

                IdentityReadable:
                    identity is not null,

                DeviceId:
                    identity?.DeviceId,

                ServerUrl:
                    serverUrl,

                ServerReachable:
                    connectivity.Reachable,

                ServerStatusCode:
                    connectivity.StatusCode,

                ServerError:
                    connectivity.Error,

                AgentServiceStatus:
                    serviceStatus,

                RemoteHostPath:
                    remoteHostPath,

                RemoteHostExists:
                    File.Exists(
                        remoteHostPath),

                ProgramDataWritable:
                    TestDirectoryWritable(
                        programData),

                AgentDirectoryWritable:
                    TestDirectoryWritable(
                        agentDirectory),

                AvailableDiskBytes:
                    disk.AvailableBytes,

                TotalDiskBytes:
                    disk.TotalBytes,

                UptimeSeconds:
                    Environment.TickCount64 /
                    1000L,

                PendingReboot:
                    IsPendingReboot());

        await SaveSnapshotAsync(
            snapshot,
            cancellationToken);

        return snapshot;
    }

    private async Task SaveSnapshotAsync(
        AgentDiagnosticSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            var directory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder
                            .CommonApplicationData),
                    "TitanMDM",
                    "diagnostics");

            Directory.CreateDirectory(
                directory);

            var path =
                Path.Combine(
                    directory,
                    "agent-health.json");

            var temporaryPath =
                path + ".tmp";

            var json =
                JsonSerializer.Serialize(
                    snapshot,
                    JsonOptions);

            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                cancellationToken);

            File.Move(
                temporaryPath,
                path,
                overwrite:
                    true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No fue posible persistir el diagnóstico TitanMDM.");
        }
    }

    private static string GetServiceStatus(
        string serviceName)
    {
        try
        {
            using var process =
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName =
                            "sc.exe",

                        Arguments =
                            $"query \"{serviceName}\"",

                        UseShellExecute =
                            false,

                        RedirectStandardOutput =
                            true,

                        RedirectStandardError =
                            true,

                        CreateNoWindow =
                            true
                    });

            if (process is null)
            {
                return "Unknown";
            }

            var output =
                process.StandardOutput
                    .ReadToEnd();

            process.WaitForExit(
                5000);

            if (
                output.Contains(
                    "RUNNING",
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return "Running";
            }

            if (
                output.Contains(
                    "STOPPED",
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return "Stopped";
            }

            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static async Task<
        ConnectivityResult>
        TestServerConnectivityAsync(
            string? serverUrl,
            CancellationToken cancellationToken)
    {
        if (
            string.IsNullOrWhiteSpace(
                serverUrl)
            ||
            !Uri.TryCreate(
                serverUrl,
                UriKind.Absolute,
                out var uri))
        {
            return new ConnectivityResult(
                false,
                null,
                "ServerUrl inválido.");
        }

        try
        {
            using var handler =
                new HttpClientHandler();

            using var client =
                new HttpClient(
                    handler)
                {
                    Timeout =
                        TimeSpan
                            .FromSeconds(10)
                };

            using var response =
                await client.GetAsync(
                    new Uri(
                        uri,
                        "/api/health/live"),
                    cancellationToken);

            return new ConnectivityResult(
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                response.IsSuccessStatusCode
                    ? null
                    : response
                        .ReasonPhrase);
        }
        catch (Exception ex)
            when (
                ex is
                    HttpRequestException
                ||
                ex is
                    TaskCanceledException
                ||
                ex is
                    OperationCanceledException)
        {
            return new ConnectivityResult(
                false,
                null,
                ex.Message);
        }
    }

    private static bool TestDirectoryWritable(
        string directory)
    {
        try
        {
            Directory.CreateDirectory(
                directory);

            var path =
                Path.Combine(
                    directory,
                    $".titan-write-test-{Guid.NewGuid():N}");

            File.WriteAllText(
                path,
                "test");

            File.Delete(
                path);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DiskResult GetDiskInfo()
    {
        try
        {
            var root =
                Path.GetPathRoot(
                    AppContext
                        .BaseDirectory);

            if (
                string.IsNullOrWhiteSpace(
                    root))
            {
                return new DiskResult(
                    0,
                    0);
            }

            var drive =
                new DriveInfo(
                    root);

            return new DiskResult(
                drive
                    .AvailableFreeSpace,
                drive
                    .TotalSize);
        }
        catch
        {
            return new DiskResult(
                0,
                0);
        }
    }

    private static bool IsPendingReboot()
    {
        try
        {
            using var key =
                Microsoft.Win32.Registry
                    .LocalMachine
                    .OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");

            if (key is not null)
            {
                return true;
            }

            using var updateKey =
                Microsoft.Win32.Registry
                    .LocalMachine
                    .OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");

            return updateKey is not null;
        }
        catch
        {
            return false;
        }
    }

    private sealed record
        ConnectivityResult(
            bool Reachable,
            int? StatusCode,
            string? Error);

    private sealed record
        DiskResult(
            long AvailableBytes,
            long TotalBytes);
}

public sealed record
    AgentDiagnosticSnapshot(
        DateTime CapturedAtUtc,
        string MachineName,
        string UserName,
        string OperatingSystem,
        bool Is64BitOperatingSystem,
        string AgentVersion,
        string AgentDirectory,
        string ProgramDataDirectory,
        string SettingsFile,
        bool SettingsExists,
        string IdentityFile,
        bool IdentityExists,
        bool IdentityReadable,
        Guid? DeviceId,
        string? ServerUrl,
        bool ServerReachable,
        int? ServerStatusCode,
        string? ServerError,
        string AgentServiceStatus,
        string RemoteHostPath,
        bool RemoteHostExists,
        bool ProgramDataWritable,
        bool AgentDirectoryWritable,
        long AvailableDiskBytes,
        long TotalDiskBytes,
        long UptimeSeconds,
        bool PendingReboot);