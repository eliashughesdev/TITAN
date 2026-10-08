using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Services;

/// <summary>
/// Enriquece tickets de Helpdesk antes del routing.
///
/// Orden de confianza:
///
/// 1. Usuario TitanMDM.
/// 2. Usuario sincronizado desde Entra ID.
/// 3. Dispositivo corporativo asociado al usuario.
/// 4. Datos externos del correo.
/// 5. AI posteriormente.
///
/// Este servicio NO asigna técnicos.
/// Este servicio NO inventa categorías.
/// Este servicio NO modifica disponibilidad.
/// </summary>
public sealed class HelpdeskRequesterIntelligenceService
{
    private readonly TitanMdmDbContext _db;
    private readonly ILogger<HelpdeskRequesterIntelligenceService> _logger;

    public HelpdeskRequesterIntelligenceService(
        TitanMdmDbContext db,
        ILogger<HelpdeskRequesterIntelligenceService> logger)
    {
        _db =
            db
            ?? throw new ArgumentNullException(
                nameof(db));

        _logger =
            logger
            ?? throw new ArgumentNullException(
                nameof(logger));
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    public async Task<HelpdeskRequesterIntelligenceResult>
        EnrichAsync(
            Guid organizationId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId requerido.",
                nameof(organizationId));
        }

        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException(
                "TicketId requerido.",
                nameof(ticketId));
        }

        var ticket =
            await _db.HelpdeskTickets
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            ticketId,
                    cancellationToken);

        if (ticket is null)
        {
            return HelpdeskRequesterIntelligenceResult
                .NotFound(ticketId);
        }

        if (
            ticket.Status
                is "closed"
                or "resolved")
        {
            return HelpdeskRequesterIntelligenceResult
                .Skipped(
                    ticket.Id,
                    ticket.Number,
                    "El ticket está cerrado o resuelto.");
        }

        // ========================================================
        // REQUESTER BASE
        // ========================================================

        var requesterUser =
            await _db.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            ticket.RequesterUserId,
                    cancellationToken);

        var externalEmail =
            NormalizeEmail(
                ticket.ExternalRequesterEmail);

        var requesterEmail =
            NormalizeEmail(
                requesterUser?.Email);

        /*
         * Para tickets creados por correo:
         *
         * ExternalRequesterEmail tiene prioridad.
         *
         * El RequesterUserId puede ser todavía el actor técnico
         * configurado en el buzón.
         */
        var identityEmail =
            externalEmail
            ??
            requesterEmail;

        if (
            string.IsNullOrWhiteSpace(
                identityEmail))
        {
            return HelpdeskRequesterIntelligenceResult
                .Skipped(
                    ticket.Id,
                    ticket.Number,
                    "El ticket no contiene una identidad de correo utilizable.");
        }

        // ========================================================
        // TITAN USER BY EMAIL
        // ========================================================

        var normalizedEmail =
            identityEmail
                .Trim()
                .ToLowerInvariant();

        var titanUser =
            await _db.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive
                        &&
                        x.Email ==
                            normalizedEmail,
                    cancellationToken);

        // ========================================================
        // ENTRA DIRECTORY LOOKUP
        // ========================================================

        var entraUser =
            await _db.EntraDirectoryUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive
                        &&
                        (
                            x.Mail ==
                                normalizedEmail
                            ||
                            x.UserPrincipalName ==
                                normalizedEmail
                        ),
                    cancellationToken);

        /*
         * Si no hubo match directo en Users pero el registro
         * de Entra ya está vinculado a un Titan user,
         * usamos ese vínculo.
         */
        if (
            titanUser is null
            &&
            entraUser?
                .LinkedTitanUserId
                .HasValue ==
                true)
        {
            titanUser =
                await _db.Users
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.Id ==
                                entraUser.LinkedTitanUserId.Value
                            &&
                            x.IsActive,
                        cancellationToken);
        }

        // ========================================================
        // CORPORATE DEVICE CORRELATION
        // ========================================================

        var possibleDeviceUsers =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        AddIdentity(
            possibleDeviceUsers,
            normalizedEmail);

        AddIdentity(
            possibleDeviceUsers,
            titanUser?.Email);

        AddIdentity(
            possibleDeviceUsers,
            entraUser?.Mail);

        AddIdentity(
            possibleDeviceUsers,
            entraUser?.UserPrincipalName);

        if (titanUser is not null)
        {
            AddIdentity(
                possibleDeviceUsers,
                titanUser.FullName);
        }

        if (
            !string.IsNullOrWhiteSpace(
                ticket.ExternalRequesterName))
        {
            AddIdentity(
                possibleDeviceUsers,
                ticket.ExternalRequesterName);
        }

        var candidateDevices =
            await _db.Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        !x.IsDeleted
                        &&
                        x.AssignedUser !=
                            null)
                .OrderByDescending(
                    x =>
                        x.IsManaged)
                .ThenByDescending(
                    x =>
                        x.LastSeenAtUtc)
                .Take(
                    500)
                .Select(
                    x =>
                        new DeviceCandidate(
                            x.Id,
                            x.DeviceName,
                            x.AssignedUser,
                            x.Department,
                            x.SiteId,
                            x.SiteLocationId,
                            x.IsManaged,
                            x.LastSeenAtUtc))
                .ToListAsync(
                    cancellationToken);

        var matchedDevices =
            candidateDevices
                .Where(
                    device =>
                        IsIdentityMatch(
                            device.AssignedUser,
                            possibleDeviceUsers))
                .OrderByDescending(
                    x =>
                        x.IsManaged)
                .ThenByDescending(
                    x =>
                        x.LastSeenAtUtc)
                .ToArray();

        var primaryDevice =
            matchedDevices
                .FirstOrDefault();

        // ========================================================
        // SITE RESOLUTION
        //
        // Prioridad:
        //
        // 1. User.SiteId
        // 2. Device.SiteId
        // ========================================================

        Guid?
            resolvedSiteId =
                titanUser?.SiteId;

        Guid?
            resolvedSiteLocationId =
                titanUser?.SiteLocationId;

        string siteSource =
            titanUser?.SiteId
                .HasValue ==
                true
                    ? "Titan user"
                    : "none";

        if (
            !resolvedSiteId.HasValue
            &&
            primaryDevice?
                .SiteId
                .HasValue ==
                true)
        {
            resolvedSiteId =
                primaryDevice.SiteId;

            resolvedSiteLocationId =
                primaryDevice
                    .SiteLocationId;

            siteSource =
                "managed device";
        }

        // ========================================================
        // VALIDATE SITE RELATION
        // ========================================================

        if (resolvedSiteId.HasValue)
        {
            var siteExists =
                await _db.Sites
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.Id ==
                                resolvedSiteId.Value,
                        cancellationToken);

            if (!siteExists)
            {
                resolvedSiteId =
                    null;

                resolvedSiteLocationId =
                    null;

                siteSource =
                    "invalid";
            }
        }

        if (
            resolvedSiteLocationId
                .HasValue)
        {
            var locationExists =
                await _db.SiteLocations
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.Id ==
                                resolvedSiteLocationId.Value
                            &&
                            x.SiteId ==
                                resolvedSiteId,
                        cancellationToken);

            if (!locationExists)
            {
                resolvedSiteLocationId =
                    null;
            }
        }

        // ========================================================
        // APPLY ENRICHMENT
        // ========================================================

        var changed =
            false;

        var changes =
            new List<string>();

        // --------------------------------------------------------
        // ENTRA LINK
        // --------------------------------------------------------

        if (
            entraUser is not null
            &&
            (
                string.IsNullOrWhiteSpace(
                    ticket.EntraObjectId)
                ||
                string.IsNullOrWhiteSpace(
                    ticket.EntraUserPrincipalName)
            ))
        {
            ticket.LinkEntraRequester(
                entraUser.EntraObjectId,
                entraUser.UserPrincipalName);

            changed =
                true;

            changes.Add(
                "identidad Entra vinculada");
        }

        // --------------------------------------------------------
        // SITE
        // --------------------------------------------------------

        if (
            !ticket.SiteId.HasValue
            &&
            resolvedSiteId.HasValue)
        {
            ticket.AssignSite(
                resolvedSiteId,
                resolvedSiteLocationId);

            changed =
                true;

            changes.Add(
                resolvedSiteLocationId
                    .HasValue
                        ? "localidad y sublocalidad inferidas"
                        : "localidad inferida");
        }

        // --------------------------------------------------------
        // DEVICE
        // --------------------------------------------------------

        if (
            !ticket.DeviceId.HasValue
            &&
            primaryDevice is not null)
        {
            ticket.LinkDevice(
                primaryDevice.Id);

            changed =
                true;

            changes.Add(
                $"dispositivo correlacionado ({primaryDevice.DeviceName})");
        }

        // ========================================================
        // SAVE + AUDIT
        // ========================================================

        if (changed)
        {
            var requesterDescription =
                BuildRequesterDescription(
                    normalizedEmail,
                    titanUser,
                    entraUser,
                    primaryDevice,
                    resolvedSiteId,
                    resolvedSiteLocationId,
                    siteSource);

            _db.HelpdeskTicketEvents
                .Add(
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticket.Id,
                        ticket.RequesterUserId,
                        "requester_intelligence",
                        requesterDescription));

            await _db.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Requester Intelligence enriqueció ticket {TicketNumber}: {Changes}",
                ticket.Number,
                string.Join(
                    ", ",
                    changes));
        }

        return new HelpdeskRequesterIntelligenceResult(
            Found:
                true,

            TicketId:
                ticket.Id,

            TicketNumber:
                ticket.Number,

            Changed:
                changed,

            IdentityEmail:
                normalizedEmail,

            TitanUserId:
                titanUser?.Id,

            EntraObjectId:
                entraUser?.EntraObjectId,

            JobTitle:
                titanUser?.JobTitle
                ??
                entraUser?.JobTitle,

            Department:
                entraUser?.Department
                ??
                primaryDevice?.Department,

            SiteId:
                resolvedSiteId,

            SiteLocationId:
                resolvedSiteLocationId,

            DeviceId:
                primaryDevice?.Id,

            DeviceName:
                primaryDevice?.DeviceName,

            Reason:
                changed
                    ? string.Join(
                        ", ",
                        changes)
                    : "No había datos nuevos que aplicar.");
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string?
        NormalizeEmail(
            string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            value
                .Trim()
                .ToLowerInvariant();

        return normalized.Length <=
            320
                ? normalized
                : null;
    }

    private static void
        AddIdentity(
            ISet<string> identities,
            string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return;
        }

        var normalized =
            NormalizeIdentity(
                value);

        if (
            !string.IsNullOrWhiteSpace(
                normalized))
        {
            identities.Add(
                normalized);
        }
    }

    private static bool
        IsIdentityMatch(
            string? assignedUser,
            IReadOnlySet<string> identities)
    {
        if (
            string.IsNullOrWhiteSpace(
                assignedUser))
        {
            return false;
        }

        var normalized =
            NormalizeIdentity(
                assignedUser);

        if (
            identities.Contains(
                normalized))
        {
            return true;
        }

        /*
         * Algunos agentes reportan:
         *
         * DOMAIN\usuario
         *
         * mientras Entra entrega:
         *
         * usuario@dominio.com
         */
        var slash =
            normalized
                .LastIndexOf(
                    '\\');

        if (
            slash >= 0
            &&
            slash <
                normalized.Length -
                1)
        {
            var shortName =
                normalized[
                    (slash + 1)..];

            if (
                identities.Any(
                    identity =>
                        identity.StartsWith(
                            shortName +
                            "@",
                            StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        /*
         * También puede venir solamente el alias:
         *
         * esosa
         *
         * frente a:
         *
         * esosa@empresa.com
         */
        if (
            !normalized.Contains(
                '@'))
        {
            return identities
                .Any(
                    identity =>
                        identity.StartsWith(
                            normalized +
                            "@",
                            StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static string
        NormalizeIdentity(
            string value)
    {
        return value
            .Trim()
            .ToLowerInvariant();
    }

    private static string
        BuildRequesterDescription(
            string email,
            User? titanUser,
            EntraDirectoryUser? entraUser,
            DeviceCandidate? device,
            Guid? siteId,
            Guid? siteLocationId,
            string siteSource)
    {
        var parts =
            new List<string>
            {
                $"Identidad resuelta para {email}."
            };

        if (titanUser is not null)
        {
            parts.Add(
                $"Usuario TitanMDM: {titanUser.FullName}.");
        }

        if (entraUser is not null)
        {
            parts.Add(
                $"Entra: {entraUser.DisplayName}.");

            if (
                !string.IsNullOrWhiteSpace(
                    entraUser.JobTitle))
            {
                parts.Add(
                    $"Cargo: {entraUser.JobTitle}.");
            }

            if (
                !string.IsNullOrWhiteSpace(
                    entraUser.Department))
            {
                parts.Add(
                    $"Departamento: {entraUser.Department}.");
            }
        }

        if (device is not null)
        {
            parts.Add(
                $"Equipo correlacionado: {device.DeviceName}.");
        }

        if (siteId.HasValue)
        {
            parts.Add(
                $"Localidad inferida mediante {siteSource}.");

            if (
                siteLocationId.HasValue)
            {
                parts.Add(
                    "Sublocalidad disponible.");
            }
        }

        return string.Join(
            " ",
            parts);
    }

    // ============================================================
    // INTERNAL RECORDS
    // ============================================================

    private sealed record DeviceCandidate(
        Guid Id,
        string DeviceName,
        string? AssignedUser,
        string? Department,
        Guid? SiteId,
        Guid? SiteLocationId,
        bool IsManaged,
        DateTime? LastSeenAtUtc);
}

public sealed record HelpdeskRequesterIntelligenceResult(
    bool Found,
    Guid TicketId,
    string TicketNumber,
    bool Changed,
    string? IdentityEmail,
    Guid? TitanUserId,
    string? EntraObjectId,
    string? JobTitle,
    string? Department,
    Guid? SiteId,
    Guid? SiteLocationId,
    Guid? DeviceId,
    string? DeviceName,
    string Reason)
{
    public static HelpdeskRequesterIntelligenceResult
        NotFound(
            Guid ticketId) =>
            new(
                Found:
                    false,

                TicketId:
                    ticketId,

                TicketNumber:
                    string.Empty,

                Changed:
                    false,

                IdentityEmail:
                    null,

                TitanUserId:
                    null,

                EntraObjectId:
                    null,

                JobTitle:
                    null,

                Department:
                    null,

                SiteId:
                    null,

                SiteLocationId:
                    null,

                DeviceId:
                    null,

                DeviceName:
                    null,

                Reason:
                    "Ticket no encontrado.");

    public static HelpdeskRequesterIntelligenceResult
        Skipped(
            Guid ticketId,
            string ticketNumber,
            string reason) =>
            new(
                Found:
                    true,

                TicketId:
                    ticketId,

                TicketNumber:
                    ticketNumber,

                Changed:
                    false,

                IdentityEmail:
                    null,

                TitanUserId:
                    null,

                EntraObjectId:
                    null,

                JobTitle:
                    null,

                Department:
                    null,

                SiteId:
                    null,

                SiteLocationId:
                    null,

                DeviceId:
                    null,

                DeviceName:
                    null,

                Reason:
                    reason);
}
