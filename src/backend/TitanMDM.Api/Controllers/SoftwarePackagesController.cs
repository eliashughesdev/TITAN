using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Applications;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/software-packages")]
[Authorize]
public sealed class SoftwarePackagesController
    : ControllerBase
{
    private readonly ISoftwareDeploymentService
        _service;

    public SoftwarePackagesController(
        ISoftwareDeploymentService service)
    {
        _service =
            service;
    }

    [HttpGet]
    public async Task<IActionResult>
        GetPackages(
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
            return Unauthorized();

        return Ok(
            await _service
                .GetPackagesAsync(
                    organizationId.Value,
                    cancellationToken));
    }

    [HttpGet("deployments")]
    public async Task<IActionResult>
        GetDeployments(
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
            return Unauthorized();

        return Ok(
            await _service
                .GetDeploymentsAsync(
                    organizationId.Value,
                    cancellationToken));
    }

    [HttpPost]
    [RequestSizeLimit(
        2L * 1024L * 1024L * 1024L)]
    public async Task<IActionResult>
        Upload(
            [FromForm] string name,
            [FromForm] string version,
            [FromForm] string packageType,
            [FromForm] string? installArguments,
            [FromForm] IFormFile file,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (
            !organizationId.HasValue
            ||
            !userId.HasValue)
        {
            return Unauthorized();
        }

        if (
            file is null
            ||
            file.Length == 0)
        {
            return BadRequest(
                new
                {
                    message =
                        "Debe seleccionar un paquete."
                });
        }

        await using var stream =
            file.OpenReadStream();

        var result =
            await _service
                .UploadPackageAsync(
                    organizationId.Value,
                    userId.Value,
                    new CreateSoftwarePackageRequest(
                        name,
                        version,
                        packageType,
                        installArguments),
                    file.FileName,
                    stream,
                    cancellationToken);

        return Ok(result);
    }

    [HttpPost("{packageId:guid}/deploy")]
    public async Task<IActionResult>
        Deploy(
            Guid packageId,
            [FromBody]
            DeploySoftwarePackageRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (
            !organizationId.HasValue
            ||
            !userId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(
            await _service
                .DeployAsync(
                    organizationId.Value,
                    userId.Value,
                    packageId,
                    request,
                    cancellationToken));
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var id)
            ? id
            : null;
    }

    private Guid? GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub");

        return Guid.TryParse(
            value,
            out var id)
            ? id
            : null;
    }
}