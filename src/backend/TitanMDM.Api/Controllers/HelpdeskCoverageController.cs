using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/coverage")]
public sealed class HelpdeskCoverageController : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    public HelpdeskCoverageController(TitanMdmDbContext db)
    {
        _db = db;
    }

    [HttpGet("readiness")]
    public async Task<IActionResult> GetReadiness(
        CancellationToken cancellationToken)
    {
        if (!CanManage())
            return Forbid();

        var organizationClaim =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        if (!Guid.TryParse(organizationClaim, out var organizationId))
            return Unauthorized();

        var zones = await _db.HelpdeskZones
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Type,
                x.ParentZoneId
            })
            .ToListAsync(cancellationToken);

        var teams = await _db.HelpdeskTeams
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .ToListAsync(cancellationToken);

        var coverage = await _db.HelpdeskTeamZones
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new { x.TeamId, x.ZoneId })
            .ToListAsync(cancellationToken);

        var members = await _db.HelpdeskTeamMembers
            .AsNoTracking()
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

        var userZones = await _db.HelpdeskUserZones
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new { x.UserId, x.ZoneId })
            .ToListAsync(cancellationToken);

        var eligibleUserIds = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join rolePermission in _db.RolePermissions.AsNoTracking()
                on userRole.RoleId equals rolePermission.RoleId
            join permission in _db.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where permission.IsActive &&
                  permission.Code == "tickets.comment"
            select userRole.UserId
        )
        .Distinct()
        .ToListAsync(cancellationToken);

        var candidateUserIds = members
            .Select(x => x.UserId)
            .Distinct()
            .ToArray();

        var activeUserIds = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.IsActive &&
                candidateUserIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var loads = await _db.HelpdeskTickets
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.AssigneeUserId.HasValue &&
                candidateUserIds.Contains(x.AssigneeUserId.Value) &&
                x.Status != "resolved" &&
                x.Status != "closed")
            .GroupBy(x => x.AssigneeUserId!.Value)
            .Select(x => new
            {
                UserId = x.Key,
                Count = x.Count()
            })
            .ToDictionaryAsync(
                x => x.UserId,
                x => x.Count,
                cancellationToken);

        var eligibleSet = eligibleUserIds.ToHashSet();
        var activeSet = activeUserIds.ToHashSet();
        var zoneById = zones.ToDictionary(x => x.Id);
        var teamById = teams.ToDictionary(x => x.Id);

        var categories = teams
            .SelectMany(x =>
                x.Categories.Split(
                    '|',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
            .Append("general")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        var rows = new List<object>();
        var readyCount = 0;
        var uncoveredCount = 0;

        foreach (var zone in zones.OrderBy(x => x.Name))
        {
            var chain = new List<Guid>();
            var currentId = zone.Id;

            for (var depth = 0; depth < 12; depth++)
            {
                if (!zoneById.TryGetValue(currentId, out var current) ||
                    chain.Contains(currentId))
                {
                    break;
                }

                chain.Add(currentId);

                if (!current.ParentZoneId.HasValue)
                    break;

                currentId = current.ParentZoneId.Value;
            }

            var assignedUsers = userZones
                .Where(x => x.ZoneId == zone.Id)
                .Select(x => x.UserId)
                .Distinct()
                .Count();

            foreach (var category in categories)
            {
                var specialistIds = teams
                    .Where(x => x.HandlesCategory(category))
                    .Select(x => x.Id)
                    .ToHashSet();

                var generalIds = teams
                    .Where(x => string.IsNullOrWhiteSpace(x.Categories))
                    .Select(x => x.Id)
                    .ToHashSet();

                string? selectedTeam = null;
                string? coveredByZone = null;
                int availableAgents = 0;
                string reason = "Sin grupo que cubra esta zona y categoría.";

                foreach (var teamIds in new[]
                {
                    specialistIds,
                    generalIds
                })
                {
                    if (teamIds.Count == 0)
                        continue;

                    foreach (var coveredZoneId in chain)
                    {
                        var matchingTeamIds = coverage
                            .Where(x =>
                                x.ZoneId == coveredZoneId &&
                                teamIds.Contains(x.TeamId) &&
                                teamById.ContainsKey(x.TeamId))
                            .Select(x => x.TeamId)
                            .ToHashSet();

                        if (matchingTeamIds.Count == 0)
                            continue;

                        reason =
                            "Hay cobertura, pero ningún agente autorizado " +
                            "está disponible con capacidad.";

                        var candidates = members
                            .Where(x =>
                                matchingTeamIds.Contains(x.TeamId) &&
                                x.IsAvailable &&
                                x.AcceptsAutomaticAssignments &&
                                x.MaxOpenTickets > 0 &&
                                eligibleSet.Contains(x.UserId) &&
                                activeSet.Contains(x.UserId) &&
                                loads.GetValueOrDefault(x.UserId) <
                                    x.MaxOpenTickets)
                            .GroupBy(x => x.UserId)
                            .Select(x => x.First())
                            .ToList();

                        if (candidates.Count == 0)
                            continue;

                        availableAgents = candidates.Count;
                        var firstTeamId = candidates[0].TeamId;
                        selectedTeam = teamById[firstTeamId].Name;
                        coveredByZone = zoneById[coveredZoneId].Name;
                        reason = string.Empty;
                        break;
                    }

                    if (availableAgents > 0)
                        break;
                }

                if (availableAgents > 0)
                    readyCount++;
                else
                    uncoveredCount++;

                rows.Add(new
                {
                    zoneId = zone.Id,
                    zoneName = zone.Name,
                    zoneType = zone.Type,
                    category,
                    usersInZone = assignedUsers,
                    ready = availableAgents > 0,
                    availableAgents,
                    selectedTeam,
                    coveredByZone,
                    reason
                });
            }
        }

        return Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            totalZones = zones.Count,
            totalCategories = categories.Length,
            readyCount,
            uncoveredCount,
            rows
        });
    }

    private bool CanManage() =>
        User.Claims.Any(claim =>
            claim.Type == "permission" &&
            (string.Equals(
                 claim.Value,
                 "helpdesk.manage",
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 claim.Value,
                 "settings.manage",
                 StringComparison.OrdinalIgnoreCase)));
}