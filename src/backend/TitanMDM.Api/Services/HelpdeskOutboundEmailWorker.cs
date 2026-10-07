using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Services;

public sealed class HelpdeskOutboundEmailWorker
    : BackgroundService
{
    private const int WorkerTickSeconds =
        10;

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly IHttpClientFactory
        _httpClientFactory;

    private readonly IDataProtector
        _protector;

    private readonly ILogger<
        HelpdeskOutboundEmailWorker>
        _logger;

    public HelpdeskOutboundEmailWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider protectionProvider,
        ILogger<HelpdeskOutboundEmailWorker> logger)
    {
        _scopeFactory =
            scopeFactory;

        _httpClientFactory =
            httpClientFactory;

        _protector =
            protectionProvider.CreateProtector(
                "TitanMDM.Helpdesk.Entra.ClientSecret.v1");

        _logger =
            logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken
            .IsCancellationRequested)
        {
            try
            {
                await ExecuteDueOrganizationsAsync(
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
                    "Error general del worker de correo saliente de Helpdesk.");
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
    }

    // ============================================================
    // SCHEDULER
    // ============================================================

    private async Task ExecuteDueOrganizationsAsync(
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
                        x.OutboundEnabled
                        &&
                        x.Mailbox !=
                            null)
                .Select(
                    x =>
                        new
                        {
                            x.OrganizationId,
                            x.OutboundPollSeconds,
                            x.LastOutboundAttemptAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var dueOrganizations =
            configurations
                .Where(
                    x =>
                        !x.LastOutboundAttemptAtUtc
                            .HasValue
                        ||
                        x.LastOutboundAttemptAtUtc
                            .Value
                            .AddSeconds(
                                Math.Clamp(
                                    x.OutboundPollSeconds,
                                    10,
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

        var mailSettings =
            await db.HelpdeskMailSettings
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (mailSettings is null ||
            !mailSettings.OutboundEnabled ||
            string.IsNullOrWhiteSpace(
                mailSettings.Mailbox))
        {
            return;
        }

        var mailbox =
            mailSettings.Mailbox
                .Trim()
                .ToLowerInvariant();

        var batchSize =
            Math.Clamp(
                mailSettings.BatchSize,
                1,
                100);

        var maxAttempts =
            Math.Clamp(
                mailSettings.MaxAttempts,
                1,
                20);

        mailSettings
            .MarkOutboundAttempt();

        await TrySaveRuntimeStatusAsync(
            db,
            cancellationToken);

        try
        {
            await DiscoverMessagesAsync(
                db,
                organizationId,
                batchSize,
                cancellationToken);

            await RecoverAbandonedMessagesAsync(
                db,
                organizationId,
                batchSize,
                cancellationToken);

            var failed =
                await SendPendingMessagesAsync(
                    db,
                    organizationId,
                    mailbox,
                    batchSize,
                    maxAttempts,
                    cancellationToken);

            /*
             * Reload after all queue operations so a concurrent
             * administrator update does not get overwritten.
             */
            await ReloadMailSettingsAsync(
                db,
                mailSettings,
                cancellationToken);

            if (failed >
                0)
            {
                mailSettings
                    .MarkOutboundFailure(
                        $"{failed} mensaje(s) no pudieron enviarse durante el ciclo.");
            }
            else
            {
                mailSettings
                    .MarkOutboundSuccess();
            }

            await TrySaveRuntimeStatusAsync(
                db,
                cancellationToken);
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
                mailSettings,
                CancellationToken.None);

            mailSettings
                .MarkOutboundFailure(
                    exception.Message);

            await TrySaveRuntimeStatusAsync(
                db,
                CancellationToken.None);

            _logger.LogError(
                exception,
                "Error procesando correo saliente para organización {OrganizationId}, buzón {Mailbox}.",
                organizationId,
                mailbox);
        }
    }

    // ============================================================
    // OUTBOX DISCOVERY
    // ============================================================

    private async Task DiscoverMessagesAsync(
        TitanMdmDbContext db,
        Guid organizationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var outbox =
            db.HelpdeskOutboundEmails;

        var candidates =
            await (
                from comment
                    in db.HelpdeskTicketComments
                        .AsNoTracking()

                join ticket
                    in db.HelpdeskTickets
                        .AsNoTracking()

                    on new
                    {
                        comment.OrganizationId,
                        TicketId =
                            comment.TicketId
                    }
                    equals new
                    {
                        ticket.OrganizationId,
                        TicketId =
                            ticket.Id
                    }

                join requester
                    in db.Users
                        .AsNoTracking()

                    on new
                    {
                        ticket.OrganizationId,
                        UserId =
                            ticket.RequesterUserId
                    }
                    equals new
                    {
                        requester.OrganizationId,
                        UserId =
                            requester.Id
                    }

                where
                    comment.OrganizationId ==
                        organizationId

                    &&
                    !comment.IsInternal

                    &&
                    comment.ExternalAuthorEmail ==
                        null

                    &&
                    comment.AuthorUserId !=
                        ticket.RequesterUserId

                    &&
                    !outbox.Any(
                        queued =>
                            queued.OrganizationId ==
                                organizationId
                            &&
                            queued.CommentId ==
                                comment.Id)

                orderby
                    comment.CreatedAtUtc

                select new
                {
                    comment.Id,
                    comment.OrganizationId,
                    comment.TicketId,
                    comment.Body,

                    TicketNumber =
                        ticket.Number,

                    TicketSubject =
                        ticket.Subject,

                    ExternalEmail =
                        ticket.ExternalRequesterEmail,

                    RequesterEmail =
                        requester.Email
                }
            )
            .Take(
                batchSize)
            .ToListAsync(
                cancellationToken);

        if (candidates.Count ==
            0)
        {
            return;
        }

        foreach (
            var candidate
            in candidates)
        {
            var destination =
                !string.IsNullOrWhiteSpace(
                    candidate.ExternalEmail)
                    ? candidate.ExternalEmail
                    : candidate.RequesterEmail;

            if (string.IsNullOrWhiteSpace(
                    destination))
            {
                _logger.LogWarning(
                    "Ticket {TicketId} no tiene correo de solicitante; comentario {CommentId} no puede enviarse.",
                    candidate.TicketId,
                    candidate.Id);

                continue;
            }

            outbox.Add(
                new HelpdeskOutboundEmail(
                    candidate.OrganizationId,
                    candidate.TicketId,
                    candidate.Id,
                    destination,
                    BuildSubject(
                        candidate.TicketNumber,
                        candidate.TicketSubject),
                    BuildBody(
                        candidate.TicketNumber,
                        candidate.Body)));
        }

        try
        {
            await db.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            /*
             * Another application node may have discovered the same
             * comments. OrganizationId + CommentId is unique.
             */
            foreach (
                var entry
                in db.ChangeTracker
                    .Entries<
                        HelpdeskOutboundEmail>()
                    .Where(
                        x =>
                            x.State ==
                                EntityState.Added))
            {
                entry.State =
                    EntityState.Detached;
            }

            _logger.LogInformation(
                exception,
                "Uno o más correos salientes ya estaban registrados en Outbox.");
        }
    }

    // ============================================================
    // CRASH RECOVERY
    // ============================================================

    private static async Task
        RecoverAbandonedMessagesAsync(
            TitanMdmDbContext db,
            Guid organizationId,
            int batchSize,
            CancellationToken cancellationToken)
    {
        var staleBefore =
            DateTime.UtcNow
                .AddMinutes(
                    -10);

        var abandoned =
            await db.HelpdeskOutboundEmails
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId

                        &&
                        x.Status ==
                            HelpdeskOutboundEmail
                                .SendingStatus

                        &&
                        x.LastAttemptAtUtc
                            .HasValue

                        &&
                        x.LastAttemptAtUtc <
                            staleBefore)
                .Take(
                    batchSize)
                .ToListAsync(
                    cancellationToken);

        if (abandoned.Count ==
            0)
        {
            return;
        }

        foreach (
            var message
            in abandoned)
        {
            message
                .RecoverAbandonedSend();
        }

        await db.SaveChangesAsync(
            cancellationToken);
    }

    // ============================================================
    // DELIVERY
    // ============================================================

    private async Task<int>
        SendPendingMessagesAsync(
            TitanMdmDbContext db,
            Guid organizationId,
            string mailbox,
            int batchSize,
            int maxAttempts,
            CancellationToken cancellationToken)
    {
        var now =
            DateTime.UtcNow;

        var messages =
            await db.HelpdeskOutboundEmails
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId

                        &&
                        (
                            x.Status ==
                                HelpdeskOutboundEmail
                                    .PendingStatus
                            ||
                            x.Status ==
                                HelpdeskOutboundEmail
                                    .RetryStatus
                        )

                        &&
                        (
                            !x.NextAttemptAtUtc
                                .HasValue
                            ||
                            x.NextAttemptAtUtc <=
                                now
                        ))
                .OrderBy(
                    x =>
                        x.CreatedAtUtc)
                .Take(
                    batchSize)
                .ToListAsync(
                    cancellationToken);

        if (messages.Count ==
            0)
        {
            return 0;
        }

        var entra =
            await db.EntraIdSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (entra is null ||
            !entra.IsEnabled ||
            string.IsNullOrWhiteSpace(
                entra.TenantId) ||
            string.IsNullOrWhiteSpace(
                entra.ClientId) ||
            string.IsNullOrWhiteSpace(
                entra.ClientSecretProtected))
        {
            throw new InvalidOperationException(
                "Entra ID no está configurado para el envío de correo Helpdesk.");
        }

        var client =
            _httpClientFactory
                .CreateClient(
                    "entra-id");

        var secret =
            _protector.Unprotect(
                entra
                    .ClientSecretProtected);

        var accessToken =
            await GetAccessTokenAsync(
                client,
                entra.TenantId,
                entra.ClientId,
                secret,
                cancellationToken);

        var failures =
            0;

        foreach (
            var message
            in messages)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            try
            {
                message
                    .MarkSending();

                await db.SaveChangesAsync(
                    cancellationToken);

                await SendMailAsync(
                    client,
                    accessToken,
                    mailbox,
                    message,
                    cancellationToken);

                message
                    .MarkSent();

                db.HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            message.OrganizationId,
                            message.TicketId,
                            null,
                            "email_sent",
                            $"Respuesta enviada por correo a {message.ToEmail}."));

                await db.SaveChangesAsync(
                    cancellationToken);

                _logger.LogInformation(
                    "Correo Helpdesk {OutboundEmailId} enviado para ticket {TicketId}.",
                    message.Id,
                    message.TicketId);
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
                failures++;

                message.MarkFailure(
                    exception.Message,
                    maxAttempts);

                await db.SaveChangesAsync(
                    CancellationToken.None);

                _logger.LogWarning(
                    exception,
                    "Falló correo Helpdesk {OutboundEmailId}. Intento {AttemptCount}.",
                    message.Id,
                    message.AttemptCount);
            }
        }

        return failures;
    }

    // ============================================================
    // GRAPH AUTH
    // ============================================================

    private static async Task<string>
        GetAccessTokenAsync(
            HttpClient client,
            string tenantId,
            string clientId,
            string clientSecret,
            CancellationToken cancellationToken)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"https://login.microsoftonline.com/" +
                $"{Uri.EscapeDataString(tenantId)}" +
                "/oauth2/v2.0/token")
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
                                clientSecret,

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

        var content =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken);

        if (!response
            .IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Microsoft Entra rechazó autenticación de correo saliente. HTTP {(int)response.StatusCode}.");
        }

        using var json =
            JsonDocument.Parse(
                content);

        if (!json.RootElement
                .TryGetProperty(
                    "access_token",
                    out var token) ||
            string.IsNullOrWhiteSpace(
                token.GetString()))
        {
            throw new InvalidOperationException(
                "Microsoft Entra no devolvió access_token.");
        }

        return token.GetString()!;
    }

    // ============================================================
    // GRAPH SEND
    // ============================================================

    private static async Task SendMailAsync(
        HttpClient client,
        string accessToken,
        string mailbox,
        HelpdeskOutboundEmail message,
        CancellationToken cancellationToken)
    {
        var endpoint =
            $"https://graph.microsoft.com/v1.0/users/" +
            $"{Uri.EscapeDataString(mailbox)}/sendMail";

        var payload =
            new
            {
                message =
                    new
                    {
                        subject =
                            message.Subject,

                        body =
                            new
                            {
                                contentType =
                                    "Text",

                                content =
                                    message.Body
                            },

                        toRecipients =
                            new[]
                            {
                                new
                                {
                                    emailAddress =
                                        new
                                        {
                                            address =
                                                message.ToEmail
                                        }
                                }
                            }
                    },

                saveToSentItems =
                    true
            };

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                endpoint);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Content =
            new StringContent(
                JsonSerializer.Serialize(
                    payload),
                Encoding.UTF8,
                "application/json");

        using var response =
            await client.SendAsync(
                request,
                cancellationToken);

        if (!response
            .IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken);

            var safeBody =
                responseBody[
                    ..Math.Min(
                        responseBody.Length,
                        1000)];

            throw new InvalidOperationException(
                $"Microsoft Graph rechazó sendMail. " +
                $"HTTP {(int)response.StatusCode}. {safeBody}");
        }
    }

    // ============================================================
    // CONTENT
    // ============================================================

    private static string BuildSubject(
        string ticketNumber,
        string subject)
    {
        var value =
            $"[TitanMDM][{ticketNumber}] {subject}";

        return value[
            ..Math.Min(
                value.Length,
                250)];
    }

    private static string BuildBody(
        string ticketNumber,
        string comment)
    {
        var value =
            $"""
            TitanMDM - Mesa de Ayuda
            Ticket: {ticketNumber}

            {comment}

            ------------------------------------------------------------
            Responde directamente a este correo para continuar
            la conversación del ticket.

            Este mensaje fue generado por TitanMDM.
            """;

        return value[
            ..Math.Min(
                value.Length,
                10000)];
    }

    // ============================================================
    // SETTINGS STATUS
    // ============================================================

    private static async Task
        ReloadMailSettingsAsync(
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
             * Diagnostic state must never hide the original failure.
             */
        }
    }

    private static async Task
        TrySaveRuntimeStatusAsync(
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
             * Configuration changes made by an administrator win
             * over background diagnostic updates.
             */
        }
    }
}