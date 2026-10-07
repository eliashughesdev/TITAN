using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Dashboard;
using TitanMDM.Application.Dashboard.DTOs;
using TitanMDM.Application.Dashboard.Interfaces;

using TitanMDM.Domain.Enums;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Dashboard;

public sealed class DashboardService
    : IDashboardService
{
    private readonly TitanMdmDbContext
        _dbContext;

    public DashboardService(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(
        Guid organizationId,
        DashboardWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        /*
         * ========================================================
         * DEVICES
         * ========================================================
         */

        var devices =
            _dbContext.Devices
                .AsNoTracking()
                .Where(
                    device =>
                        device.OrganizationId ==
                            organizationId
                        &&
                        !device.IsDeleted);

        devices =
            workspace switch
            {
                DashboardWorkspace.Windows =>
                    devices.Where(
                        device =>
                            device.Platform ==
                            DevicePlatform.Windows),

                DashboardWorkspace.Android =>
                    devices.Where(
                        device =>
                            device.Platform ==
                            DevicePlatform.Android),

                _ =>
                    devices
            };

        var total =
            await devices.CountAsync(
                cancellationToken);

        var online =
            await devices.CountAsync(
                x =>
                    x.Status ==
                    DeviceStatus.Online,
                cancellationToken);

        var offline =
            await devices.CountAsync(
                x =>
                    x.Status ==
                    DeviceStatus.Offline,
                cancellationToken);

        var pending =
            await devices.CountAsync(
                x =>
                    x.Status ==
                    DeviceStatus.Pending,
                cancellationToken);

        var enrolling =
            await devices.CountAsync(
                x =>
                    x.Status ==
                    DeviceStatus.Enrolling,
                cancellationToken);

        var quarantined =
            await devices.CountAsync(
                x =>
                    x.Status ==
                    DeviceStatus.Quarantined,
                cancellationToken);

        var retired =
            await devices.CountAsync(
                x =>
                    x.Status ==
                    DeviceStatus.Retired,
                cancellationToken);

        var managed =
            await devices.CountAsync(
                x =>
                    x.IsManaged,
                cancellationToken);

        /*
         * ========================================================
         * PLATFORMS
         * ========================================================
         */

        var windows =
            await devices.CountAsync(
                x =>
                    x.Platform ==
                    DevicePlatform.Windows,
                cancellationToken);

        var android =
            await devices.CountAsync(
                x =>
                    x.Platform ==
                    DevicePlatform.Android,
                cancellationToken);

        var unknownPlatform =
            await devices.CountAsync(
                x =>
                    x.Platform ==
                    DevicePlatform.Unknown,
                cancellationToken);

        /*
         * ========================================================
         * COMPLIANCE
         * ========================================================
         */

        var compliant =
            await devices.CountAsync(
                x =>
                    x.ComplianceStatus ==
                    ComplianceStatus.Compliant,
                cancellationToken);

        var nonCompliant =
            await devices.CountAsync(
                x =>
                    x.ComplianceStatus ==
                    ComplianceStatus.NonCompliant,
                cancellationToken);

        var evaluating =
            await devices.CountAsync(
                x =>
                    x.ComplianceStatus ==
                    ComplianceStatus.Evaluating,
                cancellationToken);

        var complianceQuarantined =
            await devices.CountAsync(
                x =>
                    x.ComplianceStatus ==
                    ComplianceStatus.Quarantined,
                cancellationToken);

        var unknownCompliance =
            await devices.CountAsync(
                x =>
                    x.ComplianceStatus ==
                    ComplianceStatus.Unknown,
                cancellationToken);

        var evaluatedDevices =
            compliant +
            nonCompliant;

        decimal? compliancePercentage =
            null;

        if (evaluatedDevices > 0)
        {
            compliancePercentage =
                Math.Round(
                    (decimal)compliant
                    /
                    evaluatedDevices
                    *
                    100m,
                    2);
        }

        /*
         * ========================================================
         * COMMANDS
         * ========================================================
         */

        var commands =
            _dbContext.DeviceCommands
                .AsNoTracking()
                .Where(
                    command =>
                        command.OrganizationId ==
                        organizationId);

        if (workspace != DashboardWorkspace.Global)
        {
            var visibleDeviceIds =
                devices.Select(
                    x =>
                        x.Id);

            commands =
                commands.Where(
                    command =>
                        visibleDeviceIds.Contains(
                            command.DeviceId));
        }

        var totalCommands =
            await commands.CountAsync(
                cancellationToken);

        var pendingCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Pending,
                cancellationToken);

        var queuedCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Queued,
                cancellationToken);

        var dispatchingCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Dispatching,
                cancellationToken);

        var sentCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Sent,
                cancellationToken);

        var deliveredCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Delivered,
                cancellationToken);

        var executingCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Executing,
                cancellationToken);

        var successfulCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Success,
                cancellationToken);

        var failedCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Failed,
                cancellationToken);

        var timeoutCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Timeout,
                cancellationToken);

        var cancelledCommands =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Cancelled,
                cancellationToken);

        var activeCommands =
            pendingCommands
            +
            queuedCommands
            +
            dispatchingCommands
            +
            sentCommands
            +
            deliveredCommands
            +
            executingCommands;

        var commandProblems =
            failedCommands
            +
            timeoutCommands;

        /*
         * ========================================================
         * ADVANCED WINDOWS
         * ========================================================
         */

        WindowsDashboardSummaryDto?
            windowsSummary =
                null;

        if (
            workspace ==
                DashboardWorkspace.Windows
            ||
            workspace ==
                DashboardWorkspace.Global)
        {
            windowsSummary =
                await BuildWindowsSummaryAsync(
                    organizationId,
                    cancellationToken);
        }

        /*
         * ========================================================
         * ADVANCED ANDROID
         * ========================================================
         */

        AndroidDashboardSummaryDto?
            androidSummary =
                null;

        if (
            workspace ==
                DashboardWorkspace.Android
            ||
            workspace ==
                DashboardWorkspace.Global)
        {
            androidSummary =
                await BuildAndroidSummaryAsync(
                    organizationId,
                    cancellationToken);
        }

        /*
         * ========================================================
         * SYSTEM
         * ========================================================
         */

        var databaseConnected =
            await _dbContext.Database
                .CanConnectAsync(
                    cancellationToken);

        /*
         * ========================================================
         * RESULT
         * ========================================================
         */

        return new DashboardSummaryDto
        {
            Devices =
                new DeviceSummaryDto
                {
                    Total =
                        total,

                    Online =
                        online,

                    Offline =
                        offline,

                    Pending =
                        pending,

                    Enrolling =
                        enrolling,

                    Quarantined =
                        quarantined,

                    Retired =
                        retired,

                    Managed =
                        managed
                },

            Platforms =
                new PlatformSummaryDto
                {
                    Windows =
                        windows,

                    Android =
                        android,

                    Unknown =
                        unknownPlatform
                },

            Compliance =
                new ComplianceSummaryDto
                {
                    Compliant =
                        compliant,

                    NonCompliant =
                        nonCompliant,

                    Evaluating =
                        evaluating,

                    Quarantined =
                        complianceQuarantined,

                    Unknown =
                        unknownCompliance,

                    CompliancePercentage =
                        compliancePercentage
                },

            Commands =
                new CommandSummaryDto
                {
                    Total =
                        totalCommands,

                    Pending =
                        pendingCommands,

                    Queued =
                        queuedCommands,

                    Dispatching =
                        dispatchingCommands,

                    Sent =
                        sentCommands,

                    Delivered =
                        deliveredCommands,

                    Executing =
                        executingCommands,

                    Success =
                        successfulCommands,

                    Failed =
                        failedCommands,

                    Timeout =
                        timeoutCommands,

                    Cancelled =
                        cancelledCommands,

                    Active =
                        activeCommands,

                    Problems =
                        commandProblems
                },

            System =
                new SystemStatusDto
                {
                    Api =
                        "Operational",

                    Database =
                        databaseConnected
                            ? "Connected"
                            : "Unavailable"
                },

            Windows =
                windowsSummary,

            Android =
                androidSummary,

            GeneratedAtUtc =
                DateTime.UtcNow
        };
    }

    /*
     * ============================================================
     * WINDOWS SUMMARY
     * ============================================================
     */

    private async Task<WindowsDashboardSummaryDto>
        BuildWindowsSummaryAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
    {
        var now =
            DateTime.UtcNow;

        var last24Hours =
            now.AddHours(
                -24);

        var windowsDevices =
            await _dbContext.Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        !x.IsDeleted
                        &&
                        x.Platform ==
                            DevicePlatform.Windows)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.Status,
                            x.IsManaged,
                            x.LastSeenAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var windowsDeviceIds =
            windowsDevices
                .Select(
                    x =>
                        x.Id)
                .ToArray();

        var windowsCommands =
            await _dbContext.DeviceCommands
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        windowsDeviceIds.Contains(
                            x.DeviceId)
                        &&
                        (
                            x.CommandType ==
                                "SECURITY_STATUS"
                            ||
                            x.CommandType ==
                                "WINDOWS_UPDATE_STATUS"
                            ||
                            x.CommandType ==
                                "WINDOWS_UPDATE_SCAN"
                        ))
                .Select(
                    x =>
                        new
                        {
                            x.DeviceId,
                            x.CommandType,
                            x.Status,
                            x.ResultJson,
                            x.CreatedAtUtc,
                            x.CompletedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        /*
         * Latest security snapshot per Windows device.
         */

        var securityCommands =
            windowsCommands
                .Where(
                    x =>
                        x.CommandType ==
                            "SECURITY_STATUS"
                        &&
                        x.Status ==
                            DeviceCommandStatus.Success
                        &&
                        !string.IsNullOrWhiteSpace(
                            x.ResultJson))
                .GroupBy(
                    x =>
                        x.DeviceId)
                .Select(
                    group =>
                        group
                            .OrderByDescending(
                                x =>
                                    x.CompletedAtUtc
                                    ??
                                    x.CreatedAtUtc)
                            .First())
                .ToList();

        /*
         * Latest Windows Update snapshot per device.
         */

        var updateStatusCommands =
            windowsCommands
                .Where(
                    x =>
                        x.CommandType ==
                            "WINDOWS_UPDATE_STATUS"
                        &&
                        x.Status ==
                            DeviceCommandStatus.Success
                        &&
                        !string.IsNullOrWhiteSpace(
                            x.ResultJson))
                .GroupBy(
                    x =>
                        x.DeviceId)
                .Select(
                    group =>
                        group
                            .OrderByDescending(
                                x =>
                                    x.CompletedAtUtc
                                    ??
                                    x.CreatedAtUtc)
                            .First())
                .ToList();

        var updateStatusChecks =
            windowsCommands.Count(
                x =>
                    x.CommandType ==
                    "WINDOWS_UPDATE_STATUS");

        var updateScanRequests =
            windowsCommands.Count(
                x =>
                    x.CommandType ==
                    "WINDOWS_UPDATE_SCAN");

        /*
         * ========================================================
         * SECURITY TELEMETRY
         * ========================================================
         */

        var pendingRebootDeviceIds =
            new HashSet<Guid>();

        var defenderAvailable =
            0;

        var firewallAvailable =
            0;

        var bitLockerAvailable =
            0;

        var tpmAvailable =
            0;

        var secureBootEnabled =
            0;

        foreach (
            var command
            in securityCommands)
        {
            if (
                TryReadBoolean(
                    command.ResultJson,
                    "PendingReboot",
                    out var pendingReboot)
                &&
                pendingReboot)
            {
                pendingRebootDeviceIds.Add(
                    command.DeviceId);
            }

            if (
                TryReadNestedBoolean(
                    command.ResultJson,
                    "Defender",
                    "Available",
                    out var defender)
                &&
                defender)
            {
                defenderAvailable++;
            }

            if (
                TryReadNestedBoolean(
                    command.ResultJson,
                    "Firewall",
                    "Available",
                    out var firewall)
                &&
                firewall)
            {
                firewallAvailable++;
            }

            if (
                TryReadNestedBoolean(
                    command.ResultJson,
                    "BitLocker",
                    "Available",
                    out var bitLocker)
                &&
                bitLocker)
            {
                bitLockerAvailable++;
            }

            if (
                TryReadNestedBoolean(
                    command.ResultJson,
                    "Tpm",
                    "Available",
                    out var tpm)
                &&
                tpm)
            {
                tpmAvailable++;
            }

            if (
                TryReadBoolean(
                    command.ResultJson,
                    "SecureBootEnabled",
                    out var secureBoot)
                &&
                secureBoot)
            {
                secureBootEnabled++;
            }
        }

        /*
         * ========================================================
         * WINDOWS UPDATE TELEMETRY
         * ========================================================
         */

        var updateServiceRunning =
            0;

        foreach (
            var command
            in updateStatusCommands)
        {
            if (
                TryReadNestedString(
                    command.ResultJson,
                    "WindowsUpdateService",
                    "Status",
                    out var updateServiceStatus)
                &&
                string.Equals(
                    updateServiceStatus,
                    "Running",
                    StringComparison.OrdinalIgnoreCase))
            {
                updateServiceRunning++;
            }

            if (
                TryReadBoolean(
                    command.ResultJson,
                    "PendingReboot",
                    out var pendingReboot)
                &&
                pendingReboot)
            {
                pendingRebootDeviceIds.Add(
                    command.DeviceId);
            }
        }

        /*
         * ========================================================
         * REMOTE SUPPORT
         * ========================================================
         */

        var remoteSessions =
            await _dbContext.RemoteSessions
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        windowsDeviceIds.Contains(
                            x.DeviceId))
                .Select(
                    x =>
                        new
                        {
                            x.Status,
                            x.RequestedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var activeRemoteSessions =
            remoteSessions.Count(
                x =>
                    x.Status ==
                        RemoteSessionStatus.Requested
                    ||
                    x.Status ==
                        RemoteSessionStatus.Connecting
                    ||
                    x.Status ==
                        RemoteSessionStatus.Connected
                    ||
                    x.Status ==
                        RemoteSessionStatus.Disconnecting);

        return new WindowsDashboardSummaryDto
        {
            Devices =
                windowsDevices.Count,

            Online =
                windowsDevices.Count(
                    x =>
                        x.Status ==
                        DeviceStatus.Online),

            Offline =
                windowsDevices.Count(
                    x =>
                        x.Status ==
                        DeviceStatus.Offline),

            Managed =
                windowsDevices.Count(
                    x =>
                        x.IsManaged),

            CheckInsLast24Hours =
                windowsDevices.Count(
                    x =>
                        x.LastSeenAtUtc.HasValue
                        &&
                        x.LastSeenAtUtc.Value >=
                            last24Hours),

            UpdateStatusChecks =
                updateStatusChecks,

            UpdateScanRequests =
                updateScanRequests,

            PendingReboot =
                pendingRebootDeviceIds.Count,

            UpdateServiceRunning =
                updateServiceRunning,

            UpdateTelemetryDevices =
                updateStatusCommands.Count,

            SecurityTelemetryDevices =
                securityCommands.Count,

            DefenderAvailable =
                defenderAvailable,

            FirewallAvailable =
                firewallAvailable,

            BitLockerAvailable =
                bitLockerAvailable,

            TpmAvailable =
                tpmAvailable,

            SecureBootEnabled =
                secureBootEnabled,

            RemoteSessionsTotal =
                remoteSessions.Count,

            RemoteSessionsActive =
                activeRemoteSessions,

            RemoteSessionsCompleted =
                remoteSessions.Count(
                    x =>
                        x.Status ==
                        RemoteSessionStatus.Completed),

            RemoteSessionsFailed =
                remoteSessions.Count(
                    x =>
                        x.Status ==
                        RemoteSessionStatus.Failed),

            RemoteSessionsLast24Hours =
                remoteSessions.Count(
                    x =>
                        x.RequestedAtUtc >=
                            last24Hours)
        };
    }

    /*
     * ============================================================
     * ANDROID SUMMARY
     * ============================================================
     */

    private async Task<AndroidDashboardSummaryDto>
        BuildAndroidSummaryAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
    {
        var now =
            DateTime.UtcNow;

        var androidDevices =
            await _dbContext.AndroidDevices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId)
                .ToListAsync(
                    cancellationToken);

        var androidDeviceIds =
            androidDevices
                .Select(
                    x =>
                        x.DeviceId)
                .ToArray();

        var enrollments =
            await _dbContext.AndroidEnrollments
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId)
                .ToListAsync(
                    cancellationToken);

        var applicationsPresent =
            await _dbContext.DeviceApplications
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        androidDeviceIds.Contains(
                            x.DeviceId)
                        &&
                        x.IsPresent)
                .CountAsync(
                    cancellationToken);

        var securityPostures =
            await _dbContext.DeviceSecurityPostures
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        androidDeviceIds.Contains(
                            x.DeviceId))
                .ToListAsync(
                    cancellationToken);

        /*
         * ========================================================
         * MANAGEMENT MODES
         * ========================================================
         */

        var fullyManaged =
            androidDevices.Count(
                x =>
                    ContainsAny(
                        x.ManagementMode,
                        "FULLY_MANAGED",
                        "DEVICE_OWNER",
                        "FULLYMANAGED"));

        var dedicated =
            androidDevices.Count(
                x =>
                    ContainsAny(
                        x.ManagementMode,
                        "DEDICATED",
                        "KIOSK"));

        var workProfile =
            androidDevices.Count(
                x =>
                    ContainsAny(
                        x.ManagementMode,
                        "PROFILE_OWNER",
                        "WORK_PROFILE",
                        "WORKPROFILE"));

        /*
         * ========================================================
         * POLICIES
         * ========================================================
         */

        var policyApplied =
            androidDevices.Count(
                x =>
                    !string.IsNullOrWhiteSpace(
                        x.AppliedPolicyName)
                    &&
                    ContainsAny(
                        x.AppliedPolicyState,
                        "APPLIED",
                        "SUCCESS"));

        var policyPending =
            androidDevices.Count
            -
            policyApplied;

        /*
         * ========================================================
         * SECURITY
         * ========================================================
         */

        var encrypted =
            androidDevices.Count(
                x =>
                    ContainsAny(
                        x.EncryptionStatus,
                        "ENCRYPTED",
                        "ENCRYPTION_STATUS_ACTIVE"));

        var postureReported =
            androidDevices.Count(
                x =>
                    !string.IsNullOrWhiteSpace(
                        x.SecurityPosture));

        /*
         * ========================================================
         * LAST SYNC
         *
         * Explicit nullable declaration prevents CS0173.
         * ========================================================
         */

        DateTime?
            lastSynchronizationUtc =
                null;

        if (
            androidDevices.Count >
            0)
        {
            lastSynchronizationUtc =
                androidDevices.Max(
                    x =>
                        x.LastSynchronizedAtUtc);
        }

        /*
         * ========================================================
         * RESULT
         * ========================================================
         */

        return new AndroidDashboardSummaryDto
        {
            Devices =
                androidDevices.Count,

            Managed =
                androidDevices.Count(
                    x =>
                        !x.IsDeletedInGoogle),

            MissingInGoogle =
                androidDevices.Count(
                    x =>
                        x.IsDeletedInGoogle),

            FullyManaged =
                fullyManaged,

            Dedicated =
                dedicated,

            WorkProfile =
                workProfile,

            ActiveEnrollments =
                enrollments.Count(
                    x =>
                        !x.IsRevoked
                        &&
                        x.ExpiresAtUtc >
                            now),

            ExpiredEnrollments =
                enrollments.Count(
                    x =>
                        !x.IsRevoked
                        &&
                        x.ExpiresAtUtc <=
                            now),

            RevokedEnrollments =
                enrollments.Count(
                    x =>
                        x.IsRevoked),

            PolicyApplied =
                policyApplied,

            PolicyPendingOrUnknown =
                policyPending,

            ApplicationsPresent =
                applicationsPresent,

            SecurityTelemetryDevices =
                securityPostures.Count,

            RootDetected =
                securityPostures.Count(
                    x =>
                        x.RootDetected),

            AdbEnabled =
                securityPostures.Count(
                    x =>
                        x.AdbEnabled),

            DeviceSecure =
                securityPostures.Count(
                    x =>
                        x.DeviceSecure),

            Encrypted =
                encrypted,

            SecurityPostureReported =
                postureReported,

            LastSynchronizationUtc =
                lastSynchronizationUtc
        };
    }

    /*
     * ============================================================
     * JSON
     * ============================================================
     */

    private static bool TryReadBoolean(
        string? json,
        string propertyName,
        out bool value)
    {
        value =
            false;

        if (
            string.IsNullOrWhiteSpace(
                json))
        {
            return false;
        }

        try
        {
            using var document =
                JsonDocument.Parse(
                    json);

            if (
                !TryGetPropertyIgnoreCase(
                    document.RootElement,
                    propertyName,
                    out var element))
            {
                return false;
            }

            if (
                element.ValueKind ==
                JsonValueKind.True)
            {
                value =
                    true;

                return true;
            }

            if (
                element.ValueKind ==
                JsonValueKind.False)
            {
                value =
                    false;

                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadNestedBoolean(
        string? json,
        string parentProperty,
        string childProperty,
        out bool value)
    {
        value =
            false;

        if (
            string.IsNullOrWhiteSpace(
                json))
        {
            return false;
        }

        try
        {
            using var document =
                JsonDocument.Parse(
                    json);

            if (
                !TryGetPropertyIgnoreCase(
                    document.RootElement,
                    parentProperty,
                    out var parent))
            {
                return false;
            }

            if (
                !TryGetPropertyIgnoreCase(
                    parent,
                    childProperty,
                    out var child))
            {
                return false;
            }

            if (
                child.ValueKind ==
                JsonValueKind.True)
            {
                value =
                    true;

                return true;
            }

            if (
                child.ValueKind ==
                JsonValueKind.False)
            {
                value =
                    false;

                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadNestedString(
        string? json,
        string parentProperty,
        string childProperty,
        out string? value)
    {
        value =
            null;

        if (
            string.IsNullOrWhiteSpace(
                json))
        {
            return false;
        }

        try
        {
            using var document =
                JsonDocument.Parse(
                    json);

            if (
                !TryGetPropertyIgnoreCase(
                    document.RootElement,
                    parentProperty,
                    out var parent))
            {
                return false;
            }

            if (
                !TryGetPropertyIgnoreCase(
                    parent,
                    childProperty,
                    out var child))
            {
                return false;
            }

            if (
                child.ValueKind !=
                JsonValueKind.String)
            {
                return false;
            }

            value =
                child.GetString();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        value =
            default;

        if (
            element.ValueKind !=
            JsonValueKind.Object)
        {
            return false;
        }

        foreach (
            var property
            in element.EnumerateObject())
        {
            if (
                string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                value =
                    property.Value;

                return true;
            }
        }

        return false;
    }

    /*
     * ============================================================
     * TEXT
     * ============================================================
     */

    private static bool ContainsAny(
        string? value,
        params string[] candidates)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        return candidates.Any(
            candidate =>
                value.Contains(
                    candidate,
                    StringComparison.OrdinalIgnoreCase));
    }
}