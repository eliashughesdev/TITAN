using System.Runtime.InteropServices;
using System.Text.Json;

using Microsoft.Win32;

namespace TitanMDM.WindowsAgent.Services;

/// <summary>
/// Ctrl+Alt+Supr real para soporte remoto.
///
/// Windows descarta la combinacion inyectada con SendInput: la Secure
/// Attention Sequence solo puede generarla sas.dll!SendSAS desde un
/// servicio LocalSystem y con la politica SoftwareSASGeneration
/// habilitada para servicios. El agente corre como SYSTEM, por eso esta
/// accion vive aqui y no en el RemoteHost (que usa el token del usuario).
/// </summary>
public sealed class WindowsSecureAttentionService
{
    private const string PolicyKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string PolicyValue = "SoftwareSASGeneration";

    // 1 = servicios, 2 = Facilidad de acceso, 3 = ambos.
    private const int ServicesFlag = 1;

    private readonly ILogger<WindowsSecureAttentionService> _logger;

    public WindowsSecureAttentionService(ILogger<WindowsSecureAttentionService> logger)
    {
        _logger = logger;
    }

    public string Send()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Ctrl+Alt+Supr solo está disponible en Windows.");
        }

        var policyUpdated = EnsureServicePolicy();

        // false = invocado desde un servicio (no desde Facilidad de acceso).
        SendSAS(false);

        _logger.LogInformation("Secure Attention Sequence enviada. PolicyUpdated={PolicyUpdated}.", policyUpdated);

        return JsonSerializer.Serialize(new
        {
            sent = true,
            policyUpdated,
            sentAtUtc = DateTime.UtcNow,
        });
    }

    /// <returns>true si fue necesario habilitar la politica.</returns>
    private static bool EnsureServicePolicy()
    {
        using var key = Registry.LocalMachine.CreateSubKey(PolicyKey, writable: true)
            ?? throw new InvalidOperationException("No se pudo abrir la política del sistema.");

        var current = key.GetValue(PolicyValue) is int value ? value : 0;

        if ((current & ServicesFlag) != 0)
        {
            return false;
        }

        key.SetValue(PolicyValue, current | ServicesFlag, RegistryValueKind.DWord);
        return true;
    }

    [DllImport("sas.dll", SetLastError = true)]
    private static extern void SendSAS([MarshalAs(UnmanagedType.Bool)] bool asUser);
}
