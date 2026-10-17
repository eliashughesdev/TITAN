using TitanMDM.Application.Security;

namespace TitanMDM.Application.Automation;

/*
 * AUT-1.8 — Autorización empresarial del motor de
 * automatizaciones.
 *
 * A diferencia de los filtros HTTP (RequirePermission), este
 * servicio resuelve permisos y alcance consultando la base de
 * datos para un actor concreto. Es indispensable para el
 * trabajo en segundo plano (worker de cola y tareas
 * programadas), donde no existe HttpContext.
 *
 * Todas las operaciones fallan cerrado: ante duda o actor
 * desconocido (Guid.Empty) se deniega.
 */
public interface IAutomationAuthorizationService
{
    /// <summary>
    /// Indica si el usuario posee (directa o por equivalencia)
    /// el permiso indicado dentro de la organización.
    /// </summary>
    Task<bool> HasPermissionAsync(
        Guid organizationId,
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lanza <see cref="AutomationAuthorizationException"/> si
    /// el usuario no posee el permiso indicado.
    /// </summary>
    Task EnsurePermissionAsync(
        Guid organizationId,
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el alcance (organización o sitios) del usuario.
    /// </summary>
    Task<AuthorizationScopeSnapshot> GetScopeAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Garantiza que el alcance del usuario cubra todos los
    /// sitios indicados. Un alcance de organización cubre
    /// cualquier sitio.
    /// </summary>
    Task EnsureScopeCoversSitesAsync(
        Guid organizationId,
        Guid userId,
        IReadOnlyCollection<Guid> siteIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el usuario puede operar sobre el dispositivo.
    /// </summary>
    Task<bool> CanAccessDeviceAsync(
        Guid organizationId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el usuario puede operar sobre el ticket.
    /// </summary>
    Task<bool> CanAccessTicketAsync(
        Guid organizationId,
        Guid userId,
        Guid ticketId,
        CancellationToken cancellationToken = default);
}
