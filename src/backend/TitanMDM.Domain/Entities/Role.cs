namespace TitanMDM.Domain.Entities;

public sealed class Role
{
    private Role()
    {
    }

    public Role(
        Guid organizationId,
        string name)
    {
        if (
            organizationId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (
            string.IsNullOrWhiteSpace(
                name))
        {
            throw new ArgumentException(
                "Role name is required.",
                nameof(name));
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        Name =
            NormalizeName(
                name);

        IsSystemRole =
            false;

        IsActive =
            true;

        CreatedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    /*
     * ============================================================
     * PROPERTIES
     * ============================================================
     */

    public Guid Id
    {
        get;
        private set;
    }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public string Name
    {
        get;
        private set;
    } =
        string.Empty;

    public string? Description
    {
        get;
        private set;
    }

    public bool IsSystemRole
    {
        get;
        private set;
    }

    public bool IsActive
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

    /*
     * ============================================================
     * UPDATE
     * ============================================================
     */

    public void Update(
        string name,
        string? description)
    {
        if (
            string.IsNullOrWhiteSpace(
                name))
        {
            throw new ArgumentException(
                "Role name is required.",
                nameof(name));
        }

        /*
         * Los roles del sistema pueden modificar descripción,
         * pero su nombre queda protegido para evitar romper
         * reglas como SuperAdmin.
         */
        if (
            !IsSystemRole)
        {
            Name =
                NormalizeName(
                    name);
        }

        Description =
            NormalizeNullable(
                description);

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    /*
     * ============================================================
     * DESCRIPTION
     * ============================================================
     */

    public void SetDescription(
        string? description)
    {
        Description =
            NormalizeNullable(
                description);

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    /*
     * ============================================================
     * SYSTEM ROLE
     * ============================================================
     */

    public void MarkAsSystemRole()
    {
        IsSystemRole =
            true;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    /*
     * ============================================================
     * STATUS
     * ============================================================
     */

    public void Activate()
    {
        IsActive =
            true;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (
            IsSystemRole)
        {
            throw new InvalidOperationException(
                "Los roles del sistema no pueden ser desactivados.");
        }

        IsActive =
            false;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    /*
     * ============================================================
     * NORMALIZATION
     * ============================================================
     */

    private static string NormalizeName(
        string value)
    {
        return value.Trim();
    }

    private static string? NormalizeNullable(
        string? value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return value.Trim();
    }
}