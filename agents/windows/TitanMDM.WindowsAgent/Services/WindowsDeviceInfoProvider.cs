
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace TitanMDM.WindowsAgent.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceInfoProvider
{
    private static readonly TimeSpan QueryTimeout =
        TimeSpan.FromSeconds(15);

    private const string WindowsVersionKey =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    private const string HardwareScript = """
        $ErrorActionPreference = 'Stop'
        $computer = Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop
        $bios = Get-CimInstance -ClassName Win32_BIOS -ErrorAction SilentlyContinue
        $processor = Get-CimInstance -ClassName Win32_Processor -ErrorAction SilentlyContinue |
            Select-Object -First 1
        $result = @{
            Manufacturer = [string]$computer.Manufacturer
            Model = [string]$computer.Model
            TotalMemoryBytes = [long]$computer.TotalPhysicalMemory
            SerialNumber = [string]$bios.SerialNumber
            BiosVersion = [string]$bios.SMBIOSBIOSVersion
            CpuName = [string]$processor.Name
        }
        $result | ConvertTo-Json -Compress -Depth 3
        """;

    public WindowsDeviceInformation GetDeviceInformation()
    {
        var hardware = QueryHardware();

        var version = typeof(WindowsDeviceInfoProvider)
            .Assembly.GetName().Version?.ToString() ?? "1.0.0";

        var productName = ReadRegistry(
            WindowsVersionKey, "ProductName");

        var displayVersion = ReadRegistry(
            WindowsVersionKey, "DisplayVersion");

        var currentBuild = ReadRegistry(
            WindowsVersionKey, "CurrentBuildNumber")
            ?? ReadRegistry(WindowsVersionKey, "CurrentBuild");

        var machineGuid = ReadRegistry(
            @"SOFTWARE\Microsoft\Cryptography", "MachineGuid");

        var interactiveUser =
            new WindowsInteractiveUserResolver().GetActiveUser();

        var operatingSystem =
            Normalize(productName) ?? RuntimeInformation.OSDescription;

        return new WindowsDeviceInformation(
            DeviceName: Environment.MachineName,
            SerialNumber: Normalize(hardware?.SerialNumber)
                ?? Normalize(machineGuid)
                ?? Environment.MachineName,
            Manufacturer: Normalize(hardware?.Manufacturer),
            Model: Normalize(hardware?.Model),
            OperatingSystem: operatingSystem,
            OperatingSystemVersion: BuildWindowsVersion(
                displayVersion, currentBuild),
            AgentVersion: version,
            UserName: interactiveUser?.UserName,
            DomainName: interactiveUser?.DomainName,
            Architecture: RuntimeInformation.OSArchitecture.ToString(),
            BiosVersion: Normalize(hardware?.BiosVersion),
            CpuName: Normalize(hardware?.CpuName),
            TotalMemoryBytes: hardware?.TotalMemoryBytes is > 0
                ? hardware.TotalMemoryBytes
                : null,
            MachineGuid: Normalize(machineGuid),
            CollectedAtUtc: DateTime.UtcNow);
    }

    private static HardwareSnapshot? QueryHardware()
    {
        Process? process = null;

        try
        {
            // EncodedCommand transporta el script en UTF-16LE;
            // no utiliza interpolación de entradas del usuario.
            var encoded = Convert.ToBase64String(
                Encoding.Unicode.GetBytes(HardwareScript));

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.System),
                        @"WindowsPowerShell\v1.0\powershell.exe"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.StartInfo.ArgumentList.Add("-NoLogo");
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-EncodedCommand");
            process.StartInfo.ArgumentList.Add(encoded);

            if (!process.Start())
                return null;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)QueryTimeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
                catch
                {
                    // Fallo de consulta: mantener el agente disponible.
                }

                return null;
            }

            var output = outputTask.GetAwaiter().GetResult();
            _ = errorTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            return JsonSerializer.Deserialize<HardwareSnapshot>(
                output,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling =
                        System.Text.Json.Serialization.JsonNumberHandling
                            .AllowReadingFromString
                });
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or IOException
            or JsonException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            // El inventario no debe cerrar el servicio del agente.
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static string? ReadRegistry(string keyPath, string value)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            return Normalize(key?.GetValue(value)?.ToString());
        }
        catch
        {
            return null;
        }
    }

    private static string BuildWindowsVersion(
        string? displayVersion,
        string? build)
    {
        if (!string.IsNullOrWhiteSpace(displayVersion)
            && !string.IsNullOrWhiteSpace(build))
        {
            return $"{displayVersion} (Build {build})";
        }

        if (!string.IsNullOrWhiteSpace(build))
            return $"Build {build}";

        return Environment.OSVersion.Version.ToString();
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        return normalized.Equals(
            "To Be Filled By O.E.M.",
            StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(
                "System Product Name",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(
                "Default string",
                StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private sealed class HardwareSnapshot
    {
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? BiosVersion { get; set; }
        public string? CpuName { get; set; }
        public long? TotalMemoryBytes { get; set; }
    }
}

public sealed record WindowsDeviceInformation(
    string DeviceName,
    string SerialNumber,
    string? Manufacturer,
    string? Model,
    string OperatingSystem,
    string OperatingSystemVersion,
    string AgentVersion,
    string? UserName,
    string? DomainName,
    string Architecture,
    string? BiosVersion,
    string? CpuName,
    long? TotalMemoryBytes,
    string? MachineGuid,
    DateTime CollectedAtUtc);
