namespace TitanMDM.Application.Sites;

public interface ISiteOperationsService
{
    Task<SiteOperationalSummaryDto>
        GetSummaryAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            CancellationToken cancellationToken = default);

    Task<SitePolicyOperationResultDto>
        AssignPolicyAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            Guid policyId,
            CancellationToken cancellationToken = default);

    Task<SiteSoftwareOperationResultDto>
        DeploySoftwareAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            Guid packageId,
            CancellationToken cancellationToken = default);

    Task<byte[]>
        ExportDevicesCsvAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            CancellationToken cancellationToken = default);
}