namespace TitanMDM.Application.Devices;

public interface IDeviceRemovalService
{
    /// <summary>
    /// Borra el dispositivo y sus datos. null = no existe en la organización.
    /// Lanza InvalidOperationException si la base de datos rechaza el
    /// borrado (en ese caso no se borró nada).
    /// </summary>
    Task<DeviceRemovalResult?> RemovePermanentlyAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}

/// <param name="GoogleManaged">El equipo estaba registrado en Android Management.</param>
/// <param name="GoogleDeleted">Google confirmó la eliminación.</param>
/// <param name="GoogleError">Aviso cuando Google falló; el borrado local se hizo igual.</param>
public sealed record DeviceRemovalResult(
    Guid DeviceId,
    bool GoogleManaged,
    bool GoogleDeleted,
    string? GoogleError);
