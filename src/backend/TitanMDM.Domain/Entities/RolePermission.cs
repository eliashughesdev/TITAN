namespace TitanMDM.Domain.Entities;

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public RolePermission(
        Guid roleId,
        Guid permissionId)
    {
        if (roleId == Guid.Empty)
            throw new ArgumentException(
                "RoleId is required.",
                nameof(roleId));

        if (permissionId == Guid.Empty)
            throw new ArgumentException(
                "PermissionId is required.",
                nameof(permissionId));

        RoleId = roleId;
        PermissionId = permissionId;

        GrantedAtUtc = DateTime.UtcNow;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public DateTime GrantedAtUtc { get; private set; }
}