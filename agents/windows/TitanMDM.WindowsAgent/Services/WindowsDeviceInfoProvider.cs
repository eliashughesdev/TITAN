using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace TitanMDM.WindowsAgent.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceInfoProvider
{
    private const int PowerShellTimeoutMilliseconds =
        8_000;

    public WindowsDeviceInformation
        GetDeviceInformation()
    {
        var agentVersion =
            typeof(WindowsDeviceInfoProvider)
                .Assembly
                .GetName()
                .Version?
                .ToString()
            ??
            "1.0.0";

        var manufacturer =
            QueryCimString(
                "Win32_ComputerSystem",
                "Manufacturer");

        var model =
            QueryCimString(
                "Win32_ComputerSystem",
                "Model");

        var serialNumber =
            QueryCimString(
                "Win32_BIOS",
                "SerialNumber");

        var biosVersion =
            QueryCimString(
                "Win32_BIOS",
                "SMBIOSBIOSVersion");

        var cpuName =
            QueryCimString(
                "Win32_Processor",
                "Name");

        var totalMemoryBytes =
            QueryCimInt64(
                "Win32_ComputerSystem",
                "TotalPhysicalMemory");

        var productName =
            ReadRegistryString(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "ProductName");

        var displayVersion =
            ReadRegistryString(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "DisplayVersion");

        var currentBuild =
            ReadRegistryString(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "CurrentBuildNumber")
            ??
            ReadRegistryString(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "CurrentBuild");

        var machineGuid =
            ReadRegistryString(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Cryptography",
                "MachineGuid");

        var operatingSystem =
            Normalize(
                productName)
            ??
            RuntimeInformation
                .OSDescription;

        var operatingSystemVersion =
            BuildOperatingSystemVersion(
                displayVersion,
                currentBuild);

        return new WindowsDeviceInformation(
            DeviceName:
                Environment.MachineName,

            SerialNumber:
                Normalize(
                    serialNumber)
                ??
                Normalize(
                    machineGuid)
                ??
                Environment.MachineName,

            Manufacturer:
                Normalize(
                    manufacturer),

            Model:
                Normalize(
                    model),

            OperatingSystem:
                operatingSystem,

            OperatingSystemVersion:
                operatingSystemVersion,

            AgentVersion:
                agentVersion,

            UserName:
                Normalize(
                    Environment.UserName),

            DomainName:
                Normalize(
                    Environment.UserDomainName),

            Architecture:
                RuntimeInformation
                    .OSArchitecture
                    .ToString(),

            BiosVersion:
                Normalize(
                    biosVersion),

            CpuName:
                Normalize(
                    cpuName),

            TotalMemoryBytes:
                totalMemoryBytes,

            MachineGuid:
                Normalize(
                    machineGuid),

            CollectedAtUtc:
                DateTime.UtcNow);
    }

    // ============================================================
    // CIM - STRING
    // ============================================================

    private static string?
        QueryCimString(
            string className,
            string propertyName)
    {
        if (
            string.IsNullOrWhiteSpace(
                className)
            ||
            string.IsNullOrWhiteSpace(
                propertyName))
        {
            return null;
        }

        /*
         * Importante:
         *
         * No usamos raw interpolated strings aquí.
         *
         * PowerShell utiliza llaves, $, comillas y expresiones que
         * pueden entrar en conflicto con la interpolación C#.
         *
         * ArgumentList evita tener que escapar manualmente todo
         * el comando.
         */

        var script =
            string.Concat(
                "$ErrorActionPreference='Stop'; ",
                "$item=Get-CimInstance -ClassName '",
                EscapePowerShellSingleQuoted(
                    className),
                "' | Select-Object -First 1; ",
                "if($null -ne $item){ ",
                "$value=$item.'",
                EscapePowerShellSingleQuoted(
                    propertyName),
                "'; ",
                "if($null -ne $value){ ",
                "[Console]::Out.Write([string]$value); ",
                "} ",
                "}");

        return RunPowerShell(
            script);
    }

    // ============================================================
    // CIM - INT64
    // ============================================================

    private static long?
        QueryCimInt64(
            string className,
            string propertyName)
    {
        var raw =
            QueryCimString(
                className,
                propertyName);

        if (
            string.IsNullOrWhiteSpace(
                raw))
        {
            return null;
        }

        if (
            long.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return value;
        }

        return null;
    }

    // ============================================================
    // POWERSHELL EXECUTION
    // ============================================================

    private static string?
        RunPowerShell(
            string script)
    {
        if (
            string.IsNullOrWhiteSpace(
                script))
        {
            return null;
        }

        try
        {
            using var process =
                new Process();

            process.StartInfo =
                new ProcessStartInfo
                {
                    FileName =
                        "powershell.exe",

                    UseShellExecute =
                        false,

                    RedirectStandardOutput =
                        true,

                    RedirectStandardError =
                        true,

                    CreateNoWindow =
                        true
                };

            /*
             * ArgumentList evita problemas con:
             *
             * - comillas
             * - $
             * - {
             * - }
             * - espacios
             * - interpolación
             */

            process.StartInfo
                .ArgumentList
                .Add(
                    "-NoProfile");

            process.StartInfo
                .ArgumentList
                .Add(
                    "-NonInteractive");

            process.StartInfo
                .ArgumentList
                .Add(
                    "-ExecutionPolicy");

            process.StartInfo
                .ArgumentList
                .Add(
                    "Bypass");

            process.StartInfo
                .ArgumentList
                .Add(
                    "-Command");

            process.StartInfo
                .ArgumentList
                .Add(
                    script);

            if (
                !process.Start())
            {
                return null;
            }

            var standardOutputTask =
                process
                    .StandardOutput
                    .ReadToEndAsync();

            var standardErrorTask =
                process
                    .StandardError
                    .ReadToEndAsync();

            if (
                !process.WaitForExit(
                    PowerShellTimeoutMilliseconds))
            {
                try
                {
                    process.Kill(
                        entireProcessTree:
                            true);
                }
                catch
                {
                    // Limpieza best-effort.
                }

                return null;
            }

            var standardOutput =
                standardOutputTask
                    .GetAwaiter()
                    .GetResult();

            _ =
                standardErrorTask
                    .GetAwaiter()
                    .GetResult();

            if (
                process.ExitCode !=
                    0)
            {
                return null;
            }

            return Normalize(
                standardOutput);
        }
        catch
        {
            /*
             * El inventario nunca debe provocar la caída
             * del servicio del agente por una consulta CIM.
             */

            return null;
        }
    }

    // ============================================================
    // POWERSHELL ESCAPING
    // ============================================================

    private static string
        EscapePowerShellSingleQuoted(
            string value)
    {
        /*
         * Dentro de una cadena PowerShell delimitada por '
         * una comilla simple se representa duplicándola.
         */

        return value.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
    }

    // ============================================================
    // WINDOWS REGISTRY
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
                root.OpenSubKey(
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
    // OPERATING SYSTEM VERSION
    // ============================================================

    private static string
        BuildOperatingSystemVersion(
            string? displayVersion,
            string? currentBuild)
    {
        var normalizedDisplayVersion =
            Normalize(
                displayVersion);

        var normalizedBuild =
            Normalize(
                currentBuild);

        if (
            normalizedDisplayVersion is not null
            &&
            normalizedBuild is not null)
        {
            return
                $"{normalizedDisplayVersion} " +
                $"(Build {normalizedBuild})";
        }

        if (
            normalizedBuild is not null)
        {
            return
                $"Build {normalizedBuild}";
        }

        return Environment
            .OSVersion
            .Version
            .ToString();
    }

    // ============================================================
    // NORMALIZATION
    // ============================================================

    private static string?
        Normalize(
            string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return value.Trim();
    }
}

// ================================================================
// CONTRACT
// ================================================================

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