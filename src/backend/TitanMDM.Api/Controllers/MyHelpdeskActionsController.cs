using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController, Authorize]
[Route("api/my/helpdesk/actions")]
public sealed class MyHelpdeskActionsController(
    TitanMdmDbContext db) : ControllerBase
{
    [HttpPost("{ticketId:guid}/reopen")]
    public Task<IActionResult> Reopen(
        Guid ticketId,
        [FromBody] ReasonRequest request,
        CancellationToken ct) =>
        Run(ticketId, false, request.Reason, ct);

    [HttpPost("{ticketId:guid}/confirm")]
    public Task<IActionResult> Confirm(
        Guid ticketId,
        CancellationToken ct) =>
        Run(
            ticketId, true,
            "El solicitante confirmó la solución.", ct);

    private async Task<IActionResult> Run(
        Guid id,
        bool confirm,
        string? reason,
        CancellationToken ct)
    {
        var org =
            User.FindFirstValue("organization_id") ??
            User.FindFirstValue("organizationId");

        var actor =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("user_id") ??
            User.FindFirstValue("userId");

        if (!Guid.TryParse(org, out var orgId) ||
            !Guid.TryParse(actor, out var userId))
            return Unauthorized();

        if (!await db.Users.AsNoTracking().AnyAsync(
            x => x.OrganizationId == orgId &&
                 x.Id == userId && x.IsActive, ct))
            return Unauthorized();

        reason = reason?.Trim();

        if (!confirm &&
            (reason is null || reason.Length is < 10 or > 2000))
            return BadRequest(new
            {
                message =
                    "Explica la reapertura con entre " +
                    "10 y 2000 caracteres."
            });

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            () => Change(
                id, orgId, userId, confirm, reason!, ct));
    }

    private async Task<IActionResult> Change(
        Guid id,
        Guid org,
        Guid user,
        bool confirm,
        string reason,
        CancellationToken ct)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);

        var ticket = await db.HelpdeskTickets.FirstOrDefaultAsync(
            x => x.Id == id &&
                 x.OrganizationId == org &&
                 x.RequesterUserId == user, ct);

        if (ticket is null) return NotFound();

        HelpdeskTicketEvent? activity = null;
        HelpdeskTicketComment? comment = null;

        try
        {
            if (confirm && ticket.Status != "resolved")
                return Conflict(new
                {
                    message =
                        "Solo puedes confirmar una solicitud resuelta."
                });

            if (!confirm)
            {
                if (ticket.Status is not ("resolved" or "closed"))
                    return Conflict(new
                    {
                        message = "La solicitud ya está activa."
                    });

                var settings =
                    await db.Set<HelpdeskAutomationSettings>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x => x.OrganizationId == org, ct);

                if (!ticket.ResolvedAtUtc.HasValue ||
                    ticket.ResolvedAtUtc.Value <
                        DateTime.UtcNow.AddDays(
                            -(settings?.ReopenDays ?? 7)))
                    return Conflict(new
                    {
                        message =
                            "El plazo de reapertura terminó. " +
                            "Crea una nueva solicitud."
                    });
            }

            ticket.Transition(confirm ? "closed" : "open");

            activity = new HelpdeskTicketEvent(
                org, id, user,
                confirm ? "status" : "reopened",
                confirm
                    ? "El solicitante confirmó la solución " +
                      "y cerró el ticket."
                    : "El solicitante reabrió el ticket: " +
                      reason[..Math.Min(reason.Length, 400)]);

            db.HelpdeskTicketEvents.Add(activity);

            if (!confirm)
            {
                comment = new HelpdeskTicketComment(
                    org, id, user, reason, false);

                db.HelpdeskTicketComments.Add(comment);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Ok(new { ticket.Id, ticket.Status });
        }
        finally
        {
            db.Entry(ticket).State = EntityState.Detached;

            if (activity is not null)
                db.Entry(activity).State = EntityState.Detached;

            if (comment is not null)
                db.Entry(comment).State = EntityState.Detached;
        }
    }

    public sealed record ReasonRequest(string? Reason);
}