namespace TitanMDM.Domain.Entities;

public sealed class EntraDirectoryUser
{
    private EntraDirectoryUser()
    {
    }

    public EntraDirectoryUser(
        Guid organizationId,
        string entraObjectId,
        string displayName,
        string userPrincipalName,
        string? mail)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        EntraObjectId = entraObjectId.Trim();
        DisplayName = displayName.Trim();
        UserPrincipalName = userPrincipalName.Trim().ToLowerInvariant();
        Mail = mail?.Trim().ToLowerInvariant();
        IsActive = true;
        SyncedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string EntraObjectId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string UserPrincipalName { get; private set; } = string.Empty;
    public string? Mail { get; private set; }
    public string? JobTitle { get; private set; }
    public string? Department { get; private set; }
    public Guid? LinkedTitanUserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime SyncedAtUtc { get; private set; }

    public void Update(
        string displayName,
        string? mail,
        string? jobTitle,
        string? department,
        Guid? linkedTitanUserId,
        bool isActive)
    {
        DisplayName = displayName.Trim();
        Mail = mail?.Trim().ToLowerInvariant();
        JobTitle = jobTitle?.Trim();
        Department = department?.Trim();
        LinkedTitanUserId = linkedTitanUserId;
        IsActive = isActive;
        SyncedAtUtc = DateTime.UtcNow;
    }
}
