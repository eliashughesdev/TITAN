namespace TitanMDM.Application.Audit;

public interface IAdministrativeAuditWriter
{
    Task WriteAsync(
        AdministrativeAuditEntry entry,
        CancellationToken cancellationToken = default);
}

public sealed record AdministrativeAuditEntry(
    Guid OrganizationId,
    Guid? ActorUserId,
    string Action,
    string TargetType,
    string? TargetId,
    string Result,
    string CorrelationId,
    string? IpAddress);