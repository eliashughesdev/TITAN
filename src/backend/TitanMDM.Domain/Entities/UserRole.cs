namespace TitanMDM.Domain.Entities;

public sealed class UserRole
{
    private UserRole()
    {
    }

    public UserRole(
        Guid userId,
        Guid roleId,
        Guid? assignedByUserId = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "UserId is required.",
                nameof(userId));

        if (roleId == Guid.Empty)
            throw new ArgumentException(
                "RoleId is required.",
                nameof(roleId));

        UserId = userId;
        RoleId = roleId;
        AssignedByUserId = assignedByUserId;

        AssignedAtUtc = DateTime.UtcNow;
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public Guid? AssignedByUserId { get; private set; }

    public DateTime AssignedAtUtc { get; private set; }
}