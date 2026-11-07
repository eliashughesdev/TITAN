namespace TitanMDM.Application.Devices;

public sealed record UpdateDeviceAliasRequest(string? Alias);

/// <summary>Alias operativo del equipo (p. ej. HH-ALMACEN-01).</summary>
public interface IDeviceAliasService
{
    /// <returns>El alias guardado (null si se borró), o null si el equipo no existe.</returns>
    Task<DeviceAliasResult?> SetAliasAsync(
        Guid organizationId,
        Guid deviceId,
        string? alias,
        CancellationToken cancellationToken = default);
}

public sealed record DeviceAliasResult(Guid DeviceId, string? Alias);
