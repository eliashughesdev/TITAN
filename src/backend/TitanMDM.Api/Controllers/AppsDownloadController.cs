using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Applications;
using TitanMDM.Application.Devices.Agent;
using TitanMDM.Infrastructure.Applications;

namespace TitanMDM.Api.Controllers;

/// <summary>
/// APK firmado de una app administrada, por nombre de paquete.
/// Lo usa el agente cuando el equipo no tiene contenedor GMS.
/// </summary>
[ApiController]
[Route("api/apps")]
public sealed class AppsDownloadController : ControllerBase
{
    private readonly IDeviceAuthenticator _deviceAuthenticator;
    private readonly ISoftwareDeploymentService _softwareDeploymentService;

    public AppsDownloadController(
        IDeviceAuthenticator deviceAuthenticator,
        ISoftwareDeploymentService softwareDeploymentService)
    {
        _deviceAuthenticator = deviceAuthenticator;
        _softwareDeploymentService = softwareDeploymentService;
    }

    [HttpGet("download/{packageName}")]
    public async Task<IActionResult> Download(
        string packageName,
        CancellationToken cancellationToken)
    {
        if (!ManagedAppDownload.IsValidPackageName(packageName))
            return NotFound();

        if (!Request.Headers.TryGetValue("X-Titan-Device-Id", out var deviceIdValue)
            || !Guid.TryParse(deviceIdValue, out var deviceId))
        {
            return Unauthorized();
        }

        if (!Request.Headers.TryGetValue("X-Titan-Device-Secret", out var deviceSecret)
            || string.IsNullOrWhiteSpace(deviceSecret))
        {
            return Unauthorized();
        }

        await _deviceAuthenticator.AuthenticateAsync(
            deviceId,
            deviceSecret!,
            cancellationToken);

        var package = await _softwareDeploymentService.GetApkDownloadByPackageNameAsync(
            deviceId,
            packageName,
            cancellationToken);

        if (package is null)
            return NotFound();

        return PhysicalFile(
            package.FullPath,
            package.ContentType,
            package.FileName,
            enableRangeProcessing: true);
    }
}
