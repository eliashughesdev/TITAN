using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Applications;
using TitanMDM.Application.Devices.Agent;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/device/software-packages")]
public sealed class DeviceSoftwarePackagesController
    : ControllerBase
{
    private readonly IDeviceAuthenticator
        _deviceAuthenticator;

    private readonly ISoftwareDeploymentService
        _softwareDeploymentService;

    public DeviceSoftwarePackagesController(
        IDeviceAuthenticator deviceAuthenticator,
        ISoftwareDeploymentService softwareDeploymentService)
    {
        _deviceAuthenticator =
            deviceAuthenticator;

        _softwareDeploymentService =
            softwareDeploymentService;
    }

    [HttpGet("{packageId:guid}/download")]
    public async Task<IActionResult>
        Download(
            Guid packageId,
            CancellationToken cancellationToken)
    {
        if (
            !Request.Headers
                .TryGetValue(
                    "X-Titan-Device-Id",
                    out var deviceIdValue)
            ||
            !Guid.TryParse(
                deviceIdValue,
                out var deviceId))
        {
            return Unauthorized();
        }

        if (
            !Request.Headers
                .TryGetValue(
                    "X-Titan-Device-Secret",
                    out var deviceSecret)
            ||
            string.IsNullOrWhiteSpace(
                deviceSecret))
        {
            return Unauthorized();
        }

        await _deviceAuthenticator
            .AuthenticateAsync(
                deviceId,
                deviceSecret!,
                cancellationToken);

        var package =
            await _softwareDeploymentService
                .GetPackageDownloadAsync(
                    deviceId,
                    packageId,
                    cancellationToken);

        if (package is null)
        {
            return NotFound();
        }

        return PhysicalFile(
            package.FullPath,
            package.ContentType,
            package.FileName,
            enableRangeProcessing:
                true);
    }
}