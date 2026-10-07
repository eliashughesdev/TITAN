using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed record IncomingHelpdeskAttachment(
    string SourceId,
    string FileName,
    string ContentType,
    byte[] Content,
    bool IsInline = false,
    string? ContentId = null);

public sealed class HelpdeskEmailAttachmentImportService
{
    public const long MaxAttachmentBytes =
        10L * 1024L * 1024L;

    private const long MaxTotalAttachmentBytes =
        25L * 1024L * 1024L;

    private readonly TitanMdmDbContext
        _db;

    private readonly string
        _root;

    private readonly ILogger<
        HelpdeskEmailAttachmentImportService>
        _logger;

    public HelpdeskEmailAttachmentImportService(
        TitanMdmDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<HelpdeskEmailAttachmentImportService> logger)
    {
        _db =
            db;

        _logger =
            logger;

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

    public async Task<int> ImportAsync(
        Guid organizationId,
        Guid ticketId,
        Guid uploadedByUserId,
        string internetMessageId,
        IReadOnlyCollection<IncomingHelpdeskAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        Validate(
            organizationId,
            ticketId,
            uploadedByUserId,
            internetMessageId);

        if (
            attachments is null
            ||
            attachments.Count == 0)
        {
            return 0;
        }

        var ticketExists =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            ticketId,
                    cancellationToken);

        if (!ticketExists)
        {
            throw new InvalidOperationException(
                "El ticket asociado al correo no existe.");
        }

        var directory =
            Path.Combine(
                _root,
                organizationId.ToString("N"),
                ticketId.ToString("N"));

        Directory.CreateDirectory(
            directory);

        var imported =
            0;

        long totalBytes =
            0;

        /*
         * Evita duplicados dentro del MISMO correo.
         *
         * Outlook puede devolver dos representaciones
         * del mismo CID o logo/firma.
         */
        var processedStorageNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (
            var attachment
            in attachments)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (
                attachment.Content is null
                ||
                attachment.Content.LongLength <= 0)
            {
                LogSkipped(
                    attachment,
                    "empty_content");

                continue;
            }

            if (
                attachment.Content.LongLength >
                    MaxAttachmentBytes)
            {
                LogSkipped(
                    attachment,
                    "max_individual_size");

                continue;
            }

            if (
                totalBytes +
                    attachment.Content.LongLength >
                    MaxTotalAttachmentBytes)
            {
                LogSkipped(
                    attachment,
                    "max_total_size");

                continue;
            }

            var normalized =
                NormalizeAttachment(
                    attachment);

            if (normalized is null)
            {
                LogSkipped(
                    attachment,
                    "unsupported_extension");

                continue;
            }

            if (
                !HasValidSignature(
                    normalized.Extension,
                    attachment.Content))
            {
                LogSkipped(
                    attachment,
                    "invalid_file_signature");

                continue;
            }

            var storageName =
                BuildStorageName(
                    organizationId,
                    ticketId,
                    internetMessageId,
                    attachment);

            /*
             * Duplicado dentro del lote actual.
             */
            if (
                !processedStorageNames.Add(
                    storageName))
            {
                LogSkipped(
                    attachment,
                    "duplicate_in_message");

                continue;
            }

            var alreadyExists =
                await _db
                    .Set<HelpdeskTicketAttachment>()
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.TicketId ==
                                ticketId
                            &&
                            x.StorageName ==
                                storageName,
                        cancellationToken);

            if (alreadyExists)
            {
                LogSkipped(
                    attachment,
                    "already_imported");

                continue;
            }

            var path =
                Path.Combine(
                    directory,
                    storageName);

            var fileCreated =
                false;

