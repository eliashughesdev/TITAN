using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Devices;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Devices;

public sealed class DeviceQueryService
    : IDeviceQueryService
{
    private static readonly string[]
        OperationalCommandTypes =
        [
            "DEVICE_INFO",
            "DEVICE_INVENTORY",
            "APP_INVENTORY",
            "PROCESS_INVENTORY",
            "SERVICE_INVENTORY",
            "NETWORK_INFO",
            "SECURITY_STATUS",
            "COMPLIANCE_CHECK",
            "WINDOWS_UPDATE_STATUS"
        ];

    private readonly TitanMdmDbContext
        _dbContext;

    public DeviceQueryService(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext
            ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public async Task<DeviceListResultDto>
        GetDevicesAsync(
            Guid organizationId,
            string? search,
            string? platform,
            string? status,
            string? compliance,
            bool? managed,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize,
            CancellationToken cancellationToken =
                default)
    {
        ValidateOrganization(
            organizationId);

        page =
            Math.Max(
                page,
                1);

        pageSize =
            Math.Clamp(
                pageSize,
                10,
                100);

        var query =
            _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    device =>
                        device.OrganizationId ==
                            organizationId
                        &&
                        !device.IsDeleted);

        if (
            !string.IsNullOrWhiteSpace(
                search))
        {
            var term =
                search.Trim();

            query =
                query.Where(
                    device =>
                        device.DeviceName
                            .Contains(term)
                        ||
                        device.SerialNumber
                            .Contains(term)
                        ||
                        (
                            device.Manufacturer !=
                                null
                            &&
                            device.Manufacturer
                                .Contains(term)
                        )
                        ||
                        (
                            device.Model !=
                                null
                            &&
                            device.Model
                                .Contains(term)
                        )
                        ||
                        (
                            device.AssignedUser !=
                                null
                            &&
                            device.AssignedUser
                                .Contains(term)
                        )
                        ||
                        (
                            device.Department !=
                                null
                            &&
                            device.Department
                                .Contains(term)
                        )
                        ||
                        (
                            device.IpAddress !=
                                null
                            &&
                            device.IpAddress
                                .Contains(term)
                        ));
        }

        if (
            !string.IsNullOrWhiteSpace(
                platform)
            &&
            Enum.TryParse<DevicePlatform>(
                platform,
                true,
                out var parsedPlatform))
        {
            query =
                query.Where(
                    device =>
                        device.Platform ==
                            parsedPlatform);
        }

        if (
            !string.IsNullOrWhiteSpace(
                status)
            &&
            Enum.TryParse<DeviceStatus>(
                status,
                true,
                out var parsedStatus))
        {
            query =
                query.Where(
                    device =>
                        device.Status ==
                            parsedStatus);
        }

        if (
            !string.IsNullOrWhiteSpace(
                compliance)
            &&
            Enum.TryParse<ComplianceStatus>(
                compliance,
                true,
                out var parsedCompliance))
        {
            query =
                query.Where(
                    device =>
                        device.ComplianceStatus ==
                            parsedCompliance);
        }

        if (
            managed.HasValue)
        {
            query =
                query.Where(
                    device =>
                        device.IsManaged ==
                            managed.Value);
        }

        var descending =
            !string.Equals(
                sortDirection,
                "asc",
                StringComparison.OrdinalIgnoreCase);

        query =
            ApplySorting(
                query,
                sortBy,
                descending);

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var totalPages =
            totalCount ==
            0
                ? 0
                : (int)Math.Ceiling(
                    totalCount /
                    (double)pageSize);

        var items =
            await query
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(
                    pageSize)
                .Select(
                    device =>
                        new DeviceListItemDto(
                            device.Id,
                            device.DeviceName,
                            device.Platform
                                .ToString(),
                            device.Status
                                .ToString(),
                            device.ComplianceStatus
                                .ToString(),
                            device.SerialNumber,
                            device.Manufacturer,
                            device.Model,
                            device.OperatingSystem,
                            device.OperatingSystemVersion,
                            device.AssignedUser,
                            device.Department,
                            device.IpAddress,
                            device.BatteryLevel,
                            device.IsManaged,
                            device.EnrolledAtUtc,
                            device.LastSeenAtUtc))
                .ToListAsync(
                    cancellationToken);

        return
            new DeviceListResultDto(
                items,
                totalCount,
                page,
                pageSize,
                totalPages);
    }

    public async Task<DeviceDetailsDto?>
        GetDeviceByIdAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken =
                default)
    {
        ValidateOrganization(
            organizationId);

        if (
            deviceId ==
            Guid.Empty)
        {
            return null;
        }

        return
            await _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    device =>
                        device.OrganizationId ==
                            organizationId
                        &&
                        device.Id ==
                            deviceId
                        &&
                        !device.IsDeleted)
                .Select(
                    device =>
                        new DeviceDetailsDto(
                            device.Id,
                            device.OrganizationId,
                            device.DeviceName,
                            device.Platform.ToString(),
                            device.Status.ToString(),
                            device.ComplianceStatus.ToString(),
                            device.SerialNumber,
                            device.Imei,
                            device.Manufacturer,
                            device.Model,
                            device.OperatingSystem,
                            device.OperatingSystemVersion,
                            device.AgentVersion,
                            device.IpAddress,
                            device.MacAddress,
                            device.AssignedUser,
                            device.Department,
                            device.BatteryLevel,
                            device.IsManaged,
                            device.EnrolledAtUtc,
                            device.LastSeenAtUtc,
                            device.CreatedAtUtc,
                            device.UpdatedAtUtc))
                .FirstOrDefaultAsync(
                    cancellationToken);
    }

    public async Task<AndroidDeviceDetailsDto?>
        GetAndroidDeviceDetailsAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken =
                default)
    {
        ValidateOrganization(
            organizationId);

        if (
            deviceId ==
            Guid.Empty)
        {
            return null;
        }

        var belongsToOrganization =
            await _dbContext
                .Devices
                .AsNoTracking()
                .AnyAsync(
                    device =>
                        device.Id ==
                            deviceId
                        &&
                        device.OrganizationId ==
                            organizationId
                        &&
                        device.Platform ==
                            DevicePlatform.Android
                        &&
                        !device.IsDeleted,
                    cancellationToken);

        if (
            !belongsToOrganization)
        {
            return null;
        }

        return
            await _dbContext
                .AndroidDevices
                .AsNoTracking()
                .Where(
                    android =>
                        android.OrganizationId ==
                            organizationId
                        &&
                        android.DeviceId ==
                            deviceId)
                .Select(
                    android =>
                        new AndroidDeviceDetailsDto(
                            android.Id,
                            android.DeviceId,
                            android.GoogleDeviceName,
                            android.GoogleDeviceId,
                            android.ManagementMode,
                            android.Ownership,
                            android.State,
                            android.AppliedPolicyName,
                            android.AppliedPolicyVersion,
                            android.AppliedPolicyState,
                            android.EnrollmentTokenName,
                            android.UserName,
                            android.Brand,
                            android.Hardware,
                            android.DeviceBasebandVersion,
                            android.BootloaderVersion,
                            android.SecurityPatchLevel,
                            android.ApiLevel,
                            android.BuildNumber,
                            android.KernelVersion,
                            android.AndroidDevicePolicyVersion,
                            android.AndroidDevicePolicyVersionCode,
                            android.EncryptionStatus,
                            android.SecurityPosture,
                            android.EnrollmentTimeUtc,
                            android.LastStatusReportTimeUtc,
                            android.LastPolicySyncTimeUtc,
                            android.LastSynchronizedAtUtc,
                            android.IsDeletedInGoogle,
                            android.DeletedInGoogleAtUtc))
                .FirstOrDefaultAsync(
                    cancellationToken);
    }

    public async Task<DeviceOperationalSnapshotDto?>
        GetOperationalSnapshotAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken =
                default)
    {
        ValidateOrganization(
            organizationId);

        var device =
            await GetDeviceByIdAsync(
                organizationId,
                deviceId,
                cancellationToken);

        if (
            device is null)
        {
            return null;
        }

        var groups =
    await (
        from member
            in _dbContext
                .DeviceGroupMembers
                .AsNoTracking()

        join deviceGroup
            in _dbContext
                .DeviceGroups
                .AsNoTracking()

            on member.GroupId
            equals deviceGroup.Id

        where
            member.OrganizationId ==
                organizationId
            &&
            member.DeviceId ==
                deviceId
            &&
            deviceGroup.OrganizationId ==
                organizationId

        orderby deviceGroup.Name

        select
            new DeviceGroupMembershipDto(
                deviceGroup.Id,
                deviceGroup.Name,
                deviceGroup.IsDynamic,
                member.Source))
        .ToListAsync(
            cancellationToken);

        var posture =
            await _dbContext
                .DeviceSecurityPostures
                .AsNoTracking()
                .Where(
                    security =>
                        security.OrganizationId ==
                            organizationId
                        &&
                        security.DeviceId ==
                            deviceId)
                .Select(
                    security =>
                        new DeviceSecuritySummaryDto(
                            security.ComplianceScore,
                            security.RiskLevel,
                            security.ComplianceStatus,
                            security.AgentInstalled,
                            security.AgentVersionName,
                            security.LastSecurityScanAtUtc,
                            security.LastComplianceCheckAtUtc))
                .FirstOrDefaultAsync(
                    cancellationToken);

        var successfulCommands =
            await _dbContext
                .DeviceCommands
                .AsNoTracking()
                .Where(
                    command =>
                        command.OrganizationId ==
                            organizationId
                        &&
                        command.DeviceId ==
                            deviceId
                        &&
                        command.Status ==
                            DeviceCommandStatus.Success
                        &&
                        OperationalCommandTypes
                            .Contains(
                                command.CommandType))
                .OrderByDescending(
                    command =>
                        command.CompletedAtUtc
                        ??
                        command.UpdatedAtUtc)
                .ToListAsync(
                    cancellationToken);

        var latest =
            successfulCommands
                .GroupBy(
                    command =>
                        command.CommandType
                            .ToUpperInvariant())
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                    {
                        var command =
                            group.First();

                        return
                            new DeviceCommandSnapshotDto(
                                command.Id,
                                command.CommandType,
                                command.Status
                                    .ToString(),
                                command.CreatedAtUtc,
                                command.CompletedAtUtc,
                                command.ResultJson,
                                command.ErrorCode,
                                command.ErrorMessage);
                    });

        var lastInventoryAtUtc =
            successfulCommands
                .Where(
                    command =>
                        command.CommandType ==
                            "DEVICE_INVENTORY"
                        ||
                        command.CommandType ==
                            "DEVICE_INFO")
                .Select(
                    command =>
                        command.CompletedAtUtc
                        ??
                        command.UpdatedAtUtc)
                .Cast<DateTime?>()
                .FirstOrDefault();

        return
            new DeviceOperationalSnapshotDto(
                device,
                groups,
                posture,
                latest,
                lastInventoryAtUtc);
    }

    private static IQueryable<
        TitanMDM.Domain.Entities.Device>
        ApplySorting(
            IQueryable<
                TitanMDM.Domain.Entities.Device>
                query,
            string? sortBy,
            bool descending)
    {
        var normalized =
            sortBy
                ?.Trim()
                .ToLowerInvariant();

        return
            normalized switch
            {
                "name" =>
                    descending
                        ? query
                            .OrderByDescending(
                                x =>
                                    x.DeviceName)
                        : query
                            .OrderBy(
                                x =>
                                    x.DeviceName),

                "platform" =>
                    descending
                        ? query
                            .OrderByDescending(
                                x =>
                                    x.Platform)
                        : query
                            .OrderBy(
                                x =>
                                    x.Platform),

                "status" =>
                    descending
                        ? query
                            .OrderByDescending(
                                x =>
                                    x.Status)
                        : query
                            .OrderBy(
                                x =>
                                    x.Status),

                "compliance" =>
                    descending
                        ? query
                            .OrderByDescending(
                                x =>
                                    x.ComplianceStatus)
                        : query
                            .OrderBy(
                                x =>
                                    x.ComplianceStatus),

                "enrolled" =>
                    descending
                        ? query
                            .OrderByDescending(
                                x =>
                                    x.EnrolledAtUtc)
                        : query
                            .OrderBy(
                                x =>
                                    x.EnrolledAtUtc),

                _ =>
                    descending
                        ? query
                            .OrderByDescending(
                                x =>
                                    x.LastSeenAtUtc
                                    ??
                                    x.CreatedAtUtc)
                        : query
                            .OrderBy(
                                x =>
                                    x.LastSeenAtUtc
                                    ??
                                    x.CreatedAtUtc)
            };
    }

    private static void ValidateOrganization(
        Guid organizationId)
    {
        if (
            organizationId ==
            Guid.Empty)
        {
            throw
                new ArgumentException(
                    "OrganizationId is required.",
                    nameof(
                        organizationId));
        }
    }
}