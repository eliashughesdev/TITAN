namespace TitanMDM.Domain.Entities;

public sealed class EntraIdSettings
{
    private EntraIdSettings()
    {
    }

    public EntraIdSettings(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        IsEnabled = false;
        SyncRequestersOnly = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? TenantId { get; private set; }
    public string? ClientId { get; private set; }
    public string? ClientSecretProtected { get; private set; }
    public string? AllowedGroupIds { get; private set; }
    public bool SyncRequestersOnly { get; private set; }
    public DateTime? LastSyncAtUtc { get; private set; }
    public string? LastSyncStatus { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Configure(
        string tenantId,
        string clientId,
        string? clientSecretProtected,
        string? allowedGroupIds,
        bool syncRequestersOnly,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("ClientId is required.", nameof(clientId));

        TenantId = tenantId.Trim();
        ClientId = clientId.Trim();
        if (!string.IsNullOrWhiteSpace(clientSecretProtected))
            ClientSecretProtected = clientSecretProtected;
        AllowedGroupIds = string.IsNullOrWhiteSpace(allowedGroupIds) ? null : allowedGroupIds.Trim();
        SyncRequestersOnly = syncRequestersOnly;
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSync(string status)
    {
        LastSyncAtUtc = DateTime.UtcNow;
        LastSyncStatus = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
