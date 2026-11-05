namespace TitanMDM.Application.Security;

/*
 * P1 Windows MDM - Motor de Remediacion Activa.
 *
 * Cuando el servidor detecta un fallo de seguridad o
 * cumplimiento en un equipo Windows, dispara
 * automaticamente comandos correctivos seguros (no
 * destructivos) y una re-verificacion COMPLIANCE_CHECK.
 *
 * Los fallos que requieren intervencion humana
 * (firmware, cifrado, reinicio) se devuelven como
 * acciones manuales; nunca se reinicia ni se cifra un
 * equipo sin aprobacion.
 */
public interface IWindowsRemediationEngine
{
    Task<WindowsRemediationResult> RemediateAsync(
        Guid organizationId,
        Guid deviceId,
        IReadOnlyCollection<string> failedCheckCodes,
        CancellationToken cancellationToken = default);
}

public sealed record WindowsRemediationResult(
    bool Enabled,
    IReadOnlyCollection<WindowsRemediationAction> Queued,
    IReadOnlyCollection<WindowsRemediationAction> Suppressed,
    IReadOnlyCollection<WindowsRemediationAction> Manual,
    string? SkippedReason)
{
    public static WindowsRemediationResult Disabled(string reason) =>
        new(
            false,
            Array.Empty<WindowsRemediationAction>(),
            Array.Empty<WindowsRemediationAction>(),
            Array.Empty<WindowsRemediationAction>(),
            reason);
}

public sealed record WindowsRemediationAction(
    string CheckCode,
    string CommandType,
    string Description,
    Guid? CommandId = null);
