namespace TitanMDM.Domain.Entities;

public sealed class AdministrativeAuditEvent
{
    private AdministrativeAuditEvent()
    {
    }

    public AdministrativeAuditEvent(
        Guid organizationId,
        Guid? actorUserId,
        string action,
        string targetType,
        string? targetId,
        string result,
        string correlationId,
        string? ipAddress = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException(
                "Action is required.",
                nameof(action));
        }

        if (string.IsNullOrWhiteSpace(targetType))
        {
            throw new ArgumentException(
                "TargetType is required.",
                nameof(targetType));
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        ActorUserId =
            actorUserId;

        Action =
            action.Trim();

        TargetType =
            targetType.Trim();

        TargetId =
            string.IsNullOrWhiteSpace(targetId)
                ? null
                : targetId.Trim();

        Result =
            string.IsNullOrWhiteSpace(result)
                ? "Unknown"
                : result.Trim();

        CorrelationId =
            correlationId.Trim();

        IpAddress =
            string.IsNullOrWhiteSpace(ipAddress)
                ? null
                : ipAddress.Trim();

        CreatedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string TargetType { get; private set; } = string.Empty;

    public string? TargetId { get; private set; }

    public string Result { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public string? IpAddress { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}