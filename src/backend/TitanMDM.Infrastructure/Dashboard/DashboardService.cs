using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Dashboard;
using TitanMDM.Application.Dashboard.DTOs;
using TitanMDM.Application.Dashboard.Interfaces;

using TitanMDM.Domain.Entities;
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
            dbContext
            ??
            throw new ArgumentNullException(
                nameof(
                    dbContext));
    }

    // ============================================================
    // SUMMARY
    // ============================================================

    public async Task<DashboardSummaryDto>
        GetSummaryAsync(
            Guid organizationId,
            DashboardWorkspace workspace,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default)
    {
        if (
            organizationId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(
                    organizationId));
        }

        /*
         * ========================================================
         * VISIBLE DEVICES
         * ========================================================
         */

        var devices =
            _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    device =>
                        device.OrganizationId ==
                            organizationId
                        &&
                        !device.IsDeleted);

        devices =
            ApplyScope(
                devices,
                accessibleSiteIds);

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

        /*
         * ========================================================
         * DEVICE COUNTERS
         * ========================================================
         */

        var total =
            await devices
                .CountAsync(
                    cancellationToken);

        var online =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Online,
                    cancellationToken);

        var offline =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Offline,
                    cancellationToken);

        var pending =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Pending,
                    cancellationToken);

        var enrolling =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Enrolling,
                    cancellationToken);

        var quarantined =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Quarantined,
                    cancellationToken);

        var retired =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Retired,
                    cancellationToken);

        var managed =
            await devices
                .CountAsync(
                    device =>
                        device.IsManaged,
                    cancellationToken);

        /*
         * ========================================================
         * PLATFORM COUNTERS
         * ========================================================
         */

        var windows =
            await devices
                .CountAsync(
                    device =>
                        device.Platform ==
                            DevicePlatform.Windows,
                    cancellationToken);

        var android =
            await devices
                .CountAsync(
                    device =>
                        device.Platform ==
                            DevicePlatform.Android,
                    cancellationToken);

        var unknownPlatform =
            await devices
                .CountAsync(
                    device =>
                        device.Platform ==
                            DevicePlatform.Unknown,
                    cancellationToken);

        /*
         * ========================================================
         * COMPLIANCE
         * ========================================================
         */

        var compliant =
            await devices
                .CountAsync(
                    device =>
                        device.ComplianceStatus ==
                            ComplianceStatus.Compliant,
                    cancellationToken);

        var nonCompliant =
            await devices
                .CountAsync(
                    device =>
                        device.ComplianceStatus ==
                            ComplianceStatus.NonCompliant,
                    cancellationToken);

        var evaluating =
            await devices
                .CountAsync(
                    device =>
                        device.ComplianceStatus ==
                            ComplianceStatus.Evaluating,
                    cancellationToken);

        var complianceQuarantined =
            await devices
                .CountAsync(
                    device =>
                        device.ComplianceStatus ==
                            ComplianceStatus.Quarantined,
                    cancellationToken);

        var unknownCompliance =
            await devices
                .CountAsync(
                    device =>
                        device.ComplianceStatus ==
                            ComplianceStatus.Unknown,
                    cancellationToken);

        var evaluatedDevices =
            compliant
            +
            nonCompliant;

        decimal?
            compliancePercentage =
                null;

        if (
            evaluatedDevices >
            0)
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
         *
         * IMPORTANTE:
         *
         * Incluso para workspace Global, los comandos se filtran
         * por los DeviceIds visibles.
         *
         * Así un usuario Site-scoped nunca obtiene contadores
         * corporativos indirectamente.
         * ========================================================
         */

        var visibleDeviceIds =
            devices
                .Select(
                    device =>
                        device.Id);

        var commands =
            _dbContext
                .DeviceCommands
                .AsNoTracking()
                .Where(
                    command =>
                        command.OrganizationId ==
                            organizationId
                        &&
                        visibleDeviceIds.Contains(
                            command.DeviceId));

        var totalCommands =
            await commands
                .CountAsync(
                    cancellationToken);

        var pendingCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Pending,
                    cancellationToken);

        var queuedCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Queued,
                    cancellationToken);

        var dispatchingCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Dispatching,
                    cancellationToken);

        var sentCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Sent,
                    cancellationToken);

        var deliveredCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Delivered,
                    cancellationToken);

        var executingCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Executing,
                    cancellationToken);

        var successfulCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Success,
                    cancellationToken);

        var failedCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Failed,
                    cancellationToken);

        var timeoutCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Timeout,
                    cancellationToken);

        var cancelledCommands =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
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
                    accessibleSiteIds,
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
                    accessibleSiteIds,
                    cancellationToken);
        }

        /*
         * ========================================================
         * SYSTEM
         * ========================================================
         */

        var databaseConnected =
            await _dbContext
                .Database
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

    // ============================================================
    // WINDOWS SUMMARY
    // ============================================================

    private async Task<WindowsDashboardSummaryDto>
        BuildWindowsSummaryAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken)
    {
        var now =
            DateTime.UtcNow;

        var last24Hours =
            now.AddHours(
                -24);

        /*
         * ========================================================
         * VISIBLE WINDOWS DEVICES
         * ========================================================
         */

        var windowsQuery =
            _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    device =>
                        device.OrganizationId ==
                            organizationId
                        &&
                        !device.IsDeleted
                        &&
                        device.Platform ==
                            DevicePlatform.Windows);

        windowsQuery =
            ApplyScope(
                windowsQuery,
                accessibleSiteIds);

        var windowsDevices =
            await windowsQuery
                .Select(
                    device =>
                        new
                        {
                            device.Id,
                            device.Status,
                            device.IsManaged,
                            device.LastSeenAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var windowsDeviceIds =
            windowsDevices
                .Select(
                    device =>
                        device.Id)
                .ToArray();

        /*
         * ========================================================
         * WINDOWS COMMANDS
         * ========================================================
         */

        var windowsCommands =
            await _dbContext
                .DeviceCommands
                .AsNoTracking()
                .Where(
                    command =>
                        command.OrganizationId ==
                            organizationId
                        &&
                        windowsDeviceIds.Contains(
                            command.DeviceId)
                        &&
                        (
                            command.CommandType ==
                                "SECURITY_STATUS"
                            ||
                            command.CommandType ==
                                "WINDOWS_UPDATE_STATUS"
                            ||
                            command.CommandType ==
                                "WINDOWS_UPDATE_SCAN"
                        ))
                .Select(
                    command =>
                        new
                        {
                            command.DeviceId,
                            command.CommandType,
                            command.Status,
                            command.ResultJson,
                            command.CreatedAtUtc,
                            command.CompletedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        /*
         * Latest security snapshot per device.
         */

        var securityCommands =
            windowsCommands
                .Where(
                    command =>
                        command.CommandType ==
                            "SECURITY_STATUS"
                        &&
                        command.Status ==
                            DeviceCommandStatus.Success
                        &&
                        !string.IsNullOrWhiteSpace(
                            command.ResultJson))
                .GroupBy(
                    command =>
                        command.DeviceId)
                .Select(
                    group =>
                        group
                            .OrderByDescending(
                                command =>
                                    command.CompletedAtUtc
                                    ??
                                    command.CreatedAtUtc)
                            .First())
                .ToList();

        /*
         * Latest Windows Update snapshot.
         */

        var updateStatusCommands =
            windowsCommands
                .Where(
                    command =>
                        command.CommandType ==
                            "WINDOWS_UPDATE_STATUS"
                        &&
                        command.Status ==
                            DeviceCommandStatus.Success
                        &&
                        !string.IsNullOrWhiteSpace(
                            command.ResultJson))
                .GroupBy(
                    command =>
                        command.DeviceId)
                .Select(
                    group =>
                        group
                            .OrderByDescending(
                                command =>
                                    command.CompletedAtUtc
                                    ??
                                    command.CreatedAtUtc)
                            .First())
                .ToList();

        var updateStatusChecks =
            windowsCommands.Count(
                command =>
                    command.CommandType ==
                        "WINDOWS_UPDATE_STATUS");

        var updateScanRequests =
            windowsCommands.Count(
                command =>
                    command.CommandType ==
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
                pendingRebootDeviceIds
                    .Add(
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
         * WINDOWS UPDATE
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
                pendingRebootDeviceIds
                    .Add(
                        command.DeviceId);
            }
        }

        /*
         * ========================================================
         * REMOTE SUPPORT
         * ========================================================
         */

        var remoteSessions =
            await _dbContext
                .RemoteSessions
                .AsNoTracking()
                .Where(
                    session =>
                        session.OrganizationId ==
                            organizationId
                        &&
                        windowsDeviceIds.Contains(
                            session.DeviceId))
                .Select(
                    session =>
                        new
                        {
                            session.Status,
                            session.RequestedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var activeRemoteSessions =
            remoteSessions.Count(
                session =>
                    session.Status ==
                        RemoteSessionStatus.Requested
                    ||
                    session.Status ==
                        RemoteSessionStatus.Connecting
                    ||
                    session.Status ==
                        RemoteSessionStatus.Connected
                    ||
                    session.Status ==
                        RemoteSessionStatus.Disconnecting);

        return new WindowsDashboardSummaryDto
        {
            Devices =
                windowsDevices.Count,

            Online =
                windowsDevices.Count(
                    device =>
                        device.Status ==
                            DeviceStatus.Online),

            Offline =
                windowsDevices.Count(
                    device =>
                        device.Status ==
                            DeviceStatus.Offline),

            Managed =
                windowsDevices.Count(
                    device =>
                        device.IsManaged),

            CheckInsLast24Hours =
                windowsDevices.Count(
                    device =>
                        device.LastSeenAtUtc.HasValue
                        &&
                        device.LastSeenAtUtc.Value >=
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
                    session =>
                        session.Status ==
                            RemoteSessionStatus.Completed),

            RemoteSessionsFailed =
                remoteSessions.Count(
                    session =>
                        session.Status ==
                            RemoteSessionStatus.Failed),

            RemoteSessionsLast24Hours =
                remoteSessions.Count(
                    session =>
                        session.RequestedAtUtc >=
                            last24Hours)
        };
    }

    // ============================================================
    // ANDROID SUMMARY
    // ============================================================

    private async Task<AndroidDashboardSummaryDto>
        BuildAndroidSummaryAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken)
    {
        var now =
            DateTime.UtcNow;

        /*
         * ========================================================
         * VISIBLE ANDROID DEVICE IDS
         * ========================================================
         *
         * AndroidDevice no posee SiteId.
         *
         * El Scope se determina mediante la entidad Device,
         * que es la autoridad transversal para Windows/Android.
         * ========================================================
         */

        var visibleAndroidDevices =
            _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    device =>
                        device.OrganizationId ==
                            organizationId
                        &&
                        !device.IsDeleted
                        &&
                        device.Platform ==
                            DevicePlatform.Android);

        visibleAndroidDevices =
            ApplyScope(
                visibleAndroidDevices,
                accessibleSiteIds);

        var visibleAndroidDeviceIds =
            await visibleAndroidDevices
                .Select(
                    device =>
                        device.Id)
                .ToArrayAsync(
                    cancellationToken);

        var androidDevices =
            await _dbContext
                .AndroidDevices
                .AsNoTracking()
                .Where(
                    android =>
                        android.OrganizationId ==
                            organizationId
                        &&
                        visibleAndroidDeviceIds.Contains(
                            android.DeviceId))
                .ToListAsync(
                    cancellationToken);

        var androidDeviceIds =
            androidDevices
                .Select(
                    device =>
                        device.DeviceId)
                .ToArray();

        /*
         * ========================================================
         * ENROLLMENTS
         * ========================================================
         *
         * AndroidEnrollment actualmente no posee SiteId.
         *
         * Por seguridad:
         *
         * Organization scope:
         * puede ver métricas globales de enrollment.
         *
         * Site scope:
         * NO mostramos métricas globales que revelarían
         * información de otras localidades.
         *
         * En RBAC-G/Android agregaremos Scope explícito
         * al enrollment.
         * ========================================================
         */

        var enrollments =
            accessibleSiteIds is null
                ? await _dbContext
                    .AndroidEnrollments
                    .AsNoTracking()
                    .Where(
                        enrollment =>
                            enrollment.OrganizationId ==
                                organizationId)
                    .ToListAsync(
                        cancellationToken)
                : [];

        var applicationsPresent =
            await _dbContext
                .DeviceApplications
                .AsNoTracking()
                .Where(
                    application =>
                        application.OrganizationId ==
                            organizationId
                        &&
                        androidDeviceIds.Contains(
                            application.DeviceId)
                        &&
                        application.IsPresent)
                .CountAsync(
                    cancellationToken);

        var securityPostures =
            await _dbContext
                .DeviceSecurityPostures
                .AsNoTracking()
                .Where(
                    posture =>
                        posture.OrganizationId ==
                            organizationId
                        &&
                        androidDeviceIds.Contains(
                            posture.DeviceId))
                .ToListAsync(
                    cancellationToken);

        /*
         * ========================================================
         * MANAGEMENT MODES
         * ========================================================
         */

        var fullyManaged =
            androidDevices.Count(
                device =>
                    ContainsAny(
                        device.ManagementMode,
                        "FULLY_MANAGED",
                        "DEVICE_OWNER",
                        "FULLYMANAGED"));

        var dedicated =
            androidDevices.Count(
                device =>
                    ContainsAny(
                        device.ManagementMode,
                        "DEDICATED",
                        "KIOSK"));

        var workProfile =
            androidDevices.Count(
                device =>
                    ContainsAny(
                        device.ManagementMode,
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
                device =>
                    !string.IsNullOrWhiteSpace(
                        device.AppliedPolicyName)
                    &&
                    ContainsAny(
                        device.AppliedPolicyState,
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
                device =>
                    ContainsAny(
                        device.EncryptionStatus,
                        "ENCRYPTED",
                        "ENCRYPTION_STATUS_ACTIVE"));

        var postureReported =
            androidDevices.Count(
                device =>
                    !string.IsNullOrWhiteSpace(
                        device.SecurityPosture));

        /*
         * ========================================================
         * LAST SYNC
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
                    device =>
                        device.LastSynchronizedAtUtc);
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
                    device =>
                        !device.IsDeletedInGoogle),

            MissingInGoogle =
                androidDevices.Count(
                    device =>
                        device.IsDeletedInGoogle),

            FullyManaged =
                fullyManaged,

            Dedicated =
                dedicated,

            WorkProfile =
                workProfile,

            ActiveEnrollments =
                enrollments.Count(
                    enrollment =>
                        !enrollment.IsRevoked
                        &&
                        enrollment.ExpiresAtUtc >
                            now),

            ExpiredEnrollments =
                enrollments.Count(
                    enrollment =>
                        !enrollment.IsRevoked
                        &&
                        enrollment.ExpiresAtUtc <=
                            now),

            RevokedEnrollments =
                enrollments.Count(
                    enrollment =>
                        enrollment.IsRevoked),

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
                    posture =>
                        posture.RootDetected),

            AdbEnabled =
                securityPostures.Count(
                    posture =>
                        posture.AdbEnabled),

            DeviceSecure =
                securityPostures.Count(
                    posture =>
                        posture.DeviceSecure),

            Encrypted =
                encrypted,

            SecurityPostureReported =
                postureReported,

            LastSynchronizationUtc =
                lastSynchronizationUtc
        };
    }

    // ============================================================
    // SCOPE
    // ============================================================

    private static IQueryable<Device>
        ApplyScope(
            IQueryable<Device> query,
            IReadOnlyCollection<Guid>?
                accessibleSiteIds)
    {
        /*
         * null significa Organization scope.
         */

        if (
            accessibleSiteIds is null)
        {
            return query;
        }

        var siteIds =
            accessibleSiteIds
                .Where(
                    id =>
                        id != Guid.Empty)
                .Distinct()
                .ToArray();

        /*
         * Usuario autenticado con permiso funcional,
         * pero sin alcance:
         *
         * consulta válida sin resultados.
         */

        if (
            siteIds.Length ==
            0)
        {
            return query.Where(
                _ =>
                    false);
        }

        return query.Where(
            device =>
                device.SiteId.HasValue
                &&
                siteIds.Contains(
                    device.SiteId.Value));
    }

    // ============================================================
    // JSON
    // ============================================================

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

    // ============================================================
    // TEXT
    // ============================================================

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