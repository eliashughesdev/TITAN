using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Dashboard;
using TitanMDM.Application.Dashboard.DTOs;
using TitanMDM.Application.Dashboard.Interfaces;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController
    : ControllerBase
{
    private readonly IDashboardService
        _dashboardService;

    private readonly IScopeAccessService
        _scopeAccessService;

    public DashboardController(
        IDashboardService dashboardService,
        IScopeAccessService scopeAccessService)
    {
        _dashboardService =
            dashboardService;

        _scopeAccessService =
            scopeAccessService;
    }

    // ============================================================
    // SUMMARY
    // ============================================================

    [HttpGet("summary")]
    [ProducesResponseType(
        typeof(DashboardSummaryDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardSummaryDto>>
        GetSummary(
            [FromQuery]
            string? workspace,
            CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "dashboard.view"))
        {
            return Forbid();
        }

        var context =
            GetSecurityContext();

        if (context is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "El token no contiene una organización o usuario válido."
                });
        }

        var requestedWorkspace =
            ParseWorkspace(
                workspace);

        if (
            !CanAccessWorkspace(
                requestedWorkspace))
        {
            return Forbid();
        }

        /*
         * ========================================================
         * AUTHORIZATION SCOPE
         * ========================================================
         *
         * Organization:
         * accessibleSiteIds = null
         *
         * Site:
         * accessibleSiteIds = SiteIds
         *
         * Sin scope:
         * accessibleSiteIds = []
         *
         * El servicio jamás debe convertir un Site scope en 403
         * solamente por no ser Organization.
         * ========================================================
         */

        var scope =
            await _scopeAccessService
                .GetScopeSnapshotAsync(
                    context.Value.OrganizationId,
                    context.Value.UserId,
                    cancellationToken);

        IReadOnlyCollection<Guid>?
            accessibleSiteIds;

        if (
            scope.OrganizationWide)
        {
            accessibleSiteIds =
                null;
        }
        else
        {
            accessibleSiteIds =
                scope.SiteIds;
        }

        var summary =
            await _dashboardService
                .GetSummaryAsync(
                    context.Value.OrganizationId,
                    requestedWorkspace,
                    accessibleSiteIds,
                    cancellationToken);

        return Ok(
            summary);
    }

    // ============================================================
    // WORKSPACE
    // ============================================================

    private static DashboardWorkspace
        ParseWorkspace(
            string? workspace)
    {
        if (
            string.IsNullOrWhiteSpace(
                workspace))
        {
            return DashboardWorkspace.Global;
        }

        return workspace
            .Trim()
            .ToLowerInvariant()
            switch
        {
            "windows" =>
                DashboardWorkspace.Windows,

            "android" =>
                DashboardWorkspace.Android,

            "global" =>
                DashboardWorkspace.Global,

            _ =>
                DashboardWorkspace.Global
        };
    }

    private bool CanAccessWorkspace(
        DashboardWorkspace workspace)
    {
        return workspace switch
        {
            DashboardWorkspace.Windows =>
                HasPermission(
                    "workspace.windows.view"),

            DashboardWorkspace.Android =>
                HasPermission(
                    "workspace.android.view"),

            DashboardWorkspace.Global =>
                HasPermission(
                    "dashboard.global.view"),

            _ =>
                false
        };
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

    private readonly record struct
        SecurityContext(
            Guid OrganizationId,
            Guid UserId);
}