namespace TitanMDM.Application.Audit;

public interface IAuditQueryService
{
    Task<AuditListResultDto> GetEventsAsync(
        Guid organizationId,
        AuditQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AuditSummaryDto> GetSummaryAsync(
        Guid organizationId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<AuditFilterOptionsDto> GetFilterOptionsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportCsvAsync(
        Guid organizationId,
        AuditQueryDto query,
        CancellationToken cancellationToken = default);
}