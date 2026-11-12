using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TitanMDM.Application.Devices;
using TitanMDM.Infrastructure.Android;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Devices;

/// <summary>
/// Borrado definitivo de un dispositivo desde la consola.
///
/// 1. Si el equipo está en Android Management (Google), se pide a Google
///    que lo elimine. Un fallo de Google (red, 404, cuota, credenciales)
///    no impide el borrado local: se registra y se informa en el
///    resultado para que TI lo revise en la consola de Google.
/// 2. Las filas locales se borran en una sola transacción, en el orden
///    que exigen las claves foráneas. Si algo falla, no queda nada a
///    medias y la consola recibe un error claro.
///
/// No se envía ningún borrado de fábrica al agente: sus credenciales se
/// eliminan en el mismo paso, así que un comando no llegaría nunca. Para
/// reutilizar el equipo se restablece de fábrica y se vuelve a inscribir.
/// </summary>
public sealed class DeviceRemovalService : IDeviceRemovalService
{
    /*
     * Orden obligatorio (SQL Server, claves foráneas del modelo):
     *  - Referencias opcionales a NULL (historial que se conserva).
     *  - Hijas con Restrict/NoAction antes que sus padres:
     *    DevicePolicyAssignments -> DeviceCommands -> Devices,
     *    hijas de RemoteSessions -> RemoteSessions -> Devices.
     *  - El resto (Cascade) se borra explícitamente para no depender de
     *    la configuración de la base.
     */
    private static readonly string[] PurgeStatements =
    [
        "UPDATE HelpdeskTickets SET DeviceId = NULL WHERE DeviceId = {0}",
        "UPDATE HelpdeskTickets SET RemoteSessionId = NULL WHERE RemoteSessionId IN (SELECT Id FROM RemoteSessions WHERE DeviceId = {0})",
        "UPDATE AutomationExecutions SET DeviceId = NULL WHERE DeviceId = {0}",
        "UPDATE AutomationQueueItems SET DeviceId = NULL WHERE DeviceId = {0}",
        "UPDATE AutomationNotifications SET DeviceId = NULL WHERE DeviceId = {0}",
        "DELETE FROM GeofenceEvents WHERE DeviceId = {0}",
        "DELETE FROM GeofenceDeviceStates WHERE DeviceId = {0}",
        "DELETE FROM GeofenceDeviceAssignments WHERE DeviceId = {0}",
        "DELETE FROM DeviceLocations WHERE DeviceId = {0}",
        "DELETE FROM DeviceLocationStates WHERE DeviceId = {0}",
        "DELETE FROM DeviceGroupMembers WHERE DeviceId = {0}",
        "DELETE FROM DeviceApplications WHERE DeviceId = {0}",
        "DELETE FROM DeviceSecurityPostures WHERE DeviceId = {0}",
        "DELETE FROM LostModeSessions WHERE DeviceId = {0}",
        "DELETE FROM SoftwareDeployments WHERE TargetType = N'Device' AND TargetId = {0}",
        "DELETE FROM RemoteSessionParticipants WHERE RemoteSessionId IN (SELECT Id FROM RemoteSessions WHERE DeviceId = {0})",
        "DELETE FROM RemoteSessionEvents WHERE RemoteSessionId IN (SELECT Id FROM RemoteSessions WHERE DeviceId = {0})",
        "DELETE FROM RemoteSessionControlLeases WHERE RemoteSessionId IN (SELECT Id FROM RemoteSessions WHERE DeviceId = {0})",
        "DELETE FROM RemoteSessions WHERE DeviceId = {0}",
        "DELETE FROM DevicePolicyAssignments WHERE DeviceId = {0}",
        "DELETE FROM AndroidDevices WHERE DeviceId = {0}",
        "DELETE FROM DeviceCommands WHERE DeviceId = {0}",
        "DELETE FROM DeviceCredentials WHERE DeviceId = {0}",
    ];

    private readonly TitanMdmDbContext _dbContext;
    private readonly AndroidManagementClient _android;
    private readonly ILogger<DeviceRemovalService> _logger;

    public DeviceRemovalService(
        TitanMdmDbContext dbContext,
        AndroidManagementClient android,
        ILogger<DeviceRemovalService> logger)
    {
        _dbContext = dbContext;
        _android = android;
        _logger = logger;
    }

    public async Task<DeviceRemovalResult?> RemovePermanentlyAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Devices
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == deviceId &&
                     x.OrganizationId == organizationId &&
                     !x.IsDeleted,
                cancellationToken);

        if (!exists)
            return null;

        var googleName = await _dbContext.AndroidDevices
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.DeviceId == deviceId)
            .Select(x => x.GoogleDeviceName)
            .FirstOrDefaultAsync(cancellationToken);

        var google = await DeleteFromGoogleAsync(googleName, cancellationToken);

        await PurgeLocalRowsAsync(organizationId, deviceId, cancellationToken);

        _logger.LogInformation(
            "Dispositivo {DeviceId} eliminado por {ActorUserId}. Google: {GoogleStatus}.",
            deviceId,
            actorUserId,
            google);

        return new DeviceRemovalResult(
            deviceId,
            GoogleManaged: google != GoogleRemoval.NotManaged,
            GoogleDeleted: google == GoogleRemoval.Deleted,
            GoogleError: google == GoogleRemoval.Failed
                ? "Google no confirmó la eliminación. Revise el equipo en la consola de Android Management."
                : null);
    }

    private enum GoogleRemoval
    {
        NotManaged,
        Deleted,
        Failed
    }

    private async Task<GoogleRemoval> DeleteFromGoogleAsync(
        string? googleName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(googleName) ||
            !googleName.Contains("/devices/", StringComparison.Ordinal))
        {
            return GoogleRemoval.NotManaged;
        }

        try
        {
            // devices.delete de Android Management ya restablece el equipo:
            // no hace falta un WIPE previo.
            await _android.DeleteDeviceAsync(
                googleName,
                cancellationToken: cancellationToken);
            return GoogleRemoval.Deleted;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Google no aceptó el borrado de {GoogleDeviceName}. Se continúa con el borrado local.",
                googleName);
            return GoogleRemoval.Failed;
        }
    }

    private async Task PurgeLocalRowsAsync(
        Guid organizationId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        // EnableRetryOnFailure: la transacción manual va dentro de la estrategia.
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(cancellationToken);

                foreach (var statement in PurgeStatements)
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        statement,
                        [deviceId],
                        cancellationToken);
                }

                var deleted = await _dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Devices WHERE Id = {0} AND OrganizationId = {1}",
                    [deviceId, organizationId],
                    cancellationToken);

                if (deleted == 0)
                {
                    throw new InvalidOperationException(
                        "El dispositivo ya no existe o pertenece a otra organización.");
                }

                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "No se pudo borrar el dispositivo {DeviceId}; la transacción se revirtió.",
                deviceId);

            throw new InvalidOperationException(
                "No se pudo borrar el dispositivo: aún tiene datos relacionados que la base de datos no permite eliminar. No se borró nada.",
                exception);
        }
    }
}
