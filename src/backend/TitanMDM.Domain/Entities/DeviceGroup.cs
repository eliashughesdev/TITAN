namespace TitanMDM.Domain.Entities;

public sealed class DeviceGroup
{
    private DeviceGroup()
    {
    }

    public DeviceGroup(
        Guid organizationId,
        string name,
        string? description,
        bool isDynamic,
        string? ruleJson)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Group name is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();

        Description =
            Normalize(description);

        IsDynamic = isDynamic;

        RuleJson =
            isDynamic
                ? Normalize(ruleJson)
                : null;

        IsEnabled = true;

        CreatedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public string Name
    {
        get;
        private set;
    } = string.Empty;

    public string? Description
    {
        get;
        private set;
    }

    public bool IsDynamic
    {
        get;
        private set;
    }

    public string? RuleJson
    {
        get;
        private set;
    }

    public bool IsEnabled
    {
        get;
        private set;
    }

    public DateTime CreatedAtUtc
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    }

    public void Update(
        string name,
        string? description,
        bool isDynamic,
        string? ruleJson)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Group name is required.");

        Name = name.Trim();

        Description =
            Normalize(description);

        IsDynamic = isDynamic;

        RuleJson =
            isDynamic
                ? Normalize(ruleJson)
                : null;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsEnabled = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}