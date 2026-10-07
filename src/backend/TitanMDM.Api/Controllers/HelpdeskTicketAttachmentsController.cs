using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly TitanMdmDbContext
        _db;

    private readonly string
        _root;

    public HelpdeskTicketAttachmentsController(
        TitanMdmDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _db =
            db;

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

        if (identity is null)
        {
            return Unauthorized();
        }

        if (
            !CanViewTickets())
        {
            return Forbid();
        }

        var ticketExists =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            identity.Value.OrganizationId
                        &&
                        x.Id ==
                            ticketId,
                    cancellationToken);

        if (!ticketExists)
        {
            return NotFound();
        }

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
                            ticketId)
                .OrderBy(
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
                                x.ContentType.StartsWith(
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

    [HttpGet("{attachmentId:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        return await OpenFile(
            ticketId,
            attachmentId,
            download:
                false,
            cancellationToken);
    }

    // ============================================================
    // DOWNLOAD
    // ============================================================

    [HttpGet("{attachmentId:guid}/download")]
    public async Task<IActionResult> Download(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        return await OpenFile(
            ticketId,
            attachmentId,
            download:
                true,
            cancellationToken);
    }

    // ============================================================
    // FILE
    // ============================================================

    private async Task<IActionResult> OpenFile(
        Guid ticketId,
        Guid attachmentId,
        bool download,
        CancellationToken cancellationToken)
    {
        var identity =
            GetIdentity();

        if (identity is null)
        {
            return Unauthorized();
        }

        if (!CanViewTickets())
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

        if (attachment is null)
        {
            return NotFound();
        }

        /*
         * Notas internas:
         * solo TIC/Admin.
         */
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

        var path =
            Path.Combine(
                directory,
                attachment.StorageName);

        /*
         * Protección adicional:
         * StorageName viene de BD, pero no confiamos
         * ciegamente en un path.
         */
        var fullPath =
            Path.GetFullPath(
                path);

        var fullDirectory =
            Path.GetFullPath(
                directory)
            +
            Path.DirectorySeparatorChar;

        if (
            !fullPath.StartsWith(
                fullDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest();
        }

        if (
            !System.IO.File.Exists(
                fullPath))
        {
            return NotFound(
                new
                {
                    message =
                        "El archivo existe en la base de datos pero no se encontró en almacenamiento."
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

        if (download)
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
    // SECURITY
    // ============================================================

    private bool CanViewTickets()
    {
        return HasAnyPermission(
            "helpdesk.ticket.details.view",
            "helpdesk.inbox.all",
            "helpdesk.agent.access",
            "helpdesk.admin.access",
            "helpdesk.view",
            "tickets.view",
            "helpdesk.manage");
    }

    private bool CanViewInternal()
    {
        return HasAnyPermission(
            "helpdesk.ticket.internal-note",
            "helpdesk.admin.access",
            "tickets.comment",
            "helpdesk.manage");
    }

    private bool HasAnyPermission(
        params string[] permissions)
    {
        return permissions.Any(
            permission =>
                User.Claims.Any(
                    claim =>
                        claim.Type ==
                            "permission"
                        &&
                        string.Equals(
                            claim.Value,
                            permission,
                            StringComparison.OrdinalIgnoreCase)));
    }

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
}