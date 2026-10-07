using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/group-planning")]
public sealed class HelpdeskGroupPlanningController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _db;

    public HelpdeskGroupPlanningController(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    // ============================================================
    // CATALOG
    // ============================================================

    [HttpGet]
    public async Task<IActionResult>
        Get(
            CancellationToken cancellationToken)
    {
        if (!CanView())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

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

        var sites =
            await _db.Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive)
                .OrderBy(
                    x =>
                        x.Name)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Code,
                            x.Name,
                            x.City,
                            x.Province,
                            x.IsActive
                        })
                .ToListAsync(
                    cancellationToken);

        var locations =
            await _db.SiteLocations
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive)
                .OrderBy(
                    x =>
                        x.Name)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.SiteId,
                            x.Name,
                            x.Description,
                            x.IsActive
                        })
                .ToListAsync(
                    cancellationToken);

        var coverages =
            await _db.HelpdeskSiteCoverages
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .ToListAsync(
                    cancellationToken);

        var members =
            await _db.HelpdeskTeamMembers
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .ToListAsync(
                    cancellationToken);

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

        var eligibleUserIds =
            await EligibleTechnicians(
                    organizationId)
                .ToListAsync(
                    cancellationToken);

        var eligibleSet =
            eligibleUserIds
                .ToHashSet();

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive)
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

        var siteNames =
            sites.ToDictionary(
                x =>
                    x.Id,
                x =>
                    x.Name);

        var locationNames =
            locations.ToDictionary(
                x =>
                    x.Id,
                x =>
                    x.Name);

        var resultUsers =
            users.Select(
                user =>
                {
                    string? siteName =
                        null;

                    string? locationName =
                        null;

                    if (
                        user.SiteId.HasValue)
                    {
                        siteNames.TryGetValue(
                            user.SiteId.Value,
                            out siteName);
                    }

                    if (
                        user.SiteLocationId.HasValue)
                    {
                        locationNames.TryGetValue(
                            user.SiteLocationId.Value,
                            out locationName);
                    }

                    return new
                    {
                        user.Id,

                        name =
                            $"{user.FirstName} {user.LastName}"
                                .Trim(),

                        user.Email,

                        eligible =
                            eligibleSet.Contains(
                                user.Id),

                        user.SiteId,

                        siteName,

                        user.SiteLocationId,

                        siteLocationName =
                            locationName
                    };
                })
                .ToArray();

        var resultGroups =
            teams.Select(
                team =>
                {
                    var teamCoverages =
                        coverages
                            .Where(
                                x =>
                                    x.TeamId ==
                                        team.Id)
                            .OrderBy(
                                x =>
                                    x.Priority)
                            .Select(
                                x =>
                                    new
                                    {
                                        x.Id,
                                        x.SiteId,
                                        x.SiteLocationId,
                                        x.Category,
                                        x.Priority,
                                        x.IsActive
                                    })
                            .ToArray();

                    var technicians =
                        members
                            .Where(
                                x =>
                                    x.TeamId ==
                                        team.Id)
                            .Select(
                                member =>
                                {
                                    var schedule =
                                        schedules
                                            .FirstOrDefault(
                                                x =>
                                                    x.TeamId ==
                                                        team.Id
                                                    &&
                                                    x.UserId ==
                                                        member.UserId);

                                    return new
                                    {
                                        member.UserId,
                                        member.IsAvailable,
                                        member.AcceptsAutomaticAssignments,
                                        member.MaxOpenTickets,

                                        priority =
                                            schedule?.Priority
                                            ??
                                            1,

                                        timeZoneId =
                                            schedule?.TimeZoneId
                                            ??
                                            "America/Santo_Domingo",

                                        slots =
                                            schedule?.GetSlots()
                                            ??
                                            Array.Empty<HelpdeskWeeklySlot>(),

                                        configured =
                                            schedule is not null,

                                        onDuty =
                                            schedule?.IsOnDuty(
                                                DateTime.UtcNow)
                                            ??
                                            false
                                    };
                                })
                            .OrderBy(
                                x =>
                                    x.priority)
                            .ThenBy(
                                x =>
                                    x.UserId)
                            .ToArray();

                    return new
                    {
                        team.Id,
                        team.Name,
                        team.Description,
                        team.IsActive,

                        tasks =
                            ParseTasks(
                                team.Categories),

                        coverages =
                            teamCoverages,

                        technicians
                    };
                })
                .ToArray();

        return Ok(
            new
            {
                sites,
                locations,
                users =
                    resultUsers,
                groups =
                    resultGroups
            });
    }

    // ============================================================
    // CREATE GROUP
    // ============================================================

    [HttpPost("groups")]
    public async Task<IActionResult>
        CreateGroup(
            [FromBody]
            CreateGroupRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var name =
            request.Name?
                .Trim()
            ??
            string.Empty;

        var description =
            string.IsNullOrWhiteSpace(
                request.Description)
                ? null
                : request.Description
                    .Trim();

        if (
            name.Length is < 1 or > 120)
        {
            return BadRequest(
                new
                {
                    message =
                        "El nombre del grupo debe tener entre 1 y 120 caracteres."
                });
        }

        if (
            description?.Length >
            500)
        {
            return BadRequest(
                new
                {
                    message =
                        "La descripción no puede exceder 500 caracteres."
                });
        }

        var duplicate =
            await _db.HelpdeskTeams
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Name ==
                            name,
                    cancellationToken);

        if (duplicate)
        {
            return Conflict(
                new
                {
                    message =
                        "Ya existe un grupo con ese nombre."
                });
        }

        var team =
            new HelpdeskTeam(
                organizationId,
                name,
                description);

        _db.HelpdeskTeams
            .Add(
                team);

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return Ok(
            new
            {
                team.Id,
                team.Name
            });
    }

    // ============================================================
    // SAVE COMPLETE GROUP
    // ============================================================

    [HttpPut("groups/{teamId:guid}")]
    public async Task<IActionResult>
        SaveGroup(
            Guid teamId,
            [FromBody]
            SaveGroupRequest request,
            CancellationToken cancellationToken)
    {
        if (!CanManage())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        if (
            request.Tasks is null
            ||
            request.Coverages is null
            ||
            request.Technicians is null)
        {
            return BadRequest(
                new
                {
                    message =
                        "Envía tareas, coberturas y técnicos."
                });
        }

        if (
            request.Tasks.Length >
                50
            ||
            request.Tasks.Any(
                x =>
                    string.IsNullOrWhiteSpace(
                        x)
                    ||
                    x.Length >
                        100))
        {
            return BadRequest(
                new
                {
                    message =
                        "Las tareas son inválidas."
                });
        }

        if (
            request.Technicians.Length >
                100
            ||
            request.Technicians
                .Select(
                    x =>
                        x.UserId)
                .Distinct()
                .Count()
                !=
                request.Technicians
                    .Length)
        {
            return BadRequest(
                new
                {
                    message =
                        "Admite hasta 100 técnicos sin duplicados."
                });
        }

        var team =
            await _db.HelpdeskTeams
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            teamId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            team is null)
        {
            return NotFound();
        }

        // ========================================================
        // VALIDATE COVERAGE
        // ========================================================

        var siteIds =
            request.Coverages
                .Select(
                    x =>
                        x.SiteId)
                .Distinct()
                .ToArray();

        var validSites =
            await _db.Sites
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive
                        &&
                        siteIds.Contains(
                            x.Id))
                .Select(
                    x =>
                        x.Id)
                .ToListAsync(
                    cancellationToken);

        if (
            validSites.Count !=
            siteIds.Length)
        {
            return BadRequest(
                new
                {
                    message =
                        "Hay localidades inexistentes o inactivas."
                });
        }

        var locationIds =
            request.Coverages
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

        if (
            locationIds.Length >
            0)
        {
            var locations =
                await _db.SiteLocations
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.IsActive
                            &&
                            locationIds.Contains(
                                x.Id))
                    .Select(
                        x =>
                            new
                            {
                                x.Id,
                                x.SiteId
                            })
                    .ToListAsync(
                        cancellationToken);

            if (
                locations.Count !=
                locationIds.Length)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "Hay sublocalidades inexistentes o inactivas."
                    });
            }

            foreach (
                var coverage
                in request.Coverages)
            {
                if (
                    !coverage.SiteLocationId
                        .HasValue)
                {
                    continue;
                }

                var valid =
                    locations.Any(
                        location =>
                            location.Id ==
                                coverage.SiteLocationId.Value
                            &&
                            location.SiteId ==
                                coverage.SiteId);

                if (!valid)
                {
                    return BadRequest(
                        new
                        {
                            message =
                                "Una sublocalidad no pertenece a la localidad indicada."
                        });
                }
            }
        }

        var duplicateCoverage =
            request.Coverages
                .GroupBy(
                    x =>
                        new
                        {
                            x.SiteId,
                            x.SiteLocationId,

                            Category =
                                NormalizeCategory(
                                    x.Category)
                        })
                .Any(
                    group =>
                        group.Count() >
                        1);

        if (
            duplicateCoverage)
        {
            return BadRequest(
                new
                {
                    message =
                        "No puedes repetir la misma cobertura."
                });
        }

        // ========================================================
        // VALIDATE TECHNICIANS
        // ========================================================

        var technicianIds =
            request.Technicians
                .Select(
                    x =>
                        x.UserId)
                .ToArray();

        var eligible =
            await EligibleTechnicians(
                    organizationId)
                .Where(
                    id =>
                        technicianIds
                            .Contains(
                                id))
                .ToListAsync(
                    cancellationToken);

        if (
            eligible.Count !=
            technicianIds.Length)
        {
            return BadRequest(
                new
                {
                    message =
                        "Cada técnico debe estar activo y disponer de permisos operativos de Mesa de Ayuda."
                });
        }

        var schedulesToCreate =
            new Dictionary<
                Guid,
                HelpdeskTechnicianSchedule>();

        try
        {
            team.ConfigureCategories(
                request.Tasks);

            foreach (
                var technician
                in request.Technicians)
            {
                if (
                    technician.MaxOpenTickets
                    is < 1 or > 500)
                {
                    throw new ArgumentException(
                        "El límite de tickets debe estar entre 1 y 500.");
                }

                if (
                    technician.Slots
                    is null)
                {
                    throw new ArgumentException(
                        "Debe enviarse el horario de cada técnico.");
                }

                var profile =
                    new HelpdeskTechnicianSchedule(
                        organizationId,
                        teamId,
                        technician.UserId);

                profile.Configure(
                    technician.Priority,
                    technician.AcceptsAutomaticAssignments,
                    technician.TimeZoneId,
                    technician.Slots);

                schedulesToCreate[
                    technician.UserId
                ] =
                    profile;
            }
        }
        catch (
            ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    message =
                        exception.Message
                });
        }

        // ========================================================
        // REPLACE SITE COVERAGE
        // ========================================================

        var existingCoverage =
            await _db.HelpdeskSiteCoverages
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.TeamId ==
                            teamId)
                .ToListAsync(
                    cancellationToken);

        _db.HelpdeskSiteCoverages
            .RemoveRange(
                existingCoverage);

        foreach (
            var coverage
            in request.Coverages)
        {
            _db.HelpdeskSiteCoverages
                .Add(
                    new HelpdeskSiteCoverage(
                        organizationId,
                        teamId,
                        coverage.SiteId,
                        coverage.SiteLocationId,
                        NormalizeCategory(
                            coverage.Category),
                        coverage.Priority));
        }

        // ========================================================
        // TEAM MEMBERS
        // ========================================================

        var existingMembers =
            await _db.HelpdeskTeamMembers
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.TeamId ==
                            teamId)
                .ToListAsync(
                    cancellationToken);

        var existingSchedules =
            await _db
                .Set<HelpdeskTechnicianSchedule>()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.TeamId ==
                            teamId)
                .ToListAsync(
                    cancellationToken);

        _db.HelpdeskTeamMembers
            .RemoveRange(
                existingMembers
                    .Where(
                        x =>
                            !technicianIds
                                .Contains(
                                    x.UserId)));

        _db
            .Set<HelpdeskTechnicianSchedule>()
            .RemoveRange(
                existingSchedules
                    .Where(
                        x =>
                            !technicianIds
                                .Contains(
                                    x.UserId)));

        foreach (
            var technician
            in request.Technicians)
        {
            var member =
                existingMembers
                    .FirstOrDefault(
                        x =>
                            x.UserId ==
                                technician.UserId);

            if (
                member is null)
            {
                member =
                    new HelpdeskTeamMember(
                        organizationId,
                        teamId,
                        technician.UserId,
                        technician.AcceptsAutomaticAssignments,
                        technician.MaxOpenTickets);

                _db.HelpdeskTeamMembers
                    .Add(
                        member);
            }
            else
            {
                member
                    .ConfigureAutomaticAssignments(
                        technician.AcceptsAutomaticAssignments,
                        technician.MaxOpenTickets);
            }

            member.SetAvailability(
                technician.IsAvailable);

            var schedule =
                existingSchedules
                    .FirstOrDefault(
                        x =>
                            x.UserId ==
                                technician.UserId);

            if (
                schedule is null)
            {
                _db
                    .Set<HelpdeskTechnicianSchedule>()
                    .Add(
                        schedulesToCreate[
                            technician.UserId
                        ]);
            }
            else
            {
                schedule.Configure(
                    technician.Priority,
                    technician.AcceptsAutomaticAssignments,
                    technician.TimeZoneId,
                    technician.Slots
                    ?? Array.Empty<HelpdeskWeeklySlot>());
            }
        }

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return Ok(
            new
            {
                message =
                    "Grupo, tareas, localidades, técnicos y horarios guardados correctamente."
            });
    }

    // ============================================================
    // ELIGIBLE TECHNICIANS
    // ============================================================

    private IQueryable<Guid>
        EligibleTechnicians(
            Guid organizationId)
    {
        var operationalPermissions =
            new[]
            {
                "helpdesk.agent.access",
                "helpdesk.ticket.comment",
                "helpdesk.ticket.take",
                "helpdesk.ticket.assign",
                "helpdesk.ticket.transition",
                "helpdesk.ticket.resolve",
                "helpdesk.ticket.close",

                // Legacy
                "tickets.comment",
                "tickets.assign",
                "tickets.close",
                "helpdesk.manage"
            };

        return (
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
                operationalPermissions
                    .Contains(
                        permission.Code)

            select user.Id
        )
        .Distinct();
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string[]
        ParseTasks(
            string? value)
    {
        return (
            value
            ??
            string.Empty
        )
        .Split(
            '|',
            StringSplitOptions
                .RemoveEmptyEntries
            |
            StringSplitOptions
                .TrimEntries);
    }

    private static string?
        NormalizeCategory(
            string? value)
    {
        return string.IsNullOrWhiteSpace(
            value)
            ? null
            : value
                .Trim()
                .ToLowerInvariant();
    }

    private bool
        TryGetOrganization(
            out Guid organizationId)
    {
        var value =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        return Guid.TryParse(
            value,
            out organizationId);
    }

    private bool
        CanView()
    {
        return HasAnyPermission(
            "helpdesk.groups.view",
            "helpdesk.groups.manage",
            "helpdesk.technicians.view",
            "helpdesk.technicians.manage",
            "helpdesk.schedules.view",
            "helpdesk.schedules.manage",
            "helpdesk.admin.access",
            "helpdesk.manage",
            "settings.manage");
    }

    private bool
        CanManage()
    {
        return HasAnyPermission(
            "helpdesk.groups.manage",
            "helpdesk.technicians.manage",
            "helpdesk.schedules.manage",
            "helpdesk.admin.access",
            "helpdesk.manage",
            "settings.manage");
    }

    private bool
        HasAnyPermission(
            params string[] permissions)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                permissions.Any(
                    permission =>
                        string.Equals(
                            claim.Value,
                            permission,
                            StringComparison.OrdinalIgnoreCase)));
    }

    // ============================================================
    // CONTRACTS
    // ============================================================

    public sealed record
        CreateGroupRequest(
            string Name,
            string? Description);

    public sealed record
        SaveGroupRequest(
            string[] Tasks,
            CoverageRequest[] Coverages,
            TechnicianRequest[] Technicians);

    public sealed record
        CoverageRequest(
            Guid SiteId,
            Guid? SiteLocationId,
            string? Category,
            int Priority = 100);

    public sealed record
        TechnicianRequest(
            Guid UserId,
            bool IsAvailable,
            bool AcceptsAutomaticAssignments,
            int MaxOpenTickets,
            int Priority,
            string TimeZoneId,
            HelpdeskWeeklySlot[]? Slots);
}