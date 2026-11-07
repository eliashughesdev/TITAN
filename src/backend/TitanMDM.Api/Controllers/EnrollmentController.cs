using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.Enrollment;
using TitanMDM.Application.Enrollment.DeviceRegistration;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/enrollment")]
public sealed class EnrollmentController : ControllerBase
{

    private readonly IDeviceRegistrationService
    _deviceRegistrationService;
    private readonly IEnrollmentService _enrollmentService;

    private readonly IAndroidProvisioningInfoProvider? _androidProvisioning;

    public EnrollmentController(
      IEnrollmentService enrollmentService,
      IDeviceRegistrationService deviceRegistrationService,
      IAndroidProvisioningInfoProvider? androidProvisioning = null)
    {
        _enrollmentService = enrollmentService;
        _deviceRegistrationService = deviceRegistrationService;
        _androidProvisioning = androidProvisioning;
    }

    /// <summary>
    /// APK y checksum de firma del TitanMDM Agent para el QR de
    /// aprovisionamiento de fábrica (Android Enterprise, 6 toques).
    /// </summary>
    [Authorize]
    [HttpGet("android/provisioning")]
    public async Task<IActionResult> GetAndroidProvisioning(
        CancellationToken cancellationToken = default)
    {
        if (_androidProvisioning is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        try
        {
            return Ok(await _androidProvisioning.GetAsync(cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = exception.Message });
        }
    }

    [Authorize]
    [HttpPost("tokens")]
    public async Task<IActionResult> CreateToken(
        [FromBody] CreateEnrollmentTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(new
            {
                message =
                    "El token de autenticación no contiene una organización válida."
            });
        }

        var userId =
            GetUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message =
                    "El token de autenticación no contiene un usuario válido."
            });
        }

        try
        {
            var result =
                await _enrollmentService.CreateTokenAsync(
                    organizationId.Value,
                    userId.Value,
                    request,
                    cancellationToken);

            return Created(
                $"/api/enrollment/tokens/{result.Id}",
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("register")]


    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _deviceRegistrationService.RegisterAsync(
                    request,
                    cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                result);
        }
        catch (DeviceRegistrationException ex)
        {
            var statusCode =
                ex.Code switch
                {
                    "INVALID_REQUEST" =>
                        StatusCodes.Status400BadRequest,

                    "INVALID_PLATFORM" =>
                        StatusCodes.Status400BadRequest,

                    "INVALID_TOKEN" =>
                        StatusCodes.Status401Unauthorized,

                    "TOKEN_EXPIRED" =>
                        StatusCodes.Status401Unauthorized,

                    "TOKEN_REVOKED" =>
                        StatusCodes.Status401Unauthorized,

                    "TOKEN_EXHAUSTED" =>
                        StatusCodes.Status409Conflict,

                    "TOKEN_NOT_ACTIVE" =>
                        StatusCodes.Status409Conflict,

                    "PLATFORM_MISMATCH" =>
                        StatusCodes.Status409Conflict,

                    "SERIAL_ALREADY_REGISTERED" =>
                        StatusCodes.Status409Conflict,

                    "RECOVERY_ORGANIZATION_MISMATCH" =>
                        StatusCodes.Status409Conflict,

                    "RECOVERY_PLATFORM_MISMATCH" =>
                        StatusCodes.Status409Conflict,

                    "RECOVERY_DEVICE_NAME_MISMATCH" =>
                        StatusCodes.Status409Conflict,

                    "RECOVERY_DEVICE_STILL_ACTIVE" =>
                        StatusCodes.Status409Conflict,

                    _ =>
                        StatusCodes.Status400BadRequest



                };

            return StatusCode(
                statusCode,
                new
                {
                    code = ex.Code,
                    message = ex.Message
                });
        }
    }


    [Authorize]
    [HttpGet("tokens")]
    public async Task<IActionResult> GetTokens(
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(new
            {
                message =
                    "El token de autenticación no contiene una organización válida."
            });
        }

        var result =
            await _enrollmentService.GetTokensAsync(
                organizationId.Value,
                cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("tokens/{enrollmentTokenId:guid}/revoke")]
    public async Task<IActionResult> RevokeToken(
        Guid enrollmentTokenId,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized(new
            {
                message =
                    "El token de autenticación no contiene una organización válida."
            });
        }

        try
        {
            var revoked =
                await _enrollmentService.RevokeTokenAsync(
                    organizationId.Value,
                    enrollmentTokenId,
                    cancellationToken);

            if (!revoked)
            {
                return NotFound(new
                {
                    message =
                        "El token de inscripción no existe o no pertenece a la organización."
                });
            }

            return Ok(new
            {
                message =
                    "Token de inscripción revocado correctamente."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("validate")]

    public async Task<IActionResult> ValidateToken(
        [FromBody] ValidateEnrollmentTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var result =
            await _enrollmentService.ValidateTokenAsync(
                request.Token,
                request.Platform,
                cancellationToken);

        if (!result.IsValid)
        {
            return BadRequest(result);
        }

        return Ok(result);
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

        if (Guid.TryParse(
                value,
                out var userId))
        {
            return userId;
        }

        value =
            User.FindFirstValue("sub");

        return Guid.TryParse(
            value,
            out userId)
            ? userId
            : null;
    }
}