using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class DeviceCommand
{
    private DeviceCommand()
    {
    }

    public DeviceCommand(
        Guid organizationId,
        Guid deviceId,
        string commandType,
        string payloadJson,
        Guid createdByUserId,
        DateTime expiresAtUtc)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(commandType))
        {
            throw new ArgumentException(
                "CommandType is required.",
                nameof(commandType));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "CreatedByUserId is required.",
                nameof(createdByUserId));
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException(
                "Expiration must be in the future.",
                nameof(expiresAtUtc));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        DeviceId = deviceId;
        CommandType = commandType.Trim();
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson)
            ? "{}"
            : payloadJson.Trim();

        CreatedByUserId = createdByUserId;
        Status = DeviceCommandStatus.Pending;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid DeviceId { get; private set; }

    public string CommandType { get; private set; } =
        string.Empty;

    public string PayloadJson { get; private set; } =
        "{}";

    public DeviceCommandStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? QueuedAtUtc { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public string? ResultJson { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public int DeliveryAttempts { get; private set; }

    public bool IsExpired =>
        DateTime.UtcNow >= ExpiresAtUtc;

    public void Queue()
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Queued;
        QueuedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkDispatching()
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Dispatching;
        DeliveryAttempts++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSent()
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Sent;
        SentAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkExecuting()
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Executing;
        StartedAtUtc ??= DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void CompleteSuccess(
        string? resultJson)
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Success;
        ResultJson = string.IsNullOrWhiteSpace(resultJson)
            ? "{}"
            : resultJson.Trim();

        ErrorCode = null;
        ErrorMessage = null;

        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void CompleteFailure(
        string? errorCode,
        string? errorMessage,
        string? resultJson = null)
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Failed;
        ErrorCode = errorCode?.Trim();
        ErrorMessage = errorMessage?.Trim();
        ResultJson = resultJson?.Trim();

        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkTimeout()
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Timeout;
        ErrorCode = "COMMAND_TIMEOUT";
        ErrorMessage =
            "The command expired before successful completion.";

        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        EnsureNotTerminal();

        Status = DeviceCommandStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void EnsureNotTerminal()
    {
        if (Status is
            DeviceCommandStatus.Success or
            DeviceCommandStatus.Failed or
            DeviceCommandStatus.Timeout or
            DeviceCommandStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "A terminal device command cannot change state.");
        }
    }
}