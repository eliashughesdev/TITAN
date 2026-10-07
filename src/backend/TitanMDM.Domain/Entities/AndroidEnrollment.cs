using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class AndroidEnrollment
{
    private AndroidEnrollment()
    {
    }

    public AndroidEnrollment(
        Guid organizationId,
        AndroidEnrollmentMode mode,
        string googleEnrollmentTokenName,
        DateTime expiresAtUtc,
        Guid createdByUserId,
        Guid? policyId = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (string.IsNullOrWhiteSpace(
                googleEnrollmentTokenName))
            throw new ArgumentException(
                "Google enrollment token name is required.");

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException(
                "CreatedByUserId is required.");

        if (expiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentException(
                "Expiration must be in the future.");

        Id = Guid.NewGuid();

        OrganizationId = organizationId;
        Mode = mode;

        GoogleEnrollmentTokenName =
            googleEnrollmentTokenName.Trim();

        PolicyId = policyId;
        CreatedByUserId = createdByUserId;

        ExpiresAtUtc = expiresAtUtc;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;

        IsRevoked = false;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public AndroidEnrollmentMode Mode { get; private set; }

    public string GoogleEnrollmentTokenName {
        get;
        private set;
    } = string.Empty;

    public Guid? PolicyId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsRevoked { get; private set; }

    public bool IsExpired =>
        DateTime.UtcNow >= ExpiresAtUtc;

    public void Revoke()
    {
        if (IsRevoked)
            return;

        IsRevoked = true;
        RevokedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}