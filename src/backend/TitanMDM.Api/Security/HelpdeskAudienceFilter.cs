using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Security;

public sealed class HelpdeskAudienceFilter(
    TitanMdmDbContext db)
    : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> StaffPermissions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "helpdesk.view",
            "tickets.comment",
            "tickets.assign",
            "tickets.close",
            "helpdesk.manage",
            "settings.manage"
        };

    public async Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not
            ControllerActionDescriptor action)
        {
            return;
        }

        // Las API personales /api/my/helpdesk mantienen
        // sus comprobaciones de propiedad del ticket.
        // Las API operativas /api/helpdesk quedan
        // restringidas por defecto.
        if (!context.HttpContext.Request.Path
            .StartsWithSegments("/api/helpdesk"))
        {
            return;
        }

        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var isStaff = user.Claims.Any(
            claim =>
                claim.Type == "permission" &&
                StaffPermissions.Contains(claim.Value));

        if (isStaff)
        {
            // No concede permisos adicionales.
            // El controlador debe autorizar la operación.
            return;
        }

        var isGet = HttpMethods.IsGet(
            context.HttpContext.Request.Method);

        // Catálogo necesario para crear solicitudes.
        if (isGet &&
            action.ControllerName == "HelpdeskCategories")
        {
            return;
        }

        // Solo informa si el administrador habilitó
        // el asistente para el usuario de la sesión.
        if (isGet &&
            action.ControllerName == "HelpdeskOperations" &&
            action.ActionName == "GetMyAssistantAccess")
        {
            return;
        }

        // Los adjuntos son compartidos por ambas interfaces.
        // El solicitante solo puede acceder a su propio ticket.
        if (action.ControllerName == "HelpdeskAttachments")
        {
            var organizationClaim =
                user.FindFirstValue("organization_id") ??
                user.FindFirstValue("organizationId");

            var userClaim =
                user.FindFirstValue(ClaimTypes.NameIdentifier) ??
                user.FindFirstValue("sub") ??
                user.FindFirstValue("user_id") ??
                user.FindFirstValue("userId");

            var ticketClaim =
                context.RouteData.Values["ticketId"]?
                    .ToString();

            if (Guid.TryParse(
                    organizationClaim,
                    out var organizationId) &&
                Guid.TryParse(
                    userClaim,
                    out var userId) &&
                Guid.TryParse(
                    ticketClaim,
                    out var ticketId))
            {
                var ownsTicket = await db.HelpdeskTickets
                    .AsNoTracking()
                    .AnyAsync(
                        ticket =>
                            ticket.Id == ticketId &&
                            ticket.OrganizationId ==
                                organizationId &&
                            ticket.RequesterUserId == userId,
                        context.HttpContext.RequestAborted);

                if (ownsTicket)
                {
                    // El controlador mantiene validaciones
                    // de archivos, tamaño y operación.
                    return;
                }
            }

            // No revela si existe un ticket de otra persona.
            context.Result = new NotFoundResult();
            return;
        }

        context.Result = new ForbidResult();
    }
}