namespace TitanMDM.Application.Dashboard.DTOs;

public sealed class DashboardSummaryDto
{
    public DeviceSummaryDto Devices { get; init; } =
        new();

    public PlatformSummaryDto Platforms { get; init; } =
        new();

    public ComplianceSummaryDto Compliance { get; init; } =
        new();

    public CommandSummaryDto Commands { get; init; } =
        new();

    public SystemStatusDto System { get; init; } =
        new();

    public WindowsDashboardSummaryDto? Windows
    {
        get;
        init;
    }

    public AndroidDashboardSummaryDto? Android
    {
        get;
        init;
    }

    public DateTime GeneratedAtUtc { get; init; }
}

/*
 * ================================================================
 * DEVICES
 * ================================================================
 */

public sealed class DeviceSummaryDto
{
    public int Total { get; init; }

    public int Online { get; init; }

    public int Offline { get; init; }

    public int Pending { get; init; }

    public int Enrolling { get; init; }

    public int Quarantined { get; init; }

    public int Retired { get; init; }

    public int Managed { get; init; }
}

/*
 * ================================================================
 * PLATFORMS
 * ================================================================
 */

public sealed class PlatformSummaryDto
{
    public int Windows { get; init; }

    public int Android { get; init; }

    public int Unknown { get; init; }
}

/*
 * ================================================================
 * COMPLIANCE
 * ================================================================
 */

public sealed class ComplianceSummaryDto
{
    public int Compliant { get; init; }

    public int NonCompliant { get; init; }

    public int Evaluating { get; init; }

    public int Quarantined { get; init; }

    public int Unknown { get; init; }

    public decimal? CompliancePercentage { get; init; }
}

/*
 * ================================================================
 * COMMAND ENGINE
 * ================================================================
 */

public sealed class CommandSummaryDto
{
    public int Total { get; init; }

    public int Pending { get; init; }

    public int Queued { get; init; }

    public int Dispatching { get; init; }

    public int Sent { get; init; }

    public int Delivered { get; init; }

    public int Executing { get; init; }

    public int Success { get; init; }

    public int Failed { get; init; }

    public int Timeout { get; init; }

    public int Cancelled { get; init; }

    public int Active { get; init; }

    public int Problems { get; init; }
}

/*
 * ================================================================
 * SYSTEM
 * ================================================================
 */

public sealed class SystemStatusDto
{
    public string Api { get; init; } =
        "Operational";

    public string Database { get; init; } =
        "Connected";
}

/*
 * ================================================================
 * WINDOWS ADVANCED
 * ================================================================
 */

public sealed class WindowsDashboardSummaryDto
{
    public int Devices { get; init; }

    public int Online { get; init; }

    public int Offline { get; init; }

    public int Managed { get; init; }

    public int CheckInsLast24Hours { get; init; }

    /*
     * Windows Update
     */

    public int UpdateStatusChecks { get; init; }

    public int UpdateScanRequests { get; init; }

    public int PendingReboot { get; init; }

    public int UpdateServiceRunning { get; init; }

    public int UpdateTelemetryDevices { get; init; }

    /*
     * Security
     */

    public int SecurityTelemetryDevices { get; init; }

    public int DefenderAvailable { get; init; }

    public int FirewallAvailable { get; init; }

    public int BitLockerAvailable { get; init; }

    public int TpmAvailable { get; init; }

    public int SecureBootEnabled { get; init; }

    /*
     * Remote Support
     */

    public int RemoteSessionsTotal { get; init; }

    public int RemoteSessionsActive { get; init; }

    public int RemoteSessionsCompleted { get; init; }

    public int RemoteSessionsFailed { get; init; }

    public int RemoteSessionsLast24Hours { get; init; }
}

/*
 * ================================================================
 * ANDROID ADVANCED
 * ================================================================
 */

public sealed class AndroidDashboardSummaryDto
{
    public int Devices { get; init; }

    public int Managed { get; init; }

    public int MissingInGoogle { get; init; }

    /*
     * Management Mode
     */

    public int FullyManaged { get; init; }

    public int Dedicated { get; init; }

    public int WorkProfile { get; init; }

    /*
     * Enrollment
     */

    public int ActiveEnrollments { get; init; }

    public int ExpiredEnrollments { get; init; }

    public int RevokedEnrollments { get; init; }

    /*
     * Policies
     */

    public int PolicyApplied { get; init; }

    public int PolicyPendingOrUnknown { get; init; }

    /*
     * Applications
     */

    public int ApplicationsPresent { get; init; }

    /*
     * Security
     */

    public int SecurityTelemetryDevices { get; init; }

    public int RootDetected { get; init; }

    public int AdbEnabled { get; init; }

    public int DeviceSecure { get; init; }

    public int Encrypted { get; init; }

    public int SecurityPostureReported { get; init; }

    public DateTime? LastSynchronizationUtc { get; init; }
}