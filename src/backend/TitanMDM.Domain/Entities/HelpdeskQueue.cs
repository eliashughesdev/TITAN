namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskQueue
{
    private HelpdeskQueue()
    {
    }

    public HelpdeskQueue(
        Guid organizationId,
        string name,
        string? description)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Description = description?.Trim();
        IsDefault = false;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void MarkDefault()
    {
        IsDefault = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
