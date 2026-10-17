namespace TitanMDM.Domain.Automation;

public sealed class AutomationNotification
{
    private AutomationNotification()
    {
    }

    public AutomationNotification(
        Guid organizationId,
        string title,
        string message,
        string severity = "info",
        string channel = "in_app",
        Guid? automationRuleId = null,
        Guid? automationExecutionId = null,
        Guid? deviceId = null,
        Guid? targetUserId = null,
        string? targetRole = null,
        string? metadataJson = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "Title is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Title = title.Trim();

        Message =
            string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message.Trim();

        Severity = string.IsNullOrWhiteSpace(
                severity)
            ? "info"
            : severity.Trim().ToLowerInvariant();

        Channel = string.IsNullOrWhiteSpace(
                channel)
            ? "in_app"
            : channel.Trim().ToLowerInvariant();

        AutomationRuleId = automationRuleId;
        AutomationExecutionId =
            automationExecutionId;
        DeviceId = deviceId;
        TargetUserId = targetUserId;
        TargetRole =
            string.IsNullOrWhiteSpace(targetRole)
                ? null
                : targetRole.Trim();
        MetadataJson = metadataJson;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? AutomationRuleId { get; private set; }

    public Guid? AutomationExecutionId
    {
        get;
        private set;
    }

    public Guid? DeviceId { get; private set; }

    public string Title { get; private set; } =
        string.Empty;

    public string Message { get; private set; } =
        string.Empty;

    public string Severity { get; private set; } =
        "info";

    public string Channel { get; private set; } =
        "in_app";

    public Guid? TargetUserId { get; private set; }

    public string? TargetRole { get; private set; }

    public string? MetadataJson { get; private set; }

    public bool IsRead { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public void MarkRead()
    {
        if (IsRead)
            return;

        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
    }
}
