namespace TitanMDM.Application.Location;

/// <summary>
/// Notifica a los visores autenticados cuando llega
/// una nueva posicion o estado de un dispositivo.
/// Implementado con el hub SignalR existente.
/// </summary>
public interface ILocationUpdateNotifier
{
    Task NotifyLocationUpdatedAsync(
        Guid organizationId,
        Guid deviceId,
        string locationState,
        DateTime? capturedAtUtc,
        CancellationToken cancellationToken = default);
}
