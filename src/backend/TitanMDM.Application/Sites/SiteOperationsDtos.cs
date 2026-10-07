using TitanMDM.Application.Applications;
using TitanMDM.Application.Policies;

namespace TitanMDM.Application.Sites;

public sealed record SiteDeviceSummaryDto(
    int Total,
    int Online,
    int Offline,
    int Managed,
    int Windows,
    int Android,
    int Compliant,
    int NonCompliant,
    int Quarantined);

public sealed record SiteHelpdeskSummaryDto(
    int Total,
    int New,
    int Open,
    int InProgress,
    int PendingUser,
    int Resolved,
    int Closed,
    int Unassigned,
    int SlaBreached);

public sealed record SiteSecuritySummaryDto(
    int EvaluatedDevices,
    decimal AverageComplianceScore,
    int CriticalRisk,
    int HighRisk);

public sealed record SiteOperationalSummaryDto(
    Guid SiteId,
    string SiteCode,
    string SiteName,
    bool IsActive,
    DateTime GeneratedAtUtc,
    SiteDeviceSummaryDto Devices,
    SiteHelpdeskSummaryDto Helpdesk,
    SiteSecuritySummaryDto Security);

public sealed record AssignPolicyToSiteRequest(
    Guid PolicyId);

public sealed record DeploySoftwareToSiteRequest(
    Guid PackageId);

public sealed record SitePolicyOperationResultDto(
    Guid SiteId,
    Guid PolicyId,
    int TargetDevices,
    IReadOnlyCollection<PolicyAssignmentDto> Assignments);

public sealed record SiteSoftwareOperationResultDto(
    Guid SiteId,
    Guid PackageId,
    int TargetDevices,
    IReadOnlyCollection<SoftwareDeploymentDto> Deployments);