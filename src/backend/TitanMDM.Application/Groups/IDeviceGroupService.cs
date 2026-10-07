namespace TitanMDM.Application.Groups;

public interface IDeviceGroupService
{
    Task<IReadOnlyCollection<DeviceGroupDto>>
        GetAllAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<DeviceGroupDetailsDto?>
        GetByIdAsync(
            Guid organizationId,
            Guid groupId,
            CancellationToken cancellationToken = default);

    Task<DeviceGroupDetailsDto>
        CreateAsync(
            Guid organizationId,
            CreateDeviceGroupRequest request,
            CancellationToken cancellationToken = default);

    Task<DeviceGroupDetailsDto>
        UpdateAsync(
            Guid organizationId,
            Guid groupId,
            UpdateDeviceGroupRequest request,
            CancellationToken cancellationToken = default);

    Task AddMembersAsync(
        Guid organizationId,
        Guid groupId,
        IReadOnlyCollection<Guid> deviceIds,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        Guid organizationId,
        Guid groupId,
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<int> ExecuteCommandAsync(
        Guid organizationId,
        Guid userId,
        Guid groupId,
        GroupCommandRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid organizationId,
        Guid groupId,
        CancellationToken cancellationToken = default);
}

public sealed record DeviceGroupDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDynamic,
    string? RuleJson,
    bool IsEnabled,
    int DeviceCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DeviceGroupMemberDto(
    Guid DeviceId,
    string DeviceName,
    string Platform,
    string Status,
    string ComplianceStatus,
    string Source,
    DateTime AddedAtUtc);

public sealed record DeviceGroupDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDynamic,
    string? RuleJson,
    bool IsEnabled,
    IReadOnlyCollection<DeviceGroupMemberDto> Members,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateDeviceGroupRequest(
    string Name,
    string? Description,
    bool IsDynamic,
    string? RuleJson,
    IReadOnlyCollection<Guid>? DeviceIds);

public sealed record UpdateDeviceGroupRequest(
    string Name,
    string? Description,
    bool IsDynamic,
    string? RuleJson);

public sealed record AddGroupMembersRequest(
    IReadOnlyCollection<Guid> DeviceIds);

public sealed record GroupCommandRequest(
    string CommandType,
    string? PayloadJson,
    int ExpiresInMinutes = 60);