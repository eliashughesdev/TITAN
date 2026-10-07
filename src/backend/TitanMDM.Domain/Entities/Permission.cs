namespace TitanMDM.Domain.Entities;

public sealed class Permission
{
    private Permission()
    {
    }

    public Permission(
        string code,
        string name,
        string module,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "Permission code is required.",
                nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Permission name is required.",
                nameof(name));

        if (string.IsNullOrWhiteSpace(module))
            throw new ArgumentException(
                "Permission module is required.",
                nameof(module));

        Id = Guid.NewGuid();

        Code = code.Trim().ToLowerInvariant();
        Name = name.Trim();
        Module = module.Trim();
        Description = description?.Trim();

        IsActive = true;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Module { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string module,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Permission name is required.",
                nameof(name));

        if (string.IsNullOrWhiteSpace(module))
            throw new ArgumentException(
                "Permission module is required.",
                nameof(module));

        Name = name.Trim();
        Module = module.Trim();
        Description = description?.Trim();

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}