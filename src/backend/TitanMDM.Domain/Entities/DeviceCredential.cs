namespace TitanMDM.Domain.Entities;

public sealed class DeviceCredential
{
    private DeviceCredential()
    {
    }

    public DeviceCredential(
        Guid deviceId,
        Guid organizationId,
        string secretHash)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(secretHash))
        {
            throw new ArgumentException(
                "SecretHash is required.",
                nameof(secretHash));
        }

        Id = Guid.NewGuid();
        DeviceId = deviceId;
        OrganizationId = organizationId;
        SecretHash = secretHash.Trim();

        IsActive = true;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid DeviceId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string SecretHash { get; private set; } =
        string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? LastAuthenticatedAtUtc
    {
        get;
        private set;
    }

    public DateTime? RevokedAtUtc
    {
        get;
        private set;
    }

    public void RegisterAuthentication()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException(
                "Device credential is revoked.");
        }

        LastAuthenticatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Revoke()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RevokedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}