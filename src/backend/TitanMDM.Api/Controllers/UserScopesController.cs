using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;
using TitanMDM.Api.Services;
using TitanMDM.Application.Security;
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

    private readonly IScopeAccessService
        _scopeAccessService;

    private readonly SessionSecurityService
        _sessionSecurity;

    private readonly ILogger<
        UserScopesController>
        _logger;

    public UserScopesController(
        IAuthorizationScopeService scopeService,
        IScopeAccessService scopeAccessService,
        SessionSecurityService sessionSecurity,
        ILogger<UserScopesController> logger)
    {
        _scopeService =
            scopeService;

        _scopeAccessService =
            scopeAccessService;

        _sessionSecurity =
            sessionSecurity;

        _logger =
            logger;
    }

    // ============================================================
    // GET USER SCOPES
    // ============================================================

    [HttpGet]
    [RequirePermission(
        PermissionCodes.Users.View)]
    public async Task<IActionResult>
        GetScopes(
            Guid userId,
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        try
        {
            var grants =
                await _scopeService
                    .GetUserScopesAsync(
                        context.Value.OrganizationId,
                        userId,
                        cancellationToken);

            var snapshot =
                await _scopeAccessService
                    .GetScopeSnapshotAsync(
                        context.Value.OrganizationId,
                        userId,
                        cancellationToken);

            return Ok(
                new
                {
                    userId,

                    organizationWide =
                        snapshot
                            .OrganizationWide,

                    siteIds =
                        snapshot
                            .SiteIds,

                    hasAnyScope =
                        snapshot
                            .HasAnyScope,

                    grants
                });
        }
        catch (
            InvalidOperationException exception)
        {
            return NotFound(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    // ============================================================
    // GRANT SINGLE SCOPE
    // ============================================================

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
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        try
        {
            await _scopeService
                .GrantAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    userId,
                    request.ScopeType,
                    request.ScopeId,
                    cancellationToken);

            await RevokeUserSessionsAsync(
                userId,
                cancellationToken);

            _logger
                .LogInformation(
                    "RBAC scope granted. TargetUser={TargetUserId}, ScopeType={ScopeType}, ScopeId={ScopeId}, Actor={ActorUserId}.",
                    userId,
                    request.ScopeType,
                    request.ScopeId,
                    context.Value.UserId);

            return NoContent();
        }
        catch (
            InvalidOperationException exception)
        {
            return BadRequest(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    // ============================================================
    // REPLACE ALL SCOPES
    // ============================================================

    /*
     * Este endpoint es el recomendado para la UI.
     *
     * Ejemplo:
     *
     * PUT /api/users/{id}/scopes
     *
     * {
     *   "scopes": [
     *      {
     *          "scopeType": 1,
     *          "scopeId": "SITE-1"
     *      },
     *      {
     *          "scopeType": 1,
     *          "scopeId": "SITE-2"
     *      }
     *   ]
     * }
     *
     * Reemplaza de manera coherente el conjunto completo.
     */

    [HttpPut]
    [RequirePermission(
        PermissionCodes.Users.Manage)]
    [RequirePermission(
        PermissionCodes.Roles.Manage)]
    public async Task<IActionResult>
        ReplaceScopes(
            Guid userId,
            [FromBody]
            ReplaceUserScopesRequest request,
            CancellationToken cancellationToken)
    {
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        if (
            request.Scopes is null)
        {
            return BadRequest(
                new
                {
                    message =
                        "Debe especificar la colección de scopes."
                });
        }

        var scopes =
            request
                .Scopes
                .Select(
                    scope =>
                        new AuthorizationScopeAssignment(
                            scope.ScopeType,
                            scope.ScopeId))
                .ToArray();

        try
        {
            await _scopeService
                .ReplaceAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    userId,
                    scopes,
                    cancellationToken);

            await RevokeUserSessionsAsync(
                userId,
                cancellationToken);

            var snapshot =
                await _scopeAccessService
                    .GetScopeSnapshotAsync(
                        context.Value.OrganizationId,
                        userId,
                        cancellationToken);

            _logger
                .LogInformation(
                    "RBAC scopes replaced. TargetUser={TargetUserId}, OrganizationWide={OrganizationWide}, SiteCount={SiteCount}, Actor={ActorUserId}.",
                    userId,
                    snapshot.OrganizationWide,
                    snapshot.SiteIds.Count,
                    context.Value.UserId);

            return Ok(
                new
                {
                    userId,

                    organizationWide =
                        snapshot
                            .OrganizationWide,

                    siteIds =
                        snapshot
                            .SiteIds,

                    hasAnyScope =
                        snapshot
                            .HasAnyScope
                });
        }
        catch (
            InvalidOperationException exception)
        {
            return BadRequest(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    // ============================================================
    // REVOKE SINGLE SCOPE
    // ============================================================

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
        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized();
        }

        await _scopeService
            .RevokeAsync(
                context.Value.OrganizationId,
                userId,
                grantId,
                cancellationToken);

        await RevokeUserSessionsAsync(
            userId,
            cancellationToken);

        _logger
            .LogInformation(
                "RBAC scope revoked. TargetUser={TargetUserId}, GrantId={GrantId}, Actor={ActorUserId}.",
                userId,
                grantId,
                context.Value.UserId);

        return NoContent();
    }

    // ============================================================
    // SESSION INVALIDATION
    // ============================================================

    private Task
        RevokeUserSessionsAsync(
            Guid userId,
            CancellationToken cancellationToken)
    {
        return _sessionSecurity
            .RevokeUserSessionsAsync(
                userId,
                HttpContext
                    .Connection
                    .RemoteIpAddress
                    ?.ToString(),
                cancellationToken);
    }

    // ============================================================
    // SECURITY CONTEXT
    // ============================================================

    private SecurityContext?
        GetSecurityContext()
    {
        var organizationText =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var userText =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub");

        if (
            !Guid.TryParse(
                organizationText,
                out var organizationId)
            ||
            !Guid.TryParse(
                userText,
                out var userId))
        {
            return null;
        }

        return new SecurityContext(
            organizationId,
            userId);
    }

    private readonly record struct
        SecurityContext(
            Guid OrganizationId,
            Guid UserId);
}

// ================================================================
// CONTRACTS
// ================================================================

public sealed record GrantUserScopeRequest(
    AuthorizationScopeType ScopeType,
    Guid ScopeId);

public sealed record ReplaceUserScopesRequest(
    IReadOnlyCollection<
        UserScopeAssignmentRequest>
        Scopes);

public sealed record UserScopeAssignmentRequest(
    AuthorizationScopeType ScopeType,
    Guid ScopeId);