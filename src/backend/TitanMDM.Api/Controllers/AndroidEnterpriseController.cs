using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Infrastructure.Android;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/android-enterprise")]
public sealed class AndroidEnterpriseController
    : ControllerBase
{
    private readonly IAndroidEnterpriseService _service;
    private readonly AndroidManagementOptions _options;

    public AndroidEnterpriseController(
        IAndroidEnterpriseService service,
        IOptions<AndroidManagementOptions> options)
    {
        _service = service;
        _options = options.Value;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var result =
            await _service.GetStatusAsync(
                organizationId.Value,
                cancellationToken);

        return Ok(result);
    }

    [HttpPost("signup")]
    public async Task<IActionResult> CreateSignup(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result =
                await _service.CreateSignupUrlAsync(
                    organizationId.Value,
                    userId.Value,
                    cancellationToken);

            return Ok(result);
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? state,
        [FromQuery] string? enterpriseToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(
                enterpriseToken))
        {
            return Redirect(
                BuildFrontendRedirect(
                    _options.FrontendErrorUrl,
                    "invalid_callback"));
        }

        try
        {
            await _service.CompleteSignupAsync(
                state,
                enterpriseToken,
                cancellationToken);

            return Redirect(
                _options.FrontendSuccessUrl);
        }
        catch
        {
            return Redirect(
                BuildFrontendRedirect(
                    _options.FrontendErrorUrl,
                    "signup_failed"));
        }
    }

    [HttpGet("enrollments")]
    public async Task<IActionResult> GetEnrollments(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        return Ok(
            await _service.GetEnrollmentsAsync(
                organizationId.Value,
                cancellationToken));
    }

    [HttpPost("enrollments")]
    public async Task<IActionResult> CreateEnrollment(
        [FromBody]
        CreateAndroidEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result =
                await _service.CreateEnrollmentAsync(
                    organizationId.Value,
                    userId.Value,
                    request,
                    cancellationToken);

            return Created(
                $"/api/android-enterprise/enrollments/{result.Id}",
                result);
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [HttpDelete("enrollments/{id:guid}")]
    public async Task<IActionResult> RevokeEnrollment(
        Guid id,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        try
        {
            var revoked =
                await _service.RevokeEnrollmentAsync(
                    organizationId.Value,
                    id,
                    cancellationToken);

            return revoked
                ? NoContent()
                : NotFound();
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                message = exception.Message
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
            out var id)
            ? id
            : null;
    }

    private Guid? GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(
            value,
            out var id)
            ? id
            : null;
    }

    private static string BuildFrontendRedirect(
        string url,
        string error)
    {
        var separator =
            url.Contains(
                '?',
                StringComparison.Ordinal)
                ? "&"
                : "?";

        return
            $"{url}{separator}error=" +
            Uri.EscapeDataString(error);
    }
}