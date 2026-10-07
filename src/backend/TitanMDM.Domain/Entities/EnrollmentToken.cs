using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class EnrollmentToken
{
    private EnrollmentToken()
    {
    }

    public EnrollmentToken(
        Guid organizationId,
        DevicePlatform platform,
        string tokenHash,
        DateTime expiresAtUtc,
        int maxUses,
        Guid createdByUserId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException(
                "Token hash is required.",
                nameof(tokenHash));
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException(
                "Expiration must be in the future.",
                nameof(expiresAtUtc));
        }

        if (maxUses <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxUses),
                "MaxUses must be greater than zero.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "CreatedByUserId is required.",
                nameof(createdByUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Platform = platform;
        TokenHash = tokenHash.Trim();
        ExpiresAtUtc = expiresAtUtc;
        MaxUses = maxUses;
        UsedCount = 0;
        CreatedByUserId = createdByUserId;

        Status = EnrollmentStatus.Active;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public DevicePlatform Platform { get; private set; }

    public string TokenHash { get; private set; } =
        string.Empty;

    public EnrollmentStatus Status { get; private set; }

    public int MaxUses { get; private set; }

    public int UsedCount { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? LastUsedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public bool CanBeUsed()
    {
        if (Status != EnrollmentStatus.Active)
        {
            return false;
        }

        if (DateTime.UtcNow >= ExpiresAtUtc)
        {
            return false;
        }

        return UsedCount < MaxUses;
    }

    public void RegisterUse()
    {
        if (!CanBeUsed())
        {
            throw new InvalidOperationException(
                "Enrollment token cannot be used.");
        }

        UsedCount++;
        LastUsedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;

        if (UsedCount >= MaxUses)
        {
            Status = EnrollmentStatus.Completed;
        }
    }

    public void Revoke()
    {
        if (Status == EnrollmentStatus.Revoked)
        {
            return;
        }

        if (Status == EnrollmentStatus.Completed)
        {
            throw new InvalidOperationException(
                "A completed enrollment token cannot be revoked.");
        }

        Status = EnrollmentStatus.Revoked;
        RevokedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkExpired()
    {
        if (Status != EnrollmentStatus.Active)
        {
            return;
        }

        if (DateTime.UtcNow < ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "Enrollment token has not expired.");
        }

        Status = EnrollmentStatus.Expired;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}