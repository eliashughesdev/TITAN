using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/mail")]
public sealed class HelpdeskMailSettingsController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _db;

    private readonly IHttpClientFactory
        _httpClientFactory;

    private readonly IDataProtector
        _protector;

    public HelpdeskMailSettingsController(
        TitanMdmDbContext db,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _db =
            db;

        _httpClientFactory =
            httpClientFactory;

        _protector =
            dataProtectionProvider
                .CreateProtector(
                    "TitanMDM.Helpdesk.Entra.ClientSecret.v1");
    }

    // ============================================================
    // SETTINGS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        if (!CanViewMail())
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        var settings =
            await _db.HelpdeskMailSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value,
                    cancellationToken)
            ??
            new HelpdeskMailSettings(
                organizationId.Value);

        var actorName =
            settings.ActorUserId.HasValue
                ? await _db.Users
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                organizationId.Value
                            &&
                            x.Id ==
                                settings.ActorUserId.Value)
                    .Select(
                        x =>
                            (
                                x.FirstName +
                                " " +
                                x.LastName
                            )
                            .Trim())
                    .FirstOrDefaultAsync(
                        cancellationToken)
                : null;

        var pending =
            await CountOutboundAsync(
                organizationId.Value,
                HelpdeskOutboundEmail.PendingStatus,
                cancellationToken);

        var retry =
            await CountOutboundAsync(
                organizationId.Value,
                HelpdeskOutboundEmail.RetryStatus,
                cancellationToken);

        var sending =
            await CountOutboundAsync(
                organizationId.Value,
                HelpdeskOutboundEmail.SendingStatus,
                cancellationToken);

        var deadLetter =
            await CountOutboundAsync(
                organizationId.Value,
                HelpdeskOutboundEmail.DeadLetterStatus,
                cancellationToken);

        var sent =
            await CountOutboundAsync(
                organizationId.Value,
                HelpdeskOutboundEmail.SentStatus,
                cancellationToken);

        var received =
            await _db.HelpdeskEmailMessages
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value,
                    cancellationToken);

        var accepted =
            await _db.HelpdeskMailProcessingLogs
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value
                        &&
                        x.Decision ==
                            "accepted",
                    cancellationToken);

        var ignored =
            await _db.HelpdeskMailProcessingLogs
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value
                        &&
                        x.Decision ==
                            "ignored",
                    cancellationToken);

        var failed =
            await _db.HelpdeskMailProcessingLogs
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value
                        &&
                        x.Decision ==
                            "failed",
                    cancellationToken);

        return Ok(
            new
            {
                settings.Mailbox,
                settings.AcceptedRecipients,
                settings.ActorUserId,

                actorName,

                settings.InboundEnabled,
                settings.OutboundEnabled,

                settings.IgnoreAutomaticMessages,
                settings.IgnoreBulkMessages,
                settings.IgnoreBounceMessages,
                settings.IgnoreNoReplyMessages,

                settings.BlockedSenders,
                settings.BlockedDomains,

                settings.AllowedSenders,
                settings.AllowedDomains,

                settings.IgnoredSubjectPatterns,

                settings.InboundPollSeconds,
                settings.OutboundPollSeconds,

                settings.BatchSize,
                settings.MaxAttempts,

                settings.LastInboundAttemptAtUtc,
                settings.LastInboundSuccessAtUtc,
                settings.LastInboundError,

                settings.LastOutboundAttemptAtUtc,
                settings.LastOutboundSuccessAtUtc,
                settings.LastOutboundError,

                settings.Revision,
                settings.UpdatedAtUtc,

                statistics =
                    new
                    {
                        received,
                        accepted,
                        ignored,
                        failed,

                        sent,
                        pending,
                        retry,
                        sending,
                        deadLetter
                    }
            });
    }

    // ============================================================
    // SAVE
    // ============================================================

    [HttpPut]
    public async Task<IActionResult> Save(
        [FromBody]
        SaveMailSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManageMail())
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        var settings =
            await _db.HelpdeskMailSettings
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value,
                    cancellationToken);

        if (
            (settings?.Revision ?? 0)
            !=
            request.Revision)
        {
            return Conflict(
                new
                {
                    message =
                        "La configuración cambió. Actualiza la pantalla antes de guardar."
                });
        }

        if (
            request.ActorUserId.HasValue)
        {
            var actorValid =
                await _db.Users
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId.Value
                            &&
                            x.Id ==
                                request.ActorUserId.Value
                            &&
                            x.IsActive,
                        cancellationToken);

            if (!actorValid)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "El actor del sistema seleccionado no existe o está inactivo."
                    });
            }
        }

        if (settings is null)
        {
            settings =
                new HelpdeskMailSettings(
                    organizationId.Value);

            _db.HelpdeskMailSettings
                .Add(
                    settings);
        }

        try
        {
            settings.Configure(
                mailbox:
                    request.Mailbox,

                acceptedRecipients:
                    request.AcceptedRecipients,

                actorUserId:
                    request.ActorUserId,

                inboundEnabled:
                    request.InboundEnabled,

                outboundEnabled:
                    request.OutboundEnabled,

                ignoreAutomaticMessages:
                    request.IgnoreAutomaticMessages,

                ignoreBulkMessages:
                    request.IgnoreBulkMessages,

                ignoreBounceMessages:
                    request.IgnoreBounceMessages,

                ignoreNoReplyMessages:
                    request.IgnoreNoReplyMessages,

                blockedSenders:
                    request.BlockedSenders,

                blockedDomains:
                    request.BlockedDomains,

                allowedSenders:
                    request.AllowedSenders,

                allowedDomains:
                    request.AllowedDomains,

                ignoredSubjectPatterns:
                    request.IgnoredSubjectPatterns,

                inboundPollSeconds:
                    request.InboundPollSeconds,

                outboundPollSeconds:
                    request.OutboundPollSeconds,

                batchSize:
                    request.BatchSize,

                maxAttempts:
                    request.MaxAttempts);

            await _db.SaveChangesAsync(
                cancellationToken);

            return await Get(
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    message =
                        exception.Message
                });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(
                new
                {
                    message =
                        "Otro administrador modificó la configuración. Actualiza antes de guardar."
                });
        }
    }

    // ============================================================
    // PROCESSING ACTIVITY
    // ============================================================

    [HttpGet("activity")]
    public async Task<IActionResult> Activity(
        [FromQuery]
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!CanViewMail())
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        take =
            Math.Clamp(
                take,
                1,
                200);

        var items =
            await _db.HelpdeskMailProcessingLogs
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value)
                .OrderByDescending(
                    x =>
                        x.ProcessedAtUtc)
                .Take(
                    take)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.TicketId,

                            x.Mailbox,
                            x.FromEmail,

                            x.Subject,

                            x.Decision,
                            x.ReasonCode,
                            x.Reason,

                            x.ReceivedAtUtc,
                            x.ProcessedAtUtc,

                            x.InternetMessageId,
                            x.ConversationId
                        })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            items);
    }

    // ============================================================
    // ACTIVITY SUMMARY
    // ============================================================

    [HttpGet("activity/summary")]
    public async Task<IActionResult> ActivitySummary(
        CancellationToken cancellationToken)
    {
        if (!CanViewMail())
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        var baseQuery =
            _db.HelpdeskMailProcessingLogs
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value);

        var accepted =
            await baseQuery.CountAsync(
                x =>
                    x.Decision ==
                        "accepted",
                cancellationToken);

        var ignored =
            await baseQuery.CountAsync(
                x =>
                    x.Decision ==
                        "ignored",
                cancellationToken);

        var failed =
            await baseQuery.CountAsync(
                x =>
                    x.Decision ==
                        "failed",
                cancellationToken);

        var today =
            DateTime.UtcNow.Date;

        var processedToday =
            await baseQuery.CountAsync(
                x =>
                    x.ProcessedAtUtc >=
                        today,
                cancellationToken);

        return Ok(
            new
            {
                accepted,
                ignored,
                failed,
                processedToday,
                total =
                    accepted +
                    ignored +
                    failed
            });
    }

    // ============================================================
    // GRAPH DIAGNOSTIC
    // ============================================================

    [HttpPost("test")]
    public async Task<IActionResult> Test(
        CancellationToken cancellationToken)
    {
        if (!CanManageMail())
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        var mailSettings =
            await _db.HelpdeskMailSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value,
                    cancellationToken);

        if (
            mailSettings is null
            ||
            string.IsNullOrWhiteSpace(
                mailSettings.Mailbox))
        {
            return BadRequest(
                new
                {
                    success = false,

                    message =
                        "Configura primero el buzón de Mesa de Ayuda."
                });
        }

        var entra =
            await _db.EntraIdSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value,
                    cancellationToken);

        if (entra is null)
        {
            return Conflict(
                new
                {
                    success = false,

                    message =
                        "No existe configuración de Microsoft Entra ID."
                });
        }

        if (!entra.IsEnabled)
        {
            return Conflict(
                new
                {
                    success = false,

                    message =
                        "Microsoft Entra ID está configurado pero deshabilitado."
                });
        }

        if (
            string.IsNullOrWhiteSpace(
                entra.TenantId)
            ||
            string.IsNullOrWhiteSpace(
                entra.ClientId)
            ||
            string.IsNullOrWhiteSpace(
                entra.ClientSecretProtected))
        {
            return Conflict(
                new
                {
                    success = false,

                    message =
                        "Falta Tenant ID, Client ID o Client Secret."
                });
        }

        string secret;

        try
        {
            secret =
                _protector.Unprotect(
                    entra.ClientSecretProtected);
        }
        catch
        {
            return Conflict(
                new
                {
                    success = false,

                    message =
                        "El Client Secret no puede descifrarse con las claves actuales de TitanMDM. Guarda nuevamente el secreto de Entra ID."
                });
        }

        try
        {
            var client =
                _httpClientFactory
                    .CreateClient(
                        "entra-id");

            var tokenResult =
                await GetAccessTokenAsync(
                    client,
                    entra.TenantId,
                    entra.ClientId,
                    secret,
                    cancellationToken);

            if (!tokenResult.Success)
            {
                return StatusCode(
                    StatusCodes
                        .Status502BadGateway,
                    new
                    {
                        success = false,

                        stage =
                            "entra-token",

                        message =
                            tokenResult.Message,

                        tenantId =
                            entra.TenantId,

                        clientId =
                            entra.ClientId,

                        mailbox =
                            mailSettings.Mailbox,

                        requiredPermissions =
                            InboundPermissions
                    });
            }

            var endpoint =
                $"https://graph.microsoft.com/v1.0/users/" +
                $"{Uri.EscapeDataString(mailSettings.Mailbox)}" +
                "/mailFolders/inbox" +
                "?$select=id,displayName,totalItemCount,unreadItemCount";

            using var graphRequest =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    endpoint);

            graphRequest.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokenResult.AccessToken);

            graphRequest.Headers
                .TryAddWithoutValidation(
                    "client-request-id",
                    Guid.NewGuid().ToString());

            using var response =
                await client.SendAsync(
                    graphRequest,
                    cancellationToken);

            var content =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken);

            var graphError =
                ParseGraphError(
                    content);

            var requestId =
                ReadHeader(
                    response,
                    "request-id")
                ??
                ReadHeader(
                    response,
                    "client-request-id");

            if (
                !response.IsSuccessStatusCode)
            {
                return StatusCode(
                    StatusCodes
                        .Status502BadGateway,
                    new
                    {
                        success = false,

                        stage =
                            "graph-mailbox",

                        httpStatus =
                            (int)response.StatusCode,

                        graphCode =
                            graphError.Code,

                        graphMessage =
                            graphError.Message,

                        requestId,

                        tenantId =
                            entra.TenantId,

                        clientId =
                            entra.ClientId,

                        mailbox =
                            mailSettings.Mailbox,

                        message =
                            BuildGraphDiagnosticMessage(
                                response.StatusCode,
                                graphError.Code,
                                graphError.Message),

                        inboundVerified =
                            false,

                        outboundConfigured =
                            mailSettings.OutboundEnabled,

                        requiredPermissions =
                            InboundPermissions
                    });
            }

            using var json =
                JsonDocument.Parse(
                    content);

            var root =
                json.RootElement;

            return Ok(
                new
                {
                    success = true,

                    stage =
                        "complete",

                    message =
                        "Conexión con Microsoft Graph y acceso al Inbox validados correctamente.",

                    tenantId =
                        entra.TenantId,

                    clientId =
                        entra.ClientId,

                    mailbox =
                        mailSettings.Mailbox,

                    acceptedRecipients =
                        mailSettings.AcceptedRecipients,

                    inbox =
                        root.TryGetProperty(
                            "displayName",
                            out var displayName)
                            ? displayName.GetString()
                            : "Inbox",

                    totalItemCount =
                        root.TryGetProperty(
                            "totalItemCount",
                            out var total)
                        &&
                        total.TryGetInt32(
                            out var totalValue)
                            ? totalValue
                            : 0,

                    unreadItemCount =
                        root.TryGetProperty(
                            "unreadItemCount",
                            out var unread)
                        &&
                        unread.TryGetInt32(
                            out var unreadValue)
                            ? unreadValue
                            : 0,

                    requestId,

                    inboundVerified =
                        true,

                    outboundConfigured =
                        mailSettings.OutboundEnabled,

                    requiredPermissions =
                        InboundPermissions
                });
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
            return StatusCode(
                StatusCodes
                    .Status502BadGateway,
                new
                {
                    success = false,

                    stage =
                        "unexpected",

                    message =
                        exception.Message,

                    mailbox =
                        mailSettings.Mailbox,

                    inboundVerified =
                        false,

                    outboundConfigured =
                        mailSettings.OutboundEnabled,

                    requiredPermissions =
                        InboundPermissions
                });
        }
    }

    // ============================================================
    // ACTORS
    // ============================================================

    [HttpGet("actors")]
    public async Task<IActionResult> Actors(
        CancellationToken cancellationToken)
    {
        if (!CanManageMail())
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (!organizationId.HasValue)
        {
            return Unauthorized();
        }

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId.Value
                        &&
                        x.IsActive)
                .OrderBy(
                    x =>
                        x.FirstName)
                .ThenBy(
                    x =>
                        x.LastName)
                .Select(
                    x =>
                        new
                        {
                            x.Id,

                            name =
                                (
                                    x.FirstName +
                                    " " +
                                    x.LastName
                                )
                                .Trim(),

                            x.Email
                        })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            users);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private async Task<int> CountOutboundAsync(
        Guid organizationId,
        string status,
        CancellationToken cancellationToken)
    {
        return await _db
            .Set<HelpdeskOutboundEmail>()
            .AsNoTracking()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.Status ==
                        status,
                cancellationToken);
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        return Guid.TryParse(
            value,
            out var organizationId)
                ? organizationId
                : null;
    }

    private bool CanViewMail()
    {
        return HasAnyPermission(
            "helpdesk.mail.view",
            "helpdesk.mail.manage",
            "helpdesk.admin.access",
            "helpdesk.manage",
            "settings.manage");
    }

    private bool CanManageMail()
    {
        return HasAnyPermission(
            "helpdesk.mail.manage",
            "helpdesk.admin.access",
            "helpdesk.manage",
            "settings.manage");
    }

    private bool HasAnyPermission(
        params string[] permissions)
    {
        return User.Claims.Any(
            claim =>
                claim.Type ==
                    "permission"
                &&
                permissions.Any(
                    permission =>
                        string.Equals(
                            claim.Value,
                            permission,
                            StringComparison
                                .OrdinalIgnoreCase)));
    }

    /*
     * Para la recepción actual no mostramos Mail.Send como
     * requisito obligatorio.
     *
     * Mail.Send se agregará cuando cerremos el bloque outbound.
     */
    private static readonly string[]
        InboundPermissions =
        [
            "Mail.Read (Application)"
        ];

    private static async Task<TokenResult>
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
                        new Dictionary<string, string>
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

        if (
            !response.IsSuccessStatusCode)
        {
            var error =
                ParseGraphError(
                    content);

            return new TokenResult(
                false,
                null,
                error.Code is null
                    ? $"Microsoft Entra rechazó la autenticación. HTTP {(int)response.StatusCode}."
                    : $"{error.Code}: {error.Message}");
        }

        using var json =
            JsonDocument.Parse(
                content);

        if (
            !json.RootElement
                .TryGetProperty(
                    "access_token",
                    out var token)
            ||
            string.IsNullOrWhiteSpace(
                token.GetString()))
        {
            return new TokenResult(
                false,
                null,
                "Microsoft Entra no devolvió access_token.");
        }

        return new TokenResult(
            true,
            token.GetString(),
            "OK");
    }

    private static GraphError ParseGraphError(
        string? content)
    {
        if (
            string.IsNullOrWhiteSpace(
                content))
        {
            return new GraphError(
                null,
                null);
        }

        try
        {
            using var json =
                JsonDocument.Parse(
                    content);

            var root =
                json.RootElement;

            if (
                root.TryGetProperty(
                    "error",
                    out var error))
            {
                string? code =
                    null;

                string? message =
                    null;

                if (
                    error.ValueKind ==
                    JsonValueKind.Object)
                {
                    if (
                        error.TryGetProperty(
                            "code",
                            out var codeValue))
                    {
                        code =
                            codeValue.GetString();
                    }

                    if (
                        error.TryGetProperty(
                            "message",
                            out var messageValue))
                    {
                        message =
                            messageValue.GetString();
                    }
                }
                else if (
                    error.ValueKind ==
                    JsonValueKind.String)
                {
                    code =
                        error.GetString();

                    if (
                        root.TryGetProperty(
                            "error_description",
                            out var description))
                    {
                        message =
                            description.GetString();
                    }
                }

                return new GraphError(
                    code,
                    message);
            }

            return new GraphError(
                null,
                content.Length > 1000
                    ? content[..1000]
                    : content);
        }
        catch
        {
            return new GraphError(
                null,
                content.Length > 1000
                    ? content[..1000]
                    : content);
        }
    }

    private static string BuildGraphDiagnosticMessage(
        System.Net.HttpStatusCode status,
        string? code,
        string? graphMessage)
    {
        var prefix =
            $"Microsoft Graph rechazó el acceso al buzón (HTTP {(int)status}).";

        if (
            !string.IsNullOrWhiteSpace(
                code))
        {
            prefix +=
                $" Código: {code}.";
        }

        if (
            !string.IsNullOrWhiteSpace(
                graphMessage))
        {
            prefix +=
                $" {graphMessage}";
        }

        if (
            (int)status ==
            403)
        {
            prefix +=
                " Revisa Mail.Read como permiso Application, " +
                "Grant admin consent y cualquier restricción de Exchange sobre el buzón.";
        }

        return prefix;
    }

    private static string? ReadHeader(
        HttpResponseMessage response,
        string name)
    {
        return response.Headers
            .TryGetValues(
                name,
                out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private sealed record TokenResult(
        bool Success,
        string? AccessToken,
        string Message);

    private sealed record GraphError(
        string? Code,
        string? Message);

    public sealed record SaveMailSettingsRequest(
        string? Mailbox,
        string? AcceptedRecipients,

        Guid? ActorUserId,

        bool InboundEnabled,
        bool OutboundEnabled,

        bool IgnoreAutomaticMessages,
        bool IgnoreBulkMessages,
        bool IgnoreBounceMessages,
        bool IgnoreNoReplyMessages,

        string? BlockedSenders,
        string? BlockedDomains,

        string? AllowedSenders,
        string? AllowedDomains,

        string? IgnoredSubjectPatterns,

        int InboundPollSeconds,
        int OutboundPollSeconds,

        int BatchSize,
        int MaxAttempts,

        int Revision);
}