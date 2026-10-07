using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;

using TitanMDM.Application.Policies;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/policies")]
[Authorize]
public sealed class PoliciesController
    : ControllerBase
{
    private readonly IPolicyService
        _policyService;

    public PoliciesController(
        IPolicyService policyService)
    {
        _policyService =
            policyService;
    }

    [HttpGet]
    [RequirePermission(
        PermissionCodes.Policies.View)]
    public async Task<IActionResult>
        GetAll(
            [FromQuery] string? platform,
            [FromQuery] string? status,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _policyService
                    .GetAllAsync(
                        organizationId.Value,
                        platform,
                        status,
                        cancellationToken));
        }
        catch (PolicyException ex)
        {
            return MapException(ex);
        }
    }

    [HttpGet("{policyId:guid}")]
    [RequirePermission(
        PermissionCodes.Policies.View)]
    public async Task<IActionResult>
        GetById(
            Guid policyId,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized();
        }

        var result =
            await _policyService
                .GetByIdAsync(
                    organizationId.Value,
                    policyId,
                    cancellationToken);

        return result is null
            ? NotFound(
                new
                {
                    code =
                        "POLICY_NOT_FOUND",

                    message =
                        "La política no existe."
                })
            : Ok(result);
    }

    [HttpPost]
    [RequirePermission(
        PermissionCodes.Policies.Manage)]
    public async Task<IActionResult>
        Create(
            [FromBody]
            CreatePolicyRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (
            organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result =
                await _policyService
                    .CreateAsync(
                        organizationId.Value,
                        userId.Value,
                        request,
                        cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    policyId =
                        result.Id
                },
                result);
        }
        catch (PolicyException ex)
        {
            return MapException(ex);
        }
    }

    [HttpPut("{policyId:guid}")]
    [RequirePermission(
        PermissionCodes.Policies.Manage)]
    public async Task<IActionResult>
        Update(
            Guid policyId,
            [FromBody]
            UpdatePolicyRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (
            organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _policyService
                    .UpdateAsync(
                        organizationId.Value,
                        userId.Value,
                        policyId,
                        request,
                        cancellationToken));
        }
        catch (PolicyException ex)
        {
            return MapException(ex);
        }
    }

    [HttpPost("{policyId:guid}/activate")]
    [RequirePermission(
        PermissionCodes.Policies.Manage)]
    public Task<IActionResult>
        Activate(
            Guid policyId,
            CancellationToken cancellationToken)
    {
        return ChangeState(
            policyId,
            "activate",
            cancellationToken);
    }

    [HttpPost("{policyId:guid}/disable")]
    [RequirePermission(
        PermissionCodes.Policies.Manage)]
    public Task<IActionResult>
        Disable(
            Guid policyId,
            CancellationToken cancellationToken)
    {
        return ChangeState(
            policyId,
            "disable",
            cancellationToken);
    }

    [HttpPost("{policyId:guid}/archive")]
    [RequirePermission(
        PermissionCodes.Policies.Manage)]
    public Task<IActionResult>
        Archive(
            Guid policyId,
            CancellationToken cancellationToken)
    {
        return ChangeState(
            policyId,
            "archive",
            cancellationToken);
    }

    [HttpPost("{policyId:guid}/assign")]
    [RequirePermission(
        PermissionCodes.Policies.Manage)]
    public async Task<IActionResult>
        Assign(
            Guid policyId,
            [FromBody]
            AssignPolicyRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (
            organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(
                await _policyService
                    .AssignAsync(
                        organizationId.Value,
                        userId.Value,
                        policyId,
                        request,
                        cancellationToken));
        }
        catch (PolicyException ex)
        {
            return MapException(ex);
        }
    }

    [HttpGet("{policyId:guid}/assignments")]
    [RequirePermission(
        PermissionCodes.Policies.View)]
    public async Task<IActionResult>
        GetAssignments(
            Guid policyId,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized();
        }

        return Ok(
            await _policyService
                .GetAssignmentsAsync(
                    organizationId.Value,
                    policyId,
                    cancellationToken));
    }

    private async Task<IActionResult>
        ChangeState(
            Guid policyId,
            string action,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
        {
            return Unauthorized();
        }

        try
        {
            switch (action)
            {
                case "activate":

                    await _policyService
                        .ActivateAsync(
                            organizationId.Value,
                            policyId,
                            cancellationToken);

                    break;

                case "disable":

                    await _policyService
                        .DisableAsync(
                            organizationId.Value,
                            policyId,
                            cancellationToken);

                    break;

                case "archive":

                    await _policyService
                        .ArchiveAsync(
                            organizationId.Value,
                            policyId,
                            cancellationToken);

                    break;

                default:

                    return BadRequest(
                        new
                        {
                            code =
                                "INVALID_POLICY_ACTION",

                            message =
                                "La acción de política no es válida."
                        });
            }

            return NoContent();
        }
        catch (PolicyException ex)
        {
            return MapException(ex);
        }
    }

    private IActionResult MapException(
        PolicyException exception)
    {
        var statusCode =
            exception.Code switch
            {
                "POLICY_NOT_FOUND" =>
                    StatusCodes
                        .Status404NotFound,

                "POLICY_NAME_EXISTS" =>
                    StatusCodes
                        .Status409Conflict,

                "POLICY_ARCHIVED" =>
                    StatusCodes
                        .Status409Conflict,

                "POLICY_NOT_ACTIVE" =>
                    StatusCodes
                        .Status409Conflict,

                "DEVICE_NOT_FOUND" =>
                    StatusCodes
                        .Status404NotFound,

                "PLATFORM_MISMATCH" =>
                    StatusCodes
                        .Status409Conflict,

                _ =>
                    StatusCodes
                        .Status400BadRequest
            };

        return StatusCode(
            statusCode,
            new
            {
                code =
                    exception.Code,

                message =
                    exception.Message
            });
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