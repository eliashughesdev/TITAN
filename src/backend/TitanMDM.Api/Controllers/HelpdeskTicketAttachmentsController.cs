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
                            x.EmailMessageId,
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
    // UPLOAD
    // ============================================================

    private const long MaxUploadBytes =
        10L * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(
        MaxUploadBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(
        Guid ticketId,
        [FromForm] IFormFile? file,
        [FromForm] bool isInternal,
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
            !access.CanView
            ||
            !access.CanUpload)
        {
            return Forbid();
        }

        if (
            isInternal
            &&
            !CanViewInternal())
        {
            return Forbid();
        }

        if (
            file is null
            ||
            file.Length is <= 0
                or > MaxUploadBytes)
        {
            return BadRequest(
                new
                {
                    message =
                        "Selecciona un archivo de hasta 10 MiB."
                });
        }

        var extension =
            Path.GetExtension(
                file.FileName)
                .ToLowerInvariant();

        var contentType =
            extension switch
            {
                ".pdf" =>
                    "application/pdf",
                ".png" =>
                    "image/png",
                ".jpg" or ".jpeg" or ".jfif" =>
                    "image/jpeg",
                ".gif" =>
                    "image/gif",
                _ => null
            };

        if (
            contentType is null)
        {
            return BadRequest(
                new
                {
                    message =
                        "Solo se permiten PDF, PNG, JPEG y GIF."
                });
        }

        var fileName =
            Path.GetFileName(
                file.FileName)
                .Trim();

        if (
            string.IsNullOrWhiteSpace(
                fileName))
        {
            return BadRequest(
                new
                {
                    message =
                        "El nombre del archivo no es válido."
                });
        }

        if (
            fileName.Length > 180)
        {
            fileName =
                fileName[^180..];
        }

        var storageName =
            Guid.NewGuid()
                .ToString("N")
            +
            ".bin";

        var directory =
            Path.Combine(
                _root,
                identity.Value
                    .OrganizationId
                    .ToString("N"),
                ticketId
                    .ToString("N"));

        Directory.CreateDirectory(
            directory);

        var path =
            Path.Combine(
                directory,
                storageName);

        try
        {
            await using (
                var output =
                    new FileStream(
                        path,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        useAsync:
                            true))
            {
                await file.CopyToAsync(
                    output,
                    cancellationToken);
            }

            if (
                !await HasValidSignatureAsync(
                    path,
                    extension,
                    cancellationToken))
            {
                return BadRequest(
                    new
                    {
                        message =
                            "El contenido no coincide con el tipo de archivo."
                    });
            }

            var attachment =
                new HelpdeskTicketAttachment(
                    identity.Value
                        .OrganizationId,
                    ticketId,
                    identity.Value.UserId,
                    fileName,
                    storageName,
                    contentType,
                    file.Length,
                    isInternal);

            _db.Set<
                HelpdeskTicketAttachment>()
                .Add(
                    attachment);

            _db.HelpdeskTicketEvents.Add(
                new HelpdeskTicketEvent(
                    identity.Value
                        .OrganizationId,
                    ticketId,
                    identity.Value.UserId,
                    "attachment_added",
                    isInternal
                        ? "TIC agregó un adjunto interno."
                        : "Se agregó un adjunto al ticket."));

            await _db.SaveChangesAsync(
                cancellationToken);

            return Ok(
                new
                {
                    attachment.Id,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.SizeBytes,
                    attachment.IsInternal,
                    attachment.IsInline,
                    attachment.ContentId,
                    attachment.UploadedByUserId,
                    attachment.CreatedAtUtc
                });
        }
        finally
        {
            var existsInDatabase =
                await _db
                    .Set<HelpdeskTicketAttachment>()
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                identity.Value
                                    .OrganizationId
                            &&
                            x.TicketId ==
                                ticketId
                            &&
                            x.StorageName ==
                                storageName,
                        CancellationToken.None);

            if (
                !existsInDatabase
                &&
                System.IO.File.Exists(
                    path))
            {
                System.IO.File.Delete(
                    path);
            }
        }
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

    /*
     * Alias histórico utilizado por el panel del portal
     * (MyHelpdesk). Mantiene la compatibilidad sin duplicar
     * controladores ni rutas ambiguas.
     */
    [HttpGet(
        "{attachmentId:guid}")]
    public async Task<IActionResult> DownloadLegacy(
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
                            x.RequesterUserId,
                            x.Status
                        })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (
            ticket is null)
        {
            return new TicketAccessResult(
                false,
                false,
                false);
        }

        var canUpload =
            ticket.Status is not
                ("closed" or "resolved");

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
                true,
                canUpload);
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
                false,
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
            canAccess,
            canAccess && canUpload);
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
        bool CanView,
        bool CanUpload);

    private static async Task<bool>
        HasValidSignatureAsync(
            string path,
            string extension,
            CancellationToken cancellationToken)
    {
        var signature =
            new byte[8];

        await using var stream =
            System.IO.File.OpenRead(
                path);

        var read =
            await stream.ReadAsync(
                signature,
                cancellationToken);

        return extension switch
        {
            ".pdf" =>
                read >= 5
                &&
                signature
                    .AsSpan(0, 5)
                    .SequenceEqual(
                        "%PDF-"u8),

            ".png" =>
                read >= 8
                &&
                signature
                    .AsSpan()
                    .SequenceEqual(
                        new byte[]
                        {
                            137, 80, 78, 71,
                            13, 10, 26, 10
                        }),

            ".jpg"
                or ".jpeg"
                or ".jfif" =>
                read >= 3
                &&
                signature[0] == 0xFF
                &&
                signature[1] == 0xD8
                &&
                signature[2] == 0xFF,

            ".gif" =>
                read >= 6
                &&
                (
                    signature
                        .AsSpan(0, 6)
                        .SequenceEqual(
                            "GIF87a"u8)
                    ||
                    signature
                        .AsSpan(0, 6)
                        .SequenceEqual(
                            "GIF89a"u8)
                ),

            _ => false
        };
    }
}