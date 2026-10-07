using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class AndroidEnterpriseConfiguration
{
    private AndroidEnterpriseConfiguration()
    {
    }

    public AndroidEnterpriseConfiguration(
        Guid organizationId,
        string googleProjectId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (string.IsNullOrWhiteSpace(googleProjectId))
            throw new ArgumentException(
                "GoogleProjectId is required.",
                nameof(googleProjectId));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        GoogleProjectId = googleProjectId.Trim();

        Status =
            AndroidEnterpriseStatus.NotConfigured;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string GoogleProjectId { get; private set; } =
        string.Empty;

    public string? EnterpriseName { get; private set; }

    public string? EnterpriseDisplayName { get; private set; }

    public AndroidEnterpriseStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ConnectedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public DateTime? LastDeviceSyncAtUtc
{
    get;
    private set;
}

public int LastDeviceSyncCount
{
    get;
    private set;
}

public int LastDeviceSyncErrors
{
    get;
    private set;
}

    public void MarkPending()
    {
        Status =
            AndroidEnterpriseStatus.Pending;

        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate(
        string enterpriseName,
        string? enterpriseDisplayName)
    {
        if (string.IsNullOrWhiteSpace(enterpriseName))
            throw new ArgumentException(
                "EnterpriseName is required.",
                nameof(enterpriseName));

        EnterpriseName =
            enterpriseName.Trim();

        EnterpriseDisplayName =
            string.IsNullOrWhiteSpace(
                enterpriseDisplayName)
                ? null
                : enterpriseDisplayName.Trim();

        Status =
            AndroidEnterpriseStatus.Active;

        ConnectedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkError(string message)
    {
        Status =
            AndroidEnterpriseStatus.Error;

        LastError =
            string.IsNullOrWhiteSpace(message)
                ? "Unknown Android Enterprise error."
                : message.Trim();

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordDeviceSynchronization(
    int deviceCount,
    int errorCount)
{
    LastDeviceSyncAtUtc = DateTime.UtcNow;

    LastDeviceSyncCount =
        Math.Max(deviceCount, 0);

    LastDeviceSyncErrors =
        Math.Max(errorCount, 0);

    UpdatedAtUtc = DateTime.UtcNow;
}
}