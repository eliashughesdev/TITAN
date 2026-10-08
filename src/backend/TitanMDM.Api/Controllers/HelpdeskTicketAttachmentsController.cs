using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route(
    "api/helpdesk/tickets/{ticketId:guid}/attachments")]
public sealed class HelpdeskTicketAttachmentsController
    : ControllerBase
{
    // ============================================================
    // PERMISSIONS
    // ============================================================

    private const string PortalAccess =
        "helpdesk.portal.access";

    private const string RequestOwnView =
        "helpdesk.request.own.view";

    private const string TicketDetails =
        "helpdesk.ticket.details.view";

    private const string AgentAccess =
        "helpdesk.agent.access";

    private const string AdminAccess =
        "helpdesk.admin.access";

    private const string TicketInternalNote =
        "helpdesk.ticket.internal-note";

    private const string LegacyHelpdeskView =
        "helpdesk.view";

    private const string LegacyHelpdeskManage =
        "helpdesk.manage";

    private const string LegacyTicketsView =
        "tickets.view";

    private const string LegacyTicketsComment =
        "tickets.comment";

    // ============================================================
    // DEPENDENCIES
    // ============================================================

    private readonly TitanMdmDbContext
        _db;

    private readonly IScopeAccessService
        _scopeAccessService;

    private readonly string
        _root;

    public HelpdeskTicketAttachmentsController(
        TitanMdmDbContext db,
        IScopeAccessService scopeAccessService,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _db =
            db;

        _scopeAccessService =
            scopeAccessService;

        var configured =
            configuration[
                "HelpdeskAttachments:StoragePath"];

        _root =
            string.IsNullOrWhiteSpace(
                configured)
                ? Path.Combine(
                    environment.ContentRootPath,
                    "App_Data",
                    "helpdesk-attachments")
                : Path.GetFullPath(
                    configured);
    }

    // ============================================================
    // LIST
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> List(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var identity =
            GetIdentity();

        if (
            identity is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "La sesión no contiene una identidad válida."
                });
        }

        var access =
            await GetTicketAccessAsync(
                identity.Value,
                ticketId,
                cancellationToken);

        if (
            !access.Exists)
        {
            return NotFound(
                new
                {
                    message =
                        "El ticket no existe."
                });
        }

        if (
            !access.CanView)
        {
            return Forbid();
        }

        var canViewInternal =
            CanViewInternal();

        var items =
            await _db
                .Set<HelpdeskTicketAttachment>()
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            identity.Value.OrganizationId
                        &&
                        x.TicketId ==
                            ticketId
                        &&
                        (
                            !x.IsInternal
                            ||
                            canViewInternal
                        ))
                .OrderByDescending(
                    x =>
                        x.IsInline)
                .ThenBy(
                    x =>
                        x.CreatedAtUtc)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.FileName,
                            x.ContentType,
                            x.SizeBytes,
                            x.IsInternal,
                            x.IsInline,
                            x.ContentId,
                            x.CreatedAtUtc,

                            canPreview =
                                x.ContentType
                                    .StartsWith(
                                        "image/")
                                ||
                                x.ContentType ==
                                    "application/pdf"
                        })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            items);
    }

    // ============================================================
    // PREVIEW
    // ============================================================

    [HttpGet(
        "{attachmentId:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        return await OpenFileAsync(
            ticketId,
            attachmentId,
            false,
            cancellationToken);
    }

    // ============================================================
    // DOWNLOAD
    // ============================================================

    [HttpGet(
        "{attachmentId:guid}/download")]
    public async Task<IActionResult> Download(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        return await OpenFileAsync(
            ticketId,
            attachmentId,
            true,
            cancellationToken);
    }

    // ============================================================
    // FILE
    // ============================================================

    private async Task<IActionResult>
        OpenFileAsync(
            Guid ticketId,
            Guid attachmentId,
            bool download,
            CancellationToken cancellationToken)
    {
        var identity =
            GetIdentity();

        if (
            identity is null)
        {
            return Unauthorized();
        }

        var access =
            await GetTicketAccessAsync(
                identity.Value,
                ticketId,
                cancellationToken);

        if (
            !access.Exists)
        {
            return NotFound();
        }

        if (
            !access.CanView)
        {
            return Forbid();
        }

        var attachment =
            await _db
                .Set<HelpdeskTicketAttachment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            identity.Value.OrganizationId
                        &&
                        x.TicketId ==
                            ticketId
                        &&
                        x.Id ==
                            attachmentId,
                    cancellationToken);

        if (
            attachment is null)
        {
            return NotFound(
                new
                {
                    message =
                        "El adjunto no existe."
                });
        }

        if (
            attachment.IsInternal
            &&
            !CanViewInternal())
        {
            return Forbid();
        }

        var directory =
            Path.Combine(
                _root,
                identity.Value
                    .OrganizationId
                    .ToString("N"),
                ticketId
                    .ToString("N"));

        var fullDirectory =
            Path.GetFullPath(
                directory);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    fullDirectory,
                    attachment.StorageName));

        var expectedPrefix =
            fullDirectory
            +
            Path.DirectorySeparatorChar;

        if (
            !fullPath.StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(
                new
                {
                    message =
                        "La ruta física del adjunto no es válida."
                });
        }

        if (
            !System.IO.File.Exists(
                fullPath))
        {
            return NotFound(
                new
                {
                    message =
                        "El archivo está registrado, pero no existe en almacenamiento."
                });
        }

        var stream =
            new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync:
                    true);

        /*
         * Seguridad para contenido servido
         * directamente al navegador.
         */
        Response.Headers[
            "X-Content-Type-Options"] =
            "nosniff";

        Response.Headers[
            "Cache-Control"] =
            "private, max-age=300";

        if (
            download)
        {
            return File(
                stream,
                attachment.ContentType,
                attachment.FileName,
                enableRangeProcessing:
                    true);
        }

        return File(
            stream,
            attachment.ContentType,
            enableRangeProcessing:
                true);
    }

    // ============================================================
    // ACCESS
    // ============================================================

    private async Task<TicketAccessResult>
        GetTicketAccessAsync(
            (
                Guid OrganizationId,
                Guid UserId
            ) identity,
            Guid ticketId,
            CancellationToken cancellationToken)
    {
        var ticket =
            await _db
                .HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            identity.OrganizationId
                        &&
                        x.Id ==
                            ticketId)
                .Select(
                    x =>
                        new
                        {
                            x.RequesterUserId
                        })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (
            ticket is null)
        {
            return new TicketAccessResult(
                false,
                false);
        }

        /*
         * Solicitante viendo SU ticket.
         */
        if (
            ticket.RequesterUserId ==
                identity.UserId
            &&
            HasAnyPermission(
                PortalAccess,
                RequestOwnView,
                AdminAccess,
                LegacyHelpdeskManage))
        {
            return new TicketAccessResult(
                true,
                true);
        }

        /*
         * Consola TIC.
         */
        if (
            !HasAnyPermission(
                TicketDetails,
                AgentAccess,
                AdminAccess,
                LegacyHelpdeskView,
                LegacyTicketsView,
                LegacyHelpdeskManage))
        {
            return new TicketAccessResult(
                true,
                false);
        }

        /*
         * Mismo modelo de scopes utilizado
         * por el resto del Helpdesk.
         */
        var canAccess =
            await _scopeAccessService
                .CanAccessTicketAsync(
                    identity.OrganizationId,
                    identity.UserId,
                    ticketId,
                    cancellationToken);

        return new TicketAccessResult(
            true,
            canAccess);
    }

    private bool CanViewInternal()
    {
        return HasAnyPermission(
            TicketInternalNote,
            AgentAccess,
            AdminAccess,
            LegacyTicketsComment,
            LegacyHelpdeskManage);
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

    private bool HasAnyPermission(
        params string[] permissions)
    {
        return permissions.Any(
            HasPermission);
    }

    // ============================================================
    // IDENTITY
    // ============================================================

    private (
        Guid OrganizationId,
        Guid UserId)?
        GetIdentity()
    {
        var organizationValue =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var userValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub")
            ??
            User.FindFirstValue(
                "user_id")
            ??
            User.FindFirstValue(
                "userId");

        if (
            !Guid.TryParse(
                organizationValue,
                out var organizationId)
            ||
            !Guid.TryParse(
                userValue,
                out var userId))
        {
            return null;
        }

        return (
            organizationId,
            userId);
    }

    private sealed record TicketAccessResult(
        bool Exists,
        bool CanView);
}