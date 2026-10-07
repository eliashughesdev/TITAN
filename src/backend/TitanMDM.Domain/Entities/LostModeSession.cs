namespace TitanMDM.Domain.Entities;

public sealed class LostModeSession
{
    private LostModeSession()
    {
    }

    public LostModeSession(
        Guid organizationId,
        Guid deviceId,
        Guid activatedByUserId,
        string message,
        string? phoneNumber)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.");

        if (activatedByUserId == Guid.Empty)
            throw new ArgumentException(
                "ActivatedByUserId is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        DeviceId = deviceId;
        ActivatedByUserId = activatedByUserId;

        Message =
            string.IsNullOrWhiteSpace(message)
                ? "Este dispositivo está administrado por TitanMDM."
                : message.Trim();

        PhoneNumber =
            string.IsNullOrWhiteSpace(phoneNumber)
                ? null
                : phoneNumber.Trim();

        Status = "Active";
        ActivatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid ActivatedByUserId { get; private set; }

    public string Message { get; private set; } =
        string.Empty;

    public string? PhoneNumber { get; private set; }

    public string Status { get; private set; } =
        "Active";

    public DateTime ActivatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? DeactivatedAtUtc { get; private set; }

    public void Deactivate()
    {
        if (Status == "Inactive")
            return;

        Status = "Inactive";
        DeactivatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}