using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Win32;

namespace TitanMDM.WindowsAgent.Execution;

[SupportedOSPlatform("windows")]
public sealed class WindowsKioskExecutor
{
    private readonly ILogger<WindowsKioskExecutor>
        _logger;

    private static readonly JsonSerializerOptions
        JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

    private static readonly string
        StateDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "TitanMDM",
                "Kiosk");

    private static readonly string
        StateFile =
            Path.Combine(
                StateDirectory,
                "state.json");

    public WindowsKioskExecutor(
        ILogger<WindowsKioskExecutor> logger)
    {
        _logger =
            logger;

        Directory.CreateDirectory(
            StateDirectory);
    }

    public async Task<string> ApplyAsync(
        JsonElement configuration,
        CancellationToken cancellationToken = default)
    {
        var request =
            JsonSerializer.Deserialize<
                WindowsKioskConfiguration>(
                    configuration.GetRawText(),
                    JsonOptions)
            ??
            throw new InvalidOperationException(
                "La configuración Kiosk Windows no es válida.");

        Validate(
            request);

        var profileId =
            request.ProfileId
            ??
            Guid.NewGuid();

        var assignedAccessXml =
            BuildAssignedAccessXml(
                request,
                profileId);

        var previousState =
            CaptureCurrentState();

        try
        {
            await ApplyAssignedAccessAsync(
                assignedAccessXml,
                cancellationToken);

            ApplyRestrictions(
                request.Restrictions);

            var persistedState =
                new WindowsKioskState
                {
                    ProfileId =
                        profileId,

                    Account =
                        request.Account,

                    Mode =
                        request.KioskMode,

                    AppliedAtUtc =
                        DateTime.UtcNow,

                    PreviousRestrictions =
                        previousState
                };

            await File.WriteAllTextAsync(
                StateFile,
                JsonSerializer.Serialize(
                    persistedState,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }),
                cancellationToken);

            return JsonSerializer.Serialize(
                new
                {
                    success = true,

                    action =
                        "WINDOWS_KIOSK_APPLY",

                    profileId,

                    account =
                        request.Account,

                    kioskMode =
                        request.KioskMode,

                    applications =
                        request.Applications.Count,

                    appliedAtUtc =
                        DateTime.UtcNow
                });
        }
        catch
        {
            RestoreRestrictions(
                previousState);

            throw;
        }
    }

    public async Task<string> RemoveAsync(
        CancellationToken cancellationToken = default)
    {
        WindowsKioskState? state =
            null;

        if (File.Exists(
                StateFile))
        {
            try
            {
                state =
                    JsonSerializer.Deserialize<
                        WindowsKioskState>(
                            await File.ReadAllTextAsync(
                                StateFile,
                                cancellationToken),
                            JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No fue posible leer el estado previo de Kiosk.");
            }
        }

        await RemoveAssignedAccessAsync(
            cancellationToken);

        if (
            state?.PreviousRestrictions
            is not null)
        {
            RestoreRestrictions(
                state.PreviousRestrictions);
        }

        TryDeleteState();

        return JsonSerializer.Serialize(
            new
            {
                success = true,

                action =
                    "WINDOWS_KIOSK_REMOVE",

                removedAtUtc =
                    DateTime.UtcNow
            });
    }

    public async Task<string> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var assignedAccess =
            await QueryAssignedAccessAsync(
                cancellationToken);

        WindowsKioskState? localState =
            null;

        if (File.Exists(
                StateFile))
        {
            try
            {
                localState =
                    JsonSerializer.Deserialize<
                        WindowsKioskState>(
                            await File.ReadAllTextAsync(
                                StateFile,
                                cancellationToken),
                            JsonOptions);
            }
            catch
            {
                // Status no debe fallar por un archivo local dañado.
            }
        }

        return JsonSerializer.Serialize(
            new
            {
                action =
                    "WINDOWS_KIOSK_STATUS",

                configured =
                    !string.IsNullOrWhiteSpace(
                        assignedAccess),

                profileId =
                    localState?.ProfileId,

                account =
                    localState?.Account,

                mode =
                    localState?.Mode,

                appliedAtUtc =
                    localState?.AppliedAtUtc,

                queriedAtUtc =
                    DateTime.UtcNow
            });
    }

    private static void Validate(
        WindowsKioskConfiguration request)
    {
        if (
            !request.TitanProfileType.Equals(
                "kiosk",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La política no es un perfil TitanMDM Kiosk.");
        }

        if (
            string.IsNullOrWhiteSpace(
                request.Account))
        {
            throw new InvalidOperationException(
                "El perfil Kiosk necesita una cuenta de Windows.");
        }

        if (
            request.Applications is null
            ||
            request.Applications.Count == 0)
        {
            throw new InvalidOperationException(
                "Debe existir al menos una aplicación.");
        }

        if (
            request.KioskMode.Equals(
                "singleApp",
                StringComparison.OrdinalIgnoreCase)
            &&
            request.Applications.Count != 1)
        {
            throw new InvalidOperationException(
                "Single App requiere exactamente una aplicación.");
        }

        if (
            request.KioskMode.Equals(
                "singleApp",
                StringComparison.OrdinalIgnoreCase)
            &&
            string.IsNullOrWhiteSpace(
                request.Applications[0]
                    .AppUserModelId))
        {
            throw new InvalidOperationException(
                "Single App requiere AppUserModelId. Para aplicaciones Win32 utiliza Multi App.");
        }

        foreach (
            var application
            in request.Applications)
        {
            if (
                string.IsNullOrWhiteSpace(
                    application.AppUserModelId)
                &&
                string.IsNullOrWhiteSpace(
                    application.DesktopAppPath))
            {
                throw new InvalidOperationException(
                    $"La aplicación '{application.DisplayName}' no tiene AppUserModelId ni DesktopAppPath.");
            }
        }
    }

    private static string BuildAssignedAccessXml(
        WindowsKioskConfiguration request,
        Guid profileId)
    {
        XNamespace ns =
            "http://schemas.microsoft.com/AssignedAccess/2017/config";

        XNamespace rs5 =
            "http://schemas.microsoft.com/AssignedAccess/201810/config";

        var profile =
            new XElement(
                ns + "Profile",
                new XAttribute(
                    "Id",
                    $"{{{profileId:D}}}"));

        if (
            request.KioskMode.Equals(
                "singleApp",
                StringComparison.OrdinalIgnoreCase))
        {
            profile.Add(
                new XElement(
                    ns + "KioskModeApp",
                    new XAttribute(
                        "AppUserModelId",
                        request.Applications[0]
                            .AppUserModelId!)));
        }
        else
        {
            var allowedApps =
                new XElement(
                    ns + "AllowedApps");

            foreach (
                var application
                in request.Applications)
            {
                if (
                    !string.IsNullOrWhiteSpace(
                        application.AppUserModelId))
                {
                    allowedApps.Add(
                        new XElement(
                            ns + "App",
                            new XAttribute(
                                "AppUserModelId",
                                application
                                    .AppUserModelId)));
                }
                else
                {
                    allowedApps.Add(
                        new XElement(
                            ns + "App",
                            new XAttribute(
                                "DesktopAppPath",
                                application
                                    .DesktopAppPath!)));
                }
            }

            profile.Add(
                new XElement(
                    ns + "AllAppsList",
                    allowedApps));

            profile.Add(
                new XElement(
                    ns + "Taskbar",
                    new XAttribute(
                        "ShowTaskbar",
                        request.Restrictions
                            .ShowTaskbar
                            .ToString()
                            .ToLowerInvariant())));
        }

        var root =
            new XElement(
                ns + "AssignedAccessConfiguration",

                new XAttribute(
                    XNamespace.Xmlns +
                    "rs5",
                    rs5),

                new XElement(
                    ns + "Profiles",
                    profile),

                new XElement(
                    ns + "Configs",

                    new XElement(
                        ns + "Config",

                        new XElement(
                            ns + "Account",
                            request.Account),

                        new XElement(
                            ns + "DefaultProfile",

                            new XAttribute(
                                "Id",
                                $"{{{profileId:D}}}")))));

        var document =
            new XDocument(
                new XDeclaration(
                    "1.0",
                    "utf-8",
                    null),
                root);

        return document
            .ToString(
                SaveOptions.DisableFormatting);
    }

    private async Task ApplyAssignedAccessAsync(
        string xml,
        CancellationToken cancellationToken)
    {
        var encodedXml =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    xml));

        var script =
            $$"""
            $ErrorActionPreference = 'Stop'

            $xml =
                [Text.Encoding]::UTF8.GetString(
                    [Convert]::FromBase64String(
                        '{{encodedXml}}'
                    )
                )

            $namespace =
                'root\cimv2\mdm\dmmap'

            $instance =
                Get-CimInstance `
                    -Namespace $namespace `
                    -ClassName MDM_AssignedAccess `
                    -ErrorAction Stop

            if ($null -eq $instance) {
                throw 'MDM_AssignedAccess no está disponible.'
            }

            $instance.Configuration =
                [System.Net.WebUtility]::HtmlEncode(
                    $xml
                )

            Set-CimInstance `
                -CimInstance $instance `
                -ErrorAction Stop |
                Out-Null
            """;

        await RunPowerShellAsync(
            script,
            cancellationToken);
    }

    private async Task RemoveAssignedAccessAsync(
        CancellationToken cancellationToken)
    {
        const string script =
            """
            $ErrorActionPreference = 'Stop'

            try {
                Clear-AssignedAccess `
                    -ErrorAction SilentlyContinue
            }
            catch {
            }

            $namespace =
                'root\cimv2\mdm\dmmap'

            $instance =
                Get-CimInstance `
                    -Namespace $namespace `
                    -ClassName MDM_AssignedAccess `
                    -ErrorAction SilentlyContinue

            if ($null -ne $instance) {
                $instance.Configuration = $null

                Set-CimInstance `
                    -CimInstance $instance `
                    -ErrorAction Stop |
                    Out-Null
            }
            """;

        await RunPowerShellAsync(
            script,
            cancellationToken);
    }

    private async Task<string?> QueryAssignedAccessAsync(
        CancellationToken cancellationToken)
    {
        const string script =
            """
            $ErrorActionPreference = 'Stop'

            $instance =
                Get-CimInstance `
                    -Namespace 'root\cimv2\mdm\dmmap' `
                    -ClassName MDM_AssignedAccess `
                    -ErrorAction SilentlyContinue

            if ($null -eq $instance) {
                exit 0
            }

            if (
                [string]::IsNullOrWhiteSpace(
                    $instance.Configuration
                )
            ) {
                exit 0
            }

            Write-Output $instance.Configuration
            """;

        var result =
            await RunPowerShellAsync(
                script,
                cancellationToken);

        return string.IsNullOrWhiteSpace(
            result)
            ? null
            : result;
    }

    private static RestrictionState
        CaptureCurrentState()
    {
        return new RestrictionState
        {
            DisableTaskManager =
                ReadDword(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    "DisableTaskMgr"),

            DisableControlPanel =
                ReadDword(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoControlPanel"),

            DisableCommandPrompt =
                ReadDword(
                    @"SOFTWARE\Policies\Microsoft\Windows\System",
                    "DisableCMD"),

            DenyRemovableStorage =
                ReadDword(
                    @"SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices",
                    "Deny_All")
        };
    }

    private static void ApplyRestrictions(
        WindowsKioskRestrictions restrictions)
    {
        WriteDword(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
            "DisableTaskMgr",
            restrictions.BlockTaskManager
                ? 1
                : 0);

        WriteDword(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
            "NoControlPanel",
            restrictions.BlockSettings
                ? 1
                : 0);

        WriteDword(
            @"SOFTWARE\Policies\Microsoft\Windows\System",
            "DisableCMD",
            restrictions.BlockCommandPrompt
                ? 2
                : 0);

        WriteDword(
            @"SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices",
            "Deny_All",
            restrictions.BlockRemovableStorage
                ? 1
                : 0);
    }

    private static void RestoreRestrictions(
        RestrictionState state)
    {
        RestoreDword(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
            "DisableTaskMgr",
            state.DisableTaskManager);

        RestoreDword(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
            "NoControlPanel",
            state.DisableControlPanel);

        RestoreDword(
            @"SOFTWARE\Policies\Microsoft\Windows\System",
            "DisableCMD",
            state.DisableCommandPrompt);

        RestoreDword(
            @"SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices",
            "Deny_All",
            state.DenyRemovableStorage);
    }

    private static int? ReadDword(
        string path,
        string name)
    {
        using var key =
            Registry.LocalMachine
                .OpenSubKey(
                    path);

        if (key is null)
        {
            return null;
        }

        var value =
            key.GetValue(
                name);

        return value is int result
            ? result
            : null;
    }

    private static void WriteDword(
        string path,
        string name,
        int value)
    {
        using var key =
            Registry.LocalMachine
                .CreateSubKey(
                    path,
                    writable: true);

        key.SetValue(
            name,
            value,
            RegistryValueKind.DWord);
    }

    private static void RestoreDword(
        string path,
        string name,
        int? value)
    {
        using var key =
            Registry.LocalMachine
                .CreateSubKey(
                    path,
                    writable: true);

        if (value.HasValue)
        {
            key.SetValue(
                name,
                value.Value,
                RegistryValueKind.DWord);
        }
        else
        {
            key.DeleteValue(
                name,
                throwOnMissingValue:
                    false);
        }
    }

    private async Task<string> RunPowerShellAsync(
        string script,
        CancellationToken cancellationToken)
    {
        var encoded =
            Convert.ToBase64String(
                Encoding.Unicode
                    .GetBytes(
                        script));

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    "powershell.exe",

                Arguments =
                    "-NoLogo -NoProfile -NonInteractive " +
                    $"-ExecutionPolicy Bypass -EncodedCommand {encoded}",

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

        process.Start();

        var outputTask =
            process.StandardOutput
                .ReadToEndAsync(
                    cancellationToken);

        var errorTask =
            process.StandardError
                .ReadToEndAsync(
                    cancellationToken);

        await process.WaitForExitAsync(
            cancellationToken);

        var output =
            (
                await outputTask
            ).Trim();

        var error =
            (
                await errorTask
            ).Trim();

        if (
            process.ExitCode != 0)
        {
            _logger.LogError(
                "Windows Kiosk PowerShell error. ExitCode={ExitCode}; Error={Error}",
                process.ExitCode,
                error);

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(
                    error)
                    ? "Windows no pudo aplicar Assigned Access."
                    : error);
        }

        return output;
    }

    private static void TryDeleteState()
    {
        try
        {
            if (File.Exists(
                    StateFile))
            {
                File.Delete(
                    StateFile);
            }
        }
        catch
        {
        }
    }

    private sealed class WindowsKioskConfiguration
    {
        public string TitanProfileType
        {
            get;
            set;
        } = string.Empty;

        public string KioskMode
        {
            get;
            set;
        } = string.Empty;

        public Guid? ProfileId
        {
            get;
            set;
        }

        public string Account
        {
            get;
            set;
        } = string.Empty;

        public List<WindowsKioskApplication>
            Applications
        {
            get;
            set;
        } = [];

        public WindowsKioskRestrictions
            Restrictions
        {
            get;
            set;
        } = new();
    }

    private sealed class WindowsKioskApplication
    {
        public string DisplayName
        {
            get;
            set;
        } = string.Empty;

        public string? AppUserModelId
        {
            get;
            set;
        }

        public string? DesktopAppPath
        {
            get;
            set;
        }
    }

    private sealed class WindowsKioskRestrictions
    {
        public bool ShowTaskbar
        {
            get;
            set;
        }

        public bool BlockTaskManager
        {
            get;
            set;
        } = true;

        public bool BlockSettings
        {
            get;
            set;
        } = true;

        public bool BlockCommandPrompt
        {
            get;
            set;
        } = true;

        public bool BlockRemovableStorage
        {
            get;
            set;
        } = true;
    }

    private sealed class WindowsKioskState
    {
        public Guid ProfileId
        {
            get;
            set;
        }

        public string Account
        {
            get;
            set;
        } = string.Empty;

        public string Mode
        {
            get;
            set;
        } = string.Empty;

        public DateTime AppliedAtUtc
        {
            get;
            set;
        }

        public RestrictionState?
            PreviousRestrictions
        {
            get;
            set;
        }
    }

    private sealed class RestrictionState
    {
        public int? DisableTaskManager
        {
            get;
            set;
        }

        public int? DisableControlPanel
        {
            get;
            set;
        }

        public int? DisableCommandPrompt
        {
            get;
            set;
        }

        public int? DenyRemovableStorage
        {
            get;
            set;
        }
    }
}