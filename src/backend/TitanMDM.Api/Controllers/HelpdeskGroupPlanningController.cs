using System.Data;
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
    // GET COMPLETE PLANNING CATALOG
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
            await _db
                .HelpdeskTeams
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
            await _db
                .Sites
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
            await _db
                .SiteLocations
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
            await _db
                .HelpdeskSiteCoverages
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId)
                .ToListAsync(
                    cancellationToken);

        var members =
            await _db
                .HelpdeskTeamMembers
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

        var eligibleIds =
            await EligibleTechnicians(
                    organizationId)
                .ToListAsync(
                    cancellationToken);

        var eligibleSet =
            eligibleIds
                .ToHashSet();

        var users =
            await _db
                .Users
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
            users
                .Select(
                    user =>
                    {
                        string? siteName =
                            null;

                        string? locationName =
                            null;

                        if (
                            user.SiteId
                                .HasValue)
                        {
                            siteNames
                                .TryGetValue(
                                    user.SiteId.Value,
                                    out siteName);
                        }

                        if (
                            user.SiteLocationId
                                .HasValue)
                        {
                            locationNames
                                .TryGetValue(
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
                                eligibleSet
                                    .Contains(
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
            teams
                .Select(
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
                                .ThenBy(
                                    x =>
                                        x.SiteId)
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
                                                Array.Empty<
                                                    HelpdeskWeeklySlot>(),

                                            configured =
                                                schedule
                                                is not null,

                                            enabled =
                                                schedule?
                                                    .IsEnabled
                                                ??
                                                false,

                                            onDuty =
                                                schedule?
                                                    .IsOnDuty(
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

                            technicians,

                            readiness =
                                CalculateReadiness(
                                    team,
                                    teamCoverages.Length,
                                    technicians.Length,
                                    technicians.Count(
                                        x =>
                                            x.AcceptsAutomaticAssignments),
                                    technicians.Count(
                                        x =>
                                            x.AcceptsAutomaticAssignments
                                            &&
                                            x.configured))
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
            name.Length
            is < 1 or > 120)
        {
            return BadRequest(
                Message(
                    "El nombre debe tener entre 1 y 120 caracteres."));
        }

        if (
            description?.Length >
            500)
        {
            return BadRequest(
                Message(
                    "La descripción no puede exceder 500 caracteres."));
        }

        var normalizedName =
            name
                .ToLowerInvariant();

        var duplicate =
            await _db
                .HelpdeskTeams
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Name
                            .ToLower() ==
                            normalizedName,
                    cancellationToken);

        if (duplicate)
        {
            return Conflict(
                Message(
                    "Ya existe un grupo con ese nombre."));
        }

        var team =
            new HelpdeskTeam(
                organizationId,
                name,
                description);

        _db
            .HelpdeskTeams
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

        var validation =
            await ValidateRequestAsync(
                organizationId,
                teamId,
                request,
                cancellationToken);

        if (!validation.Valid)
        {
            return BadRequest(
                Message(
                    validation.Error
                    ??
                    "La configuración no es válida."));
        }

        var strategy =
            _db
                .Database
                .CreateExecutionStrategy();

        await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _db
                        .Database
                        .BeginTransactionAsync(
                            IsolationLevel.Serializable,
                            cancellationToken);

                var team =
                    await _db
                        .HelpdeskTeams
                        .FirstAsync(
                            x =>
                                x.Id ==
                                    teamId
                                &&
                                x.OrganizationId ==
                                    organizationId,
                            cancellationToken);

                team.ConfigureCategories(
                    NormalizeTasks(
                        request.Tasks));

                // =================================================
                // COVERAGES
                // =================================================

                var existingCoverage =
                    await _db
                        .HelpdeskSiteCoverages
                        .Where(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.TeamId ==
                                    teamId)
                        .ToListAsync(
                            cancellationToken);

                _db
                    .HelpdeskSiteCoverages
                    .RemoveRange(
                        existingCoverage);

                foreach (
                    var coverage
                    in request.Coverages)
                {
                    _db
                        .HelpdeskSiteCoverages
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

                // =================================================
                // MEMBERS
                // =================================================

                var technicianIds =
                    request
                        .Technicians
                        .Select(
                            x =>
                                x.UserId)
                        .ToHashSet();

                var existingMembers =
                    await _db
                        .HelpdeskTeamMembers
                        .Where(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.TeamId ==
                                    teamId)
                        .ToListAsync(
                            cancellationToken);

                var membersToDelete =
                    existingMembers
                        .Where(
                            x =>
                                !technicianIds
                                    .Contains(
                                        x.UserId))
                        .ToArray();

                _db
                    .HelpdeskTeamMembers
                    .RemoveRange(
                        membersToDelete);

                // =================================================
                // SCHEDULES
                // =================================================

                var existingSchedules =
                    await _db
                        .Set<
                            HelpdeskTechnicianSchedule>()
                        .Where(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.TeamId ==
                                    teamId)
                        .ToListAsync(
                            cancellationToken);

                var schedulesToDelete =
                    existingSchedules
                        .Where(
                            x =>
                                !technicianIds
                                    .Contains(
                                        x.UserId))
                        .ToArray();

                _db
                    .Set<
                        HelpdeskTechnicianSchedule>()
                    .RemoveRange(
                        schedulesToDelete);

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
                                technician
                                    .AcceptsAutomaticAssignments,
                                technician
                                    .MaxOpenTickets);

                        _db
                            .HelpdeskTeamMembers
                            .Add(
                                member);
                    }
                    else
                    {
                        member
                            .ConfigureAutomaticAssignments(
                                technician
                                    .AcceptsAutomaticAssignments,
                                technician
                                    .MaxOpenTickets);
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
                        schedule =
                            new HelpdeskTechnicianSchedule(
                                organizationId,
                                teamId,
                                technician.UserId);

                        _db
                            .Set<
                                HelpdeskTechnicianSchedule>()
                            .Add(
                                schedule);
                    }

                    schedule.Configure(
                        technician.Priority,
                        technician
                            .AcceptsAutomaticAssignments,
                        technician.TimeZoneId,
                        technician.Slots
                        ??
                        Array.Empty<
                            HelpdeskWeeklySlot>());
                }

                await _db
                    .SaveChangesAsync(
                        cancellationToken);

                await transaction
                    .CommitAsync(
                        cancellationToken);
            });

        return Ok(
            new
            {
                message =
                    "Categorías, cobertura, técnicos, capacidad y horarios guardados correctamente.",

                readyForAutomaticRouting =
                    request.Coverages.Length >
                    0
                    &&
                    request.Technicians.Any(
                        x =>
                            x.IsAvailable
                            &&
                            x.AcceptsAutomaticAssignments
                            &&
                            x.Slots
                            is
                            {
                                Length: > 0
                            })
            });
    }

    // ============================================================
    // VALIDATION
    // ============================================================

    private async Task<
        ValidationResult>
        ValidateRequestAsync(
            Guid organizationId,
            Guid teamId,
            SaveGroupRequest request,
            CancellationToken cancellationToken)
    {
        if (
            request.Tasks is null
            ||
            request.Coverages is null
            ||
            request.Technicians is null)
        {
            return ValidationResult
                .Fail(
                    "Debes enviar categorías, coberturas y técnicos.");
        }

        var teamExists =
            await _db
                .HelpdeskTeams
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            teamId,
                    cancellationToken);

        if (!teamExists)
        {
            return ValidationResult
                .Fail(
                    "El grupo no existe.");
        }

        var tasks =
            NormalizeTasks(
                request.Tasks);

        /*
         * HelpdeskTeam.ConfigureCategories()
         * admite máximo 15.
         *
         * El controlador viejo validaba 50 y luego el dominio
         * podía lanzar excepción. Queda corregido aquí.
         */
        if (
            tasks.Length
            is < 1 or > 15)
        {
            return ValidationResult
                .Fail(
                    "Cada grupo debe tener entre 1 y 15 categorías.");
        }

        if (
            tasks.Any(
                x =>
                    x.Length >
                        50
                    ||
                    x.Contains(
                        '|')
                    ||
                    x.Contains(
                        ',')))
        {
            return ValidationResult
                .Fail(
                    "Las categorías no pueden superar 50 caracteres ni contener ',' o '|'.");
        }

        // ========================================================
        // COVERAGE
        // ========================================================

        if (
            request.Coverages.Length >
            250)
        {
            return ValidationResult
                .Fail(
                    "Un grupo no puede tener más de 250 coberturas.");
        }

        foreach (
            var coverage
            in request.Coverages)
        {
            if (
                coverage.SiteId ==
                    Guid.Empty)
            {
                return ValidationResult
                    .Fail(
                        "Toda cobertura necesita una localidad.");
            }

            if (
                coverage.Priority
                is < 1 or > 1000)
            {
                return ValidationResult
                    .Fail(
                        "La prioridad de cobertura debe estar entre 1 y 1000.");
            }

            var category =
                NormalizeCategory(
                    coverage.Category);

            if (
                category is not null
                &&
                !tasks.Contains(
                    category,
                    StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult
                    .Fail(
                        $"La cobertura '{category}' no pertenece a las categorías del grupo.");
            }
        }

        var duplicateCoverage =
            request
                .Coverages
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
                    x =>
                        x.Count() >
                        1);

        if (duplicateCoverage)
        {
            return ValidationResult
                .Fail(
                    "Existe una cobertura repetida.");
        }

        var siteIds =
            request
                .Coverages
                .Select(
                    x =>
                        x.SiteId)
                .Distinct()
                .ToArray();

        if (
            siteIds.Length >
            0)
        {
            var validSites =
                await _db
                    .Sites
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
                    .CountAsync(
                        cancellationToken);

            if (
                validSites !=
                siteIds.Length)
            {
                return ValidationResult
                    .Fail(
                        "Existe una localidad inexistente o inactiva.");
            }
        }

        var locationIds =
            request
                .Coverages
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
            var validLocations =
                await _db
                    .SiteLocations
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
                validLocations.Count !=
                locationIds.Length)
            {
                return ValidationResult
                    .Fail(
                        "Existe una sublocalidad inexistente o inactiva.");
            }

            foreach (
                var coverage
                in request.Coverages)
            {
                if (
                    !coverage
                        .SiteLocationId
                        .HasValue)
                {
                    continue;
                }

                var belongs =
                    validLocations
                        .Any(
                            x =>
                                x.Id ==
                                    coverage
                                        .SiteLocationId
                                        .Value
                                &&
                                x.SiteId ==
                                    coverage.SiteId);

                if (!belongs)
                {
                    return ValidationResult
                        .Fail(
                            "Una sublocalidad no pertenece a la localidad seleccionada.");
                }
            }
        }

        // ========================================================
        // TECHNICIANS
        // ========================================================

        if (
            request.Technicians.Length >
            100)
        {
            return ValidationResult
                .Fail(
                    "Un grupo admite hasta 100 técnicos.");
        }

        if (
            request
                .Technicians
                .Select(
                    x =>
                        x.UserId)
                .Distinct()
                .Count()
            !=
            request
                .Technicians
                .Length)
        {
            return ValidationResult
                .Fail(
                    "No puedes agregar el mismo técnico dos veces.");
        }

        var technicianIds =
            request
                .Technicians
                .Select(
                    x =>
                        x.UserId)
                .ToArray();

        if (
            technicianIds.Length >
            0)
        {
            var eligible =
                await EligibleTechnicians(
                        organizationId)
                    .Where(
                        id =>
                            technicianIds
                                .Contains(
                                    id))
                    .CountAsync(
                        cancellationToken);

            if (
                eligible !=
                technicianIds.Length)
            {
                return ValidationResult
                    .Fail(
                        "Todos los técnicos deben estar activos y tener permisos operativos de Mesa de Ayuda.");
            }
        }

        foreach (
            var technician
            in request.Technicians)
        {
            if (
                technician.MaxOpenTickets
                is < 1 or > 500)
            {
                return ValidationResult
                    .Fail(
                        "La capacidad debe estar entre 1 y 500 tickets.");
            }

            if (
                technician.Priority
                is < 1 or > 100)
            {
                return ValidationResult
                    .Fail(
                        "La prioridad del técnico debe estar entre 1 y 100.");
            }

            if (
                technician.Slots
                is null)
            {
                return ValidationResult
                    .Fail(
                        "Debes enviar el horario del técnico.");
            }

            try
            {
                /*
                 * Utilizamos la entidad del dominio como validador
                 * canónico de:
                 *
                 * - timezone;
                 * - horarios;
                 * - solapamientos;
                 * - slots nocturnos;
                 * - máximo 28 franjas.
                 */
                var validator =
                    new HelpdeskTechnicianSchedule(
                        organizationId,
                        teamId,
                        technician.UserId);

                validator.Configure(
                    technician.Priority,
                    technician
                        .AcceptsAutomaticAssignments,
                    technician.TimeZoneId,
                    technician.Slots);
            }
            catch (
                ArgumentException exception)
            {
                return ValidationResult
                    .Fail(
                        exception.Message);
            }
        }

        return ValidationResult
            .Success();
    }

    // ============================================================
    // ELIGIBLE TECHNICIANS
    // ============================================================

    private IQueryable<Guid>
        EligibleTechnicians(
            Guid organizationId)
    {
        var permissions =
            new[]
            {
                "helpdesk.agent.access",
                "helpdesk.ticket.comment",
                "helpdesk.ticket.take",
                "helpdesk.ticket.assign",
                "helpdesk.ticket.transition",
                "helpdesk.ticket.resolve",
                "helpdesk.ticket.close",

                // Compatibility
                "tickets.comment",
                "tickets.assign",
                "tickets.close",
                "helpdesk.manage"
            };

        return (
            from userRole
                in _db
                    .UserRoles
                    .AsNoTracking()

            join role
                in _db
                    .Roles
                    .AsNoTracking()
                on userRole.RoleId
                equals role.Id

            join rolePermission
                in _db
                    .RolePermissions
                    .AsNoTracking()
                on role.Id
                equals rolePermission.RoleId

            join permission
                in _db
                    .Permissions
                    .AsNoTracking()
                on rolePermission.PermissionId
                equals permission.Id

            join user
                in _db
                    .Users
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
                permissions.Contains(
                    permission.Code)

            select user.Id
        )
        .Distinct();
    }

    // ============================================================
    // READINESS
    // ============================================================

    private static object
        CalculateReadiness(
            HelpdeskTeam team,
            int coverageCount,
            int technicianCount,
            int automaticTechnicians,
            int scheduledAutomaticTechnicians)
    {
        var issues =
            new List<string>();

        var categories =
            ParseTasks(
                team.Categories);

        if (
            !team.IsActive)
        {
            issues.Add(
                "Grupo inactivo");
        }

        if (
            categories.Length ==
            0)
        {
            issues.Add(
                "Sin categorías");
        }

        if (
            coverageCount ==
            0)
        {
            issues.Add(
                "Sin cobertura");
        }

        if (
            technicianCount ==
            0)
        {
            issues.Add(
                "Sin técnicos");
        }

        if (
            automaticTechnicians ==
            0)
        {
            issues.Add(
                "Sin técnicos para autoasignación");
        }

        if (
            automaticTechnicians >
                scheduledAutomaticTechnicians)
        {
            issues.Add(
                "Existen técnicos automáticos sin turno configurado");
        }

        return new
        {
            ready =
                issues.Count ==
                0,

            issues =
                issues.ToArray()
        };
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string[]
        NormalizeTasks(
            IEnumerable<string> values)
    {
        return values
            .Select(
                x =>
                    x?
                        .Trim()
                        .ToLowerInvariant()
                    ??
                    string.Empty)
            .Where(
                x =>
                    x.Length >
                    0)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

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

    private bool CanView()
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

    private bool CanManage()
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

    private static object
        Message(
            string message)
    {
        return new
        {
            message
        };
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

    private sealed record
        ValidationResult(
            bool Valid,
            string? Error)
    {
        public static ValidationResult
            Success()
        {
            return new ValidationResult(
                true,
                null);
        }

        public static ValidationResult
            Fail(
                string error)
        {
            return new ValidationResult(
                false,
                error);
        }
    }
}