namespace TitanMDM.Domain.Enums;

/// <summary>
/// Estado de captura reportado por el agente
/// sin inventar coordenadas.
/// </summary>
public enum DeviceCaptureState
{
    Unknown = 0,
    Reported = 1,
    PermissionDenied = 2,
    Unavailable = 3,
    DisabledByPolicy = 4
}
