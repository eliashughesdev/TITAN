namespace TitanMDM.Application.Devices;

public sealed record DeviceGroupMembershipDto(
    Guid Id,
    string Name,
    bool IsDynamic,
    string Source);

public sealed record DeviceSecuritySummaryDto(
    int ComplianceScore,
    string RiskLevel,
    string ComplianceStatus,
    bool AgentInstalled,
    string AgentVersionName,
    DateTime? LastSecurityScanAtUtc,
    DateTime? LastComplianceCheckAtUtc);

public sealed record DeviceCommandSnapshotDto(
    Guid Id,
    string CommandType,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record DeviceOperationalSnapshotDto(
    DeviceDetailsDto Device,
    IReadOnlyCollection<DeviceGroupMembershipDto> Groups,
    DeviceSecuritySummaryDto? Security,
    IReadOnlyDictionary<string, DeviceCommandSnapshotDto>
        LatestResults,
    DateTime? LastInventoryAtUtc);