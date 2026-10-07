using TitanMDM.Domain.Helpdesk;

namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskTicket
{
    private HelpdeskTicket()
    {
    }

    public HelpdeskTicket(
        Guid organizationId,
        string number,
        string subject,
        string description,
        string type,
        string priority,
        string category,
        string source,
        Guid requesterUserId,
        Guid? deviceId,
        Guid? queueId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException(
                "Number is required.",
                nameof(number));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException(
                "Subject is required.",
                nameof(subject));
        }

        if (requesterUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "RequesterUserId is required.",
                nameof(requesterUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;

        Number = number.Trim();
        Subject = subject.Trim();

        Description =
            (description ?? string.Empty)
            .Trim();

        Type = NormalizeType(type);
        Priority = NormalizePriority(priority);

        Category = NormalizeCategory(category);

        Source =
            string.IsNullOrWhiteSpace(source)
                ? "console"
                : source.Trim().ToLowerInvariant();

        Status = HelpdeskTicketStatus.New;

        RequesterUserId = requesterUserId;

        DeviceId = deviceId;
        QueueId = queueId;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Number { get; private set; } =
        string.Empty;

    public string Subject { get; private set; } =
        string.Empty;

    public string Description { get; private set; } =
        string.Empty;

    public string Type { get; private set; } =
        "incident";

    public string Priority { get; private set; } =
        "medium";

    public string Status { get; private set; } =
        HelpdeskTicketStatus.New;

    public string Category { get; private set; } =
        "general";

    public string Source { get; private set; } =
        "console";

    public Guid RequesterUserId { get; private set; }

    public Guid? AssigneeUserId { get; private set; }

    public Guid? DeviceId { get; private set; }

    public Guid? QueueId { get; private set; }

    public Guid? RequestedTeamId { get; private set; }

    public Guid? RemoteSessionId { get; private set; }

    public Guid? SiteId { get; private set; }

    public Guid? SiteLocationId { get; private set; }

    public string? EntraObjectId { get; private set; }

    public string? EntraUserPrincipalName
    {
        get;
        private set;
    }

    public DateTime? FirstResponseDueAtUtc
    {
        get;
        private set;
    }

    public DateTime? ResolveDueAtUtc
    {
        get;
        private set;
    }

    public DateTime? FirstRespondedAtUtc
    {
        get;
        private set;
    }

    public DateTime? ResolvedAtUtc
    {
        get;
        private set;
    }

    public DateTime? SlaPausedAtUtc
    {
        get;
        private set;
    }

    public DateTime? SuspendedFirstResponseDueAtUtc
    {
        get;
        private set;
    }

    public DateTime? SuspendedResolveDueAtUtc
    {
        get;
        private set;
    }

    public long TotalSlaPausedSeconds
    {
        get;
        private set;
    }

    public long FirstResponsePausedSeconds
    {
        get;
        private set;
    }

    public string? ExternalRequesterName
    {
        get;
        private set;
    }

    public string? ExternalRequesterEmail
    {
        get;
        private set;
    }

    public DateTime CreatedAtUtc
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    }

    /*
     * ==========================================================
     * ASSIGNMENT
     * ==========================================================
     */

    public void SelectGroup(
        Guid teamId)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException(
                "Selecciona un grupo válido.",
                nameof(teamId));
        }

        RequestedTeamId = teamId;

        Touch();
    }

    public void Assign(
        Guid assigneeUserId)
    {
        if (assigneeUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Selecciona un técnico válido.",
                nameof(assigneeUserId));
        }

        AssigneeUserId = assigneeUserId;

        /*
         * Un ticket nuevo se considera atendido cuando
         * recibe un técnico.
         *
         * No usamos Transition aquí para evitar que una
         * asignación accidentalmente altere SLA más allá
         * del cambio new -> open.
         */
        if (Status == HelpdeskTicketStatus.New)
        {
            Status = HelpdeskTicketStatus.Open;
        }

        Touch();
    }

    public void Unassign()
    {
        AssigneeUserId = null;

        Touch();
    }

    /*
     * ==========================================================
     * STATUS STATE MACHINE
     * ==========================================================
     */

    public void Transition(
        string targetStatus)
    {
        var target =
            HelpdeskTicketStatus.Normalize(
                targetStatus);

        var current =
            HelpdeskTicketStatus.Normalize(
                Status);

        if (current == target)
        {
            Touch();
            return;
        }

        if (!HelpdeskTicketStatus.CanTransition(
                current,
                target))
        {
            throw new InvalidOperationException(
                $"Transición de ticket no permitida: " +
                $"{current} -> {target}.");
        }

        var now =
            DateTime.UtcNow;

        HandleSlaTransition(
            current,
            target,
            now);

        Status = target;

        if (HelpdeskTicketStatus.IsTerminal(
                target))
        {
            ResolvedAtUtc ??= now;
        }
        else if (
            HelpdeskTicketStatus.IsReopen(
                current,
                target))
        {
            ResolvedAtUtc = null;
        }
        else if (
            target != HelpdeskTicketStatus.Resolved &&
            target != HelpdeskTicketStatus.Closed)
        {
            ResolvedAtUtc = null;
        }

        Touch(now);
    }

    public void Reopen()
    {
        if (!HelpdeskTicketStatus.IsReopen(
                Status,
                HelpdeskTicketStatus.Open))
        {
            throw new InvalidOperationException(
                $"El ticket en estado '{Status}' " +
                "no puede reabrirse.");
        }

        Transition(
            HelpdeskTicketStatus.Open);
    }

    private void HandleSlaTransition(
        string currentStatus,
        string targetStatus,
        DateTime now)
    {
        var enteringPause =
            !HelpdeskTicketStatus.IsSlaPaused(
                currentStatus)
            &&
            HelpdeskTicketStatus.IsSlaPaused(
                targetStatus);

        var leavingPause =
            HelpdeskTicketStatus.IsSlaPaused(
                currentStatus)
            &&
            !HelpdeskTicketStatus.IsSlaPaused(
                targetStatus);

        if (enteringPause)
        {
            PauseSla(now);
            return;
        }

        if (leavingPause)
        {
            ResumeSla(now);
        }
    }

    private void PauseSla(
        DateTime now)
    {
        if (SlaPausedAtUtc.HasValue)
        {
            return;
        }

        SlaPausedAtUtc = now;

        SuspendedFirstResponseDueAtUtc =
            FirstResponseDueAtUtc;

        SuspendedResolveDueAtUtc =
            ResolveDueAtUtc;

        FirstResponseDueAtUtc = null;
        ResolveDueAtUtc = null;
    }

    private void ResumeSla(
        DateTime now)
    {
        if (!SlaPausedAtUtc.HasValue)
        {
            return;
        }

        var elapsed =
            now - SlaPausedAtUtc.Value;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var elapsedSeconds =
            (long)Math.Max(
                0,
                elapsed.TotalSeconds);

        TotalSlaPausedSeconds +=
            elapsedSeconds;

        if (FirstRespondedAtUtc is null)
        {
            FirstResponsePausedSeconds +=
                elapsedSeconds;
        }

        FirstResponseDueAtUtc =
            SuspendedFirstResponseDueAtUtc?
            .Add(elapsed);

        ResolveDueAtUtc =
            SuspendedResolveDueAtUtc?
            .Add(elapsed);

        SuspendedFirstResponseDueAtUtc =
            null;

        SuspendedResolveDueAtUtc =
            null;

        SlaPausedAtUtc =
            null;
    }

    /*
     * ==========================================================
     * SLA
     * ==========================================================
     */

    public void ApplySla(
        DateTime firstResponseDue,
        DateTime resolveDue)
    {
        if (resolveDue <= firstResponseDue)
        {
            throw new ArgumentException(
                "La fecha de resolución debe ser " +
                "posterior a la fecha de primera respuesta.");
        }

        if (SlaPausedAtUtc.HasValue)
        {
            SuspendedFirstResponseDueAtUtc =
                firstResponseDue;

            SuspendedResolveDueAtUtc =
                resolveDue;
        }
        else
        {
            FirstResponseDueAtUtc =
                firstResponseDue;

            ResolveDueAtUtc =
                resolveDue;
        }

        Touch();
    }

    public void MarkFirstResponse()
    {
        if (FirstRespondedAtUtc.HasValue)
        {
            return;
        }

        var now =
            DateTime.UtcNow;

        /*
         * Si la primera respuesta ocurre mientras el
         * ticket está pausado, contabilizamos hasta ese
         * momento únicamente para la métrica de primera
         * respuesta.
         */
        if (SlaPausedAtUtc.HasValue)
        {
            var elapsed =
                now - SlaPausedAtUtc.Value;

            if (elapsed > TimeSpan.Zero)
            {
                FirstResponsePausedSeconds +=
                    (long)elapsed.TotalSeconds;
            }
        }

        FirstRespondedAtUtc = now;

        Touch(now);
    }

    /*
     * ==========================================================
     * CLASSIFICATION
     * ==========================================================
     */

    public void Reclassify(
        string category)
    {
        Category =
            NormalizeCategory(
                category);

        Touch();
    }

    public void ChangePriority(
        string priority)
    {
        Priority =
            NormalizePriority(
                priority);

        Touch();
    }

    public void ChangeType(
        string type)
    {
        Type =
            NormalizeType(
                type);

        Touch();
    }

    /*
     * ==========================================================
     * SITE / LOCATION
     * ==========================================================
     */

    public void AssignSite(
        Guid? siteId,
        Guid? siteLocationId = null)
    {
        if (
            !siteId.HasValue &&
            siteLocationId.HasValue)
        {
            throw new InvalidOperationException(
                "No se puede asignar una ubicación " +
                "sin una localidad.");
        }

        if (
            siteId.HasValue &&
            siteId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "SiteId inválido.",
                nameof(siteId));
        }

        if (
            siteLocationId.HasValue &&
            siteLocationId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "SiteLocationId inválido.",
                nameof(siteLocationId));
        }

        SiteId = siteId;
        SiteLocationId = siteLocationId;

        Touch();
    }

    /*
     * ==========================================================
     * LINKS
     * ==========================================================
     */

    public void LinkDevice(
        Guid? deviceId)
    {
        if (
            deviceId.HasValue &&
            deviceId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "DeviceId inválido.",
                nameof(deviceId));
        }

        DeviceId = deviceId;

        Touch();
    }

    public void LinkRemoteSession(
        Guid remoteSessionId)
    {
        if (remoteSessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "RemoteSessionId inválido.",
                nameof(remoteSessionId));
        }

        RemoteSessionId =
            remoteSessionId;

        Touch();
    }

    /*
     * ==========================================================
     * ENTRA / EXTERNAL REQUESTERS
     * ==========================================================
     */

    public void LinkEntraRequester(
        string? objectId,
        string? upn)
    {
        EntraObjectId =
            string.IsNullOrWhiteSpace(
                objectId)
                ? null
                : objectId.Trim();

        EntraUserPrincipalName =
            string.IsNullOrWhiteSpace(
                upn)
                ? null
                : upn
                    .Trim()
                    .ToLowerInvariant();

        Touch();
    }

    public void SetEmailRequester(
        string name,
        string email)
    {
        if (
            string.IsNullOrWhiteSpace(
                email)
            ||
            email.Trim().Length > 320)
        {
            throw new ArgumentException(
                "Valid requester email is required.",
                nameof(email));
        }

        var normalizedEmail =
            email.Trim()
                .ToLowerInvariant();

        var normalizedName =
            string.IsNullOrWhiteSpace(
                name)
                ? normalizedEmail
                : name.Trim();

        ExternalRequesterName =
            normalizedName[
                ..Math.Min(
                    normalizedName.Length,
                    200)];

        ExternalRequesterEmail =
            normalizedEmail;

        Touch();
    }

    /*
     * ==========================================================
     * NORMALIZATION
     * ==========================================================
     */

    private static string NormalizeCategory(
        string? category)
    {
        if (string.IsNullOrWhiteSpace(
                category))
        {
            return "general";
        }

        var normalized =
            category.Trim();

        if (normalized.Length > 80)
        {
            throw new ArgumentException(
                "Categoría inválida.",
                nameof(category));
        }

        return normalized
            .ToLowerInvariant();
    }

    private static string NormalizePriority(
        string? priority)
    {
        var normalized =
            string.IsNullOrWhiteSpace(
                priority)
                ? "medium"
                : priority
                    .Trim()
                    .ToLowerInvariant();

        if (normalized is not (
                "low" or
                "medium" or
                "high" or
                "critical"))
        {
            throw new ArgumentException(
                "Prioridad inválida.",
                nameof(priority));
        }

        return normalized;
    }

    private static string NormalizeType(
        string? type)
    {
        var normalized =
            string.IsNullOrWhiteSpace(
                type)
                ? "incident"
                : type
                    .Trim()
                    .ToLowerInvariant();

        if (normalized is not (
                "incident" or
                "request" or
                "problem" or
                "change"))
        {
            throw new ArgumentException(
                "Tipo de ticket inválido.",
                nameof(type));
        }

        return normalized;
    }

    private void Touch()
    {
        Touch(
            DateTime.UtcNow);
    }

    private void Touch(
        DateTime now)
    {
        UpdatedAtUtc =
            now;
    }
}