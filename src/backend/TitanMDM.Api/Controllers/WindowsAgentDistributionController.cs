using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Api.Services;
using TitanMDM.Application.Enrollment;
using TitanMDM.Application.Security;

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

    private readonly
        ILogger<WindowsAgentDistributionController>
        _logger;

    public WindowsAgentDistributionController(
        IWindowsAgentDistributionService distributionService,
        IEnrollmentService enrollmentService,
        ILogger<WindowsAgentDistributionController> logger)
    {
        _distributionService =
            distributionService;

        _enrollmentService =
            enrollmentService;

        _logger =
            logger;
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
        if (!HasPermission(
                PermissionCodes.Enrollment.Manage))
        {
            return Forbid();
        }

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
            _distributionService
                .ValidateInstallerPrerequisites(
                    deploymentMode);

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

            try
            {
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
            }
            catch
            {
                await RevokeFailedInstallerTokenAsync(
                    organizationId.Value,
                    enrollmentToken.Id);

                throw;
            }

            Response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";

            _logger.LogInformation(
                "Generated Windows {DeploymentMode} artifact for organization {OrganizationId} by user {UserId}.",
                deploymentMode,
                organizationId.Value,
                userId.Value);

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
            LogGenerationFailure(
                ex,
                organizationId.Value,
                userId.Value,
                deploymentMode);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_PREREQUISITE_MISSING",

                    message =
                        "El instalador Windows no está disponible porque falta un componente de distribución en el servidor."
                });
        }
        catch (DirectoryNotFoundException ex)
        {
            LogGenerationFailure(
                ex,
                organizationId.Value,
                userId.Value,
                deploymentMode);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_PREREQUISITE_MISSING",

                    message =
                        "El instalador Windows no está disponible porque falta un componente de distribución en el servidor."
                });
        }
        catch (UnauthorizedAccessException ex)
        {
            LogGenerationFailure(
                ex,
                organizationId.Value,
                userId.Value,
                deploymentMode);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_ACCESS_DENIED",

                    message =
                        "El servidor no pudo acceder a los archivos necesarios para generar el instalador."
                });
        }
        catch (IOException ex)
        {
            LogGenerationFailure(
                ex,
                organizationId.Value,
                userId.Value,
                deploymentMode);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_IO_ERROR",

                    message =
                        "Ocurrió un error de archivos al generar el instalador Windows."
                });
        }
        catch (TimeoutException ex)
        {
            LogGenerationFailure(
                ex,
                organizationId.Value,
                userId.Value,
                deploymentMode);

            return StatusCode(
                StatusCodes.Status504GatewayTimeout,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_TIMEOUT",

                    message =
                        "La generación del instalador excedió el tiempo permitido."
                });
        }
        catch (InvalidOperationException ex)
        {
            LogGenerationFailure(
                ex,
                organizationId.Value,
                userId.Value,
                deploymentMode);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code =
                        "WINDOWS_INSTALLER_GENERATION_FAILED",

                    message =
                        "TitanMDM no pudo generar el instalador Windows. Revisa el registro del servidor."
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

    private bool HasPermission(
        string permission)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                string.Equals(
                    claim.Value,
                    permission,
                    StringComparison.OrdinalIgnoreCase));
    }

    private async Task RevokeFailedInstallerTokenAsync(
        Guid organizationId,
        Guid enrollmentTokenId)
    {
        try
        {
            await _enrollmentService
                .RevokeTokenAsync(
                    organizationId,
                    enrollmentTokenId,
                    CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not revoke enrollment token {EnrollmentTokenId} after Windows installer generation failed.",
                enrollmentTokenId);
        }
    }

    private void LogGenerationFailure(
        Exception exception,
        Guid organizationId,
        Guid userId,
        string deploymentMode)
    {
        _logger.LogError(
            exception,
            "Windows installer generation failed for organization {OrganizationId}, user {UserId}, mode {DeploymentMode}.",
            organizationId,
            userId,
            deploymentMode);
    }
}

public sealed record CreateWindowsInstallerRequest(
    string DeploymentMode,
    int ExpirationMinutes,
    int MaxUses);
