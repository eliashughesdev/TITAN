namespace TitanMDM.Domain.Entities;

public sealed class User
{
    private User()
    {
    }

    public User(
        Guid organizationId,
        string firstName,
        string lastName,
        string email)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException(
                "First name is required.",
                nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException(
                "Last name is required.",
                nameof(lastName));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Email is required.",
                nameof(email));
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        FirstName =
            firstName.Trim();

        LastName =
            lastName.Trim();

        Email =
            email
                .Trim()
                .ToLowerInvariant();

        IsActive =
            true;

        MfaEnabled =
            false;

        FailedLoginAttempts =
            0;

        SecurityVersion =
            1;

        CreatedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;

    }
    public void SetSite(
    Guid? siteId,
    Guid? siteLocationId = null)
    {
        if (
            !siteId.HasValue
            &&
            siteLocationId.HasValue)
        {
            throw new InvalidOperationException(
                "No se puede asignar una ubicación sin una localidad.");
        }

        SiteId =
            siteId;

        SiteLocationId =
            siteLocationId;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

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

    public Guid? DepartmentId
    {
        get;
        private set;
    }

    public Guid? SiteId
    {
        get;
        private set;
    }

    public Guid? SiteLocationId
    {
        get;
        private set;
    }

    public string FirstName
    {
        get;
        private set;
    } = string.Empty;

    public string LastName
    {
        get;
        private set;
    } = string.Empty;

    public string Email
    {
        get;
        private set;
    } = string.Empty;

    public string? PasswordHash
    {
        get;
        private set;
    }

    public string? JobTitle
    {
        get;
        private set;
    }

    public bool IsActive
    {
        get;
        private set;
    }

    public bool MfaEnabled
    {
        get;
        private set;
    }

    public int FailedLoginAttempts
    {
        get;
        private set;
    }

    public DateTime? LockedUntilUtc
    {
        get;
        private set;
    }

    /*
     * Incrementar este valor invalida inmediatamente
     * todos los Access Tokens emitidos previamente.
     */
    public int SecurityVersion
    {
        get;
        private set;
    }

    public DateTime? LastLoginAtUtc
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

    public string FullName =>
        $"{FirstName} {LastName}".Trim();

    public bool IsLockedOut =>
        LockedUntilUtc.HasValue
        &&
        LockedUntilUtc.Value >
        DateTime.UtcNow;

    // ============================================================
    // PROFILE
    // ============================================================

    public void UpdateProfile(
        string firstName,
        string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException(
                "First name is required.",
                nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException(
                "Last name is required.",
                nameof(lastName));
        }

        FirstName =
            firstName.Trim();

        LastName =
            lastName.Trim();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void SetDepartment(
        Guid? departmentId)
    {
        DepartmentId =
            departmentId;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void SetJobTitle(
        string? jobTitle)
    {
        JobTitle =
            string.IsNullOrWhiteSpace(jobTitle)
                ? null
                : jobTitle.Trim();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    // ============================================================
    // PASSWORD
    // ============================================================

    public void SetPasswordHash(
        string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(
                passwordHash))
        {
            throw new ArgumentException(
                "Password hash cannot be empty.",
                nameof(passwordHash));
        }

        PasswordHash =
            passwordHash;

        ResetLoginFailures();

        IncrementSecurityVersion();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    // ============================================================
    // LOGIN / LOCKOUT
    // ============================================================

    public void RegisterFailedLogin(
        int maximumAttempts,
        int lockoutMinutes)
    {
        maximumAttempts =
            Math.Max(
                1,
                maximumAttempts);

        lockoutMinutes =
            Math.Clamp(
                lockoutMinutes,
                1,
                1440);

        FailedLoginAttempts++;

        if (FailedLoginAttempts >= maximumAttempts)
        {
            LockedUntilUtc =
                DateTime.UtcNow
                    .AddMinutes(
                        lockoutMinutes);

            FailedLoginAttempts =
                0;
        }

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void ResetLoginFailures()
    {
        FailedLoginAttempts =
            0;

        LockedUntilUtc =
            null;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void RegisterLogin()
    {
        FailedLoginAttempts =
            0;

        LockedUntilUtc =
            null;

        LastLoginAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Unlock()
    {
        FailedLoginAttempts =
            0;

        LockedUntilUtc =
            null;

        IncrementSecurityVersion();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    // ============================================================
    // MFA
    // ============================================================

    public void EnableMfa()
    {
        MfaEnabled =
            true;

        IncrementSecurityVersion();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void DisableMfa()
    {
        MfaEnabled =
            false;

        IncrementSecurityVersion();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    // ============================================================
    // STATUS
    // ============================================================

    public void Activate()
    {
        IsActive =
            true;

        IncrementSecurityVersion();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive =
            false;

        IncrementSecurityVersion();

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    // ============================================================
    // SECURITY VERSION
    // ============================================================

    public void IncrementSecurityVersion()
    {
        SecurityVersion =
            SecurityVersion == int.MaxValue
                ? 1
                : SecurityVersion + 1;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }
}