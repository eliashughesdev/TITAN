using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/operations")]
public sealed class HelpdeskOperationsController : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    public HelpdeskOperationsController(TitanMdmDbContext db)
    {
        _db = db;
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        var zones = await _db.HelpdeskZones.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Type,
                x.ParentZoneId,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        var teams = await _db.HelpdeskTeams.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        var coverage = await _db.HelpdeskTeamZones.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new { x.TeamId, x.ZoneId })
            .ToListAsync(cancellationToken);

        var members = await _db.HelpdeskTeamMembers.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new
            {
                x.TeamId,
                x.UserId,
                x.IsAvailable,
                x.AcceptsAutomaticAssignments,
                x.MaxOpenTickets
            })
            .ToListAsync(cancellationToken);

        var userZones = await _db.HelpdeskUserZones.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new { x.UserId, x.ZoneId })
            .ToListAsync(cancellationToken);

        var eligibleUserIds = await EligibleAgents(organizationId)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            zones,
            teams,
            coverage,
            members,
            userZones,
            eligibleUserIds
        });
    }

    [HttpPost("zones")]
    public async Task<IActionResult> CreateZone(
        [FromBody] CreateZoneRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length > 120)
        {
            return BadRequest(new
            {
                message = "El nombre de la zona debe tener entre 1 y 120 caracteres."
            });
        }

        var type = request.Type?.Trim().ToLowerInvariant();

        if (type is not ("locality" or "plant" or "building" or "area"))
        {
            return BadRequest(new
            {
                message = "Tipo válido: locality, plant, building o area."
            });
        }

        if (request.ParentZoneId.HasValue)
        {
            var parentExists = await _db.HelpdeskZones.AnyAsync(
                x => x.Id == request.ParentZoneId &&
                     x.OrganizationId == organizationId &&
                     x.IsActive,
                cancellationToken);

            if (!parentExists)
            {
                return BadRequest(new
                {
                    message = "La zona superior no existe o está inactiva."
                });
            }
        }

        var duplicate = await _db.HelpdeskZones.AnyAsync(
            x => x.OrganizationId == organizationId &&
                 x.ParentZoneId == request.ParentZoneId &&
                 x.Name == request.Name.Trim(),
            cancellationToken);

        if (duplicate)
        {
            return Conflict(new
            {
                message = "Ya existe una zona con ese nombre en el mismo nivel."
            });
        }

        var zone = new HelpdeskZone(
            organizationId,
            request.Name,
            type,
            request.ParentZoneId);

        _db.HelpdeskZones.Add(zone);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetCatalog),
            null,
            new
            {
                zone.Id,
                zone.Name,
                zone.Type,
                zone.ParentZoneId,
                zone.IsActive
            });
    }

    [HttpPost("teams")]
    public async Task<IActionResult> CreateTeam(
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length > 120)
        {
            return BadRequest(new
            {
                message = "El nombre del grupo debe tener entre 1 y 120 caracteres."
            });
        }

        if (request.Description?.Length > 500)
        {
            return BadRequest(new
            {
                message = "La descripción excede 500 caracteres."
            });
        }

        var duplicate = await _db.HelpdeskTeams.AnyAsync(
            x => x.OrganizationId == organizationId &&
                 x.Name == request.Name.Trim(),
            cancellationToken);

        if (duplicate)
        {
            return Conflict(new
            {
                message = "Ya existe un grupo con ese nombre."
            });
        }

        var team = new HelpdeskTeam(
            organizationId,
            request.Name,
            request.Description);

        _db.HelpdeskTeams.Add(team);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetCatalog),
            null,
            new { team.Id, team.Name, team.Description });
    }

    [HttpPost("teams/{teamId:guid}/zones/{zoneId:guid}")]
    public async Task<IActionResult> AddCoverage(
        Guid teamId,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        var teamExists = await _db.HelpdeskTeams.AnyAsync(
            x => x.Id == teamId &&
                 x.OrganizationId == organizationId &&
                 x.IsActive,
            cancellationToken);

        var zoneExists = await _db.HelpdeskZones.AnyAsync(
            x => x.Id == zoneId &&
                 x.OrganizationId == organizationId &&
                 x.IsActive,
            cancellationToken);

        if (!teamExists || !zoneExists)
        {
            return BadRequest(new
            {
                message = "El grupo o la zona no existe en esta organización."
            });
        }

        var exists = await _db.HelpdeskTeamZones.AnyAsync(
            x => x.OrganizationId == organizationId &&
                 x.TeamId == teamId &&
                 x.ZoneId == zoneId,
            cancellationToken);

        if (!exists)
        {
            _db.HelpdeskTeamZones.Add(
                new HelpdeskTeamZone(
                    organizationId,
                    teamId,
                    zoneId));

            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { teamId, zoneId });
    }

    [HttpDelete("teams/{teamId:guid}/zones/{zoneId:guid}")]
    public async Task<IActionResult> RemoveCoverage(
        Guid teamId,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        var relation = await _db.HelpdeskTeamZones.FirstOrDefaultAsync(
            x => x.OrganizationId == organizationId &&
                 x.TeamId == teamId &&
                 x.ZoneId == zoneId,
            cancellationToken);

        if (relation is null) return NotFound();

        _db.HelpdeskTeamZones.Remove(relation);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPut("teams/{teamId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> SetTeamMember(
        Guid teamId,
        Guid userId,
        [FromBody] SetTeamMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        if (request.MaxOpenTickets is < 1 or > 500)
        {
            return BadRequest(new
            {
                message = "El límite debe estar entre 1 y 500 tickets."
            });
        }

        var teamExists = await _db.HelpdeskTeams.AnyAsync(
            x => x.Id == teamId &&
                 x.OrganizationId == organizationId &&
                 x.IsActive,
            cancellationToken);

        var userExists = await _db.Users.AnyAsync(
            x => x.Id == userId &&
                 x.OrganizationId == organizationId &&
                 x.IsActive,
            cancellationToken);

        if (!teamExists || !userExists)
        {
            return BadRequest(new
            {
                message = "El grupo o el usuario no existe en esta organización."
            });
        }

        if (request.AcceptsAutomaticAssignments &&
            !await EligibleAgents(organizationId).AnyAsync(
                x => x == userId,
                cancellationToken))
        {
            return BadRequest(new
            {
                message = "El técnico debe tener el permiso tickets.comment para recibir asignaciones automáticas."
            });
        }

        var member = await _db.HelpdeskTeamMembers.FirstOrDefaultAsync(
            x => x.OrganizationId == organizationId &&
                 x.TeamId == teamId &&
                 x.UserId == userId,
            cancellationToken);

        if (member is null)
        {
            member = new HelpdeskTeamMember(
                organizationId,
                teamId,
                userId,
                request.AcceptsAutomaticAssignments,
                request.MaxOpenTickets);

            _db.HelpdeskTeamMembers.Add(member);
        }
        else
        {
            member.ConfigureAutomaticAssignments(
                request.AcceptsAutomaticAssignments,
                request.MaxOpenTickets);
        }

        member.SetAvailability(request.IsAvailable);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            member.TeamId,
            member.UserId,
            member.IsAvailable,
            member.AcceptsAutomaticAssignments,
            member.MaxOpenTickets
        });
    }

    [HttpDelete("teams/{teamId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveTeamMember(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        var member = await _db.HelpdeskTeamMembers.FirstOrDefaultAsync(
            x => x.OrganizationId == organizationId &&
                 x.TeamId == teamId &&
                 x.UserId == userId,
            cancellationToken);

        if (member is null) return NotFound();

        _db.HelpdeskTeamMembers.Remove(member);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPut("users/{userId:guid}/zone")]
    public async Task<IActionResult> SetUserZone(
        Guid userId,
        [FromBody] SetUserZoneRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        var userExists = await _db.Users.AnyAsync(
            x => x.Id == userId &&
                 x.OrganizationId == organizationId &&
                 x.IsActive,
            cancellationToken);

        var zoneExists = await _db.HelpdeskZones.AnyAsync(
            x => x.Id == request.ZoneId &&
                 x.OrganizationId == organizationId &&
                 x.IsActive,
            cancellationToken);

        if (!userExists || !zoneExists)
        {
            return BadRequest(new
            {
                message = "El usuario o la zona no existe en esta organización."
            });
        }

        var current = await _db.HelpdeskUserZones
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (current.Count == 1 &&
            current[0].ZoneId == request.ZoneId)
        {
            return Ok(new { userId, request.ZoneId });
        }

        _db.HelpdeskUserZones.RemoveRange(current);
        _db.HelpdeskUserZones.Add(
            new HelpdeskUserZone(
                organizationId,
                userId,
                request.ZoneId));

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { userId, request.ZoneId });
    }

    [HttpDelete("users/{userId:guid}/zone")]
    public async Task<IActionResult> RemoveUserZone(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();
        if (!TryGetOrganization(out var organizationId))
            return Unauthorized();

        var current = await _db.HelpdeskUserZones
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.HelpdeskUserZones.RemoveRange(current);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("assistant/me")]
    public async Task<IActionResult> GetMyAssistantAccess(
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganization(out var organizationId) ||
            !TryGetUser(out var userId))
        {
            return Unauthorized();
        }

        var enabled = await _db.HelpdeskAssistantAccess
            .AsNoTracking()
            .AnyAsync(
                x => x.OrganizationId == organizationId &&
                     x.UserId == userId &&
                     x.IsEnabled,
                cancellationToken);

        return Ok(new { enabled });
    }

    [HttpPut("assistant/users/{userId:guid}")]
    public async Task<IActionResult> SetAssistantAccess(
        Guid userId,
        [FromBody] SetAssistantAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage()) return Forbid();

        if (!TryGetOrganization(out var organizationId) ||
            !TryGetUser(out var administratorId))
        {
            return Unauthorized();
        }

        var userExists = await _db.Users.AnyAsync(
            x => x.Id == userId &&
                 x.OrganizationId == organizationId &&
                 x.IsActive,
            cancellationToken);

        if (!userExists)
        {
            return NotFound(new
            {
                message = "El usuario no existe en esta organización."
            });
        }

        var access = await _db.HelpdeskAssistantAccess
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId &&
                     x.UserId == userId,
                cancellationToken);

        if (access is null)
        {
            if (request.Enabled)
            {
                access = new HelpdeskAssistantAccess(
                    organizationId,
                    userId,
                    administratorId);

                _db.HelpdeskAssistantAccess.Add(access);
            }
        }
        else if (request.Enabled)
        {
            access.Grant(administratorId);
        }
        else
        {
            access.Revoke();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            userId,
            enabled = request.Enabled
        });
    }

    private IQueryable<Guid> EligibleAgents(Guid organizationId) =>
        (
            from userRole in _db.UserRoles.AsNoTracking()
            join rolePermission in _db.RolePermissions.AsNoTracking()
                on userRole.RoleId equals rolePermission.RoleId
            join permission in _db.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            join user in _db.Users.AsNoTracking()
                on userRole.UserId equals user.Id
            where user.OrganizationId == organizationId &&
                  user.IsActive &&
                  permission.IsActive &&
                  permission.Code == "tickets.comment"
            select user.Id
        ).Distinct();

    private bool CanManage() =>
        User.Claims.Any(claim =>
            claim.Type == "permission" &&
            (
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

    private bool TryGetOrganization(out Guid organizationId)
    {
        var value =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        return Guid.TryParse(value, out organizationId);
    }

    private bool TryGetUser(out Guid userId)
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("user_id") ??
            User.FindFirstValue("userId");

        return Guid.TryParse(value, out userId);
    }

    public sealed record CreateZoneRequest(
        string Name,
        string Type,
        Guid? ParentZoneId);

    public sealed record CreateTeamRequest(
        string Name,
        string? Description);

    public sealed record SetTeamMemberRequest(
        bool AcceptsAutomaticAssignments,
        bool IsAvailable,
        int MaxOpenTickets);

    public sealed record SetUserZoneRequest(Guid ZoneId);

    public sealed record SetAssistantAccessRequest(bool Enabled);
}