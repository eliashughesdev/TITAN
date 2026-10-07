using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Groups;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Groups;

public sealed class DeviceGroupService
    : IDeviceGroupService
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IDeviceCommandService
        _commandService;

    private readonly DeviceGroupRuleEvaluator
        _ruleEvaluator;

    public DeviceGroupService(
        TitanMdmDbContext dbContext,
        IDeviceCommandService commandService)
    {
        _dbContext =
            dbContext;

        _commandService =
            commandService;

        _ruleEvaluator =
            new DeviceGroupRuleEvaluator();
    }

    public async Task<
        IReadOnlyCollection<DeviceGroupDto>>
        GetAllAsync(
            Guid organizationId,
            CancellationToken cancellationToken =
                default)
    {
        var deviceGroups =
            await _dbContext
                .DeviceGroups
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                    organizationId)
                .OrderBy(x =>
                    x.Name)
                .ToListAsync(
                    cancellationToken);

        foreach (
            var deviceGroup
            in deviceGroups.Where(
                group =>
                    group.IsDynamic))
        {
            await SynchronizeDynamicGroupAsync(
                organizationId,
                deviceGroup,
                cancellationToken);
        }

        var counts =
            await _dbContext
                .DeviceGroupMembers
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                    organizationId)
                .GroupBy(x =>
                    x.GroupId)
                .Select(x =>
                    new
                    {
                        GroupId =
                            x.Key,

                        Count =
                            x.Count()
                    })
                .ToDictionaryAsync(
                    x =>
                        x.GroupId,

                    x =>
                        x.Count,

                    cancellationToken);

        return deviceGroups
            .Select(deviceGroup =>
                new DeviceGroupDto(
                    deviceGroup.Id,
                    deviceGroup.Name,
                    deviceGroup.Description,
                    deviceGroup.IsDynamic,
                    deviceGroup.RuleJson,
                    deviceGroup.IsEnabled,

                    counts.GetValueOrDefault(
                        deviceGroup.Id),

                    deviceGroup.CreatedAtUtc,
                    deviceGroup.UpdatedAtUtc))
            .ToArray();
    }

    public async Task<
        DeviceGroupDetailsDto?>
        GetByIdAsync(
            Guid organizationId,
            Guid groupId,
            CancellationToken cancellationToken =
                default)
    {
        var deviceGroup =
            await _dbContext
                .DeviceGroups
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            groupId
                        &&
                        x.OrganizationId ==
                            organizationId,

                    cancellationToken);

        if (
            deviceGroup is null)
        {
            return null;
        }

        if (
            deviceGroup.IsDynamic)
        {
            await SynchronizeDynamicGroupAsync(
                organizationId,
                deviceGroup,
                cancellationToken);
        }

        var members =
            await (
                from member
                    in _dbContext
                        .DeviceGroupMembers
                        .AsNoTracking()

                join device
                    in _dbContext
                        .Devices
                        .AsNoTracking()

                    on member.DeviceId
                    equals device.Id

                where
                    member.GroupId ==
                        deviceGroup.Id
                    &&
                    member.OrganizationId ==
                        organizationId
                    &&
                    device.OrganizationId ==
                        organizationId
                    &&
                    !device.IsDeleted

                orderby
                    device.DeviceName

                select
                    new DeviceGroupMemberDto(
                        device.Id,
                        device.DeviceName,
                        device.Platform
                            .ToString(),
                        device.Status
                            .ToString(),
                        device.ComplianceStatus
                            .ToString(),
                        member.Source,
                        member.AddedAtUtc))
            .ToListAsync(
                cancellationToken);

        return
            new DeviceGroupDetailsDto(
                deviceGroup.Id,
                deviceGroup.Name,
                deviceGroup.Description,
                deviceGroup.IsDynamic,
                deviceGroup.RuleJson,
                deviceGroup.IsEnabled,
                members,
                deviceGroup.CreatedAtUtc,
                deviceGroup.UpdatedAtUtc);
    }

    public async Task<
        DeviceGroupDetailsDto>
        CreateAsync(
            Guid organizationId,
            CreateDeviceGroupRequest request,
            CancellationToken cancellationToken =
                default)
    {
        if (
            string.IsNullOrWhiteSpace(
                request.Name))
        {
            throw
                new InvalidOperationException(
                    "El nombre del grupo es obligatorio.");
        }

        var normalizedName =
            request.Name.Trim();

        var exists =
            await _dbContext
                .DeviceGroups
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Name ==
                            normalizedName,

                    cancellationToken);

        if (exists)
        {
            throw
                new InvalidOperationException(
                    "Ya existe un grupo con ese nombre.");
        }

        var deviceGroup =
            new DeviceGroup(
                organizationId,
                normalizedName,
                request.Description,
                request.IsDynamic,
                request.RuleJson);

        _dbContext
            .DeviceGroups
            .Add(
                deviceGroup);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        if (
            deviceGroup.IsDynamic)
        {
            await SynchronizeDynamicGroupAsync(
                organizationId,
                deviceGroup,
                cancellationToken);
        }
        else if (
            request.DeviceIds is not null
            &&
            request.DeviceIds.Count >
                0)
        {
            await AddMembersAsync(
                organizationId,
                deviceGroup.Id,
                request.DeviceIds,
                cancellationToken);
        }

        return
            (await GetByIdAsync(
                organizationId,
                deviceGroup.Id,
                cancellationToken))!;
    }

    public async Task<
        DeviceGroupDetailsDto>
        UpdateAsync(
            Guid organizationId,
            Guid groupId,
            UpdateDeviceGroupRequest request,
            CancellationToken cancellationToken =
                default)
    {
        var deviceGroup =
            await GetEntityAsync(
                organizationId,
                groupId,
                cancellationToken);

        if (
            string.IsNullOrWhiteSpace(
                request.Name))
        {
            throw
                new InvalidOperationException(
                    "El nombre del grupo es obligatorio.");
        }

        var normalizedName =
            request.Name.Trim();

        var duplicateName =
            await _dbContext
                .DeviceGroups
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id !=
                            groupId
                        &&
                        x.Name ==
                            normalizedName,

                    cancellationToken);

        if (
            duplicateName)
        {
            throw
                new InvalidOperationException(
                    "Ya existe otro grupo con ese nombre.");
        }

        deviceGroup.Update(
            normalizedName,
            request.Description,
            request.IsDynamic,
            request.RuleJson);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        if (
            deviceGroup.IsDynamic)
        {
            await SynchronizeDynamicGroupAsync(
                organizationId,
                deviceGroup,
                cancellationToken);
        }

        return
            (await GetByIdAsync(
                organizationId,
                groupId,
                cancellationToken))!;
    }

    public async Task
        AddMembersAsync(
            Guid organizationId,
            Guid groupId,
            IReadOnlyCollection<Guid>
                deviceIds,
            CancellationToken cancellationToken =
                default)
    {
        var deviceGroup =
            await GetEntityAsync(
                organizationId,
                groupId,
                cancellationToken);

        if (
            deviceGroup.IsDynamic)
        {
            throw
                new InvalidOperationException(
                    "Los miembros de un grupo dinámico se administran automáticamente.");
        }

        var ids =
            deviceIds
                .Where(x =>
                    x != Guid.Empty)
                .Distinct()
                .ToArray();

        if (
            ids.Length ==
            0)
        {
            return;
        }

        var validDevices =
            await _dbContext
                .Devices
                .AsNoTracking()
                .Where(x =>
                    ids.Contains(
                        x.Id)
                    &&
                    x.OrganizationId ==
                        organizationId
                    &&
                    !x.IsDeleted)
                .Select(x =>
                    x.Id)
                .ToListAsync(
                    cancellationToken);

        if (
            validDevices.Count ==
            0)
        {
            return;
        }

        var existingDeviceIds =
            await _dbContext
                .DeviceGroupMembers
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.GroupId ==
                        groupId
                    &&
                    validDevices.Contains(
                        x.DeviceId))
                .Select(x =>
                    x.DeviceId)
                .ToListAsync(
                    cancellationToken);

        foreach (
            var deviceId
            in validDevices.Except(
                existingDeviceIds))
        {
            _dbContext
                .DeviceGroupMembers
                .Add(
                    new DeviceGroupMember(
                        organizationId,
                        groupId,
                        deviceId));
        }

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    public async Task
        RemoveMemberAsync(
            Guid organizationId,
            Guid groupId,
            Guid deviceId,
            CancellationToken cancellationToken =
                default)
    {
        var deviceGroup =
            await GetEntityAsync(
                organizationId,
                groupId,
                cancellationToken);

        if (
            deviceGroup.IsDynamic)
        {
            throw
                new InvalidOperationException(
                    "Los miembros de un grupo dinámico se administran automáticamente.");
        }

        var member =
            await _dbContext
                .DeviceGroupMembers
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.GroupId ==
                            groupId
                        &&
                        x.DeviceId ==
                            deviceId,

                    cancellationToken);

        if (
            member is null)
        {
            return;
        }

        _dbContext
            .DeviceGroupMembers
            .Remove(
                member);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    public async Task<int>
        ExecuteCommandAsync(
            Guid organizationId,
            Guid userId,
            Guid groupId,
            GroupCommandRequest request,
            CancellationToken cancellationToken =
                default)
    {
        var deviceGroup =
            await GetEntityAsync(
                organizationId,
                groupId,
                cancellationToken);

        if (
            deviceGroup.IsDynamic)
        {
            await SynchronizeDynamicGroupAsync(
                organizationId,
                deviceGroup,
                cancellationToken);
        }

        if (
            string.IsNullOrWhiteSpace(
                request.CommandType))
        {
            throw
                new InvalidOperationException(
                    "CommandType is required.");
        }

        var deviceIds =
            await _dbContext
                .DeviceGroupMembers
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId ==
                        organizationId
                    &&
                    x.GroupId ==
                        groupId)
                .Select(x =>
                    x.DeviceId)
                .Distinct()
                .ToListAsync(
                    cancellationToken);

        var created =
            0;

        foreach (
            var deviceId
            in deviceIds)
        {
            await _commandService
                .CreateAsync(
                    organizationId,
                    userId,

                    new CreateDeviceCommandRequest(
                        deviceId,
                        request.CommandType.Trim(),
                        request.PayloadJson,

                        Math.Clamp(
                            request
                                .ExpiresInMinutes,
                            1,
                            10080)),

                    cancellationToken);

            created++;
        }

        return created;
    }

    public async Task
        DeleteAsync(
            Guid organizationId,
            Guid groupId,
            CancellationToken cancellationToken =
                default)
    {
        var deviceGroup =
            await GetEntityAsync(
                organizationId,
                groupId,
                cancellationToken);

        _dbContext
            .DeviceGroups
            .Remove(
                deviceGroup);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    private async Task
        SynchronizeDynamicGroupAsync(
            Guid organizationId,
            DeviceGroup deviceGroup,
            CancellationToken cancellationToken)
    {
        if (
            !deviceGroup.IsDynamic
            ||
            string.IsNullOrWhiteSpace(
                deviceGroup.RuleJson))
        {
            return;
        }

        var devices =
            await _dbContext
                .Devices
                .AsNoTracking()
                .Where(device =>
                    device.OrganizationId ==
                        organizationId
                    &&
                    !device.IsDeleted)
                .ToListAsync(
                    cancellationToken);

        var matchingIds =
            devices
                .Where(device =>
                    _ruleEvaluator.Matches(
                        device,
                        deviceGroup.RuleJson))
                .Select(device =>
                    device.Id)
                .ToHashSet();

        var existingMembers =
            await _dbContext
                .DeviceGroupMembers
                .Where(member =>
                    member.OrganizationId ==
                        organizationId
                    &&
                    member.GroupId ==
                        deviceGroup.Id)
                .ToListAsync(
                    cancellationToken);

        var existingIds =
            existingMembers
                .Select(member =>
                    member.DeviceId)
                .ToHashSet();

        var idsToAdd =
            matchingIds
                .Except(
                    existingIds)
                .ToArray();

        var membersToRemove =
            existingMembers
                .Where(member =>
                    !matchingIds.Contains(
                        member.DeviceId))
                .ToArray();

        foreach (
            var deviceId
            in idsToAdd)
        {
            _dbContext
                .DeviceGroupMembers
                .Add(
                    new DeviceGroupMember(
                        organizationId,
                        deviceGroup.Id,
                        deviceId));
        }

        if (
            membersToRemove.Length >
            0)
        {
            _dbContext
                .DeviceGroupMembers
                .RemoveRange(
                    membersToRemove);
        }

        if (
            idsToAdd.Length >
                0
            ||
            membersToRemove.Length >
                0)
        {
            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);
        }
    }

    private async Task<DeviceGroup>
        GetEntityAsync(
            Guid organizationId,
            Guid groupId,
            CancellationToken cancellationToken)
    {
        return
            await _dbContext
                .DeviceGroups
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            groupId
                        &&
                        x.OrganizationId ==
                            organizationId,

                    cancellationToken)
            ??
            throw
                new InvalidOperationException(
                    "El grupo no existe.");
    }
}