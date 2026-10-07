namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskZone
{
    private HelpdeskZone() { }

    public HelpdeskZone(
        Guid organizationId,
        string name,
        string type,
        Guid? parentZoneId = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("La organización es obligatoria.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (type is not ("locality" or "plant" or "building" or "area"))
            throw new ArgumentException("Tipo de zona no válido.", nameof(type));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Type = type;
        ParentZoneId = parentZoneId;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? ParentZoneId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));

        Name = name.Trim();
    }

    public void SetActive(bool active) => IsActive = active;
}

public sealed class HelpdeskTeam
{
    private HelpdeskTeam() { }

    public HelpdeskTeam(
        Guid organizationId,
        string name,
        string? description)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "La organización es obligatoria.",
                nameof(organizationId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "El nombre es obligatorio.",
                nameof(name));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Description = description?.Trim();
        Categories = string.Empty;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    // Vacío: grupo general que acepta cualquier categoría.
    // Formato: |redes|equipos|software|
    public string Categories { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public void SetActive(bool active) => IsActive = active;

    public void ConfigureCategories(IEnumerable<string> categories)
    {
        var normalized = categories
            .Select(x => x?.Trim().ToLowerInvariant() ?? "")
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length > 15 ||
            normalized.Any(x =>
                x.Length > 50 ||
                x.Contains('|') ||
                x.Contains(',') ||
                x.Any(char.IsControl)))
        {
            throw new ArgumentException(
                "Admite hasta 15 categorías de 50 caracteres; " +
                "no uses comas ni el carácter |.");
        }

        var value = normalized.Length == 0
            ? string.Empty
            : "|" + string.Join("|", normalized) + "|";

        if (value.Length > 800)
            throw new ArgumentException(
                "La lista de categorías es demasiado larga.");

        Categories = value;
    }

    public bool HandlesCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return false;

        return Categories.Contains(
            "|" + category.Trim().ToLowerInvariant() + "|",
            StringComparison.Ordinal);
    }
}
public sealed class HelpdeskTeamZone
{
    private HelpdeskTeamZone() { }

    public HelpdeskTeamZone(Guid organizationId, Guid teamId, Guid zoneId)
    {
        if (organizationId == Guid.Empty || teamId == Guid.Empty || zoneId == Guid.Empty)
            throw new ArgumentException("Organización, grupo y zona son obligatorios.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TeamId = teamId;
        ZoneId = zoneId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid ZoneId { get; private set; }
}

public sealed class HelpdeskTeamMember
{
    private HelpdeskTeamMember() { }

    public HelpdeskTeamMember(
        Guid organizationId,
        Guid teamId,
        Guid userId,
        bool acceptsAutomaticAssignments,
        int maxOpenTickets = 20)
    {
        if (organizationId == Guid.Empty || teamId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Organización, grupo y usuario son obligatorios.");
        if (maxOpenTickets < 1)
            throw new ArgumentOutOfRangeException(nameof(maxOpenTickets));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TeamId = teamId;
        UserId = userId;
        AcceptsAutomaticAssignments = acceptsAutomaticAssignments;
        MaxOpenTickets = maxOpenTickets;
        IsAvailable = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public bool AcceptsAutomaticAssignments { get; private set; }
    public bool IsAvailable { get; private set; }
    public int MaxOpenTickets { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public void SetAvailability(bool available) => IsAvailable = available;

    public void ConfigureAutomaticAssignments(bool enabled, int maxOpenTickets)
    {
        if (maxOpenTickets < 1)
            throw new ArgumentOutOfRangeException(nameof(maxOpenTickets));

        AcceptsAutomaticAssignments = enabled;
        MaxOpenTickets = maxOpenTickets;
    }
}

public sealed class HelpdeskUserZone
{
    private HelpdeskUserZone() { }

    public HelpdeskUserZone(Guid organizationId, Guid userId, Guid zoneId)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty || zoneId == Guid.Empty)
            throw new ArgumentException("Organización, usuario y zona son obligatorios.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        UserId = userId;
        ZoneId = zoneId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ZoneId { get; private set; }
}

public sealed class HelpdeskAssistantAccess
{
    private HelpdeskAssistantAccess() { }

    public HelpdeskAssistantAccess(
        Guid organizationId,
        Guid userId,
        Guid grantedByUserId)
    {
        if (organizationId == Guid.Empty ||
            userId == Guid.Empty ||
            grantedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organización, usuario y administrador son obligatorios.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        UserId = userId;
        GrantedByUserId = grantedByUserId;
        IsEnabled = true;
        GrantedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime GrantedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public void Grant(Guid administratorUserId)
    {
        if (administratorUserId == Guid.Empty)
            throw new ArgumentException("El administrador es obligatorio.");

        GrantedByUserId = administratorUserId;
        GrantedAtUtc = DateTime.UtcNow;
        RevokedAtUtc = null;
        IsEnabled = true;
    }

    public void Revoke()
    {
        IsEnabled = false;
        RevokedAtUtc = DateTime.UtcNow;
    }
}