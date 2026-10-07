using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed record HelpdeskMailIntakeDecision(
    bool Accepted,
    string Code,
    string Reason);

public sealed class HelpdeskMailIntakePolicy
{
    public HelpdeskMailIntakeDecision Evaluate(
        HelpdeskMailSettings settings,
        IncomingHelpdeskEmail message)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        ArgumentNullException.ThrowIfNull(
            message);

        // ========================================================
        // 1. RECIPIENT
        // ========================================================

        var acceptedRecipients =
            ParseEmails(
                settings.AcceptedRecipients
                ??
                settings.Mailbox);

        if (acceptedRecipients.Count == 0)
        {
            return Reject(
                "no_accepted_recipient",
                "No existen destinatarios autorizados para Mesa de Ayuda.");
        }

        var realRecipients =
            message.ToRecipients
                .Concat(
                    message.CcRecipients)
                .Select(
                    NormalizeEmail)
                .Where(
                    x =>
                        x is not null)
                .Cast<string>()
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray();

        if (
            !realRecipients.Any(
                acceptedRecipients.Contains))
        {
            return Reject(
                "recipient_not_allowed",
                "El mensaje no fue dirigido a una dirección autorizada de Mesa de Ayuda.");
        }

        // ========================================================
        // 2. BLOCKED SENDERS
        // ========================================================

        var sender =
            NormalizeEmail(
                message.FromEmail);

        if (sender is null)
        {
            return Reject(
                "invalid_sender",
                "El mensaje no contiene un remitente válido.");
        }

        var blockedSenders =
            ParseEmails(
                settings.BlockedSenders);

        if (
            blockedSenders.Contains(
                sender))
        {
            return Reject(
                "blocked_sender",
                $"El remitente {sender} está bloqueado.");
        }

        // ========================================================
        // 3. BLOCKED DOMAINS
        // ========================================================

        var senderDomain =
            GetDomain(
                sender);

        var blockedDomains =
            ParseValues(
                settings.BlockedDomains);

        if (
            senderDomain is not null
            &&
            blockedDomains.Contains(
                senderDomain))
        {
            return Reject(
                "blocked_domain",
                $"El dominio {senderDomain} está bloqueado.");
        }

        // ========================================================
        // 4. ALLOW LIST
        //
        // Si se configura allow-list, el remitente debe cumplir
        // al menos una regla.
        // ========================================================

        var allowedSenders =
            ParseEmails(
                settings.AllowedSenders);

        var allowedDomains =
            ParseValues(
                settings.AllowedDomains);

        var allowListEnabled =
            allowedSenders.Count > 0
            ||
            allowedDomains.Count > 0;

        if (allowListEnabled)
        {
            var senderAllowed =
                allowedSenders.Contains(
                    sender);

            var domainAllowed =
                senderDomain is not null
                &&
                allowedDomains.Contains(
                    senderDomain);

            if (
                !senderAllowed
                &&
                !domainAllowed)
            {
                return Reject(
                    "sender_not_allowed",
                    "El remitente no pertenece a la lista permitida de Mesa de Ayuda.");
            }
        }

        // ========================================================
        // 5. AUTO SUBMITTED
        // ========================================================

