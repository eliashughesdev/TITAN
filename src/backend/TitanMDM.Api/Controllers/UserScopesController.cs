using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;

using TitanMDM.Application.Security;
using TitanMDM.Api.Services;
using TitanMDM.Domain.Enums;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/{userId:guid}/scopes")]
[ServiceFilter(
    typeof(RbacAuditFilter))]
public sealed class UserScopesController
    : ControllerBase
{
    private readonly IAuthorizationScopeService
        _scopeService;

    private readonly SessionSecurityService
        _sessionSecurity;

    public UserScopesController(
        IAuthorizationScopeService scopeService,
        SessionSecurityService sessionSecurity)
    {
        _scopeService =
            scopeService;

        _sessionSecurity =
            sessionSecurity;
    }

    [HttpGet]
    [RequirePermission(
        PermissionCodes.Users.View)]
    public async Task<IActionResult>
        GetScopes(
            Guid userId,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        return Ok(
            await _scopeService
                .GetUserScopesAsync(
                    organizationId.Value,
                    userId,
                    cancellationToken));
    }

    [HttpPost]
    [RequirePermission(
        PermissionCodes.Users.Manage)]
    [RequirePermission(
        PermissionCodes.Roles.Manage)]
    public async Task<IActionResult>
        GrantScope(
            Guid userId,
            [FromBody]
            GrantUserScopeRequest request,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var actorUserId =
            GetUserId();

        if (
            !organizationId.HasValue
            ||
            !actorUserId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            await _scopeService
                .GrantAsync(
                    organizationId.Value,
                    actorUserId.Value,
                    userId,
                    request.ScopeType,
                    request.ScopeId,
                    cancellationToken);

            /*
             * Permisos efectivos cambiaron.
             */
            await _sessionSecurity
                .RevokeUserSessionsAsync(
                    userId,
                    HttpContext
                        .Connection
                        .RemoteIpAddress
                        ?.ToString(),
                    cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                new
                {
                    message =
                        ex.Message
                });
        }
    }

    [HttpDelete("{grantId:guid}")]
    [RequirePermission(
        PermissionCodes.Users.Manage)]
    [RequirePermission(
        PermissionCodes.Roles.Manage)]
    public async Task<IActionResult>
        RevokeScope(
            Guid userId,
            Guid grantId,
            CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        await _scopeService
            .RevokeAsync(
                organizationId.Value,
                userId,
                grantId,
                cancellationToken);

        await _sessionSecurity
            .RevokeUserSessionsAsync(
                userId,
                HttpContext
                    .Connection
                    .RemoteIpAddress
                    ?.ToString(),
                cancellationToken);

        return NoContent();
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

public sealed record GrantUserScopeRequest(
    AuthorizationScopeType ScopeType,
    Guid ScopeId);