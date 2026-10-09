namespace TitanMDM.Application.Reports;

public interface IReportsService
{
    Task<ReportsOverviewDto>
        GetOverviewAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default);

    Task<byte[]>
        ExportDevicesCsvAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default);

    Task<byte[]>
        ExportDevicesExcelAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default);

    Task<byte[]>
        ExportDevicesPdfAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default);
}