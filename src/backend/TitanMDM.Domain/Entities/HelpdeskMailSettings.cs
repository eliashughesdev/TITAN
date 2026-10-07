namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskMailSettings
{
    private HelpdeskMailSettings()
    {
    }

    public HelpdeskMailSettings(
        Guid organizationId)
    {
        if (
            organizationId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        OrganizationId =
            organizationId;

        InboundEnabled =
            false;

        OutboundEnabled =
            false;

        IgnoreAutomaticMessages =
            true;

        IgnoreBulkMessages =
            true;

        IgnoreBounceMessages =
            true;

        IgnoreNoReplyMessages =
            true;

        InboundPollSeconds =
            60;

        OutboundPollSeconds =
            20;

        BatchSize =
            25;

        MaxAttempts =
            8;

        Revision =
            0;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    // ============================================================
    // MAILBOX
    // ============================================================

    public string? Mailbox
    {
        get;
        private set;
    }

    public string? AcceptedRecipients
    {
        get;
        private set;
    }

    public Guid? ActorUserId
    {
        get;
        private set;
    }

    // ============================================================
    // DIRECTIONS
    // ============================================================

    public bool InboundEnabled
    {
        get;
        private set;
    }

    public bool OutboundEnabled
    {
        get;
        private set;
    }

    // ============================================================
    // FILTERS
    // ============================================================

    public bool IgnoreAutomaticMessages
    {
        get;
        private set;
    } = true;

    public bool IgnoreBulkMessages
    {
        get;
        private set;
    } = true;

    public bool IgnoreBounceMessages
    {
        get;
        private set;
    } = true;

    public bool IgnoreNoReplyMessages
    {
        get;
        private set;
    } = true;

    public string? BlockedSenders
    {
        get;
        private set;
    }

    public string? BlockedDomains
    {
        get;
        private set;
    }

    public string? AllowedSenders
    {
        get;
        private set;
    }

    public string? AllowedDomains
    {
        get;
        private set;
    }

    public string? IgnoredSubjectPatterns
    {
        get;
        private set;
    }

    // ============================================================
    // WORKER
    // ============================================================

    public int InboundPollSeconds
    {
        get;
        private set;
    }

    public int OutboundPollSeconds
    {
        get;
        private set;
    }

    public int BatchSize
    {
        get;
        private set;
    }

    public int MaxAttempts
    {
        get;
        private set;
    }

    // ============================================================
    // INBOUND RUNTIME
    // ============================================================

    public DateTime? LastInboundAttemptAtUtc
    {
        get;
        private set;
    }

    public DateTime? LastInboundSuccessAtUtc
    {
        get;
        private set;
    }

    public string? LastInboundError
    {
        get;
        private set;
    }

    // ============================================================
    // OUTBOUND RUNTIME
    // ============================================================

    public DateTime? LastOutboundAttemptAtUtc
    {
        get;
        private set;
    }

    public DateTime? LastOutboundSuccessAtUtc
    {
        get;
        private set;
    }

    public string? LastOutboundError
    {
        get;
        private set;
    }

    // ============================================================
    // CONCURRENCY
    // ============================================================

    public int Revision
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    }

    // ============================================================
    // CONFIGURATION
    // ============================================================

    public void Configure(
        string? mailbox,
        string? acceptedRecipients,
        Guid? actorUserId,
        bool inboundEnabled,
        bool outboundEnabled,
        bool ignoreAutomaticMessages,
        bool ignoreBulkMessages,
        bool ignoreBounceMessages,
        bool ignoreNoReplyMessages,
        string? blockedSenders,
        string? blockedDomains,
        string? allowedSenders,
        string? allowedDomains,
        string? ignoredSubjectPatterns,
        int inboundPollSeconds,
        int outboundPollSeconds,
        int batchSize,
        int maxAttempts)
    {
        var normalizedMailbox =
            NormalizeEmail(
                mailbox);

        if (
            (
                inboundEnabled
                ||
                outboundEnabled
            )
            &&
            normalizedMailbox is null)
        {
            throw new ArgumentException(
                "Debes configurar un buzón válido antes de habilitar el correo.");
        }

        if (
            inboundEnabled
            &&
            (
                !actorUserId.HasValue
                ||
                actorUserId.Value ==
                    Guid.Empty
            ))
        {
            throw new ArgumentException(
                "Debes seleccionar un usuario técnico para procesar el correo entrante.");
        }

        var normalizedRecipients =
            NormalizeEmailList(
                acceptedRecipients);

        if (
            inboundEnabled
            &&
            string.IsNullOrWhiteSpace(
                normalizedRecipients))
        {
            normalizedRecipients =
                normalizedMailbox;
        }

        if (
            inboundPollSeconds
            is < 30 or > 3600)
        {
            throw new ArgumentException(
                "El intervalo de correo entrante debe estar entre 30 y 3600 segundos.");
        }

        if (
            outboundPollSeconds
            is < 10 or > 3600)
        {
            throw new ArgumentException(
                "El intervalo de correo saliente debe estar entre 10 y 3600 segundos.");
        }

        if (
            batchSize
            is < 1 or > 100)
        {
            throw new ArgumentException(
                "El tamaño de lote debe estar entre 1 y 100.");
        }

        if (
            maxAttempts
            is < 1 or > 20)
        {
            throw new ArgumentException(
                "Los reintentos deben estar entre 1 y 20.");
        }

        Mailbox =
            normalizedMailbox;

        AcceptedRecipients =
            normalizedRecipients;

        ActorUserId =
            actorUserId;

        InboundEnabled =
            inboundEnabled;

        OutboundEnabled =
            outboundEnabled;

        IgnoreAutomaticMessages =
            ignoreAutomaticMessages;

        IgnoreBulkMessages =
            ignoreBulkMessages;

        IgnoreBounceMessages =
            ignoreBounceMessages;

        IgnoreNoReplyMessages =
            ignoreNoReplyMessages;

        BlockedSenders =
            NormalizeEmailList(
                blockedSenders);

        BlockedDomains =
            NormalizeSimpleList(
                blockedDomains);

        AllowedSenders =
            NormalizeEmailList(
                allowedSenders);

        AllowedDomains =
            NormalizeSimpleList(
                allowedDomains);

        IgnoredSubjectPatterns =
            NormalizeSimpleList(
                ignoredSubjectPatterns);

        InboundPollSeconds =
            inboundPollSeconds;

        OutboundPollSeconds =
            outboundPollSeconds;

        BatchSize =
            batchSize;

        MaxAttempts =
            maxAttempts;

        Revision++;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    // ============================================================
    // TEMPORARY BACKWARD COMPATIBILITY
    // ============================================================

    public void Configure(
        string? mailbox,
        string? acceptedRecipients,
        Guid? actorUserId,
        bool inboundEnabled,
        bool outboundEnabled,
        bool ignoreAutomaticMessages,
        bool ignoreBulkMessages,
        int inboundPollSeconds,
        int outboundPollSeconds,
        int batchSize,
        int maxAttempts)
    {
        Configure(
            mailbox,
            acceptedRecipients,
            actorUserId,
            inboundEnabled,
            outboundEnabled,
            ignoreAutomaticMessages,
            ignoreBulkMessages,
            IgnoreBounceMessages,
            IgnoreNoReplyMessages,
            BlockedSenders,
            BlockedDomains,
            AllowedSenders,
            AllowedDomains,
            IgnoredSubjectPatterns,
            inboundPollSeconds,
            outboundPollSeconds,
            batchSize,
            maxAttempts);
    }

    // ============================================================
    // RUNTIME
    // ============================================================

    public void MarkInboundAttempt()
    {
        LastInboundAttemptAtUtc =
            DateTime.UtcNow;
    }

    public void MarkInboundSuccess()
    {
        var now =
            DateTime.UtcNow;

        LastInboundAttemptAtUtc =
            now;

        LastInboundSuccessAtUtc =
            now;

        LastInboundError =
            null;
    }

    public void MarkInboundFailure(
        string? error)
    {
        LastInboundAttemptAtUtc =
            DateTime.UtcNow;

        LastInboundError =
            NormalizeError(
                error);
    }

    public void MarkOutboundAttempt()
    {
        LastOutboundAttemptAtUtc =
            DateTime.UtcNow;
    }

    public void MarkOutboundSuccess()
    {
        var now =
            DateTime.UtcNow;

        LastOutboundAttemptAtUtc =
            now;

        LastOutboundSuccessAtUtc =
            now;

        LastOutboundError =
            null;
    }

    public void MarkOutboundFailure(
        string? error)
    {
        LastOutboundAttemptAtUtc =
            DateTime.UtcNow;

        LastOutboundError =
            NormalizeError(
                error);
    }

    // ============================================================
    // NORMALIZATION
    // ============================================================

    private static string? NormalizeEmail(
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
                .ToLowerInvariant();

        if (
            result.Length >
            320)
        {
            throw new ArgumentException(
                "La dirección de correo excede 320 caracteres.");
        }

        var at =
            result.IndexOf(
                '@');

        if (
            at <= 0
            ||
            at !=
                result.LastIndexOf(
                    '@')
            ||
            at >=
                result.Length - 2)
        {
            throw new ArgumentException(
                $"La dirección '{result}' no es válida.");
        }

        return result;
    }

    private static string? NormalizeEmailList(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var values =
            SplitValues(
                value)
            .Select(
                NormalizeEmail)
            .Where(
                x =>
                    x is not null)
            .Cast<string>()
            .Distinct(
                StringComparer
                    .OrdinalIgnoreCase)
            .OrderBy(
                x =>
                    x,
                StringComparer
                    .OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0
            ? null
            : LimitList(
                string.Join(
                    '|',
                    values));
    }

    private static string? NormalizeSimpleList(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var values =
            SplitValues(
                value)
            .Select(
                x =>
                    x
                        .Trim()
                        .ToLowerInvariant())
            .Where(
                x =>
                    x.Length > 0)
            .Distinct(
                StringComparer
                    .OrdinalIgnoreCase)
            .OrderBy(
                x =>
                    x,
                StringComparer
                    .OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0
            ? null
            : LimitList(
                string.Join(
                    '|',
                    values));
    }

    private static IEnumerable<string> SplitValues(
        string value)
    {
        return value.Split(
            new[]
            {
                '|',
                ',',
                ';',
                '\r',
                '\n'
            },
            StringSplitOptions
                .RemoveEmptyEntries
            |
            StringSplitOptions
                .TrimEntries);
    }

    private static string LimitList(
        string value)
    {
        if (
            value.Length >
            4000)
        {
            throw new ArgumentException(
                "La lista configurada excede 4000 caracteres.");
        }

        return value;
    }

    private static string NormalizeError(
        string? value)
    {
        var result =
            string.IsNullOrWhiteSpace(
                value)
                ? "Error desconocido."
                : value.Trim();

        return result[
            ..Math.Min(
                result.Length,
                2000)];
    }
}