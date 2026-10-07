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
public sealed class HelpdeskAttachmentsController
    : ControllerBase
{
    private const long MaxBytes =
        10L * 1024 * 1024;

    private readonly TitanMdmDbContext _db;
    private readonly string _root;

    public HelpdeskAttachmentsController(
        TitanMdmDbContext db,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _db = db;

        var configured =
            configuration[
                "HelpdeskAttachments:StoragePath"];

        _root =
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(
                    environment.ContentRootPath,
                    "App_Data",
                    "helpdesk-attachments")
                : Path.GetFullPath(configured);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(
            ticketId,
            cancellationToken);

        if (access is null)
            return NotFound();

        var query = _db
            .Set<HelpdeskTicketAttachment>()
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    access.Value.OrganizationId &&
                x.TicketId == ticketId);

        if (!access.Value.IsStaff)
        {
            query = query.Where(
                x => !x.IsInternal);
        }

        var entries = await query
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.ContentType,
                x.SizeBytes,
                x.IsInternal,
                x.UploadedByUserId,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(
        Guid ticketId,
        [FromForm] IFormFile? file,
        [FromForm] bool isInternal,
        CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(
            ticketId,
            cancellationToken);

        if (access is null)
            return NotFound();

        if (!access.Value.CanUpload)
            return Forbid();

        if (isInternal &&
            !access.Value.IsStaff)
        {
            return Forbid();
        }

        if (file is null ||
            file.Length is <= 0 or > MaxBytes)
        {
            return BadRequest(new
            {
                message =
                    "Selecciona un archivo de hasta 10 MiB."
            });
        }

        var extension =
            Path.GetExtension(file.FileName)
                .ToLowerInvariant();

        var contentType =
            extension switch
            {
                ".pdf" =>
                    "application/pdf",
                ".png" =>
                    "image/png",
                ".jpg" or ".jpeg" =>
                    "image/jpeg",
                _ => null
            };

        if (contentType is null)
        {
            return BadRequest(new
            {
                message =
                    "Solo se permiten PDF, PNG y JPEG."
            });
        }

        var fileName =
            Path.GetFileName(file.FileName)
                .Trim();

        if (string.IsNullOrWhiteSpace(
                fileName))
        {
            return BadRequest(new
            {
                message =
                    "El nombre del archivo no es válido."
            });
        }

        if (fileName.Length > 180)
        {
            fileName =
                fileName[^180..];
        }

        var storageName =
            Guid.NewGuid().ToString("N") +
            ".bin";

        var directory =
            Path.Combine(
                _root,
                access.Value.OrganizationId
                    .ToString("N"),
                ticketId.ToString("N"));

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
                        useAsync: true))
            {
                await file.CopyToAsync(
                    output,
                    cancellationToken);
            }

            if (!await HasValidSignatureAsync(
                    path,
                    extension,
                    cancellationToken))
            {
                return BadRequest(new
                {
                    message =
                        "El contenido no coincide con el tipo de archivo."
                });
            }

            var attachment =
                new HelpdeskTicketAttachment(
                    access.Value.OrganizationId,
                    ticketId,
                    access.Value.UserId,
                    fileName,
                    storageName,
                    contentType,
                    file.Length,
                    isInternal);

            _db.Set<
                HelpdeskTicketAttachment>()
                .Add(attachment);

            _db.HelpdeskTicketEvents.Add(
                new HelpdeskTicketEvent(
                    access.Value.OrganizationId,
                    ticketId,
                    access.Value.UserId,
                    "attachment_added",
                    isInternal
                        ? "TIC agregó un adjunto interno."
                        : "Se agregó un adjunto al ticket."));

            await _db.SaveChangesAsync(
                cancellationToken);

            return Ok(new
            {
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.IsInternal,
                attachment.UploadedByUserId,
                attachment.CreatedAtUtc
            });
        }
        finally
        {
            // Si la operación no produjo un registro,
            // el archivo temporal no debe permanecer
            // en el almacenamiento privado.
            var existsInDatabase =
                await _db.Set<
                    HelpdeskTicketAttachment>()
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                access.Value.OrganizationId &&
                            x.TicketId ==
                                ticketId &&
                            x.StorageName ==
                                storageName,
                        CancellationToken.None);

            if (!existsInDatabase &&
                System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> Download(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(
            ticketId,
            cancellationToken);

        if (access is null)
            return NotFound();

        var query = _db
            .Set<HelpdeskTicketAttachment>()
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    access.Value.OrganizationId &&
                x.TicketId ==
                    ticketId &&
                x.Id ==
                    attachmentId);

        if (!access.Value.IsStaff)
        {
            query = query.Where(
                x => !x.IsInternal);
        }

        var item =
            await query.FirstOrDefaultAsync(
                cancellationToken);

        if (item is null)
            return NotFound();

        var path =
            Path.Combine(
                _root,
                access.Value.OrganizationId
                    .ToString("N"),
                ticketId.ToString("N"),
                item.StorageName);

        if (!System.IO.File.Exists(path))
            return NotFound();

        Response.Headers[
            "X-Content-Type-Options"] =
            "nosniff";

        return PhysicalFile(
            path,
            "application/octet-stream",
            item.FileName);
    }

    private async Task<Access?> ResolveAccessAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var organizationClaim =
            User.FindFirstValue(
                "organization_id") ??
            User.FindFirstValue(
                "organizationId");

        var userClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue(
                "user_id") ??
            User.FindFirstValue(
                "userId");

        if (!Guid.TryParse(
                organizationClaim,
                out var organizationId) ||
            !Guid.TryParse(
                userClaim,
                out var userId))
        {
            return null;
        }

        var ticket =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.Id == ticketId)
                .Select(x => new
                {
                    x.RequesterUserId,
                    x.Status
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (ticket is null)
            return null;

        var isStaff =
            HasPermission(
                "tickets.comment") &&
            (HasPermission(
                 "tickets.view") ||
             HasPermission(
                 "helpdesk.view"));

        var isOwner =
            ticket.RequesterUserId ==
            userId;

        if (!isOwner &&
            !isStaff)
        {
            return null;
        }

        var canUpload =
            ticket.Status is not
                ("closed" or "resolved") &&
            (isOwner ||
             isStaff);

        return new Access(
            organizationId,
            userId,
            isStaff,
            canUpload);
    }

    private bool HasPermission(
        string code) =>
        User.Claims.Any(claim =>
            claim.Type == "permission" &&
            string.Equals(
                claim.Value,
                code,
                StringComparison.OrdinalIgnoreCase));

    private static async Task<bool>
        HasValidSignatureAsync(
            string path,
            string extension,
            CancellationToken cancellationToken)
    {
        var signature = new byte[8];

        await using var stream =
            System.IO.File.OpenRead(path);

        var read =
            await stream.ReadAsync(
                signature,
                cancellationToken);

        return extension switch
        {
            ".pdf" =>
                read >= 5 &&
                signature.AsSpan(0, 5)
                    .SequenceEqual(
                        "%PDF-"u8),
            ".png" =>
                read >= 8 &&
                signature.AsSpan()
                    .SequenceEqual(
                        new byte[]
                        {
                            137, 80, 78, 71,
                            13, 10, 26, 10
                        }),
            ".jpg" or ".jpeg" =>
                read >= 3 &&
                signature[0] == 0xFF &&
                signature[1] == 0xD8 &&
                signature[2] == 0xFF,
            _ => false
        };
    }

    private readonly record struct Access(
        Guid OrganizationId,
        Guid UserId,
        bool IsStaff,
        bool CanUpload);
}