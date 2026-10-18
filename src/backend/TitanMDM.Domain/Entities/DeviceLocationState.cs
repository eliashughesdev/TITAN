using System;
using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

/// <summary>
/// Ultimo estado de captura de ubicacion por dispositivo.
/// Permite distinguir "sin ubicacion" de "permiso denegado"
/// sin inventar coordenadas.
/// </summary>
public sealed class DeviceLocationState
{
    private DeviceLocationState()
    {
    }

    public DeviceLocationState(
        Guid organizationId,
        Guid deviceId,
        DeviceCaptureState captureState,
        DateTime updatedAtUtc,
        string? source = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        DeviceId = deviceId;
        CaptureState = captureState;
        Source = source?.Trim();
        UpdatedAtUtc = updatedAtUtc;
        CreatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid DeviceId { get; private set; }

    public DeviceCaptureState CaptureState { get; private set; }

    public string? Source { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public void Update(
        DeviceCaptureState captureState,
        DateTime updatedAtUtc,
        string? source = null)
    {
        CaptureState = captureState;
        UpdatedAtUtc = updatedAtUtc;

        if (!string.IsNullOrWhiteSpace(source))
            Source = source.Trim();
    }
}
