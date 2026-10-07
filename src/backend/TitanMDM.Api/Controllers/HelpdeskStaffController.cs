using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/staff")]
public sealed class HelpdeskStaffController
    : ControllerBase
{
    private static readonly string[]
        OperationalPermissions =
        [
            "helpdesk.agent.access",
            "helpdesk.ticket.details.view",
            "helpdesk.ticket.comment",
            "helpdesk.ticket.take",
            "helpdesk.ticket.assign",
            "helpdesk.ticket.transition",
            "helpdesk.ticket.resolve",
            "helpdesk.ticket.close",

            // Compatibilidad legacy
            "tickets.comment",
            "tickets.assign",
            "tickets.close",
            "helpdesk.view",
            "helpdesk.manage"
        ];

    private static readonly string[]
        OpenTicketStatuses =
        [
            "new",
            "open",
            "inprogress",
            "pendinguser"
        ];

    private readonly TitanMdmDbContext
        _db;

    public HelpdeskStaffController(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    // ============================================================
    // GET TECHNICIANS / ROUTING DIAGNOSTIC
    // ============================================================

    [HttpGet("users")]
    public async Task<IActionResult>
        GetUsers(
            CancellationToken cancellationToken)
    {
        if (!CanViewTechnicians())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var nowUtc =
            DateTime.UtcNow;

        // ========================================================
        // USERS WITH OPERATIONAL PERMISSIONS
        // ========================================================

        var staffPermissionUserIds =
            await (
                from userRole
                    in _db.UserRoles
                        .AsNoTracking()

                join role
                    in _db.Roles
                        .AsNoTracking()
                    on userRole.RoleId
                    equals role.Id

                join rolePermission
                    in _db.RolePermissions
                        .AsNoTracking()
                    on role.Id
                    equals rolePermission.RoleId

                join permission
                    in _db.Permissions
                        .AsNoTracking()
                    on rolePermission.PermissionId
                    equals permission.Id

                join user
                    in _db.Users
                        .AsNoTracking()
                    on userRole.UserId
                    equals user.Id

                where
                    user.OrganizationId ==
                        organizationId
                    &&
                    role.OrganizationId ==
                        organizationId
                    &&
                    user.IsActive
                    &&
                    role.IsActive
                    &&
                    permission.IsActive
                    &&
                    OperationalPermissions
                        .Contains(
                            permission.Code)

                select user.Id
            )
            .Distinct()
            .ToListAsync(
                cancellationToken);

        var staffPermissionSet =
            staffPermissionUserIds
                .ToHashSet();

        // ========================================================
        // ROLES
        // ========================================================

        var roleRows =
            await (
                from userRole
                    in _db.UserRoles
                        .AsNoTracking()

                join role
                    in _db.Roles
                        .AsNoTracking()
                    on userRole.RoleId
                    equals role.Id

                where
                    role.OrganizationId ==
                        organizationId
                    &&
                    role.IsActive

                select new
                {
                    userRole.UserId,
                    role.Name
                }
            )
            .ToListAsync(
                cancellationToken);

        var rolesByUser =
            roleRows
                .GroupBy(
                    x =>
                        x.UserId)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group
                            .Select(
                                x =>
                                    x.Name)
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .OrderBy(
                                x =>
                                    x)
                            .ToArray());

        // ========================================================
        // ACTIVE TEAMS
        // ========================================================

        var teams =
            await _db.HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .OrderBy(
                    x =>
                        x.Name)
                .ToListAsync(
                    cancellationToken);

        var teamById =
            teams.ToDictionary(
                x =>
                    x.Id);

        // ========================================================
        // TEAM MEMBERSHIPS
        // ========================================================

        var memberships =
            await _db.HelpdeskTeamMembers
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .ToListAsync(
                    cancellationToken);

        var memberUserIds =
            memberships
                .Select(
                    x =>
                        x.UserId)
                .Distinct()
                .ToHashSet();

        // ========================================================
        // TECHNICIAN SCHEDULES
        // ========================================================

        var schedules =
            await _db
                .Set<HelpdeskTechnicianSchedule>()
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // OPEN TICKET LOAD
        // ========================================================

        var openTicketLoads =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                    &&
                    x.AssigneeUserId
                        .HasValue
                    &&
                    OpenTicketStatuses
                        .Contains(
                            x.Status))
                .GroupBy(
                    x =>
                        x.AssigneeUserId!
                            .Value)
                .Select(
                    group =>
                        new
                        {
                            UserId =
                                group.Key,

                            Count =
                                group.Count()
                        })
                .ToDictionaryAsync(
                    x =>
                        x.UserId,
                    x =>
                        x.Count,
                    cancellationToken);

        // ========================================================
        // ASSISTANT ACCESS
        // ========================================================

        var assistantAccess =
            await _db.HelpdeskAssistantAccess
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                    &&
                    x.IsEnabled)
                .Select(
                    x =>
                        x.UserId)
                .ToListAsync(
                    cancellationToken);

        var assistantSet =
            assistantAccess
                .ToHashSet();

        // ========================================================
        // USERS
        //
        // Solo devolvemos usuarios relacionados con Helpdesk:
        // - tienen permiso operacional
        // - pertenecen a un grupo
        // - tienen acceso al asistente
        //
        // Evita listar centenares de solicitantes normales.
        // ========================================================

        var relevantUserIds =
            staffPermissionUserIds
                .Concat(
                    memberUserIds)
                .Concat(
                    assistantSet)
                .Distinct()
                .ToArray();

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                    &&
                    x.IsActive
                    &&
                    relevantUserIds
                        .Contains(
                            x.Id))
                .OrderBy(
                    x =>
                        x.FirstName)
                .ThenBy(
                    x =>
                        x.LastName)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.FirstName,
                            x.LastName,
                            x.Email,
                            x.SiteId,
                            x.SiteLocationId
                        })
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // SITES / LOCATIONS
        // ========================================================

        var siteIds =
            users
                .Where(
                    x =>
                        x.SiteId.HasValue)
                .Select(
                    x =>
                        x.SiteId!.Value)
                .Distinct()
                .ToArray();

        var locationIds =
            users
                .Where(
                    x =>
                        x.SiteLocationId
                            .HasValue)
                .Select(
                    x =>
                        x.SiteLocationId!
                            .Value)
                .Distinct()
                .ToArray();

        var sites =
            await _db.Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                    &&
                    siteIds.Contains(
                        x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    x =>
                        x.Name,
                    cancellationToken);

        var locations =
            await _db.SiteLocations
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                    &&
                    locationIds.Contains(
                        x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    x =>
                        x.Name,
                    cancellationToken);

        // ========================================================
        // RESULT
        // ========================================================

        var result =
            users.Select(
                user =>
                {
                    var userMemberships =
                        memberships
                            .Where(
                                x =>
                                    x.UserId ==
                                        user.Id)
                            .ToArray();

                    var openTickets =
                        openTicketLoads
                            .TryGetValue(
                                user.Id,
                                out var currentLoad)
                            ? currentLoad
                            : 0;

                    string? siteName =
                        null;

                    string? locationName =
                        null;

                    if (user.SiteId.HasValue)
                    {
                        sites.TryGetValue(
                            user.SiteId.Value,
                            out siteName);
                    }

                    if (user.SiteLocationId
                        .HasValue)
                    {
                        locations.TryGetValue(
                            user.SiteLocationId.Value,
                            out locationName);
                    }

                    var canWorkTickets =
                        staffPermissionSet
                            .Contains(
                                user.Id);

                    var groupDiagnostics =
                        userMemberships
                            .Select(
                                membership =>
                                {
                                    teamById
                                        .TryGetValue(
                                            membership.TeamId,
                                            out var team);

                                    var schedule =
                                        schedules
                                            .FirstOrDefault(
                                                x =>
                                                    x.TeamId ==
                                                        membership.TeamId
                                                    &&
                                                    x.UserId ==
                                                        user.Id);

                                    var teamActive =
                                        team?.IsActive
                                        ??
                                        false;

                                    var scheduleConfigured =
                                        schedule is not null
                                        &&
                                        schedule.IsEnabled
                                        &&
                                        schedule
                                            .GetSlots()
                                            .Count >
                                            0;

                                    var onDuty =
                                        schedule?
                                            .IsOnDuty(
                                                nowUtc)
                                        ??
                                        false;

                                    var remainingCapacity =
                                        Math.Max(
                                            0,
                                            membership.MaxOpenTickets
                                            -
                                            openTickets);

                                    var atCapacity =
                                        openTickets >=
                                        membership.MaxOpenTickets;

                                    var routingReady =
                                        canWorkTickets
                                        &&
                                        teamActive
                                        &&
                                        membership.IsAvailable
                                        &&
                                        membership
                                            .AcceptsAutomaticAssignments
                                        &&
                                        !atCapacity
                                        &&
                                        scheduleConfigured
                                        &&
                                        onDuty;

                                    var issues =
                                        new List<string>();

                                    if (!canWorkTickets)
                                    {
                                        issues.Add(
                                            "Sin permiso operativo de Helpdesk.");
                                    }

                                    if (!teamActive)
                                    {
                                        issues.Add(
                                            "Grupo inactivo.");
                                    }

                                    if (!membership.IsAvailable)
                                    {
                                        issues.Add(
                                            "Técnico marcado como no disponible.");
                                    }

                                    if (!membership
                                        .AcceptsAutomaticAssignments)
                                    {
                                        issues.Add(
                                            "Autoasignación deshabilitada.");
                                    }

                                    if (!scheduleConfigured)
                                    {
                                        issues.Add(
                                            "Sin turno configurado.");
                                    }
                                    else if (!onDuty)
                                    {
                                        issues.Add(
                                            "Fuera de turno.");
                                    }

                                    if (atCapacity)
                                    {
                                        issues.Add(
                                            "Capacidad máxima alcanzada.");
                                    }

                                    return new
                                    {
                                        membership.TeamId,

                                        teamName =
                                            team?.Name
                                            ??
                                            "Grupo no disponible",

                                        teamActive,

                                        membership.IsAvailable,

                                        membership
                                            .AcceptsAutomaticAssignments,

                                        membership.MaxOpenTickets,

                                        openTickets,

                                        remainingCapacity,

                                        isAtCapacity =
                                            atCapacity,

                                        scheduleConfigured,

                                        scheduleEnabled =
                                            schedule?
                                                .IsEnabled
                                            ??
                                            false,

                                        onDuty,

                                        priority =
                                            schedule?
                                                .Priority
                                            ??
                                            1,

                                        timeZoneId =
                                            schedule?
                                                .TimeZoneId
                                            ??
                                            "America/Santo_Domingo",

                                        slots =
                                            schedule?
                                                .GetSlots()
                                            ??
                                            Array.Empty<HelpdeskWeeklySlot>(),

                                        routingReady,

                                        routingIssues =
                                            issues.ToArray()
                                    };
                                })
                            .OrderBy(
                                x =>
                                    x.priority)
                            .ThenBy(
                                x =>
                                    x.teamName)
                            .ToArray();

                    var routingReady =
                        groupDiagnostics
                            .Any(
                                x =>
                                    x.routingReady);

                    var routingIssues =
                        new List<string>();

                    if (!canWorkTickets)
                    {
                        routingIssues.Add(
                            "SIN ROL / PERMISOS OPERATIVOS");
                    }

                    if (userMemberships.Length ==
                        0)
                    {
                        routingIssues.Add(
                            "SIN GRUPO");
                    }

                    if (!user.SiteId.HasValue)
                    {
                        routingIssues.Add(
                            "SIN LOCALIDAD");
                    }

                    if (groupDiagnostics.Length >
                        0
                        &&
                        !routingReady)
                    {
                        if (groupDiagnostics
                            .All(
                                x =>
                                    !x.IsAvailable))
                        {
                            routingIssues.Add(
                                "NO DISPONIBLE");
                        }

                        if (groupDiagnostics
                            .All(
                                x =>
                                    !x.AcceptsAutomaticAssignments))
                        {
                            routingIssues.Add(
                                "AUTOASIGNACIÓN DESHABILITADA");
                        }

                        if (groupDiagnostics
                            .All(
                                x =>
                                    !x.scheduleConfigured))
                        {
                            routingIssues.Add(
                                "SIN TURNO");
                        }

                        if (groupDiagnostics
                            .Where(
                                x =>
                                    x.scheduleConfigured)
                            .Any()
                            &&
                            groupDiagnostics
                                .Where(
                                    x =>
                                        x.scheduleConfigured)
                                .All(
                                    x =>
                                        !x.onDuty))
                        {
                            routingIssues.Add(
                                "FUERA DE TURNO");
                        }

                        if (groupDiagnostics
                            .All(
                                x =>
                                    x.isAtCapacity))
                        {
                            routingIssues.Add(
                                "CAPACIDAD COMPLETA");
                        }
                    }

                    if (routingReady)
                    {
                        routingIssues.Clear();
                    }

                    var maxCapacity =
                        groupDiagnostics.Length ==
                            0
                            ? 0
                            : groupDiagnostics
                                .Max(
                                    x =>
                                        x.MaxOpenTickets);

                    var remainingCapacity =
                        groupDiagnostics.Length ==
                            0
                            ? 0
                            : groupDiagnostics
                                .Max(
                                    x =>
                                        x.remainingCapacity);

                    var onDuty =
                        groupDiagnostics
                            .Any(
                                x =>
                                    x.onDuty);

                    var scheduleConfigured =
                        groupDiagnostics
                            .Any(
                                x =>
                                    x.scheduleConfigured);

                    var acceptsAutomaticAssignments =
                        groupDiagnostics
                            .Any(
                                x =>
                                    x.AcceptsAutomaticAssignments);

                    var isAvailable =
                        groupDiagnostics
                            .Any(
                                x =>
                                    x.IsAvailable);

                    rolesByUser
                        .TryGetValue(
                            user.Id,
                            out var roles);

                    return new
                    {
                        user.Id,

                        name =
                            $"{user.FirstName} {user.LastName}"
                                .Trim(),

                        user.Email,

                        roles =
                            roles
                            ??
                            Array.Empty<string>(),

                        user.SiteId,

                        siteName,

                        user.SiteLocationId,

                        siteLocationName =
                            locationName,

                        canWorkTickets,

                        isEligible =
                            canWorkTickets,

                        assistantEnabled =
                            assistantSet
                                .Contains(
                                    user.Id),

                        groups =
                            groupDiagnostics,

                        groupNames =
                            groupDiagnostics
                                .Select(
                                    x =>
                                        x.teamName)
                                .Distinct(
                                    StringComparer.OrdinalIgnoreCase)
                                .ToArray(),

                        openTickets,

                        maxCapacity,

                        remainingCapacity,

                        isAtCapacity =
                            maxCapacity >
                            0
                            &&
                            remainingCapacity ==
                            0,

                        isAvailable,

                        acceptsAutomaticAssignments,

                        scheduleConfigured,

                        onDuty,

                        routingReady,

                        routingIssues =
                            routingIssues
                                .Distinct(
                                    StringComparer.OrdinalIgnoreCase)
                                .ToArray()
                    };
                })
                .OrderByDescending(
                    x =>
                        x.routingReady)
                .ThenBy(
                    x =>
                        x.name)
                .ToArray();

        return Ok(
            result);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private bool TryGetOrganization(
        out Guid organizationId)
    {
        var organizationValue =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        return Guid.TryParse(
            organizationValue,
            out organizationId);
    }

    private bool CanViewTechnicians()
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                (
                    string.Equals(
                        claim.Value,
                        "helpdesk.technicians.view",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "helpdesk.technicians.manage",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "helpdesk.admin.access",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "helpdesk.manage",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        claim.Value,
                        "settings.manage",
                        StringComparison.OrdinalIgnoreCase)
                ));
    }
}