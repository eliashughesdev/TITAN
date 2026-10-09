using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    /// <summary>
    /// Enrich missing context, then use the single transactional write path.
    /// Fallback relaxes ranking tiers, never eligibility, coverage or capacity.
    /// </summary>
    public async Task<bool> RetryAutomaticAssignmentWithFallbackAsync(
        Guid organizationId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _db.HelpdeskTickets.AsNoTracking().FirstOrDefaultAsync(
            x => x.OrganizationId == organizationId && x.Id == ticketId,
            cancellationToken);

        if (ticket is null || ticket.AssigneeUserId.HasValue ||
            ticket.Status is "resolved" or "closed" or "pendinguser")
        {
            return false;
        }

        if (!await ValidateEmailRequesterIdentityAsync(organizationId, ticket, cancellationToken))
        {
            return await RetryAutomaticAssignmentEnterpriseAsync(
                organizationId, ticketId, cancellationToken);
        }

        await EnrichRoutingContextAsync(ticket, cancellationToken);
        return await RetryAutomaticAssignmentEnterpriseAsync(
            organizationId, ticketId, cancellationToken);
    }

    private async Task EnrichRoutingContextAsync(
        HelpdeskTicket ticket,
        CancellationToken cancellationToken)
    {
        var requester = await _db.Users.AsNoTracking().FirstOrDefaultAsync(
            x => x.OrganizationId == ticket.OrganizationId &&
                 x.Id == ticket.RequesterUserId && x.IsActive, cancellationToken);

        var device = ticket.DeviceId.HasValue
            ? await _db.Devices.AsNoTracking().FirstOrDefaultAsync(
                x => x.OrganizationId == ticket.OrganizationId &&
                     x.Id == ticket.DeviceId && !x.IsDeleted, cancellationToken)
            : null;

        var siteId = ticket.SiteId ?? requester?.SiteId ?? device?.SiteId;
        var locationId = ticket.SiteId.HasValue ? ticket.SiteLocationId
            : requester?.SiteId.HasValue == true ? requester.SiteLocationId
            : device?.SiteLocationId;

        if (siteId.HasValue && !await _db.Sites.AnyAsync(
                x => x.OrganizationId == ticket.OrganizationId && x.Id == siteId && x.IsActive,
                cancellationToken))
        {
            siteId = null;
            locationId = null;
        }

        if (locationId.HasValue && !await _db.SiteLocations.AnyAsync(
                x => x.OrganizationId == ticket.OrganizationId && x.Id == locationId &&
                     x.SiteId == siteId && x.IsActive, cancellationToken))
        {
            locationId = null;
        }

        // Do not pin an arbitrary group when multiple groups match a category:
        // coverage and fair workload ranking must decide between them.
        var teamId = ticket.RequestedTeamId;
        if (!teamId.HasValue && NormalizeRoutingCategory(ticket.Category) == "general")
        {
            var teams = await _db.HelpdeskTeams.AsNoTracking().Where(
                x => x.OrganizationId == ticket.OrganizationId && x.IsActive)
                .ToListAsync(cancellationToken);
            var corporate = NormalizeFallbackText(string.Join(" ", requester?.JobTitle, device?.Department));
            var text = NormalizeFallbackText(string.Join(" ", ticket.Subject, ticket.Description));
            var scored = teams.Select(team => new
            {
                team.Id,
                Score = ScoreTeam(team, corporate) * 3 + ScoreTeam(team, text)
            }).Where(x => x.Score > 0).OrderByDescending(x => x.Score).ToArray();

            if (scored.Length > 0 && (scored.Length == 1 || scored[0].Score > scored[1].Score))
            {
                var candidateTeam = scored[0].Id;
                // A contextual hint must not pin a group that cannot cover the site.
                if (siteId.HasValue && (await LoadRankedCoveragesAsync(
                    ticket.OrganizationId, siteId.Value, locationId, "general",
                    [candidateTeam], cancellationToken)).Count > 0)
                {
                    teamId = candidateTeam;
                }
            }
        }

        if (siteId == ticket.SiteId && locationId == ticket.SiteLocationId && teamId == ticket.RequestedTeamId)
            return;

        // Optimistic compare-and-set prevents overwriting a concurrent operator,
        // enrichment worker, assignment or ticket transition.
        var changed = await _db.HelpdeskTickets.Where(x =>
                x.OrganizationId == ticket.OrganizationId && x.Id == ticket.Id &&
                x.UpdatedAtUtc == ticket.UpdatedAtUtc && x.AssigneeUserId == null &&
                x.Status != "closed" && x.Status != "resolved" && x.Status != "pendinguser")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SiteId, siteId)
                .SetProperty(x => x.SiteLocationId, locationId)
                .SetProperty(x => x.RequestedTeamId, teamId)
                .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);

        if (changed == 1)
        {
            _db.HelpdeskTicketEvents.Add(new HelpdeskTicketEvent(
                ticket.OrganizationId, ticket.Id, null, "routing_context_enriched",
                "Contexto de routing enriquecido desde identidad corporativa, dispositivo o palabras clave."));
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static int ScoreTeam(HelpdeskTeam team, string text)
    {
        var name = NormalizeFallbackText(team.Name);
        var score = name.Length > 3 && text.Contains(name, StringComparison.Ordinal) ? 100 : 0;
        foreach (var category in team.Categories.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = NormalizeFallbackText(category);
            if (normalized.Length > 2 && text.Contains(normalized, StringComparison.Ordinal))
                score += 25;
        }
        return score;
    }

    private static string NormalizeFallbackText(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ü", "u").Replace("ñ", "n");
}
