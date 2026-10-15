namespace TitanMDM.Application.Android.Policies;

/*
 * Fila del despliegue de una política Android para un
 * dispositivo administrado. Se expone aunque no exista una
 * asignación previa (AssignmentId null, AssignmentStatus
 * "NotAssigned") para representar el estado real de cada
 * dispositivo del inventario sin depender de cargar toda la
 * flota en el cliente.
 */
public sealed record AndroidPolicyAssignmentDeviceDto(
    Guid DeviceId,
    string DeviceName,
    string SerialNumber,
    string? Manufacturer,
    string? Model,
    string DeviceStatus,
    string ComplianceStatus,
    bool IsManaged,
    Guid? AssignmentId,
    int? PolicyVersion,
    string AssignmentStatus,
    DateTime? AssignedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? AppliedAtUtc,
    string? AppliedPolicyName,
    long? AppliedPolicyVersion,
    string? AppliedPolicyState,
    DateTime? LastPolicySyncTimeUtc,
    string? ErrorMessage);

public sealed record AndroidPolicyAssignmentPageDto(
    IReadOnlyCollection<AndroidPolicyAssignmentDeviceDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
