using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace TitanMDM.WindowsAgent.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsSecurityProvider
{
    private readonly ILogger<WindowsSecurityProvider> _logger;

    public WindowsSecurityProvider(
        ILogger<WindowsSecurityProvider> logger)
    {
        _logger = logger;
    }

    public async Task<WindowsSecuritySnapshot> CollectAsync(
        CancellationToken cancellationToken = default)
    {
        var defender =
            await CollectDefenderAsync(
                cancellationToken);

        var firewall =
            await CollectFirewallAsync(
                cancellationToken);

        var bitLocker =
            await CollectBitLockerAsync(
                cancellationToken);

        var tpm =
            await CollectTpmAsync(
                cancellationToken);

        var secureBoot =
            await CollectSecureBootAsync(
                cancellationToken);

        var pendingReboot =
            DetectPendingReboot();

        var uacEnabled =
            DetectUac();

        var remoteDesktopEnabled =
            DetectRemoteDesktop();

        var windowsUpdate =
            CollectWindowsUpdateConfiguration();

        return new WindowsSecuritySnapshot(
            Defender: defender,
            Firewall: firewall,
            BitLocker: bitLocker,
            Tpm: tpm,
            SecureBootEnabled: secureBoot,
            UacEnabled: uacEnabled,
            PendingReboot: pendingReboot,
            RemoteDesktopEnabled: remoteDesktopEnabled,
            WindowsUpdate: windowsUpdate,
            CollectedAtUtc: DateTime.UtcNow);
    }

    private async Task<WindowsDefenderSnapshot>
        CollectDefenderAsync(
            CancellationToken cancellationToken)
    {
        var result =
            await RunPowerShellAsync(
                """
                $ErrorActionPreference = 'Stop'

                $status = Get-MpComputerStatus

                [PSCustomObject]@{
                    AntivirusEnabled = $status.AntivirusEnabled
                    AntispywareEnabled = $status.AntispywareEnabled
                    RealTimeProtectionEnabled = $status.RealTimeProtectionEnabled
                    BehaviorMonitorEnabled = $status.BehaviorMonitorEnabled
                    IoavProtectionEnabled = $status.IoavProtectionEnabled
                    NISEnabled = $status.NISEnabled
                    AntivirusSignatureVersion = $status.AntivirusSignatureVersion
                    AntivirusSignatureLastUpdated = $status.AntivirusSignatureLastUpdated
                    QuickScanAge = $status.QuickScanAge
                    FullScanAge = $status.FullScanAge
                } | ConvertTo-Json -Compress
                """,
                cancellationToken);

        return new WindowsDefenderSnapshot(
            Available: result.Success,
            RawJson: result.StandardOutput,
            Error: result.Success
                ? null
                : result.StandardError);
    }

    private async Task<WindowsFirewallSnapshot>
        CollectFirewallAsync(
            CancellationToken cancellationToken)
    {
        var result =
            await RunPowerShellAsync(
                """
                $ErrorActionPreference = 'Stop'

                Get-NetFirewallProfile |
                    Select-Object Name, Enabled, DefaultInboundAction, DefaultOutboundAction |
                    ConvertTo-Json -Compress
                """,
                cancellationToken);

        return new WindowsFirewallSnapshot(
            Available: result.Success,
            RawJson: result.StandardOutput,
            Error: result.Success
                ? null
                : result.StandardError);
    }

    private async Task<WindowsBitLockerSnapshot>
        CollectBitLockerAsync(
            CancellationToken cancellationToken)
    {
        var result =
            await RunPowerShellAsync(
                """
                $ErrorActionPreference = 'Stop'

                Get-BitLockerVolume |
                    Select-Object MountPoint, VolumeStatus, ProtectionStatus, EncryptionPercentage, EncryptionMethod |
                    ConvertTo-Json -Compress
                """,
                cancellationToken);

        return new WindowsBitLockerSnapshot(
            Available: result.Success,
            RawJson: result.StandardOutput,
            Error: result.Success
                ? null
                : result.StandardError);
    }

    private async Task<WindowsTpmSnapshot>
        CollectTpmAsync(
            CancellationToken cancellationToken)
    {
        var result =
            await RunPowerShellAsync(
                """
                $ErrorActionPreference = 'Stop'

                $tpm = Get-Tpm

                [PSCustomObject]@{
                    TpmPresent = $tpm.TpmPresent
                    TpmReady = $tpm.TpmReady
                    TpmEnabled = $tpm.TpmEnabled
                    TpmActivated = $tpm.TpmActivated
                    AutoProvisioning = $tpm.AutoProvisioning
                } | ConvertTo-Json -Compress
                """,
                cancellationToken);

        return new WindowsTpmSnapshot(
            Available: result.Success,
            RawJson: result.StandardOutput,
            Error: result.Success
                ? null
                : result.StandardError);
    }

    private async Task<bool?>
        CollectSecureBootAsync(
            CancellationToken cancellationToken)
    {
        var result =
            await RunPowerShellAsync(
                """
                try {
                    $value = Confirm-SecureBootUEFI
                    Write-Output $value
                    exit 0
                }
                catch {
                    Write-Error $_.Exception.Message
                    exit 1
                }
                """,
                cancellationToken);

        if (!result.Success)
        {
            return null;
        }

        return bool.TryParse(
            result.StandardOutput.Trim(),
            out var enabled)
                ? enabled
                : null;
    }

    private static bool DetectUac()
    {
        try
        {
            using var key =
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");

            return Convert.ToInt32(
                key?.GetValue(
                    "EnableLUA",
                    0)) == 1;
        }
        catch
        {
            return false;
        }
    }

    private static bool DetectRemoteDesktop()
    {
        try
        {
            using var key =
                Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Terminal Server");

            return Convert.ToInt32(
                key?.GetValue(
                    "fDenyTSConnections",
                    1)) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool DetectPendingReboot()
    {
        try
        {
            using var cbs =
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");

            if (cbs is not null)
            {
                return true;
            }

            using var update =
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");

            if (update is not null)
            {
                return true;
            }

            using var sessionManager =
                Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager");

            return sessionManager?
                .GetValue(
                    "PendingFileRenameOperations")
                is not null;
        }
        catch
        {
            return false;
        }
    }

    private static WindowsUpdateConfigurationSnapshot
        CollectWindowsUpdateConfiguration()
    {
        try
        {
            using var key =
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");

            return new WindowsUpdateConfigurationSnapshot(
                NoAutoUpdate:
                    ReadInt(
                        key,
                        "NoAutoUpdate"),

                AuOptions:
                    ReadInt(
                        key,
                        "AUOptions"),

                ScheduledInstallDay:
                    ReadInt(
                        key,
                        "ScheduledInstallDay"),

                ScheduledInstallTime:
                    ReadInt(
                        key,
                        "ScheduledInstallTime"));
        }
        catch
        {
            return new WindowsUpdateConfigurationSnapshot(
                null,
                null,
                null,
                null);
        }
    }

    private static int?
        ReadInt(
            RegistryKey? key,
            string name)
    {
        var value =
            key?.GetValue(name);

        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToInt32(
                value);
        }
        catch
        {
            return null;
        }
    }

    private async Task<PowerShellExecutionResult>
        RunPowerShellAsync(
            string script,
            CancellationToken cancellationToken)
    {
        var encodedCommand =
            Convert.ToBase64String(
                System.Text.Encoding.Unicode
                    .GetBytes(script));

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    "powershell.exe",

                Arguments =
                    $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true
            };

        using var process =
            new Process
            {
                StartInfo =
                    startInfo
            };

        try
        {
            process.Start();

            var stdoutTask =
                process.StandardOutput
                    .ReadToEndAsync(
                        cancellationToken);

            var stderrTask =
                process.StandardError
                    .ReadToEndAsync(
                        cancellationToken);

            await process.WaitForExitAsync(
                cancellationToken);

            var stdout =
                await stdoutTask;

            var stderr =
                await stderrTask;

            return new PowerShellExecutionResult(
                process.ExitCode == 0,
                process.ExitCode,
                stdout.Trim(),
                stderr.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No fue posible consultar el estado de seguridad de Windows.");

            return new PowerShellExecutionResult(
                false,
                -1,
                string.Empty,
                ex.Message);
        }
    }
}

public sealed record WindowsSecuritySnapshot(
    WindowsDefenderSnapshot Defender,
    WindowsFirewallSnapshot Firewall,
    WindowsBitLockerSnapshot BitLocker,
    WindowsTpmSnapshot Tpm,
    bool? SecureBootEnabled,
    bool UacEnabled,
    bool PendingReboot,
    bool RemoteDesktopEnabled,
    WindowsUpdateConfigurationSnapshot WindowsUpdate,
    DateTime CollectedAtUtc);

public sealed record WindowsDefenderSnapshot(
    bool Available,
    string RawJson,
    string? Error);

public sealed record WindowsFirewallSnapshot(
    bool Available,
    string RawJson,
    string? Error);

public sealed record WindowsBitLockerSnapshot(
    bool Available,
    string RawJson,
    string? Error);

public sealed record WindowsTpmSnapshot(
    bool Available,
    string RawJson,
    string? Error);

public sealed record WindowsUpdateConfigurationSnapshot(
    int? NoAutoUpdate,
    int? AuOptions,
    int? ScheduledInstallDay,
    int? ScheduledInstallTime);

public sealed record PowerShellExecutionResult(
    bool Success,
    int ExitCode,
    string StandardOutput,
    string StandardError);