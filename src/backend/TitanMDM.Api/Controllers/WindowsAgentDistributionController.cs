using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Api.Services;
using TitanMDM.Application.Enrollment;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/enrollment/windows")]
public sealed class WindowsAgentDistributionController
    : ControllerBase
{
    private readonly
        IWindowsAgentDistributionService
        _distributionService;

    private readonly
        IEnrollmentService
        _enrollmentService;

    public WindowsAgentDistributionController(
        IWindowsAgentDistributionService distributionService,
        IEnrollmentService enrollmentService)
    {
        _distributionService =
            distributionService;

        _enrollmentService =
            enrollmentService;
    }

    [AllowAnonymous]
    [HttpGet("package")]
    public IActionResult DownloadPackage()
    {
        try
        {
            var packagePath =
                _distributionService
                    .GetPackagePath();

            return PhysicalFile(
                packagePath,
                "application/zip",
                _distributionService
                    .GetPackageFileName(),
                enableRangeProcessing:
                    true);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    [Authorize]
    [HttpGet("package-info")]
    public async Task<IActionResult>
        GetPackageInfo(
            CancellationToken cancellationToken =
                default)
    {
        try
        {
            var packagePath =
                _distributionService
                    .GetPackagePath();

            var file =
                new FileInfo(
                    packagePath);

            var sha256 =
                await _distributionService
                    .GetPackageSha256Async(
                        cancellationToken);

            return Ok(
                new
                {
                    fileName =
                        _distributionService
                            .GetPackageFileName(),

                    sizeBytes =
                        file.Length,

                    sha256,

                    lastModifiedUtc =
                        file.LastWriteTimeUtc
                });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    [Authorize]
    [HttpPost("installer")]
    public async Task<IActionResult>
        GenerateInstaller(
            [FromBody]
            CreateWindowsInstallerRequest request,
            CancellationToken cancellationToken =
                default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización válida."
                });
        }

        var userId =
            GetUserId();

        if (userId is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene un usuario válido."
                });
        }

        if (
            string.IsNullOrWhiteSpace(
                request.DeploymentMode))
        {
            return BadRequest(
                new
                {
                    message =
                        "DeploymentMode es obligatorio."
                });
        }

        var deploymentMode =
            request.DeploymentMode
                .Trim()
                .ToLowerInvariant();

        if (
            deploymentMode != "individual"
            &&
            deploymentMode != "gpo")
        {
            return BadRequest(
                new
                {
                    message =
                        "DeploymentMode debe ser individual o gpo."
                });
        }

        var maxUses =
            deploymentMode ==
                "individual"
                ? 1
                : request.MaxUses;

        try
        {
            var enrollmentToken =
                await _enrollmentService
                    .CreateTokenAsync(
                        organizationId.Value,
                        userId.Value,
                        new CreateEnrollmentTokenRequest(
                            "Windows",
                            request.ExpirationMinutes,
                            maxUses),
                        cancellationToken);

            WindowsDistributionArtifact artifact;

            if (
                deploymentMode ==
                "individual")
            {
                artifact =
                    await _distributionService
                        .BuildIndividualInstallerAsync(
                            enrollmentToken.Token,
                            cancellationToken);
            }
            else
            {
                artifact =
                    await _distributionService
                        .BuildGpoPackageAsync(
                            enrollmentToken.Token,
                            cancellationToken);
            }

            return File(
                artifact.Content,
                artifact.ContentType,
                artifact.FileName);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
        catch (DirectoryNotFoundException ex)
        {
            return NotFound(
                new
                {
                    message =
                        ex.Message
                });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_ACCESS_DENIED",

                    message =
                        ex.Message
                });
        }
        catch (IOException ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_IO_ERROR",

                    message =
                        ex.Message
                });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_GENERATION_FAILED",

                    message =
                        ex.Message
                });
        }
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var organizationId)
            ? organizationId
            : null;
    }

    private Guid? GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (
            Guid.TryParse(
                value,
                out var userId))
        {
            return userId;
        }

        value =
            User.FindFirstValue(
                "sub");

        return Guid.TryParse(
            value,
            out userId)
            ? userId
            : null;
    }
}

public sealed record CreateWindowsInstallerRequest(
    string DeploymentMode,
    int ExpirationMinutes,
    int MaxUses);