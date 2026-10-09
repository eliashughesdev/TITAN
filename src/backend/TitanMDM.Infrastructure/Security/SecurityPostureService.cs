using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Automation;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Security;

public sealed class SecurityPostureService
    : ISecurityPostureService
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IAutomationEventDispatcher
        _automation;

    private static readonly JsonSerializerOptions
        JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

    public SecurityPostureService(
        TitanMdmDbContext dbContext,
        IAutomationEventDispatcher automation)
    {
        _dbContext =
            dbContext;

        _automation =
            automation;
    }

    public async Task ProcessSecurityStatusAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        var device =
            await GetDeviceAsync(
                deviceId,
                cancellationToken);

        var posture =
            await GetOrCreateAsync(
                device,
                cancellationToken);

        if (
            device.Platform ==
            DevicePlatform.Windows)
        {
            await ProcessWindowsSecurityAsync(
                device,
                posture,
                resultJson,
                cancellationToken);

            return;
        }

        await ProcessAndroidSecurityAsync(
            device,
            posture,
            resultJson,
            cancellationToken);
    }

    public async Task ProcessComplianceAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        var device =
            await GetDeviceAsync(
                deviceId,
                cancellationToken);

        var posture =
            await GetOrCreateAsync(
                device,
                cancellationToken);

        if (
            device.Platform ==
            DevicePlatform.Windows)
        {
            await ProcessWindowsComplianceAsync(
                device,
                posture,
                resultJson,
                cancellationToken);

            return;
        }

        await ProcessAndroidComplianceAsync(
            device,
            posture,
            resultJson,
            cancellationToken);
    }

    public async Task<SecurityDashboardDto>
    GetDashboardAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid>? accessibleSiteIds,
        CancellationToken cancellationToken = default)
{
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

    if (accessibleSiteIds is not null)
    {
        var siteIds =
            accessibleSiteIds
                .Where(
                    id =>
                        id != Guid.Empty)
                .Distinct()
                .ToArray();

        devices =
            siteIds.Length == 0
                ? devices.Where(
                    _ =>
                        false)
                : devices.Where(
                    device =>
                        device.SiteId.HasValue
                        &&
                        siteIds.Contains(
                            device.SiteId.Value));
    }

    var visibleDeviceIds =
        devices.Select(
            device =>
                device.Id);

    var totalDevices =
        await devices.CountAsync(
            cancellationToken);

    var postures =
        await _dbContext
            .DeviceSecurityPostures
            .AsNoTracking()
            .Where(
                posture =>
                    posture.OrganizationId ==
                        organizationId
                    &&
                    visibleDeviceIds.Contains(
                        posture.DeviceId))
            .ToListAsync(
                cancellationToken);

    var evaluated =
        postures.Count;

    var compliant =
        postures.Count(
            posture =>
                posture.ComplianceStatus.Equals(
                    "Compliant",
                    StringComparison.OrdinalIgnoreCase));

    var nonCompliant =
        postures.Count(
            posture =>
                posture.ComplianceStatus.Equals(
                    "NonCompliant",
                    StringComparison.OrdinalIgnoreCase));

    var average =
        evaluated == 0
            ? 0
            : Math.Round(
                postures.Average(
                    posture =>
                        posture.ComplianceScore),
                1);

    return new SecurityDashboardDto(
        totalDevices,
        evaluated,
        compliant,
        nonCompliant,

        postures.Count(
            posture =>
                posture.RootDetected),

        postures.Count(
            posture =>
                posture.AdbEnabled),

        postures.Count(
            posture =>
                posture.DeveloperOptionsEnabled),

        postures.Count(
            posture =>
                !posture.DeviceSecure),

        postures.Count(
            posture =>
                posture.RiskLevel.Equals(
                    "Critical",
                    StringComparison.OrdinalIgnoreCase)),

        average);
}

   public async Task<
    IReadOnlyCollection<DeviceSecurityDto>>
    GetDevicesAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid>? accessibleSiteIds,
        CancellationToken cancellationToken = default)
{
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

    if (accessibleSiteIds is not null)
    {
        var siteIds =
            accessibleSiteIds
                .Where(
                    id =>
                        id != Guid.Empty)
                .Distinct()
                .ToArray();

        query =
            siteIds.Length == 0
                ? query.Where(
                    _ =>
                        false)
                : query.Where(
                    device =>
                        device.SiteId.HasValue
                        &&
                        siteIds.Contains(
                            device.SiteId.Value));
    }

    var devices =
        await query
            .OrderBy(
                device =>
                    device.DeviceName)
            .ToListAsync(
                cancellationToken);

    var deviceIds =
        devices
            .Select(
                device =>
                    device.Id)
            .ToArray();

    var postures =
        await _dbContext
            .DeviceSecurityPostures
            .AsNoTracking()
            .Where(
                posture =>
                    posture.OrganizationId ==
                        organizationId
                    &&
                    deviceIds.Contains(
                        posture.DeviceId))
            .ToDictionaryAsync(
                posture =>
                    posture.DeviceId,
                cancellationToken);

    var result =
        new List<DeviceSecurityDto>(
            devices.Count);

    foreach (
        var device
        in devices)
    {
        if (
            postures.TryGetValue(
                device.Id,
                out var posture))
        {
            result.Add(
                new DeviceSecurityDto(
                    device.Id,
                    device.DeviceName,
                    device.Platform.ToString(),
                    device.Status.ToString(),

                    posture.ComplianceStatus,
                    posture.ComplianceScore,
                    posture.RiskLevel,

                    posture.DeviceSecure,
                    posture.EncryptionStatus,

                    posture.AdbEnabled,
                    posture.DeveloperOptionsEnabled,
                    posture.RootDetected,
                    posture.EmulatorDetected,

                    posture.BootloaderLocked,
                    posture.SelinuxEnforced,

                    posture.AgentInstalled,
                    posture.AgentVersionName,

                    posture.SecurityPatchLevel,

                    posture.TotalChecks,
                    posture.PassedChecks,
                    posture.FailedChecks,

                    posture.FindingsJson,

                    posture.LastSecurityScanAtUtc,
                    posture.LastComplianceCheckAtUtc));

            continue;
        }

        result.Add(
            new DeviceSecurityDto(
                device.Id,
                device.DeviceName,
                device.Platform.ToString(),
                device.Status.ToString(),

                device.ComplianceStatus.ToString(),

                0,
                "Unknown",

                false,
                "Unknown",

                false,
                false,
                false,
                false,

                null,
                null,

                !string.IsNullOrWhiteSpace(
                    device.AgentVersion),

                device.AgentVersion
                ?? string.Empty,

                null,

                0,
                0,
                0,

                "[]",

                null,
                null));
    }

    return result;
}

    // =========================================================
    // WINDOWS
    // =========================================================

    private async Task ProcessWindowsSecurityAsync(
        Device device,
        DeviceSecurityPosture posture,
        string resultJson,
        CancellationToken cancellationToken)
    {
        var payload =
            JsonSerializer.Deserialize<
                WindowsSecurityPayload>(
                    resultJson,
                    JsonOptions)
            ??
            throw new InvalidOperationException(
                "SECURITY_STATUS Windows contiene JSON inválido.");

        ApplyWindowsSecurity(
            device,
            posture,
            payload);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        var securityRisk =
            !payload.Defender.Available
            ||
            !payload.Firewall.Available
            ||
            !payload.UacEnabled
            ||
            payload.SecureBootEnabled == false;

        if (securityRisk)
        {
            await _automation
                .DispatchAsync(
                    device.OrganizationId,
                    device.Id,
                    "SecurityRisk",
                    new
                    {
                        platform =
                            "Windows",

                        deviceName =
                            device.DeviceName,

                        defenderAvailable =
                            payload.Defender.Available,

                        firewallAvailable =
                            payload.Firewall.Available,

                        uacEnabled =
                            payload.UacEnabled,

                        secureBootEnabled =
                            payload.SecureBootEnabled,

                        remoteDesktopEnabled =
                            payload.RemoteDesktopEnabled,

                        pendingReboot =
                            payload.PendingReboot
                    },
                    cancellationToken:
                        cancellationToken);
        }
    }

    private async Task ProcessWindowsComplianceAsync(
        Device device,
        DeviceSecurityPosture posture,
        string resultJson,
        CancellationToken cancellationToken)
    {
        var payload =
            JsonSerializer.Deserialize<
                WindowsCompliancePayload>(
                    resultJson,
                    JsonOptions)
            ??
            throw new InvalidOperationException(
                "COMPLIANCE_CHECK Windows contiene JSON inválido.");

        if (payload.Security is not null)
        {
            ApplyWindowsSecurity(
                device,
                posture,
                payload.Security);
        }

        var findings =
            payload.Checks
                .Select(
                    check =>
                        new ComplianceFindingDto
                        {
                            Code =
                                check.Code,

                            Title =
                                check.Name,

                            Description =
                                check.Message,

                            Category =
                                "Windows",

                            Severity =
                                check.Passed
                                    ? "Info"
                                    : DetermineSeverity(
                                        check.Code),

                            Compliant =
                                check.Passed
                        })
                .ToArray();

        posture.UpdateCompliance(
            payload.Score,
            payload.RiskLevel,
            payload.Status,
            payload.TotalChecks,
            payload.PassedChecks,
            payload.FailedChecks,
            JsonSerializer.Serialize(
                findings));

        var compliant =
            payload.Status.Equals(
                "Compliant",
                StringComparison.OrdinalIgnoreCase);

        device.SetCompliance(
            compliant
                ? ComplianceStatus.Compliant
                : ComplianceStatus.NonCompliant);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        if (!compliant)
        {
            await _automation
                .DispatchAsync(
                    device.OrganizationId,
                    device.Id,
                    "DeviceNonCompliant",
                    new
                    {
                        platform =
                            "Windows",

                        deviceName =
                            device.DeviceName,

                        status =
                            payload.Status,

                        score =
                            payload.Score,

                        riskLevel =
                            payload.RiskLevel,

                        totalChecks =
                            payload.TotalChecks,

                        passedChecks =
                            payload.PassedChecks,

                        failedChecks =
                            payload.FailedChecks,

                        findings =
                            findings
                    },
                    cancellationToken:
                        cancellationToken);
        }
    }

    private static void ApplyWindowsSecurity(
        Device device,
        DeviceSecurityPosture posture,
        WindowsSecurityPayload payload)
    {
        var secure =
            payload.Defender.Available
            &&
            payload.Firewall.Available
            &&
            payload.UacEnabled;

        var encryption =
            payload.BitLocker.Available
                ? "Evaluated"
                : "Unknown";

        posture.UpdateSecurity(
            androidVersion:
                string.Empty,

            apiLevel:
                0,

            securityPatchLevel:
                null,

            deviceSecure:
                secure,

            encryptionStatus:
                encryption,

            adbEnabled:
                false,

            developerOptionsEnabled:
                false,

            rootDetected:
                false,

            rootSignalsJson:
                "[]",

            emulatorDetected:
                false,

            verifiedBootState:
                payload.SecureBootEnabled
                    ?.ToString(),

            bootloaderLocked:
                payload.SecureBootEnabled,

            selinuxEnforced:
                null,

            agentInstalled:
                true,

            agentVersionName:
                device.AgentVersion
                ??
                string.Empty,

            agentVersionCode:
                0,

            unknownSourcesAllowed:
                null);
    }

    // =========================================================
    // ANDROID
    // =========================================================

    private async Task ProcessAndroidSecurityAsync(
        Device device,
        DeviceSecurityPosture posture,
        string resultJson,
        CancellationToken cancellationToken)
    {
        var payload =
            JsonSerializer.Deserialize<
                AndroidSecurityPayload>(
                    resultJson,
                    JsonOptions)
            ??
            throw new InvalidOperationException(
                "SECURITY_STATUS Android contiene JSON inválido.");

        ApplyAndroidSecurity(
            posture,
            payload);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        var securityRisk =
            payload.RootDetected
            ||
            payload.AdbEnabled
            ||
            payload.DeveloperOptionsEnabled
            ||
            !payload.DeviceSecure
            ||
            payload.BootloaderLocked == false
            ||
            payload.SelinuxEnforced == false
            ||
            payload.UnknownSourcesAllowed == true;

        if (securityRisk)
        {
            await _automation
                .DispatchAsync(
                    device.OrganizationId,
                    device.Id,
                    "SecurityRisk",
                    new
                    {
                        platform =
                            "Android",

                        deviceName =
                            device.DeviceName,

                        rootDetected =
                            payload.RootDetected,

                        adbEnabled =
                            payload.AdbEnabled,

                        developerOptionsEnabled =
                            payload.DeveloperOptionsEnabled,

                        deviceSecure =
                            payload.DeviceSecure
                    },
                    cancellationToken:
                        cancellationToken);
        }
    }

    private async Task ProcessAndroidComplianceAsync(
        Device device,
        DeviceSecurityPosture posture,
        string resultJson,
        CancellationToken cancellationToken)
    {
        var payload =
            JsonSerializer.Deserialize<
                AndroidCompliancePayload>(
                    resultJson,
                    JsonOptions)
            ??
            throw new InvalidOperationException(
                "COMPLIANCE_CHECK Android contiene JSON inválido.");

        if (payload.Posture is not null)
        {
            ApplyAndroidSecurity(
                posture,
                payload.Posture);
        }

        var findingsJson =
            JsonSerializer.Serialize(
                payload.Findings
                ??
                []);

        posture.UpdateCompliance(
            payload.Score,
            payload.RiskLevel,
            payload.Status,
            payload.TotalChecks,
            payload.PassedChecks,
            payload.FailedChecks,
            findingsJson);

        var compliant =
            payload.Status.Equals(
                "Compliant",
                StringComparison.OrdinalIgnoreCase);

        device.SetCompliance(
            compliant
                ? ComplianceStatus.Compliant
                : ComplianceStatus.NonCompliant);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        if (!compliant)
        {
            await _automation
                .DispatchAsync(
                    device.OrganizationId,
                    device.Id,
                    "DeviceNonCompliant",
                    new
                    {
                        platform =
                            "Android",

                        deviceName =
                            device.DeviceName,

                        status =
                            payload.Status,

                        score =
                            payload.Score,

                        riskLevel =
                            payload.RiskLevel
                    },
                    cancellationToken:
                        cancellationToken);
        }
    }

    private static void ApplyAndroidSecurity(
        DeviceSecurityPosture posture,
        AndroidSecurityPayload payload)
    {
        posture.UpdateSecurity(
            payload.AndroidVersion,
            payload.ApiLevel,
            payload.SecurityPatchLevel,
            payload.DeviceSecure,
            payload.EncryptionStatus,
            payload.AdbEnabled,
            payload.DeveloperOptionsEnabled,
            payload.RootDetected,
            JsonSerializer.Serialize(
                payload.RootSignals
                ??
                []),
            payload.EmulatorDetected,
            payload.VerifiedBootState,
            payload.BootloaderLocked,
            payload.SelinuxEnforced,
            payload.AgentInstalled,
            payload.AgentVersionName,
            payload.AgentVersionCode,
            payload.UnknownSourcesAllowed);
    }

    // =========================================================
    // COMMON
    // =========================================================

    private async Task<Device> GetDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext
            .Devices
            .SingleOrDefaultAsync(
                x =>
                    x.Id ==
                        deviceId
                    &&
                    !x.IsDeleted,
                cancellationToken)
            ??
            throw new InvalidOperationException(
                "El dispositivo no existe.");
    }

    private async Task<DeviceSecurityPosture>
        GetOrCreateAsync(
            Device device,
            CancellationToken cancellationToken)
    {
        var posture =
            await _dbContext
                .DeviceSecurityPostures
                .SingleOrDefaultAsync(
                    x =>
                        x.DeviceId ==
                        device.Id,
                    cancellationToken);

        if (posture is not null)
        {
            return posture;
        }

        posture =
            new DeviceSecurityPosture(
                device.OrganizationId,
                device.Id);

        _dbContext
            .DeviceSecurityPostures
            .Add(
                posture);

        return posture;
    }

    private static string DetermineSeverity(
        string code)
    {
        return code.ToUpperInvariant()
            switch
        {
            "DEFENDER_AVAILABLE" =>
                "Critical",

            "FIREWALL_AVAILABLE" =>
                "High",

            "UAC_ENABLED" =>
                "High",

            "SECURE_BOOT" =>
                "High",

            "BITLOCKER_STATUS" =>
                "High",

            "TPM_STATUS" =>
                "Medium",

            "NO_PENDING_REBOOT" =>
                "Medium",

            _ =>
                "Medium"
        };
    }

    // =========================================================
    // WINDOWS DTO
    // =========================================================

    private sealed class WindowsSecurityPayload
    {
        public AvailabilityPayload Defender
        {
            get;
            set;
        } = new();

        public AvailabilityPayload Firewall
        {
            get;
            set;
        } = new();

        public AvailabilityPayload BitLocker
        {
            get;
            set;
        } = new();

        public AvailabilityPayload Tpm
        {
            get;
            set;
        } = new();

        public bool? SecureBootEnabled
        {
            get;
            set;
        }

        public bool UacEnabled
        {
            get;
            set;
        }

        public bool PendingReboot
        {
            get;
            set;
        }

        public bool RemoteDesktopEnabled
        {
            get;
            set;
        }

        public DateTime CollectedAtUtc
        {
            get;
            set;
        }
    }

    private sealed class AvailabilityPayload
    {
        public bool Available
        {
            get;
            set;
        }

        public string RawJson
        {
            get;
            set;
        } = string.Empty;

        public string? Error
        {
            get;
            set;
        }
    }

    private sealed class WindowsCompliancePayload
    {
        public string Status
        {
            get;
            set;
        } = "Unknown";

        public int Score
        {
            get;
            set;
        }

        public string RiskLevel
        {
            get;
            set;
        } = "Unknown";

        public int TotalChecks
        {
            get;
            set;
        }

        public int PassedChecks
        {
            get;
            set;
        }

        public int FailedChecks
        {
            get;
            set;
        }

        public List<WindowsComplianceCheckPayload>
            Checks
        {
            get;
            set;
        } = [];

        public WindowsSecurityPayload? Security
        {
            get;
            set;
        }
    }

    private sealed class WindowsComplianceCheckPayload
    {
        public string Code
        {
            get;
            set;
        } = string.Empty;

        public string Name
        {
            get;
            set;
        } = string.Empty;

        public bool Passed
        {
            get;
            set;
        }

        public string Message
        {
            get;
            set;
        } = string.Empty;
    }

    // =========================================================
    // ANDROID DTO
    // =========================================================

    private sealed class AndroidSecurityPayload
    {
        public string AndroidVersion
        {
            get;
            set;
        } = string.Empty;

        public int ApiLevel
        {
            get;
            set;
        }

        public string? SecurityPatchLevel
        {
            get;
            set;
        }

        public bool DeviceSecure
        {
            get;
            set;
        }

        public string EncryptionStatus
        {
            get;
            set;
        } = "Unknown";

        public bool AdbEnabled
        {
            get;
            set;
        }

        public bool DeveloperOptionsEnabled
        {
            get;
            set;
        }

        public bool RootDetected
        {
            get;
            set;
        }

        public List<string> RootSignals
        {
            get;
            set;
        } = [];

        public bool EmulatorDetected
        {
            get;
            set;
        }

        public string? VerifiedBootState
        {
            get;
            set;
        }

        public bool? BootloaderLocked
        {
            get;
            set;
        }

        public bool? SelinuxEnforced
        {
            get;
            set;
        }

        public bool AgentInstalled
        {
            get;
            set;
        }

        public string AgentVersionName
        {
            get;
            set;
        } = string.Empty;

        public long AgentVersionCode
        {
            get;
            set;
        }

        public bool? UnknownSourcesAllowed
        {
            get;
            set;
        }
    }

    private sealed class AndroidCompliancePayload
    {
        public string Status
        {
            get;
            set;
        } = "Unknown";

        public int Score
        {
            get;
            set;
        }

        public string RiskLevel
        {
            get;
            set;
        } = "Unknown";

        public int TotalChecks
        {
            get;
            set;
        }

        public int PassedChecks
        {
            get;
            set;
        }

        public int FailedChecks
        {
            get;
            set;
        }

        public List<object> Findings
        {
            get;
            set;
        } = [];

        public AndroidSecurityPayload? Posture
        {
            get;
            set;
        }
    }

    private sealed class ComplianceFindingDto
    {
        public string Code
        {
            get;
            set;
        } = string.Empty;

        public string Title
        {
            get;
            set;
        } = string.Empty;

        public string Description
        {
            get;
            set;
        } = string.Empty;

        public string Category
        {
            get;
            set;
        } = string.Empty;

        public string Severity
        {
            get;
            set;
        } = string.Empty;

        public bool Compliant
        {
            get;
            set;
        }
    }
}