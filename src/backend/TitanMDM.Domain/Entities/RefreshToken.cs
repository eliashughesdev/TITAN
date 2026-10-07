namespace TitanMDM.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        string? createdByIp = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "UserId is required.",
                nameof(userId));

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException(
                "Token hash is required.",
                nameof(tokenHash));

        if (expiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentException(
                "Expiration must be in the future.",
                nameof(expiresAtUtc));

        Id = Guid.NewGuid();
        UserId = userId;

        TokenHash = tokenHash;
        CreatedByIp = createdByIp;

        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? RevokedByIp { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked =>
        RevokedAtUtc.HasValue;

    public bool IsExpired =>
        DateTime.UtcNow >= ExpiresAtUtc;

    public bool IsActive =>
        !IsRevoked && !IsExpired;

    public void Revoke(
        string? revokedByIp = null,
        string? replacedByTokenHash = null)
    {
        if (IsRevoked)
            return;

        RevokedAtUtc = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}