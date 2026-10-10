
using System.Collections.ObjectModel;

namespace TitanMDM.RemoteUiaHost.Security;

public enum RemoteElevationBlocker
{
    FeatureDisabled,
    UacDisabled,
    SecureDesktopPolicyRestricted,
    UiAccessTokenMissing,
    UnprotectedInstallation,
    SigningNotVerified,
    AuthorizationNotIntegrated,
    AuditNotIntegrated,
    SecureTransportNotIntegrated
}

public sealed record RemoteElevationReadiness(
    bool CanStartInteractiveElevation,
    string Status,
    IReadOnlyList<RemoteElevationBlocker> Blockers,
    DateTime EvaluatedAtUtc);

/// <summary>
/// Evalúa los requisitos mínimos para una futura capacidad
/// de elevación remota.
///
/// No solicita permisos, no ejecuta aplicaciones, no recibe
/// contraseñas y no modifica políticas de Windows.
///
/// El resultado debe tratarse como una comprobación adicional,
/// nunca como sustituto de la autorización del servidor.
/// </summary>
public static class RemoteElevationReadinessEvaluator
{
    public static RemoteElevationReadiness Evaluate(
        UiaEnvironmentReport environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var blockers = new List<RemoteElevationBlocker>();

        // Esta versión es exclusivamente de preparación.
        // La habilitación debe implementarse posteriormente
        // mediante una autorización del servidor y un
        // mecanismo de despliegue aprobado.
        blockers.Add(RemoteElevationBlocker.FeatureDisabled);

        if (!environment.WindowsUacEnabled)
        {
            blockers.Add(RemoteElevationBlocker.UacDisabled);
        }

        if (environment.SecureDesktopEnabled &&
            !environment.UiAccessDesktopToggleAllowed)
        {
            blockers.Add(
                RemoteElevationBlocker.SecureDesktopPolicyRestricted);
        }

        if (!environment.ProcessHasUiAccessToken)
        {
            blockers.Add(
                RemoteElevationBlocker.UiAccessTokenMissing);
        }

        if (!environment.ExecutableInProtectedLocation)
        {
            blockers.Add(
                RemoteElevationBlocker.UnprotectedInstallation);
        }

        // No podemos inferir la validez de la firma por
        // estar instalado en Program Files.
        blockers.Add(RemoteElevationBlocker.SigningNotVerified);

        // Estas integraciones aún no existen en el nuevo
        // RemoteUiaHost; fallamos cerrado hasta implementarlas.
        blockers.Add(
            RemoteElevationBlocker.AuthorizationNotIntegrated);

        blockers.Add(
            RemoteElevationBlocker.AuditNotIntegrated);

        blockers.Add(
            RemoteElevationBlocker.SecureTransportNotIntegrated);

        return new RemoteElevationReadiness(
            CanStartInteractiveElevation: false,
            Status: "BLOCKED_NOT_CONFIGURED",
            Blockers: new ReadOnlyCollection<RemoteElevationBlocker>(
                blockers),
            EvaluatedAtUtc: DateTime.UtcNow);
    }

    public static void RequireAuthorizedReadiness(
        RemoteElevationReadiness readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);

        if (!readiness.CanStartInteractiveElevation)
        {
            throw new UnauthorizedAccessException(
                "La elevación interactiva remota no está habilitada.");
        }

        // Incluso si una versión futura devuelve true,
        // debe existir autorización de servidor, lease,
        // identidad del técnico y auditoría de la operación.
        throw new NotSupportedException(
            "El motor interactivo de elevación aún no está implementado.");
    }
}
