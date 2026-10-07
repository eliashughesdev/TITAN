namespace TitanMDM.Application.Applications;

public interface ISoftwareDeploymentService
{
    Task<SoftwarePackageDto>
        UploadPackageAsync(
            Guid organizationId,
            Guid userId,
            CreateSoftwarePackageRequest request,
            string fileName,
            Stream fileStream,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SoftwarePackageDto>>
        GetPackagesAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SoftwareDeploymentDto>>
        GetDeploymentsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<SoftwareDeploymentDto>
        DeployAsync(
            Guid organizationId,
            Guid userId,
            Guid packageId,
            DeploySoftwarePackageRequest request,
            CancellationToken cancellationToken = default);

    Task<SoftwarePackageDownloadDto?>
        GetPackageDownloadAsync(
            Guid deviceId,
            Guid packageId,
            CancellationToken cancellationToken = default);
}

public sealed record SoftwarePackageDownloadDto(
    string FullPath,
    string FileName,
    string ContentType);