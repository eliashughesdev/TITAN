
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace TitanMDM.RemoteUiaHost.Security;

public sealed record UiaEnvironmentReport(
    DateTime GeneratedAtUtc,
    string OperatingSystem,
    bool WindowsUacEnabled,
    bool SecureDesktopEnabled,
    int? ConsentPromptBehaviorAdmin,
    int? ConsentPromptBehaviorUser,
    bool UiAccessDesktopToggleAllowed,
    bool SecureUiAccessPathsRequired,
    bool ExecutableInProtectedLocation,
    bool ProcessHasUiAccessToken,
    bool CurrentProcessElevated,
    bool PrivilegedFeaturesEnabled,
    string[] Findings
);

public static class UiaEnvironmentInspector
{
    private const string UacKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

    private const int TokenUiAccessInformationClass = 26;

    public static UiaEnvironmentReport Inspect()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "RemoteUiaHost requiere Microsoft Windows.");
        }

        var enableLua = ReadDword("EnableLUA");
        var secureDesktop = ReadDword("PromptOnSecureDesktop");
        var adminPrompt = ReadDword("ConsentPromptBehaviorAdmin");
        var userPrompt = ReadDword("ConsentPromptBehaviorUser");
        var desktopToggle = ReadDword("EnableUIADesktopToggle");
        var securePaths = ReadDword("EnableSecureUIAPaths");

        var executablePath =
            Environment.ProcessPath ?? string.Empty;

        var protectedLocation =
            IsProtectedInstallPath(executablePath);

        var hasUiAccess = GetUiAccessFromCurrentToken();

        using var identity = WindowsIdentity.GetCurrent();

        var principal = new WindowsPrincipal(identity);

        var elevated = principal.IsInRole(
            WindowsBuiltInRole.Administrator);

        var findings = new List<string>();

        if (enableLua == 1)
        {
            findings.Add("UAC habilitado.");
        }
        else
        {
            findings.Add(
                "Verificar UAC: no se confirmó EnableLUA=1.");
        }

        if (secureDesktop == 1)
        {
            findings.Add(
                "Las solicitudes UAC utilizan Secure Desktop.");
        }

        if (desktopToggle != 1)
        {
            findings.Add(
                "UIAccess Desktop Toggle no está habilitado.");
        }

        if (!protectedLocation)
        {
            findings.Add(
                "El ejecutable no está en una ubicación protegida reconocida.");
        }

        if (!hasUiAccess)
        {
            findings.Add(
                "El proceso actual no posee un token UIAccess.");
        }

        findings.Add(
            "Firma Authenticode, confianza y cadena de certificados pendientes de verificación externa.");

        findings.Add(
            "Las funciones de administración privilegiada permanecen deshabilitadas.");

        return new UiaEnvironmentReport(
            GeneratedAtUtc: DateTime.UtcNow,
            OperatingSystem: Environment.OSVersion.VersionString,
            WindowsUacEnabled: enableLua == 1,
            SecureDesktopEnabled: secureDesktop == 1,
            ConsentPromptBehaviorAdmin: adminPrompt,
            ConsentPromptBehaviorUser: userPrompt,
            UiAccessDesktopToggleAllowed: desktopToggle == 1,
            SecureUiAccessPathsRequired: securePaths != 0,
            ExecutableInProtectedLocation: protectedLocation,
            ProcessHasUiAccessToken: hasUiAccess,
            CurrentProcessElevated: elevated,
            PrivilegedFeaturesEnabled: false,
            Findings: findings.ToArray());
    }

    private static int? ReadDword(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            UacKey,
            writable: false);

        if (key is null)
        {
            return null;
        }

        var raw = key.GetValue(name);

        if (raw is int number)
        {
            return number;
        }

        return null;
    }

    private static bool IsProtectedInstallPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var programFiles =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);

        var programFilesX86 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);

        var windows =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        foreach (var root in new[]
                 {
                     programFiles,
                     programFilesX86,
                     windows
                 })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            var fullFile = Path.GetFullPath(filePath);

            if (fullFile.StartsWith(
                    fullRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool GetUiAccessFromCurrentToken()
    {
        using var identity = WindowsIdentity.GetCurrent();

        var token = identity.Token;

        if (!GetTokenInformation(
                token,
                TokenUiAccessInformationClass,
                out uint value,
                sizeof(uint),
                out var returnedLength))
        {
            return false;
        }

        return returnedLength >= sizeof(uint) && value != 0;
    }

    [DllImport(
        "advapi32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        out uint tokenInformation,
        int tokenInformationLength,
        out int returnLength);
}
