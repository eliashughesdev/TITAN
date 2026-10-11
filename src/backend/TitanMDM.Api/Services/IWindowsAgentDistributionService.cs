namespace TitanMDM.Api.Services;

public interface IWindowsAgentDistributionService
{
    string GetPackagePath();

    string GetPackageFileName();

    void ValidateInstallerPrerequisites(
        string deploymentMode);

    Task<string> GetPackageSha256Async(
        CancellationToken cancellationToken =
            default);

    Task<WindowsDistributionArtifact>
        BuildIndividualInstallerAsync(
            string enrollmentToken,
            CancellationToken cancellationToken =
                default);

    Task<WindowsDistributionArtifact>
        BuildGpoPackageAsync(
            string enrollmentToken,
            CancellationToken cancellationToken =
                default);
}

public sealed record WindowsDistributionArtifact(
    byte[] Content,
    string FileName,
    string ContentType);
