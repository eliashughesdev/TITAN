namespace TitanMDM.Domain.Helpdesk;

public static class HelpdeskTicketStatus
{
    public const string New = "new";
    public const string Open = "open";
    public const string InProgress = "inprogress";
    public const string PendingUser = "pendinguser";
    public const string Resolved = "resolved";
    public const string Closed = "closed";

    private static readonly HashSet<string> ValidStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            New,
            Open,
            InProgress,
            PendingUser,
            Resolved,
            Closed
        };

    public static bool IsValid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return ValidStatuses.Contains(
            status.Trim());
    }

    public static string Normalize(string status)
    {
        if (!IsValid(status))
            throw new ArgumentException(
                "Estado de ticket inválido.",
                nameof(status));

        return status
            .Trim()
            .ToLowerInvariant();
    }

    public static bool CanTransition(
        string currentStatus,
        string targetStatus)
    {
        var current =
            Normalize(currentStatus);

        var target =
            Normalize(targetStatus);

        if (current == target)
            return true;

        return current switch
        {
            New =>
                target is
                    Open or
                    InProgress or
                    PendingUser or
                    Resolved,

            Open =>
                target is
                    InProgress or
                    PendingUser or
                    Resolved,

            InProgress =>
                target is
                    Open or
                    PendingUser or
                    Resolved,

            PendingUser =>
                target is
                    Open or
                    InProgress or
                    Resolved,

            Resolved =>
                target is
                    Open or
                    Closed,

            Closed =>
                target is
                    Open,

            _ => false
        };
    }

    public static bool IsTerminal(
        string status)
    {
        var normalized =
            Normalize(status);

        return normalized is
            Resolved or
            Closed;
    }

    public static bool IsSlaPaused(
        string status)
    {
        return Normalize(status) ==
            PendingUser;
    }

    public static bool IsReopen(
        string currentStatus,
        string targetStatus)
    {
        var current =
            Normalize(currentStatus);

        var target =
            Normalize(targetStatus);

        return
            current is Resolved or Closed
            &&
            target == Open;
    }
}