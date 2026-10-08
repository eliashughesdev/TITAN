namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskAutomationSettings
{
    private HelpdeskAutomationSettings()
    {
    }

    public HelpdeskAutomationSettings(
        Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        OrganizationId =
            organizationId;
    }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public bool ClassificationEnabled
    {
        get;
        private set;
    }

    public int EscalationDelayMinutes
    {
        get;
        private set;
    } = 120;

    public int ReopenDays
    {
        get;
        private set;
    } = 5;

    /*
     * ==========================================================
     * SLA POR PRIORIDAD
     * ==========================================================
     */

    public int LowFirstResponseMinutes
    {
        get;
        private set;
    } = 720;

    public int LowResolutionMinutes
    {
        get;
        private set;
    } = 4320;

    public int MediumFirstResponseMinutes
    {
        get;
        private set;
    } = 360;

    public int MediumResolutionMinutes
    {
        get;
        private set;
    } = 1440;

    public int HighFirstResponseMinutes
    {
        get;
        private set;
    } = 120;

    public int HighResolutionMinutes
    {
        get;
        private set;
    } = 480;

    public int CriticalFirstResponseMinutes
    {
        get;
        private set;
    } = 60;

    public int CriticalResolutionMinutes
    {
        get;
        private set;
    } = 240;

    /*
     * Cuando está habilitado:
     *
     * pendinguser -> pausa SLA.
     */
    public bool PauseSlaWhenWaitingUser
    {
        get;
        private set;
    } = true;

    public int Revision
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    } = DateTime.UtcNow;

    public void Configure(
        bool classification,
        int escalation,
        int reopen,
        int lowFirstResponseMinutes,
        int lowResolutionMinutes,
        int mediumFirstResponseMinutes,
        int mediumResolutionMinutes,
        int highFirstResponseMinutes,
        int highResolutionMinutes,
        int criticalFirstResponseMinutes,
        int criticalResolutionMinutes,
        bool pauseSlaWhenWaitingUser)
    {
        if (escalation is < 15 or > 1440)
        {
            throw new ArgumentException(
                "El escalamiento debe estar entre 15 y 1440 minutos.");
        }

        if (reopen is < 1 or > 30)
        {
            throw new ArgumentException(
                "La reapertura debe estar entre 1 y 30 días.");
        }

        ValidateSla(
            "Baja",
            lowFirstResponseMinutes,
            lowResolutionMinutes);

        ValidateSla(
            "Media",
            mediumFirstResponseMinutes,
            mediumResolutionMinutes);

        ValidateSla(
            "Alta",
            highFirstResponseMinutes,
            highResolutionMinutes);

        ValidateSla(
            "Crítica",
            criticalFirstResponseMinutes,
            criticalResolutionMinutes);

        ClassificationEnabled =
            classification;

        EscalationDelayMinutes =
            escalation;

        ReopenDays =
            reopen;

        LowFirstResponseMinutes =
            lowFirstResponseMinutes;

        LowResolutionMinutes =
            lowResolutionMinutes;

        MediumFirstResponseMinutes =
            mediumFirstResponseMinutes;

        MediumResolutionMinutes =
            mediumResolutionMinutes;

        HighFirstResponseMinutes =
            highFirstResponseMinutes;

        HighResolutionMinutes =
            highResolutionMinutes;

        CriticalFirstResponseMinutes =
            criticalFirstResponseMinutes;

        CriticalResolutionMinutes =
            criticalResolutionMinutes;

        PauseSlaWhenWaitingUser =
            pauseSlaWhenWaitingUser;

        UpdatedAtUtc =
            DateTime.UtcNow;

        Revision++;
    }

    public HelpdeskSlaDefinition GetSla(
        string? priority)
    {
        var normalized =
            string.IsNullOrWhiteSpace(
                priority)
                ? "medium"
                : priority
                    .Trim()
                    .ToLowerInvariant();

        return normalized switch
        {
            "low" =>
                new HelpdeskSlaDefinition(
                    LowFirstResponseMinutes,
                    LowResolutionMinutes),

            "high" =>
                new HelpdeskSlaDefinition(
                    HighFirstResponseMinutes,
                    HighResolutionMinutes),

            "critical" or "urgent" =>
                new HelpdeskSlaDefinition(
                    CriticalFirstResponseMinutes,
                    CriticalResolutionMinutes),

            _ =>
                new HelpdeskSlaDefinition(
                    MediumFirstResponseMinutes,
                    MediumResolutionMinutes)
        };
    }

    private static void ValidateSla(
        string priorityName,
        int firstResponseMinutes,
        int resolutionMinutes)
    {
        if (firstResponseMinutes is < 1 or > 43200)
        {
            throw new ArgumentException(
                $"El SLA de primera respuesta para prioridad {priorityName} " +
                "debe estar entre 1 y 43200 minutos.");
        }

        if (resolutionMinutes is < 1 or > 43200)
        {
            throw new ArgumentException(
                $"El SLA de resolución para prioridad {priorityName} " +
                "debe estar entre 1 y 43200 minutos.");
        }

        if (resolutionMinutes <=
            firstResponseMinutes)
        {
            throw new ArgumentException(
                $"El SLA de resolución para prioridad {priorityName} " +
                "debe ser mayor al SLA de primera respuesta.");
        }
    }
}

public sealed record HelpdeskSlaDefinition(
    int FirstResponseMinutes,
    int ResolutionMinutes);