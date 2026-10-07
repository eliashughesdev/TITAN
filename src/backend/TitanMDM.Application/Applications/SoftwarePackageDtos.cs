namespace TitanMDM.Application.Applications;

public sealed record SoftwarePackageDto(
    Guid Id,
    string Name,
    string Version,
    string PackageType,
    string OriginalFileName,
    string Sha256,
    long SizeBytes,
    string? InstallArguments,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record CreateSoftwarePackageRequest(
    string Name,
    string Version,
    string PackageType,
    string? InstallArguments);

public sealed record DeploySoftwarePackageRequest(
    string TargetType,
    Guid TargetId);

public sealed record SoftwareDeploymentDto(
    Guid Id,
    Guid PackageId,
    string PackageName,
    string PackageVersion,
    string TargetType,
    Guid TargetId,
    string TargetName,
    string Status,
    int QueuedDevices,
    DateTime CreatedAtUtc);