        if (
            settings.IgnoreAutomaticMessages
            &&
            !string.IsNullOrWhiteSpace(
                message.AutoSubmitted))
        {
            var value =
                message.AutoSubmitted
                    .Trim();

            if (
                !string.Equals(
                    value,
                    "no",
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return Reject(
                    "automatic_message",
                    $"Mensaje automático ignorado. Auto-Submitted={value}.");
            }
        }

        // ========================================================
        // 6. BULK / LIST / JUNK
        // ========================================================

        if (
            settings.IgnoreBulkMessages
            &&
            !string.IsNullOrWhiteSpace(
                message.Precedence))
        {
            var precedence =
                message.Precedence
                    .Trim()
                    .ToLowerInvariant();

            if (
                precedence
                is "bulk"
                or "list"
                or "junk")
            {
                return Reject(
                    "bulk_message",
                    $"Correo masivo ignorado. Precedence={precedence}.");
            }
        }

        // ========================================================
        // 7. NO-REPLY
        // ========================================================

        if (
            settings.IgnoreNoReplyMessages
            &&
            IsNoReplySender(
                sender))
        {
            return Reject(
                "no_reply_sender",
                $"El remitente {sender} fue identificado como no-reply.");
        }

        // ========================================================
        // 8. BOUNCES / NDR
        // ========================================================

        if (
            settings.IgnoreBounceMessages
            &&
            IsBounce(
                sender,
                message.Subject))
        {
            return Reject(
                "delivery_failure",
                "El mensaje fue identificado como rebote o notificación de entrega.");
        }

        // ========================================================
        // 9. SUBJECT FILTERS
        // ========================================================

        var ignoredPatterns =
            ParseValues(
                settings.IgnoredSubjectPatterns);

        if (
            ignoredPatterns.Count >
            0)
        {
            var subject =
                (
                    message.Subject
                    ??
                    string.Empty
                )
                .Trim()
                .ToLowerInvariant();

            var matched =
                ignoredPatterns
                    .FirstOrDefault(
                        subject.Contains);

            if (matched is not null)
            {
                return Reject(
                    "subject_pattern",
                    $"El asunto coincide con la regla de exclusión '{matched}'.");
            }
        }

        return new HelpdeskMailIntakeDecision(
            true,
            "accepted",
            "Mensaje aceptado por la política de Mesa de Ayuda.");
    }

    private static HelpdeskMailIntakeDecision Reject(
        string code,
        string reason)
    {
        return new HelpdeskMailIntakeDecision(
            false,
            code,
            reason);
    }

    private static HashSet<string> ParseEmails(
        string? value)
    {
        return ParseValues(
                value)
            .Select(
                NormalizeEmail)
            .Where(
                x =>
                    x is not null)
            .Cast<string>()
            .ToHashSet(
                StringComparer
                    .OrdinalIgnoreCase);
    }

    private static HashSet<string> ParseValues(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);
        }

        return value
            .Split(
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
                    .TrimEntries)
            .Select(
                x =>
                    x.Trim()
                        .ToLowerInvariant())
            .Where(
                x =>
                    x.Length > 0)
            .Distinct(
                StringComparer
                    .OrdinalIgnoreCase)
            .ToHashSet(
                StringComparer
                    .OrdinalIgnoreCase);
    }

    private static string? NormalizeEmail(
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

        var at =
            normalized
                .LastIndexOf(
                    '@');

        if (
            at <= 0
            ||
            at >=
                normalized.Length - 1)
        {
            return null;
        }

        return normalized;
    }

    private static string? GetDomain(
        string sender)
    {
        var at =
            sender.LastIndexOf(
                '@');

        if (
            at <= 0
            ||
            at >=
                sender.Length - 1)
        {
            return null;
        }

        return sender[
            (at + 1)..]
            .Trim()
            .ToLowerInvariant();
    }

    private static bool IsNoReplySender(
        string sender)
    {
        var local =
            sender.Split(
                '@',
                2)[0]
                .Replace(
                    ".",
                    string.Empty)
                .Replace(
                    "_",
                    string.Empty)
                .Replace(
                    "-",
                    string.Empty)
                .ToLowerInvariant();

        return
            local.Contains(
                "noreply")
            ||
            local.Contains(
                "donotreply")
            ||
            local.Contains(
                "mailerdaemon")
            ||
            local.Equals(
                "postmaster",
                StringComparison
                    .OrdinalIgnoreCase);
    }

    private static bool IsBounce(
        string sender,
        string? subject)
    {
        var senderLower =
            sender
                .ToLowerInvariant();

        if (
            senderLower.Contains(
                "mailer-daemon")
            ||
            senderLower.StartsWith(
                "postmaster@"))
        {
            return true;
        }

        var normalizedSubject =
            (
                subject
                ??
                string.Empty
            )
            .Trim()
            .ToLowerInvariant();

        return
            normalizedSubject.Contains(
                "undeliverable")
            ||
            normalizedSubject.Contains(
                "delivery status notification")
            ||
            normalizedSubject.Contains(
                "delivery failure")
            ||
            normalizedSubject.Contains(
                "failure notice")
            ||
            normalizedSubject.Contains(
                "returned mail")
            ||
            normalizedSubject.Contains(
                "no se pudo entregar")
            ||
            normalizedSubject.Contains(
                "correo no entregado");
    }
}