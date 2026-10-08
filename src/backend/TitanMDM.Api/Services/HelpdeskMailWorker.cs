using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Services;

public sealed class HelpdeskMailWorker
    : BackgroundService
{
    // ============================================================
    // CONSTANTS
    // ============================================================

    private const int WorkerTickSeconds =
        10;

    private const long MaxAttachmentBytes =
        10L * 1024L * 1024L;

    private const long MaxTotalAttachmentBytes =
        25L * 1024L * 1024L;

    // ============================================================
    // DEPENDENCIES
    // ============================================================

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly IHttpClientFactory
        _httpClientFactory;

    private readonly IDataProtector
        _protector;

    private readonly IConfiguration
        _configuration;

    private readonly ILogger<HelpdeskMailWorker>
        _logger;

    public HelpdeskMailWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider protectionProvider,
        IConfiguration configuration,
        ILogger<HelpdeskMailWorker> logger)
    {
        _scopeFactory =
            scopeFactory;

        _httpClientFactory =
            httpClientFactory;

        _protector =
            protectionProvider
                .CreateProtector(
                    "TitanMDM.Helpdesk.Entra.ClientSecret.v1");

        _configuration =
            configuration;

        _logger =
            logger;
    }

    // ============================================================
    // BACKGROUND SERVICE
    // ============================================================

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "HelpdeskMailWorker iniciado.");

        while (
            !stoppingToken
                .IsCancellationRequested)
        {
            try
            {
                await ExecuteDueMailboxesAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (
                    stoppingToken
                        .IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error general del worker de correo entrante de Helpdesk.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        WorkerTickSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (
                    stoppingToken
                        .IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "HelpdeskMailWorker detenido.");
    }

    // ============================================================
    // SCHEDULER
    // ============================================================

    private async Task ExecuteDueMailboxesAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<
                    TitanMdmDbContext>();

        var now =
            DateTime.UtcNow;

        var configurations =
            await db.HelpdeskMailSettings
                .AsNoTracking()
                .Where(
                    x =>
                        x.InboundEnabled
                        &&
                        x.Mailbox != null
                        &&
                        x.ActorUserId.HasValue)
                .Select(
                    x =>
                        new
                        {
                            x.OrganizationId,
                            x.InboundPollSeconds,
                            x.LastInboundAttemptAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var dueOrganizations =
            configurations
                .Where(
                    x =>
                        !x.LastInboundAttemptAtUtc
                            .HasValue
                        ||
                        x.LastInboundAttemptAtUtc
                            .Value
                            .AddSeconds(
                                Math.Clamp(
                                    x.InboundPollSeconds,
                                    30,
                                    3600))
                        <= now)
                .Select(
                    x =>
                        x.OrganizationId)
                .ToArray();

        foreach (
            var organizationId
            in dueOrganizations)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            await ProcessOrganizationAsync(
                organizationId,
                cancellationToken);
        }
    }

    // ============================================================
    // ORGANIZATION
    // ============================================================

    private async Task ProcessOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<
                    TitanMdmDbContext>();

        var importer =
            scope.ServiceProvider
                .GetRequiredService<
                    HelpdeskEmailImportService>();

        var attachmentImporter =
            scope.ServiceProvider
                .GetRequiredService<
                    HelpdeskEmailAttachmentImportService>();

        var intakePolicy =
            scope.ServiceProvider
                .GetRequiredService<
                    HelpdeskMailIntakePolicy>();

        var settings =
            await db.HelpdeskMailSettings
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            settings is null
            ||
            !settings.InboundEnabled
            ||
            string.IsNullOrWhiteSpace(
                settings.Mailbox)
            ||
            !settings.ActorUserId
                .HasValue)
        {
            return;
        }

        var mailbox =
            settings.Mailbox
                .Trim()
                .ToLowerInvariant();

        var actorUserId =
            settings
                .ActorUserId
                .Value;

        var batchSize =
            Math.Clamp(
                settings.BatchSize,
                1,
                100);

        settings
            .MarkInboundAttempt();

        await TrySaveRuntimeStatusAsync(
            db,
            cancellationToken);

        try
        {
            // ====================================================
            // TECHNICAL USER
            // ====================================================

            var actorValid =
                await db.Users
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.Id ==
                                actorUserId
                            &&
                            x.IsActive,
                        cancellationToken);

            if (!actorValid)
            {
                throw new InvalidOperationException(
                    "El usuario técnico configurado para el buzón no existe o está inactivo.");
            }

            // ====================================================
            // ENTRA
            // ====================================================

            var entra =
                await db.EntraIdSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId,
                        cancellationToken);

            if (
                entra is null
                ||
                !entra.IsEnabled
                ||
                string.IsNullOrWhiteSpace(
                    entra.TenantId)
                ||
                string.IsNullOrWhiteSpace(
                    entra.ClientId)
                ||
                string.IsNullOrWhiteSpace(
                    entra.ClientSecretProtected))
            {
                throw new InvalidOperationException(
                    "Entra ID no está configurado o habilitado para correo Helpdesk.");
            }

            // ====================================================
            // GRAPH
            // ====================================================

            var client =
                _httpClientFactory
                    .CreateClient(
                        "entra-id");

            var secret =
                _protector
                    .Unprotect(
                        entra.ClientSecretProtected);

            var token =
                await GetTokenAsync(
                    client,
                    entra.TenantId,
                    entra.ClientId,
                    secret,
                    cancellationToken);

            var result =
                await SyncMailboxAsync(
                    db,
                    client,
                    token,
                    organizationId,
                    actorUserId,
                    mailbox,
                    settings,
                    batchSize,
                    intakePolicy,
                    importer,
                    attachmentImporter,
                    cancellationToken);

            settings
                .MarkInboundSuccess();

            await TrySaveRuntimeStatusAsync(
                db,
                cancellationToken);

            _logger.LogInformation(
                "Sincronización Helpdesk completada. " +
                "Organization={OrganizationId}, " +
                "Mailbox={Mailbox}, " +
                "Seen={Seen}, " +
                "Accepted={Accepted}, " +
                "Ignored={Ignored}, " +
                "Failed={Failed}, " +
                "Attachments={Attachments}, " +
                "Inline={Inline}.",
                organizationId,
                mailbox,
                result.MessagesSeen,
                result.MessagesAccepted,
                result.MessagesIgnored,
                result.MessagesFailed,
                result.AttachmentsImported,
                result.InlineImported);
        }
        catch (OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await ReloadMailSettingsAsync(
                db,
                settings,
                CancellationToken.None);

            settings
                .MarkInboundFailure(
                    exception.Message);

            await TrySaveRuntimeStatusAsync(
                db,
                CancellationToken.None);

            _logger.LogError(
                exception,
                "No se pudo sincronizar correo entrante. " +
                "Organization={OrganizationId}, Mailbox={Mailbox}.",
                organizationId,
                mailbox);
        }
    }

    // ============================================================
    // MAILBOX DELTA
    // ============================================================

    private async Task<MailboxSyncResult>
        SyncMailboxAsync(
            TitanMdmDbContext db,
            HttpClient client,
            string token,
            Guid organizationId,
            Guid actorUserId,
            string mailbox,
            HelpdeskMailSettings settings,
            int batchSize,
            HelpdeskMailIntakePolicy intakePolicy,
            HelpdeskEmailImportService importer,
            HelpdeskEmailAttachmentImportService attachmentImporter,
            CancellationToken cancellationToken)
    {
        var messagesSeen =
            0;

        var messagesAccepted =
            0;

        var messagesIgnored =
            0;

        var messagesFailed =
            0;

        var attachmentsImported =
            0;

        var inlineImported =
            0;

        // ========================================================
        // CURSOR
        // ========================================================

        var cursorDirectory =
            ResolveCursorDirectory();

        Directory.CreateDirectory(
            cursorDirectory);

        var mailboxKey =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8
                        .GetBytes(
                            mailbox)))[..16];

        var cursorFile =
            Path.Combine(
                cursorDirectory,
                $"{organizationId:N}-{mailboxKey}-helpdesk-mail.txt");

        var initialUrl =
            $"https://graph.microsoft.com/v1.0/users/" +
            $"{Uri.EscapeDataString(mailbox)}" +
            "/mailFolders/inbox/messages/delta" +
            "?$select=" +
            "id," +
            "internetMessageId," +
            "conversationId," +
            "from," +
            "toRecipients," +
            "ccRecipients," +
            "receivedDateTime," +
            "subject," +
            "hasAttachments" +
            $"&$top={batchSize}";

        var url =
            File.Exists(
                cursorFile)
                ? (
                    await File.ReadAllTextAsync(
                        cursorFile,
                        cancellationToken)
                  )
                  .Trim()
                : initialUrl;

        if (
            string.IsNullOrWhiteSpace(
                url))
        {
            url =
                initialUrl;
        }

        while (
            !cancellationToken
                .IsCancellationRequested)
        {
            EnsureGraphUrl(
                url);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url);

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);

            /*
             * Conservamos BODY TEXT para evitar introducir
             * HTML externo directamente en los tickets.
             *
             * Las imágenes inline se almacenan por separado.
             */
            request.Headers
                .TryAddWithoutValidation(
                    "Prefer",
                    "outlook.body-content-type=\"text\"");

            using var response =
                await client.SendAsync(
                    request,
                    cancellationToken);

            if (
                !response
                    .IsSuccessStatusCode)
            {
                var errorBody =
                    await SafeReadContentAsync(
                        response,
                        cancellationToken);

                throw new InvalidOperationException(
                    $"Microsoft Graph devolvió HTTP {(int)response.StatusCode} " +
                    $"al consultar el buzón. {TrimDiagnostic(errorBody)}");
            }

            using var document =
                JsonDocument.Parse(
                    await response.Content
                        .ReadAsStringAsync(
                            cancellationToken));

            if (
                !document.RootElement
                    .TryGetProperty(
                        "value",
                        out var value)
                ||
                value.ValueKind !=
                    JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "Microsoft Graph devolvió una respuesta delta inválida.");
            }

            foreach (
                var item
                in value.EnumerateArray())
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                if (
                    item.TryGetProperty(
                        "@removed",
                        out _)
                    ||
                    !item.TryGetProperty(
                        "id",
                        out var idElement)
                    ||
                    idElement.GetString()
                    is not { Length: > 0 }
                        graphId)
                {
                    continue;
                }

                messagesSeen++;

                GraphInboundMessage? inbound;

                try
                {
                    inbound =
                        await GetMessageAsync(
                            client,
                            token,
                            mailbox,
                            graphId,
                            cancellationToken);
                }
                catch (Exception exception)
                    when (
                        exception is not
                            OperationCanceledException)
                {
                    messagesFailed++;

                    _logger.LogError(
                        exception,
                        "Error leyendo mensaje Graph. " +
                        "Mailbox={Mailbox}, GraphId={GraphId}.",
                        mailbox,
                        graphId);

                    throw;
                }

                if (inbound is null)
                {
                    messagesIgnored++;

                    continue;
                }

                // ====================================================
                // PROCESSING IDEMPOTENCY
                // ====================================================

                var alreadyProcessed =
                    await db.HelpdeskMailProcessingLogs
                        .AsNoTracking()
                        .AnyAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.Mailbox ==
                                    mailbox
                                &&
                                x.InternetMessageId ==
                                    inbound.Message
                                        .InternetMessageId,
                            cancellationToken);

                if (alreadyProcessed)
                {
                    continue;
                }

                // ====================================================
                // INTAKE POLICY
                // ====================================================

                var decision =
                    intakePolicy
                        .Evaluate(
                            settings,
                            inbound.Message);

                if (!decision.Accepted)
                {
                    messagesIgnored++;

                    await SaveProcessingLogAsync(
                        db,
                        new HelpdeskMailProcessingLog(
                            organizationId,
                            mailbox,
                            inbound.Message
                                .InternetMessageId,
                            inbound.Message
                                .ConversationId,
                            inbound.Message
                                .FromEmail,
                            inbound.Message
                                .Subject,
                            "ignored",
                            decision.Code,
                            decision.Reason,
                            inbound.Message
                                .ReceivedAtUtc),
                        cancellationToken);

                    _logger.LogInformation(
                        "Correo ignorado por política Helpdesk. " +
                        "Mailbox={Mailbox}, " +
                        "From={From}, " +
                        "Subject={Subject}, " +
                        "Code={Code}, " +
                        "Reason={Reason}.",
                        mailbox,
                        inbound.Message
                            .FromEmail,
                        inbound.Message
                            .Subject,
                        decision.Code,
                        decision.Reason);

                    continue;
                }

                // ====================================================
                // IMPORT
                // ====================================================

                try
                {
                    var ticketId =
                        await importer
                            .ImportAsync(
                                organizationId,
                                actorUserId,
                                inbound.Message,
                                cancellationToken);

                    if (
                        inbound.Attachments.Count >
                        0)
                    {
                        var imported =
                            await attachmentImporter
                                .ImportAsync(
                                    organizationId,
                                    ticketId,
                                    actorUserId,
                                    inbound.Message
                                        .InternetMessageId,
                                    inbound.Attachments,
                                    cancellationToken);

                        attachmentsImported +=
                            imported;

                        inlineImported +=
                            inbound.Attachments
                                .Count(
                                    x =>
                                        x.IsInline);

                        _logger.LogInformation(
                            "Adjuntos procesados para ticket {TicketId}. " +
                            "Disponibles={Available}, Importados={Imported}, Inline={Inline}.",
                            ticketId,
                            inbound.Attachments.Count,
                            imported,
                            inbound.Attachments
                                .Count(
                                    x =>
                                        x.IsInline));
                    }

                    if (
                        inbound.SkippedAttachments >
                        0)
                    {
                        _logger.LogWarning(
                            "{SkippedCount} adjunto(s) fueron omitidos " +
                            "antes del importador para ticket {TicketId}.",
                            inbound.SkippedAttachments,
                            ticketId);
                    }

                    await SaveProcessingLogAsync(
                        db,
                        new HelpdeskMailProcessingLog(
                            organizationId,
                            mailbox,
                            inbound.Message
                                .InternetMessageId,
                            inbound.Message
                                .ConversationId,
                            inbound.Message
                                .FromEmail,
                            inbound.Message
                                .Subject,
                            "accepted",
                            "accepted",
                            "Mensaje aceptado y procesado por Mesa de Ayuda.",
                            inbound.Message
                                .ReceivedAtUtc,
                            ticketId),
                        cancellationToken);

                    messagesAccepted++;
                }
                catch (OperationCanceledException)
                    when (
                        cancellationToken
                            .IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    messagesFailed++;

                    await SaveProcessingLogAsync(
                        db,
                        new HelpdeskMailProcessingLog(
                            organizationId,
                            mailbox,
                            inbound.Message
                                .InternetMessageId,
                            inbound.Message
                                .ConversationId,
                            inbound.Message
                                .FromEmail,
                            inbound.Message
                                .Subject,
                            "failed",
                            "processing_error",
                            TrimDiagnostic(
                                exception.Message),
                            inbound.Message
                                .ReceivedAtUtc),
                        CancellationToken.None);

                    _logger.LogError(
                        exception,
                        "Error procesando mensaje Helpdesk. " +
                        "Mailbox={Mailbox}, MessageId={MessageId}.",
                        mailbox,
                        inbound.Message
                            .InternetMessageId);

                    /*
                     * No confirmamos silenciosamente el delta
                     * si el proceso real del ticket falla.
                     */
                    throw;
                }
            }

            // ========================================================
            // DELTA CHECKPOINT
            // ========================================================

            var next =
                document.RootElement
                    .TryGetProperty(
                        "@odata.nextLink",
                        out var nextLink)
                    ? nextLink.GetString()
                    : null;

            var delta =
                document.RootElement
                    .TryGetProperty(
                        "@odata.deltaLink",
                        out var deltaLink)
                    ? deltaLink.GetString()
                    : null;

            var checkpoint =
                next
                ??
                delta
                ??
                throw new InvalidOperationException(
                    "Microsoft Graph no devolvió cursor delta.");

            EnsureGraphUrl(
                checkpoint);

            var temporaryFile =
                cursorFile +
                ".tmp";

            await File.WriteAllTextAsync(
                temporaryFile,
                checkpoint,
                cancellationToken);

            File.Move(
                temporaryFile,
                cursorFile,
                overwrite:
                    true);

            if (next is null)
            {
                break;
            }

            url =
                next;
        }

        return new MailboxSyncResult(
            messagesSeen,
            messagesAccepted,
            messagesIgnored,
            messagesFailed,
            attachmentsImported,
            inlineImported);
    }

    // ============================================================
    // MESSAGE
    // ============================================================

    private static async Task<GraphInboundMessage?>
        GetMessageAsync(
            HttpClient client,
            string token,
            string mailbox,
            string graphId,
            CancellationToken cancellationToken)
    {
        var url =
            $"https://graph.microsoft.com/v1.0/users/" +
            $"{Uri.EscapeDataString(mailbox)}/messages/" +
            $"{Uri.EscapeDataString(graphId)}" +
            "?$select=" +
            "internetMessageId," +
            "conversationId," +
            "from," +
            "toRecipients," +
            "ccRecipients," +
            "replyTo," +
            "receivedDateTime," +
            "internetMessageHeaders," +
            "subject," +
            "body," +
            "hasAttachments";

        EnsureGraphUrl(
            url);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        request.Headers
            .TryAddWithoutValidation(
                "Prefer",
                "outlook.body-content-type=\"text\"");

        using var response =
            await client.SendAsync(
                request,
                cancellationToken);

        if (
            response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            return null;
        }

        if (
            !response
                .IsSuccessStatusCode)
        {
            var errorBody =
                await SafeReadContentAsync(
                    response,
                    cancellationToken);

            throw new InvalidOperationException(
                $"Microsoft Graph devolvió HTTP {(int)response.StatusCode} " +
                $"al leer un mensaje. {TrimDiagnostic(errorBody)}");
        }

        using var document =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken));

        var root =
            document.RootElement;

        // ========================================================
        // FROM
        // ========================================================

        if (
            !root.TryGetProperty(
                "from",
                out var fromElement)
            ||
            fromElement.ValueKind !=
                JsonValueKind.Object
            ||
            !fromElement.TryGetProperty(
                "emailAddress",
                out var from)
            ||
            from.ValueKind !=
                JsonValueKind.Object)
        {
            return null;
        }

        var sender =
            from.TryGetProperty(
                "address",
                out var address)
                ? address
                    .GetString()?
                    .Trim()
                    .ToLowerInvariant()
                : null;

        /*
         * Evita loops con el buzón.
         */
        if (
            string.IsNullOrWhiteSpace(
                sender)
            ||
            string.Equals(
                sender,
                mailbox,
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return null;
        }

        // ========================================================
        // INTERNET MESSAGE ID
        // ========================================================

        var internetMessageId =
            root.TryGetProperty(
                "internetMessageId",
                out var messageId)
                ? messageId.GetString()
                : null;

        if (
            string.IsNullOrWhiteSpace(
                internetMessageId))
        {
            throw new InvalidOperationException(
                "El correo no contiene internetMessageId.");
        }

        // ========================================================
        // DATE
        // ========================================================

        DateTime?
            receivedAtUtc =
                null;

        if (
            root.TryGetProperty(
                "receivedDateTime",
                out var receivedElement)
            &&
            receivedElement.ValueKind ==
                JsonValueKind.String
            &&
            DateTimeOffset.TryParse(
                receivedElement.GetString(),
                out var parsedReceived))
        {
            receivedAtUtc =
                parsedReceived
                    .UtcDateTime;
        }

        // ========================================================
        // BODY
        // ========================================================

        var bodyText =
            root.TryGetProperty(
                "body",
                out var body)
            &&
            body.ValueKind ==
                JsonValueKind.Object
            &&
            body.TryGetProperty(
                "content",
                out var content)
                ? content.GetString()
                    ??
                    string.Empty
                : string.Empty;

        // ========================================================
        // INCOMING MESSAGE
        // ========================================================

        var incoming =
            new IncomingHelpdeskEmail(
                mailbox,
                internetMessageId,
                root.TryGetProperty(
                    "conversationId",
                    out var conversation)
                    ? conversation.GetString()
                    : null,
                sender,
                from.TryGetProperty(
                    "name",
                    out var name)
                    ? name.GetString()
                    : null,
                ReadRecipients(
                    root,
                    "toRecipients"),
                ReadRecipients(
                    root,
                    "ccRecipients"),
                ReadRecipients(
                    root,
                    "replyTo"),
                root.TryGetProperty(
                    "subject",
                    out var subject)
                    ? subject.GetString()
                        ??
                        string.Empty
                    : string.Empty,
                bodyText,
                receivedAtUtc,
                ReadInternetHeader(
                    root,
                    "Auto-Submitted"),
                ReadInternetHeader(
                    root,
                    "Precedence"));

        // ========================================================
        // ATTACHMENTS
        //
        // IMPORTANTE:
        //
        // No dependemos de hasAttachments porque Microsoft Graph
        // puede reportar FALSE cuando el mensaje contiene solamente
        // imágenes inline, por ejemplo logos/firma corporativa.
        // ========================================================

        var attachments =
            await GetAttachmentsAsync(
                client,
                token,
                mailbox,
                graphId,
                cancellationToken);

        return new GraphInboundMessage(
            incoming,
            attachments.Items,
            attachments.Skipped);
    }

    // ============================================================
    // RECIPIENTS
    // ============================================================

    private static IReadOnlyList<string>
        ReadRecipients(
            JsonElement root,
            string propertyName)
    {
        if (
            !root.TryGetProperty(
                propertyName,
                out var recipients)
            ||
            recipients.ValueKind !=
                JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var addresses =
            new List<string>();

        foreach (
            var recipient
            in recipients
                .EnumerateArray())
        {
            if (
                !recipient.TryGetProperty(
                    "emailAddress",
                    out var emailAddress)
                ||
                emailAddress.ValueKind !=
                    JsonValueKind.Object
                ||
                !emailAddress.TryGetProperty(
                    "address",
                    out var addressElement))
            {
                continue;
            }

            var address =
                addressElement
                    .GetString()?
                    .Trim()
                    .ToLowerInvariant();

            if (
                !string.IsNullOrWhiteSpace(
                    address))
            {
                addresses.Add(
                    address);
            }
        }

        return addresses
            .Distinct(
                StringComparer
                    .OrdinalIgnoreCase)
            .ToArray();
    }

    // ============================================================
    // INTERNET HEADERS
    // ============================================================

    private static string?
        ReadInternetHeader(
            JsonElement root,
            string headerName)
    {
        if (
            !root.TryGetProperty(
                "internetMessageHeaders",
                out var headers)
            ||
            headers.ValueKind !=
                JsonValueKind.Array)
        {
            return null;
        }

        foreach (
            var header
            in headers
                .EnumerateArray())
        {
            if (
                !header.TryGetProperty(
                    "name",
                    out var name)
                ||
                !header.TryGetProperty(
                    "value",
                    out var value))
            {
                continue;
            }

            if (
                string.Equals(
                    name.GetString(),
                    headerName,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return value
                    .GetString()?
                    .Trim();
            }
        }

        return null;
    }

    // ============================================================
    // ATTACHMENT COLLECTION
    // ============================================================

    private static async Task<GraphAttachmentResult>
     GetAttachmentsAsync(
         HttpClient client,
         string token,
         string mailbox,
         string graphId,
         CancellationToken cancellationToken)
    {
        /*
         * IMPORTANTE:
         *
         * /attachments devuelve microsoft.graph.attachment.
         *
         * contentId NO pertenece al tipo base attachment.
         * contentId se obtiene posteriormente cuando consultamos
         * individualmente el fileAttachment.
         *
         * Por eso NO debe formar parte de este $select.
         */
        var url =
            $"https://graph.microsoft.com/v1.0/users/" +
            $"{Uri.EscapeDataString(mailbox)}/messages/" +
            $"{Uri.EscapeDataString(graphId)}/attachments" +
            "?$select=" +
            "id," +
            "name," +
            "contentType," +
            "size," +
            "isInline";

        EnsureGraphUrl(
            url);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        using var response =
            await client.SendAsync(
                request,
                cancellationToken);

        if (
            !response
                .IsSuccessStatusCode)
        {
            var errorBody =
                await SafeReadContentAsync(
                    response,
                    cancellationToken);

            throw new InvalidOperationException(
                $"Microsoft Graph devolvió HTTP {(int)response.StatusCode} " +
                $"al consultar adjuntos. {TrimDiagnostic(errorBody)}");
        }

        using var document =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken));

        if (
            !document.RootElement
                .TryGetProperty(
                    "value",
                    out var value)
            ||
            value.ValueKind !=
                JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Microsoft Graph devolvió una colección de adjuntos inválida.");
        }

        var attachments =
            new List<
                IncomingHelpdeskAttachment>();

        var skipped =
            0;

        long totalBytes =
            0;

        foreach (
            var item
            in value.EnumerateArray())
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            // ====================================================
            // IDENTIFICADOR
            // ====================================================

            var attachmentId =
                item.TryGetProperty(
                    "id",
                    out var idElement)
                    ? idElement.GetString()
                    : null;

            if (
                string.IsNullOrWhiteSpace(
                    attachmentId))
            {
                skipped++;

                continue;
            }

            // ====================================================
            // METADATOS
            // ====================================================

            var name =
                item.TryGetProperty(
                    "name",
                    out var nameElement)
                    ? nameElement.GetString()
                    : null;

            var contentType =
                item.TryGetProperty(
                    "contentType",
                    out var contentTypeElement)
                    ? contentTypeElement.GetString()
                    : null;

            var isInline =
                item.TryGetProperty(
                    "isInline",
                    out var inlineElement)
                &&
                inlineElement.ValueKind ==
                    JsonValueKind.True;

            var size =
                item.TryGetProperty(
                    "size",
                    out var sizeElement)
                &&
                sizeElement.TryGetInt64(
                    out var parsedSize)
                    ? parsedSize
                    : 0;

            // ====================================================
            // SIZE SECURITY
            // ====================================================

            if (
                size <= 0
                ||
                size >
                    MaxAttachmentBytes
                ||
                totalBytes +
                    size >
                    MaxTotalAttachmentBytes)
            {
                skipped++;

                continue;
            }

            /*
             * No filtramos por isInline.
             *
             * Las firmas y logos de Outlook pueden venir
             * como fileAttachment inline.
             *
             * Tampoco filtramos aquí únicamente por extensión:
             * GetAttachmentAsync + el importador hacen la
             * validación definitiva de MIME/firma.
             */
            var attachment =
                await GetAttachmentAsync(
                    client,
                    token,
                    mailbox,
                    graphId,
                    attachmentId,
                    name,
                    contentType,
                    cancellationToken);

            if (attachment is null)
            {
                skipped++;

                continue;
            }

            if (
                attachment.Content
                    .LongLength >
                    MaxAttachmentBytes
                ||
                totalBytes +
                    attachment.Content
                        .LongLength >
                    MaxTotalAttachmentBytes)
            {
                skipped++;

                continue;
            }

            totalBytes +=
                attachment.Content
                    .LongLength;

            attachments.Add(
                attachment);

            /*
             * Log útil para HD-A5.
             *
             * Así podremos distinguir inmediatamente
             * adjuntos normales de imágenes de firma.
             */
            // No usar ILogger aquí porque este método es static.
            // El importador ya registra el resultado final.
        }

        return new GraphAttachmentResult(
            attachments,
            skipped);
    }
    // ============================================================
    // SINGLE ATTACHMENT
    // ============================================================

    private static async Task<IncomingHelpdeskAttachment?>
        GetAttachmentAsync(
            HttpClient client,
            string token,
            string mailbox,
            string graphId,
            string attachmentId,
            string? fallbackName,
            string? fallbackContentType,
            CancellationToken cancellationToken)
    {
        var url =
            $"https://graph.microsoft.com/v1.0/users/" +
            $"{Uri.EscapeDataString(mailbox)}/messages/" +
            $"{Uri.EscapeDataString(graphId)}/attachments/" +
            $"{Uri.EscapeDataString(attachmentId)}";

        EnsureGraphUrl(
            url);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        using var response =
            await client.SendAsync(
                request,
                cancellationToken);

        if (
            response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            return null;
        }

        if (
            !response
                .IsSuccessStatusCode)
        {
            var errorBody =
                await SafeReadContentAsync(
                    response,
                    cancellationToken);

            throw new InvalidOperationException(
                $"Microsoft Graph devolvió HTTP {(int)response.StatusCode} " +
                $"al descargar adjunto. {TrimDiagnostic(errorBody)}");
        }

        using var document =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken));

        var root =
            document.RootElement;

        // ========================================================
        // TYPE
        // ========================================================

        var type =
            root.TryGetProperty(
                "@odata.type",
                out var typeElement)
                ? typeElement.GetString()
                : null;

        if (
            !string.Equals(
                type,
                "#microsoft.graph.fileAttachment",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return null;
        }

        // ========================================================
        // INLINE
        // ========================================================

        var isInline =
            root.TryGetProperty(
                "isInline",
                out var inlineElement)
            &&
            inlineElement.ValueKind ==
                JsonValueKind.True;

        var contentId =
            root.TryGetProperty(
                "contentId",
                out var contentIdElement)
                ? contentIdElement.GetString()
                : null;

        // ========================================================
        // NAME / MIME
        // ========================================================

        var name =
            root.TryGetProperty(
                "name",
                out var nameElement)
                ? nameElement.GetString()
                : fallbackName;

        var contentType =
            root.TryGetProperty(
                "contentType",
                out var contentTypeElement)
                ? contentTypeElement.GetString()
                : fallbackContentType;

        contentType =
            string.IsNullOrWhiteSpace(
                contentType)
                ? "application/octet-stream"
                : contentType
                    .Trim()
                    .ToLowerInvariant();

        /*
         * Outlook puede generar inline attachments con nombre
         * extraño o incluso sin extensión.
         */
        if (
            string.IsNullOrWhiteSpace(
                name))
        {
            name =
                contentType switch
                {
                    "image/png" =>
                        "inline-image.png",

                    "image/jpeg" =>
                        "inline-image.jpg",

                    "application/pdf" =>
                        "attachment.pdf",

                    _ =>
                        "attachment"
                };
        }

        // ========================================================
        // CONTENT
        // ========================================================

        var contentBytes =
            root.TryGetProperty(
                "contentBytes",
                out var bytesElement)
                ? bytesElement.GetString()
                : null;

        if (
            string.IsNullOrWhiteSpace(
                contentBytes))
        {
            return null;
        }

        byte[] bytes;

        try
        {
            bytes =
                Convert.FromBase64String(
                    contentBytes);
        }
        catch (FormatException)
        {
            return null;
        }

        if (
            bytes.LongLength <= 0
            ||
            bytes.LongLength >
                MaxAttachmentBytes)
        {
            return null;
        }

        return new IncomingHelpdeskAttachment(
            attachmentId,
            name,
            contentType,
            bytes,
            IsInline:
                isInline,
            ContentId:
                contentId);
    }

    // ============================================================
    // PROCESSING LOG
    // ============================================================

    private static async Task SaveProcessingLogAsync(
        TitanMdmDbContext db,
        HelpdeskMailProcessingLog log,
        CancellationToken cancellationToken)
    {
        var exists =
            await db.HelpdeskMailProcessingLogs
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            log.OrganizationId
                        &&
                        x.Mailbox ==
                            log.Mailbox
                        &&
                        x.InternetMessageId ==
                            log.InternetMessageId,
                    cancellationToken);

        if (exists)
        {
            return;
        }

        db.HelpdeskMailProcessingLogs
            .Add(
                log);

        try
        {
            await db.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(
                    log)
                .State =
                    EntityState.Detached;

            var winner =
                await db.HelpdeskMailProcessingLogs
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                log.OrganizationId
                            &&
                            x.Mailbox ==
                                log.Mailbox
                            &&
                            x.InternetMessageId ==
                                log.InternetMessageId,
                        cancellationToken);

            if (!winner)
            {
                throw;
            }
        }
    }

    // ============================================================
    // TOKEN
    // ============================================================

    private static async Task<string>
        GetTokenAsync(
            HttpClient client,
            string tenant,
            string clientId,
            string secret,
            CancellationToken cancellationToken)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"https://login.microsoftonline.com/" +
                $"{Uri.EscapeDataString(tenant)}/oauth2/v2.0/token")
            {
                Content =
                    new FormUrlEncodedContent(
                        new Dictionary<
                            string,
                            string>
                        {
                            ["client_id"] =
                                clientId,

                            ["client_secret"] =
                                secret,

                            ["grant_type"] =
                                "client_credentials",

                            ["scope"] =
                                "https://graph.microsoft.com/.default"
                        })
            };

        using var response =
            await client.SendAsync(
                request,
                cancellationToken);

        if (
            !response
                .IsSuccessStatusCode)
        {
            var errorBody =
                await SafeReadContentAsync(
                    response,
                    cancellationToken);

            throw new InvalidOperationException(
                $"Microsoft Entra rechazó la autenticación del buzón. " +
                $"HTTP {(int)response.StatusCode}. " +
                TrimDiagnostic(
                    errorBody));
        }

        using var document =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken));

        if (
            !document.RootElement
                .TryGetProperty(
                    "access_token",
                    out var token)
            ||
            string.IsNullOrWhiteSpace(
                token.GetString()))
        {
            throw new InvalidOperationException(
                "Microsoft Entra no devolvió access_token.");
        }

        return token
            .GetString()!;
    }

    // ============================================================
    // CURSOR
    // ============================================================

    private string ResolveCursorDirectory()
    {
        var configured =
            _configuration[
                "HelpdeskMail:CursorDirectory"];

        if (
            !string.IsNullOrWhiteSpace(
                configured))
        {
            return Path.GetFullPath(
                configured);
        }

        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .CommonApplicationData),
            "TitanMDM",
            "MailCursors");
    }

    // ============================================================
    // GRAPH SECURITY
    // ============================================================

    private static void EnsureGraphUrl(
        string url)
    {
        if (
            !Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri)
            ||
            uri.Scheme !=
                Uri.UriSchemeHttps
            ||
            !string.Equals(
                uri.Host,
                "graph.microsoft.com",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "URL de Microsoft Graph no válida.");
        }
    }

    // ============================================================
    // HTTP HELPERS
    // ============================================================

    private static async Task<string?>
        SafeReadContentAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content
                .ReadAsStringAsync(
                    cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string TrimDiagnostic(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        var normalized =
            value.Trim();

        return normalized[
            ..Math.Min(
                normalized.Length,
                1000)];
    }

    // ============================================================
    // SETTINGS
    // ============================================================

    private static async Task ReloadMailSettingsAsync(
        TitanMdmDbContext db,
        HelpdeskMailSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.Entry(
                    settings)
                .ReloadAsync(
                    cancellationToken);
        }
        catch
        {
            /*
             * El estado runtime es diagnóstico.
             * Nunca debe ocultar el error original.
             */
        }
    }

    private static async Task TrySaveRuntimeStatusAsync(
        TitanMdmDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            /*
             * La configuración administrativa prevalece
             * sobre los timestamps del worker.
             */
        }
    }

    // ============================================================
    // INTERNAL RECORDS
    // ============================================================

    private sealed record GraphInboundMessage(
        IncomingHelpdeskEmail Message,
        IReadOnlyList<
            IncomingHelpdeskAttachment> Attachments,
        int SkippedAttachments);

    private sealed record GraphAttachmentResult(
        IReadOnlyList<
            IncomingHelpdeskAttachment> Items,
        int Skipped);

    private sealed record MailboxSyncResult(
        int MessagesSeen,
        int MessagesAccepted,
        int MessagesIgnored,
        int MessagesFailed,
        int AttachmentsImported,
        int InlineImported);
}