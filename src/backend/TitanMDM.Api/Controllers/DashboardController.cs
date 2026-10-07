using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Application.Dashboard;
using TitanMDM.Application.Dashboard.DTOs;
using TitanMDM.Application.Dashboard.Interfaces;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController
    : ControllerBase
{
    private readonly IDashboardService
        _dashboardService;

    public DashboardController(
        IDashboardService dashboardService)
    {
        _dashboardService =
            dashboardService;
    }

    /*
     * ============================================================
     * DASHBOARD SUMMARY
     * ============================================================
     *
     * Examples:
     *
     * /api/dashboard/summary?workspace=global
     * /api/dashboard/summary?workspace=windows
     * /api/dashboard/summary?workspace=android
     * ============================================================
     */

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
        /*
         * Dashboard itself is optional.
         */

        if (
            !HasPermission(
                "dashboard.view"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (
            organizationId is null)
        {
            return Forbid();
        }

        /*
         * ========================================================
         * WORKSPACE
         * ========================================================
         */

        var requestedWorkspace =
            ParseWorkspace(
                workspace);

        /*
         * No permitimos que el usuario cambie manualmente
         * ?workspace=global para obtener más información.
         */

        if (
            !CanAccessWorkspace(
                requestedWorkspace))
        {
            return Forbid();
        }

        var summary =
            await _dashboardService
                .GetSummaryAsync(
                    organizationId.Value,
                    requestedWorkspace,
                    cancellationToken);

        return Ok(
            summary);
    }

    /*
     * ============================================================
     * WORKSPACE PARSER
     * ============================================================
     */

    private static DashboardWorkspace ParseWorkspace(
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

    /*
     * ============================================================
     * WORKSPACE AUTHORIZATION
     * ============================================================
     */

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

    /*
     * ============================================================
     * CLAIMS
     * ============================================================
     */

    private Guid? GetOrganizationId()
    {
        var organizationIdValue =
            User.FindFirstValue(
                "organizationId")
            ??
            User.FindFirstValue(
                "organization_id");

        if (
            Guid.TryParse(
                organizationIdValue,
                out var organizationId))
        {
            return organizationId;
        }

        return null;
    }

    private bool HasPermission(
        string permission)
    {
        return User.Claims
            .Any(
                claim =>
                    claim.Type ==
                        "permission"
                    &&
                    string.Equals(
                        claim.Value,
                        permission,
                        StringComparison
                            .OrdinalIgnoreCase));
    }
}