using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.Devices.Agent;
using TitanMDM.Infrastructure.Android.Policies;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/device/launcher-assets")]
public sealed class DeviceLauncherAssetsController : ControllerBase
{
    private readonly IDeviceAuthenticator _authenticator;
    private readonly LauncherAssetStore _assets;

    public DeviceLauncherAssetsController(
        IDeviceAuthenticator authenticator,
        LauncherAssetStore assets)
    {
        _authenticator = authenticator;
        _assets = assets;
    }

    [HttpGet("{fileName}")]
    public async Task<IActionResult> Download(
        string fileName,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Titan-Device-Id", out var deviceIdValue) ||
            !Guid.TryParse(deviceIdValue, out var deviceId) ||
            !Request.Headers.TryGetValue("X-Titan-Device-Secret", out var deviceSecret) ||
            string.IsNullOrWhiteSpace(deviceSecret))
        {
            return Unauthorized();
        }

        var separator = fileName.LastIndexOf('.');
        if (separator <= 0 ||
            !Guid.TryParse(fileName[..separator], out var assetId))
        {
            return NotFound();
        }

        await _authenticator.AuthenticateAsync(deviceId, deviceSecret!, cancellationToken);
        var content = await _assets.ReadForDeviceAsync(deviceId, assetId, cancellationToken);
        if (content is null ||
            !fileName.EndsWith("." + content.Value.Info.Format, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        return File(
            content.Value.Bytes,
            content.Value.Info.Format switch
            {
                "png" => "image/png",
                "webp" => "image/webp",
                _ => "image/jpeg"
            });
    }
}
