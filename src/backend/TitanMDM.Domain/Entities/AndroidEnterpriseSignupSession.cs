using System.Security.Cryptography;

namespace TitanMDM.Domain.Entities;

public sealed class AndroidEnterpriseSignupSession
{
    private AndroidEnterpriseSignupSession()
    {
    }

    public AndroidEnterpriseSignupSession(
        Guid organizationId,
        Guid createdByUserId,
        DateTime expiresAtUtc)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException(
                "CreatedByUserId is required.",
                nameof(createdByUserId));

        if (expiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentException(
                "Expiration must be in the future.",
                nameof(expiresAtUtc));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;

        State = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32))
            .ToLowerInvariant();

        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string State { get; private set; } =
        string.Empty;

    public string? SignupUrlName { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public bool IsCompleted =>
        CompletedAtUtc.HasValue;

    public bool IsExpired =>
        DateTime.UtcNow >= ExpiresAtUtc;

    public void AttachSignupUrl(
        string signupUrlName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            signupUrlName);

        if (IsCompleted)
            throw new InvalidOperationException(
                "The signup session is already completed.");

        SignupUrlName = signupUrlName;
    }

    public void Complete()
    {
        if (IsCompleted)
            return;

        CompletedAtUtc = DateTime.UtcNow;
    }
}