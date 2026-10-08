using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Helpdesk;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
    : IHelpdeskService
{
    private readonly TitanMdmDbContext
    _db;

    private readonly HelpdeskTicketNumberGenerator
        _ticketNumbers;

    public HelpdeskService(
       TitanMdmDbContext db,
       HelpdeskTicketNumberGenerator ticketNumbers)
    {
        _db =
            db;

        _ticketNumbers =
            ticketNumbers;
    }

    // ============================================================
    // LIST
    // ============================================================

    public async Task<HelpdeskTicketListResult>
        GetTicketsAsync(
            Guid organizationId,
            HelpdeskTicketQuery query,
            CancellationToken cancellationToken = default)
    {
        var page =
            Math.Max(
                1,
                query.Page);

        var pageSize =
            query.PageSize is < 1 or > 100
                ? 25
                : query.PageSize;

        var tickets =
            _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId);

        if (
            !string.IsNullOrWhiteSpace(
                query.Search))
        {
            var term =
                query.Search
                    .Trim();

            tickets =
                tickets.Where(
                    x =>
                        x.Number.Contains(
                            term)
                        ||
                        x.Subject.Contains(
                            term)
                        ||
                        x.Category.Contains(
                            term));
        }

        if (
            !string.IsNullOrWhiteSpace(
                query.Status))
        {
            var status =
                query.Status
                    .Trim()
                    .ToLowerInvariant();

            tickets =
                tickets.Where(
                    x =>
                        x.Status ==
                            status);
        }

        if (
            !string.IsNullOrWhiteSpace(
                query.Priority))
        {
            var priority =
                query.Priority
                    .Trim()
                    .ToLowerInvariant();

            tickets =
                tickets.Where(
                    x =>
                        x.Priority ==
                            priority);
        }

        if (
            query.DeviceId
                .HasValue)
        {
            tickets =
                tickets.Where(
                    x =>
                        x.DeviceId ==
                            query.DeviceId.Value);
        }

        if (
            query.AssigneeUserId
                .HasValue)
        {
            tickets =
                tickets.Where(
                    x =>
                        x.AssigneeUserId ==
                            query.AssigneeUserId.Value);
        }

        var total =
            await tickets
                .CountAsync(
                    cancellationToken);

        var rows =
            await tickets
                .OrderByDescending(
                    x =>
                        x.CreatedAtUtc)
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(
                    pageSize)
                .ToListAsync(
                    cancellationToken);

        var userIds =
            rows
                .Select(
                    x =>
                        x.RequesterUserId)
                .Concat(
                    rows
                        .Where(
                            x =>
                                x.AssigneeUserId
                                    .HasValue)
                        .Select(
                            x =>
                                x.AssigneeUserId!
                                    .Value))
                .Distinct()
                .ToArray();

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        userIds.Contains(
                            x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    cancellationToken);

        var deviceIds =
            rows
                .Where(
                    x =>
                        x.DeviceId
                            .HasValue)
                .Select(
                    x =>
                        x.DeviceId!
                            .Value)
                .Distinct()
                .ToArray();

        var devices =
            await _db.Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        deviceIds.Contains(
                            x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    cancellationToken);

        var now =
            DateTime.UtcNow;

        var items =
            rows
                .Select(
                    ticket =>
                    {
                        users.TryGetValue(
                            ticket.RequesterUserId,
                            out var requester);

                        User? assignee =
                            null;

                        if (
                            ticket.AssigneeUserId
                                .HasValue)
                        {
                            users.TryGetValue(
                                ticket.AssigneeUserId.Value,
                                out assignee);
                        }

                        Device? device =
                            null;

                        if (
                            ticket.DeviceId
                                .HasValue)
                        {
                            devices.TryGetValue(
                                ticket.DeviceId.Value,
                                out device);
                        }

                        var breached =
                            ticket.Status
                                is not (
                                    "resolved"
                                    or
                                    "closed")
                            &&
                            (
                                (
                                    ticket.FirstResponseDueAtUtc
                                        .HasValue
                                    &&
                                    ticket.FirstRespondedAtUtc
                                        is null
                                    &&
                                    ticket.FirstResponseDueAtUtc <
                                        now
                                )
                                ||
                                (
                                    ticket.ResolveDueAtUtc
                                        .HasValue
                                    &&
                                    ticket.ResolvedAtUtc
                                        is null
                                    &&
                                    ticket.ResolveDueAtUtc <
                                        now
                                )
                            );

                        return new HelpdeskTicketListItemDto(
                            ticket.Id,
                            ticket.Number,
                            ticket.Subject,
                            ticket.Status,
                            ticket.Priority,
                            ticket.Type,
                            ticket.Category,
                            ticket.Source,
                            ticket.RequesterUserId,
                            requester?.FullName ??
                                "Usuario Titan",
                            ticket.AssigneeUserId,
                            assignee?.FullName,
                            ticket.DeviceId,
                            device?.DeviceName,
                            device?.Platform
                                .ToString(),
                            ticket.CreatedAtUtc,
                            ticket.UpdatedAtUtc,
                            ticket.FirstResponseDueAtUtc,
                            ticket.ResolveDueAtUtc,
                            breached);
                    })
                .ToList();

        return new HelpdeskTicketListResult(
            items,
            total,
            page,
            pageSize);
    }

    // ============================================================
    // DETAILS
    // ============================================================

    public async Task<HelpdeskTicketDetailsDto?>
        GetTicketAsync(
            Guid organizationId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        var ticket =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            ticketId,
                    cancellationToken);

        return ticket is null
            ? null
            : await MapDetailsAsync(
                ticket,
                cancellationToken);
    }

    // ============================================================
    // CREATE
    // ============================================================

    public async Task<HelpdeskTicketDetailsDto>
        CreateTicketAsync(
            Guid organizationId,
            Guid actorUserId,
            CreateHelpdeskTicketRequest request,
            CancellationToken cancellationToken = default)
    {
        if (
            string.IsNullOrWhiteSpace(
                request.Subject)
            ||
            request.Subject
                .Trim()
                .Length >
                250)
        {
            throw new ArgumentException(
                "El asunto debe tener entre 1 y 250 caracteres.");
        }

        if (
            request.Description?
                .Length >
                4000)
        {
            throw new ArgumentException(
                "La descripción excede 4000 caracteres.");
        }

        var requesterId =
            request.RequesterUserId ??
            actorUserId;

        var requester =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            requesterId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (requester is null)
        {
            throw new ArgumentException(
                "El solicitante no existe o no pertenece a esta organización.");
        }

        Device? requestDevice =
            null;

        if (
            request.DeviceId
                .HasValue)
        {
            requestDevice =
                await _db.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                                request.DeviceId.Value
                            &&
                            x.OrganizationId ==
                                organizationId,
                        cancellationToken);

            if (requestDevice is null)
            {
                throw new ArgumentException(
                    "El dispositivo no pertenece a esta organización.");
            }
        }

        var source =
            string.IsNullOrWhiteSpace(
                request.Source)
                ? "console"
                : request.Source
                    .Trim()
                    .ToLowerInvariant();

        var requestedCategory =
            string.IsNullOrWhiteSpace(
                request.Category)
                ? "general"
                : request.Category
                    .Trim()
                    .ToLowerInvariant();

        if (
            requestedCategory.Length >
            80)
        {
            throw new ArgumentException(
                "La categoría no puede superar 80 caracteres.");
        }

        var rawCategories =
            await _db.HelpdeskTeams
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive)
                .Select(
                    x =>
                        x.Categories)
                .ToListAsync(
                    cancellationToken);

        var categories =
            rawCategories
                .SelectMany(
                    value =>
                        (
                            value ??
                            string.Empty
                        )
                        .Split(
                            '|',
                            StringSplitOptions
                                .RemoveEmptyEntries
                            |
                            StringSplitOptions
                                .TrimEntries))
                .Select(
                    value =>
                        value
                            .Trim()
                            .ToLowerInvariant())
                .Where(
                    value =>
                        value.Length
                            is > 0
                            and <= 80)
                .Append(
                    "general")
                .ToHashSet(
                    StringComparer
                        .OrdinalIgnoreCase);

        var category =
            requestedCategory;

        if (
            !categories.Contains(
                category))
        {
            if (
                source ==
                "email")
            {
                category =
                    "general";
            }
            else
            {
                throw new ArgumentException(
                    "Selecciona una categoría activa del catálogo de Helpdesk.");
            }
        }

        var number =
    await _ticketNumbers
        .NextAsync(
            cancellationToken);

        var ticket =
            new HelpdeskTicket(
                organizationId,
                number,
                request.Subject
                    .Trim(),
                request.Description ??
                    string.Empty,
                request.Type ??
                    "incident",
                request.Priority ??
                    "medium",
                category,
                source,
                requesterId,
                request.DeviceId,
                null);

        // ========================================================
        // MULTI-SITE INFERENCE
        //
        // Priority:
        // 1. Explicit Device location
        // 2. User location
        // ========================================================

        Guid? inferredSiteId =
            null;

        Guid? inferredSiteLocationId =
            null;

        if (
            requestDevice?.SiteId
                .HasValue ==
            true)
        {
            inferredSiteId =
                requestDevice.SiteId;

            inferredSiteLocationId =
                requestDevice.SiteLocationId;
        }
        else if (
            requester.SiteId
                .HasValue)
        {
            inferredSiteId =
                requester.SiteId;

            inferredSiteLocationId =
                requester.SiteLocationId;
        }

        if (
            inferredSiteId
                .HasValue)
        {
            ticket.AssignSite(
                inferredSiteId.Value,
                inferredSiteLocationId);

            _db.HelpdeskTicketEvents
                .Add(
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticket.Id,
                        actorUserId,
                        "site_inferred",
                        requestDevice?.SiteId
                            .HasValue ==
                            true
                            ? "Localidad inferida desde el dispositivo asociado."
                            : "Localidad inferida desde el usuario solicitante."));
        }

        // ========================================================
        // ENTRA REQUESTER
        // ========================================================

        if (
            !string.IsNullOrWhiteSpace(
                request.EntraObjectId))
        {
            var directoryUser =
                await _db
                    .EntraDirectoryUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            x.EntraObjectId ==
                                request.EntraObjectId,
                        cancellationToken);

            if (
                directoryUser
                is null)
            {
                throw new ArgumentException(
                    "El solicitante de Entra ID no existe en esta organización.");
            }

            ticket.LinkEntraRequester(
                directoryUser.EntraObjectId,
                directoryUser.UserPrincipalName);
        }

        // ========================================================
        // SLA
        // ========================================================

        var slaSettings =
      await _db
          .Set<HelpdeskAutomationSettings>()
          .AsNoTracking()
          .FirstOrDefaultAsync(
              x =>
                  x.OrganizationId ==
                      organizationId,
              cancellationToken)
      ??
      new HelpdeskAutomationSettings(
          organizationId);

        var sla =
            slaSettings.GetSla(
                ticket.Priority);

        var now =
            DateTime.UtcNow;

        ticket.ApplySla(
            now.AddMinutes(
                sla.FirstResponseMinutes),
            now.AddMinutes(
                sla.ResolutionMinutes));

        _db.HelpdeskTickets
            .Add(
                ticket);

        _db.HelpdeskTicketEvents
            .Add(
                new HelpdeskTicketEvent(
                    organizationId,
                    ticket.Id,
                    actorUserId,
                    "created",
                    $"Ticket {ticket.Number} creado."));

        if (
            category !=
            requestedCategory)
        {
            _db.HelpdeskTicketEvents
                .Add(
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticket.Id,
                        actorUserId,
                        "category_fallback",
                        "La categoría recibida por correo no estaba configurada. " +
                        "Se utilizó general."));
        }

        // ========================================================
        // HD-D4 - IMMEDIATE ENTERPRISE ROUTING
        // ========================================================

        /*
         * El routing determinÃ­stico se intenta inmediatamente.
         *
         * Reglas:
         *
         * - dispositivo -> localidad;
         * - solicitante -> localidad;
         * - grupo solicitado -> cobertura;
         * - cobertura global -> no requiere Site;
         * - categorÃ­a general puede ser clasificada posteriormente
         *   por HelpdeskRoutingWorker;
         * - OpenRouter NO selecciona tÃ©cnico.
         */
        var routingEvaluation =
            await EvaluateRoutingAsync(
                organizationId,
                requesterId,
                ticket.Category,
                cancellationToken,
                ticket.RequestedTeamId,
                ticket.SiteId,
                ticket.SiteLocationId);

        var routing =
            routingEvaluation.Candidate;

        if (
            routing is not null)
        {
            /*
             * Si el motor pudo inferir una localidad segura,
             * persistimos dicha informaciÃ³n en el ticket.
             */
            if (
                !ticket.SiteId.HasValue
                &&
                routingEvaluation.SiteId.HasValue)
            {
                ticket.AssignSite(
                    routingEvaluation.SiteId,
                    routingEvaluation.SiteLocationId);

                _db.HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            organizationId,
                            ticket.Id,
                            actorUserId,
                            "site_inferred",
                            "Localidad inferida automÃ¡ticamente por el motor de routing."));
            }

            ticket.SelectGroup(
                routing.TeamId);

            ticket.Assign(
                routing.UserId);

            var autoAssignmentSummary =
                "AsignaciÃ³n automÃ¡tica inmediata: " +
                BuildRoutingReason(
                    routingEvaluation.RequesterLocation
                    ??
                    "Sin localidad",
                    ticket.Category,
                    routing);

            _db.HelpdeskTicketEvents
                .Add(
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticket.Id,
                        actorUserId,
                        "auto_assigned",
                        TrimSummary(
                            autoAssignmentSummary)));
        }
        else
        {
            _db.HelpdeskTicketEvents
                .Add(
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticket.Id,
                        actorUserId,
                        "routing_pending",
                        TrimSummary(
                            "Sin asignaciÃ³n automÃ¡tica inmediata: " +
                            routingEvaluation.Reason)));
        }
        await _db
            .SaveChangesAsync(
                cancellationToken);

        return (
            await GetTicketAsync(
                organizationId,
                ticket.Id,
                cancellationToken)
        )!;
    }

    // ============================================================
    // COMMENTS
    // ============================================================

    public async Task<HelpdeskTicketDetailsDto?>
        AddCommentAsync(
            Guid organizationId,
            Guid ticketId,
            Guid actorUserId,
            AddHelpdeskCommentRequest request,
            CancellationToken cancellationToken = default)
    {
        if (
            string.IsNullOrWhiteSpace(
                request.Body)
            ||
            request.Body
                .Trim()
                .Length >
                4000)
        {
            throw new ArgumentException(
                "El comentario debe tener entre 1 y 4000 caracteres.");
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

        if (
            ticket is null)
        {
            return null;
        }

        _db.HelpdeskTicketComments
            .Add(
                new HelpdeskTicketComment(
                    organizationId,
                    ticket.Id,
                    actorUserId,
                    request.Body
                        .Trim(),
                    request.IsInternal));

        if (
            !request.IsInternal
            &&
            actorUserId !=
                ticket.RequesterUserId
            &&
            await EligibleTechnicians(
                    organizationId)
                .AnyAsync(
                    x =>
                        x ==
                        actorUserId,
                    cancellationToken))
        {
            ticket.MarkFirstResponse();
        }

        if (
            ticket.Status ==
            "new")
        {
            ticket.Transition(
                "open");
        }

        _db.HelpdeskTicketEvents
            .Add(
                new HelpdeskTicketEvent(
                    organizationId,
                    ticket.Id,
                    actorUserId,
                    request.IsInternal
                        ? "internal_note"
                        : "comment",
                    request.IsInternal
                        ? "Nota interna agregada."
                        : "Respuesta pública agregada."));

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return await GetTicketAsync(
            organizationId,
            ticket.Id,
            cancellationToken);
    }

    // ============================================================
    // MANUAL ASSIGNMENT
    // ============================================================

    public async Task<HelpdeskTicketDetailsDto?>
        AssignAsync(
            Guid organizationId,
            Guid ticketId,
            Guid actorUserId,
            AssignHelpdeskTicketRequest request,
            CancellationToken cancellationToken = default)
    {
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

        if (
            ticket is null)
        {
            return null;
        }

        var assigneeExists =
            await _db.Users
                .AnyAsync(
                    x =>
                        x.Id ==
                            request.AssigneeUserId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (
            !assigneeExists
            ||
            !await EligibleTechnicians(
                    organizationId)
                .AnyAsync(
                    x =>
                        x ==
                        request.AssigneeUserId,
                    cancellationToken))
        {
            throw new InvalidOperationException(
                "El técnico debe estar activo y tener permiso tickets.comment.");
        }

        ticket.Assign(
            request.AssigneeUserId);

        _db.HelpdeskTicketEvents
            .Add(
                new HelpdeskTicketEvent(
                    organizationId,
                    ticket.Id,
                    actorUserId,
                    "assigned",
                    $"Asignación manual al usuario {request.AssigneeUserId}."));

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return await GetTicketAsync(
            organizationId,
            ticket.Id,
            cancellationToken);
    }

    // ============================================================
    // STATUS TRANSITION
    // ============================================================

    public async Task<HelpdeskTicketDetailsDto?>
        TransitionAsync(
            Guid organizationId,
            Guid ticketId,
            Guid actorUserId,
            TransitionHelpdeskTicketRequest request,
            CancellationToken cancellationToken = default)
    {
        var allowed =
            new[]
            {
                "open",
                "inprogress",
                "pendinguser",
                "resolved",
                "closed"
            };

        var status =
            request.Status?
                .Trim()
                .ToLowerInvariant();

        if (
            status is null
            ||
            !allowed.Contains(
                status))
        {
            throw new ArgumentException(
                "Estado de ticket no válido.");
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

        if (
            ticket is null)
        {
            return null;
        }

        var previousStatus =
            ticket.Status;

        ticket.Transition(
            status);

        var reopened =
            previousStatus
                is "resolved"
                or "closed"
            &&
            status
                is not (
                    "resolved"
                    or
                    "closed");

        _db.HelpdeskTicketEvents
            .Add(
                new HelpdeskTicketEvent(
                    organizationId,
                    ticket.Id,
                    actorUserId,
                    reopened
                        ? "reopened"
                        : "status",
                    $"Estado actualizado de {previousStatus} a {ticket.Status}."));

        await _db
            .SaveChangesAsync(
                cancellationToken);

        return await GetTicketAsync(
            organizationId,
            ticket.Id,
            cancellationToken);
    }

    // ============================================================
    // DETAILS MAPPING
    // ============================================================

    private async Task<HelpdeskTicketDetailsDto>
        MapDetailsAsync(
            HelpdeskTicket ticket,
            CancellationToken cancellationToken)
    {
        var requester =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            ticket.RequesterUserId
                        &&
                        x.OrganizationId ==
                            ticket.OrganizationId,
                    cancellationToken);

        User? assignee =
            null;

        if (
            ticket.AssigneeUserId
                .HasValue)
        {
            assignee =
                await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                                ticket.AssigneeUserId.Value
                            &&
                            x.OrganizationId ==
                                ticket.OrganizationId,
                        cancellationToken);
        }

        Device? device =
            null;

        if (
            ticket.DeviceId
                .HasValue)
        {
            device =
                await _db.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                                ticket.DeviceId.Value
                            &&
                            x.OrganizationId ==
                                ticket.OrganizationId,
                        cancellationToken);
        }

        var comments =
            await _db.HelpdeskTicketComments
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.TicketId ==
                            ticket.Id)
                .OrderBy(
                    x =>
                        x.CreatedAtUtc)
                .ToListAsync(
                    cancellationToken);

        var authorIds =
            comments
                .Select(
                    x =>
                        x.AuthorUserId)
                .Distinct()
                .ToArray();

        var authors =
            await _db.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        authorIds.Contains(
                            x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    cancellationToken);

        var events =
            await _db.HelpdeskTicketEvents
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.TicketId ==
                            ticket.Id)
                .OrderBy(
                    x =>
                        x.CreatedAtUtc)
                .ToListAsync(
                    cancellationToken);

        return new HelpdeskTicketDetailsDto(
            ticket.Id,
            ticket.Number,
            ticket.Subject,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.Type,
            ticket.Category,
            ticket.Source,
            ticket.RequesterUserId,
            requester?.FullName ??
                "Usuario Titan",
            ticket.AssigneeUserId,
            assignee?.FullName,
            ticket.DeviceId,
            device?.DeviceName,
            device?.Platform
                .ToString(),
            ticket.RemoteSessionId,
            ticket.EntraUserPrincipalName,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.FirstResponseDueAtUtc,
            ticket.ResolveDueAtUtc,
            ticket.Status
                is not (
                    "resolved"
                    or
                    "closed")
            &&
            (
                (
                    ticket.FirstRespondedAtUtc
                        is null
                    &&
                    ticket.FirstResponseDueAtUtc <
                        DateTime.UtcNow
                )
                ||
                (
                    ticket.ResolvedAtUtc
                        is null
                    &&
                    ticket.ResolveDueAtUtc <
                        DateTime.UtcNow
                )
            ),
            comments
                .Select(
                    item =>
                    {
                        authors.TryGetValue(
                            item.AuthorUserId,
                            out var author);

                        return new HelpdeskCommentDto(
                            item.Id,
                            item.AuthorUserId,
                            author?.FullName ??
                                "Usuario",
                            item.Body,
                            item.IsInternal,
                            item.CreatedAtUtc);
                    })
                .ToList(),
            events
                .Select(
                    item =>
                        new HelpdeskEventDto(
                            item.Id,
                            item.EventType,
                            item.Summary,
                            item.CreatedAtUtc))
                .ToList());
    }
}