            try
            {
                await using (
                    var stream =
                        new FileStream(
                            path,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            81920,
                            useAsync: true))
                {
                    await stream.WriteAsync(
                        attachment.Content,
                        cancellationToken);

                    fileCreated =
                        true;
                }

                var entity =
                    new HelpdeskTicketAttachment(
                        organizationId,
                        ticketId,
                        uploadedByUserId,
                        normalized.FileName,
                        storageName,
                        normalized.ContentType,
                        attachment.Content.LongLength,
                        isInternal: false,
                        isInline:
                            attachment.IsInline,
                        contentId:
                            NormalizeContentId(
                                attachment.ContentId));

                _db
                    .Set<HelpdeskTicketAttachment>()
                    .Add(
                        entity);

                _db.HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            organizationId,
                            ticketId,
                            uploadedByUserId,
                            attachment.IsInline
                                ? "email_inline_content_received"
                                : "attachment_received_by_email",
                            attachment.IsInline
                                ? $"Contenido incrustado/firma recibido por correo: {normalized.FileName}."
                                : $"Adjunto recibido por correo: {normalized.FileName}."));

                await _db.SaveChangesAsync(
                    cancellationToken);

                imported++;

                totalBytes +=
                    attachment.Content.LongLength;

                _logger.LogInformation(
                    "Adjunto Helpdesk importado. " +
                    "Ticket={TicketId}, File={FileName}, " +
                    "Inline={Inline}, CID={ContentId}, Bytes={Bytes}.",
                    ticketId,
                    normalized.FileName,
                    attachment.IsInline,
                    attachment.ContentId,
                    attachment.Content.LongLength);
            }
            catch (IOException)
            {
                /*
                 * El nombre es determinístico.
                 * Si ya existe físicamente, otro intento
                 * probablemente ganó la carrera.
                 */
                _logger.LogInformation(
                    "Adjunto Helpdesk duplicado omitido. " +
                    "Ticket={TicketId}, Storage={StorageName}.",
                    ticketId,
                    storageName);
            }
            catch
            {
                if (
                    fileCreated
                    &&
                    File.Exists(
                        path))
                {
                    var persisted =
                        await _db
                            .Set<HelpdeskTicketAttachment>()
                            .AsNoTracking()
                            .AnyAsync(
                                x =>
                                    x.OrganizationId ==
                                        organizationId
                                    &&
                                    x.TicketId ==
                                        ticketId
                                    &&
                                    x.StorageName ==
                                        storageName,
                                CancellationToken.None);

                    if (!persisted)
                    {
                        File.Delete(
                            path);
                    }
                }

                throw;
            }
        }

        return imported;
    }

    private static void Validate(
        Guid organizationId,
        Guid ticketId,
        Guid uploadedByUserId,
        string internetMessageId)
    {
        if (
            organizationId ==
                Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.");
        }

        if (
            ticketId ==
                Guid.Empty)
        {
            throw new ArgumentException(
                "TicketId is required.");
        }

        if (
            uploadedByUserId ==
                Guid.Empty)
        {
            throw new ArgumentException(
                "UploadedByUserId is required.");
        }

        if (
            string.IsNullOrWhiteSpace(
                internetMessageId))
        {
            throw new ArgumentException(
                "InternetMessageId is required.");
        }
    }

    private void LogSkipped(
        IncomingHelpdeskAttachment attachment,
        string reason)
    {
        _logger.LogDebug(
            "Adjunto Helpdesk omitido. " +
            "File={File}, Inline={Inline}, CID={CID}, Reason={Reason}.",
            attachment.FileName,
            attachment.IsInline,
            attachment.ContentId,
            reason);
    }

    private static NormalizedAttachment?
        NormalizeAttachment(
            IncomingHelpdeskAttachment attachment)
    {
        var fileName =
            Path.GetFileName(
                    attachment.FileName
                    ??
                    string.Empty)
                .Trim();

        var contentType =
            (
                attachment.ContentType
                ??
                string.Empty
            )
            .Trim()
            .ToLowerInvariant();

        if (
            string.IsNullOrWhiteSpace(
                fileName))
        {
            fileName =
                contentType switch
                {
                    "image/png" =>
                        "image.png",

                    "image/jpeg" =>
                        "image.jpg",

                    "application/pdf" =>
                        "attachment.pdf",

                    _ =>
                        "attachment"
                };
        }

        var extension =
            Path.GetExtension(
                    fileName)
                .ToLowerInvariant();

        if (
            string.IsNullOrWhiteSpace(
                extension))
        {
            extension =
                contentType switch
                {
                    "image/png" =>
                        ".png",

                    "image/jpeg" =>
                        ".jpg",

                    "application/pdf" =>
                        ".pdf",

                    _ =>
                        string.Empty
                };

            if (
                extension.Length >
                0)
            {
                fileName +=
                    extension;
            }
        }

        extension =
            Path.GetExtension(
                    fileName)
                .ToLowerInvariant();

        var normalizedContentType =
            extension switch
            {
                ".pdf" =>
                    "application/pdf",

                ".png" =>
                    "image/png",

                ".jpg" or
                ".jpeg" or
                ".jfif" =>
                    "image/jpeg",

                _ =>
                    null
            };

        if (
            normalizedContentType
            is null)
        {
            return null;
        }

        fileName =
            LimitFileName(
                fileName,
                180);

        return new NormalizedAttachment(
            fileName,
            extension,
            normalizedContentType);
    }

    /*
     * CLAVE DE DEDUPLICACIÓN
     *
     * inline:
     *   InternetMessageId + CID
     *
     * inline sin CID:
     *   InternetMessageId + hash contenido
     *
     * archivo normal:
     *   InternetMessageId + SourceId
     *
     * Esto elimina duplicados dentro de un correo,
     * pero conserva la misma firma si aparece en
     * respuestas futuras, porque InternetMessageId cambia.
     */
    private static string BuildStorageName(
        Guid organizationId,
        Guid ticketId,
        string internetMessageId,
        IncomingHelpdeskAttachment attachment)
    {
        string attachmentKey;

        if (
            attachment.IsInline
            &&
            !string.IsNullOrWhiteSpace(
                attachment.ContentId))
        {
            attachmentKey =
                "cid:" +
                NormalizeContentId(
                    attachment.ContentId);
        }
        else if (
            attachment.IsInline)
        {
            attachmentKey =
                "hash:" +
                Convert.ToHexString(
                    SHA256.HashData(
                        attachment.Content));
        }
        else
        {
            attachmentKey =
                "source:" +
                attachment.SourceId.Trim();
        }

        var material =
            string.Join(
                "|",
                organizationId.ToString("N"),
                ticketId.ToString("N"),
                internetMessageId.Trim(),
                attachmentKey);

        var hash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        material)));

        return hash +
               ".bin";
    }

    private static string?
        NormalizeContentId(
            string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var result =
            value
                .Trim()
                .Trim('<', '>')
                .ToLowerInvariant();

        return result[
            ..Math.Min(
                result.Length,
                500)];
    }

    private static string LimitFileName(
        string fileName,
        int max)
    {
        if (
            fileName.Length <=
            max)
        {
            return fileName;
        }

        var extension =
            Path.GetExtension(
                fileName);

        var baseName =
            Path.GetFileNameWithoutExtension(
                fileName);

        var maxBase =
            Math.Max(
                1,
                max -
                extension.Length);

        return baseName[
                   ..Math.Min(
                       baseName.Length,
                       maxBase)]
               +
               extension;
    }

    private static bool HasValidSignature(
        string extension,
        byte[] bytes)
    {
        if (
            bytes.Length <
            3)
        {
            return false;
        }

        return extension switch
        {
            ".pdf" =>
                bytes.Length >= 5
                &&
                bytes
                    .AsSpan(
                        0,
                        5)
                    .SequenceEqual(
                        "%PDF-"u8),

            ".png" =>
                bytes.Length >= 8
                &&
                bytes
                    .AsSpan(
                        0,
                        8)
                    .SequenceEqual(
                        new byte[]
                        {
                            137,
                            80,
                            78,
                            71,
                            13,
                            10,
                            26,
                            10
                        }),

            ".jpg" or
            ".jpeg" or
            ".jfif" =>
                bytes[0] ==
                    0xFF
                &&
                bytes[1] ==
                    0xD8
                &&
                bytes[2] ==
                    0xFF,

            _ =>
                false
        };
    }

    private sealed record NormalizedAttachment(
        string FileName,
        string Extension,
        string ContentType);
}