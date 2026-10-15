using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Api.Security;
using TitanMDM.Application.Android.Policies;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(RbacAuditFilter))]
[Route("api/policies/{policyId:guid}/android")]
public sealed class AndroidPoliciesController : ControllerBase
{
    private readonly IAndroidPolicyPublisher _publisher;
    private readonly IAndroidPolicyAssignmentService _assignmentService;
    private readonly IScopeAccessService _scopeAccessService;
    private readonly ILogger<AndroidPoliciesController> _logger;

    public AndroidPoliciesController(
        IAndroidPolicyPublisher publisher,
        IAndroidPolicyAssignmentService assignmentService,
        IScopeAccessService scopeAccessService,
        ILogger<AndroidPoliciesController> logger)
    {
        _publisher =
            publisher ??
            throw new ArgumentNullException(nameof(publisher));

        _assignmentService =
            assignmentService ??
            throw new ArgumentNullException(nameof(assignmentService));

        _scopeAccessService =
            scopeAccessService ??
            throw new ArgumentNullException(nameof(scopeAccessService));

        _logger =
            logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    // ============================================================
    // PUBLICATION
    // ============================================================

    [HttpPost("publish")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> Publish(
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return OrganizationNotFound();

        try
        {
            var result =
                await _publisher.PublishAsync(
                    organizationId.Value,
                    policyId,
                    cancellationToken);

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Android policy publication rejected. OrganizationId={OrganizationId}, PolicyId={PolicyId}",
                organizationId,
                policyId);

            return BadRequest(new
            {
                code = "ANDROID_POLICY_PUBLISH_REJECTED",
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Android policy publication failed. OrganizationId={OrganizationId}, PolicyId={PolicyId}",
                organizationId,
                policyId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "ANDROID_POLICY_PUBLISH_FAILED",
                    message =
                        "Ocurrió un error inesperado al publicar la política en Android Enterprise."
                });
        }
    }

    [HttpGet("publication")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetPublication(
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return OrganizationNotFound();

        try
        {
            var result =
                await _publisher.GetCurrentPublicationAsync(
                    organizationId.Value,
                    policyId,
                    cancellationToken);

            if (result is null)
            {
                return NotFound(new
                {
                    code = "ANDROID_POLICY_PUBLICATION_NOT_FOUND",
                    message =
                        "La versión actual de esta política todavía no posee una publicación Android."
                });
            }

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Android publication query failed. OrganizationId={OrganizationId}, PolicyId={PolicyId}",
                organizationId,
                policyId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "ANDROID_POLICY_PUBLICATION_QUERY_FAILED",
                    message =
                        "No fue posible consultar la publicación Android."
                });
        }
    }

    [HttpGet("verify")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> Verify(
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return OrganizationNotFound();

        try
        {
            var result =
                await _publisher.VerifyRemoteAsync(
                    organizationId.Value,
                    policyId,
                    cancellationToken);

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                code = "ANDROID_POLICY_VERIFICATION_REJECTED",
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Android policy verification failed. OrganizationId={OrganizationId}, PolicyId={PolicyId}",
                organizationId,
                policyId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "ANDROID_POLICY_VERIFICATION_FAILED",
                    message =
                        "No fue posible verificar la política contra Google Android Management API."
                });
        }
    }

    // ============================================================
    // ASSIGNMENT
    // ============================================================

    [HttpPost("assign")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> Assign(
        Guid policyId,
        [FromBody] AssignAndroidPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return OrganizationNotFound();

        if (request.DeviceId == Guid.Empty)
        {
            return BadRequest(new
            {
                code = "ANDROID_DEVICE_REQUIRED",
                message =
                    "Debe especificar un dispositivo Android."
            });
        }

        try
        {
            var accessibleSiteIds =
                await ResolveSiteScopeAsync(
                    organizationId.Value,
                    cancellationToken);

            var result =
                await _assignmentService.AssignAsync(
                    organizationId.Value,
                    policyId,
                    request.DeviceId,
                    accessibleSiteIds,
                    cancellationToken);

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Android policy assignment rejected. OrganizationId={OrganizationId}, PolicyId={PolicyId}, DeviceId={DeviceId}",
                organizationId,
                policyId,
                request.DeviceId);

            return BadRequest(new
            {
                code = "ANDROID_POLICY_ASSIGNMENT_REJECTED",
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Android policy assignment failed. OrganizationId={OrganizationId}, PolicyId={PolicyId}, DeviceId={DeviceId}",
                organizationId,
                policyId,
                request.DeviceId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "ANDROID_POLICY_ASSIGNMENT_FAILED",
                    message =
                        "No fue posible asignar la política al dispositivo Android."
                });
        }
    }

    [HttpGet("assignments")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetAssignments(
        Guid policyId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return OrganizationNotFound();

        try
        {
            var accessibleSiteIds =
                await ResolveSiteScopeAsync(
                    organizationId.Value,
                    cancellationToken);

            var result =
                await _assignmentService.GetPageAsync(
                    organizationId.Value,
                    policyId,
                    search,
                    accessibleSiteIds,
                    page,
                    pageSize,
                    cancellationToken);

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Android assignment page query failed. OrganizationId={OrganizationId}, PolicyId={PolicyId}",
                organizationId,
                policyId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "ANDROID_POLICY_ASSIGNMENT_PAGE_FAILED",
                    message =
                        "No fue posible consultar los despliegues Android."
                });
        }
    }

    [HttpGet("assignments/{deviceId:guid}")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetAssignment(
        Guid policyId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return OrganizationNotFound();

        try
        {
            var accessibleSiteIds =
                await ResolveSiteScopeAsync(
                    organizationId.Value,
                    cancellationToken);

            var result =
                await _assignmentService.GetAsync(
                    organizationId.Value,
                    policyId,
                    deviceId,
                    accessibleSiteIds,
                    cancellationToken);

            if (result is null)
            {
                return NotFound(new
                {
                    code = "ANDROID_POLICY_ASSIGNMENT_NOT_FOUND",
                    message =
                        "No existe una asignación de esta política para el dispositivo indicado."
                });
            }

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Android assignment query failed. OrganizationId={OrganizationId}, PolicyId={PolicyId}, DeviceId={DeviceId}",
                organizationId,
                policyId,
                deviceId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    code = "ANDROID_POLICY_ASSIGNMENT_QUERY_FAILED",
                    message =
                        "No fue posible consultar la asignación Android."
                });
        }
    }

    // ============================================================
    // AUTHENTICATED ORGANIZATION
    // ============================================================

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue("organization_id");

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
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(
            value,
            out var userId)
            ? userId
            : null;
    }

    private async Task<IReadOnlyCollection<Guid>?>
        ResolveSiteScopeAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
    {
        var userId =
            GetUserId();

        /*
         * Sin identidad de usuario no es posible evaluar el
         * ámbito de sitios; se mantiene el alcance
         * organizacional para no bloquear flujos ya autorizados.
         */
        if (userId is null)
        {
            return null;
        }

        var organizationWide =
            await _scopeAccessService
                .HasOrganizationScopeAsync(
                    organizationId,
                    userId.Value,
                    cancellationToken);

        if (organizationWide)
        {
            return null;
        }

        return await _scopeAccessService
            .GetAccessibleSiteIdsAsync(
                organizationId,
                userId.Value,
                cancellationToken);
    }

    private UnauthorizedObjectResult OrganizationNotFound()
    {
        return Unauthorized(new
        {
            code = "ORGANIZATION_NOT_FOUND",
            message =
                "No fue posible determinar la organización del usuario autenticado."
        });
    }
}