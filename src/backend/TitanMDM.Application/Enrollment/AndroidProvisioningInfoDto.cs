namespace TitanMDM.Application.Enrollment;

/// <summary>
/// Datos fijos del QR de aprovisionamiento Android Enterprise (pantalla de
/// bienvenida, 6 toques). Wi-Fi, servidor y token los agrega la consola.
/// </summary>
public sealed record AndroidProvisioningInfoDto(
    string ComponentName,
    string PackageName,
    string DownloadUrl,
    string SignatureChecksum,
    string? ApkSha256,
    long? VersionCode,
    string? VersionName,
    string Source);

public interface IAndroidProvisioningInfoProvider
{
    Task<AndroidProvisioningInfoDto> GetAsync(CancellationToken cancellationToken = default);
}
