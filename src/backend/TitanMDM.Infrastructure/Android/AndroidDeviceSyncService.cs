using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Android;

public sealed class AndroidDeviceSyncService
    : IAndroidDeviceSyncService
{
    private const int GooglePageSize = 1000;

    private readonly TitanMdmDbContext _dbContext;
    private readonly AndroidManagementClient _client;
    private readonly ILogger<AndroidDeviceSyncService> _logger;

    public AndroidDeviceSyncService(
        TitanMdmDbContext dbContext,
        AndroidManagementClient client,
        ILogger<AndroidDeviceSyncService> logger)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));

        _client =
            client ??
            throw new ArgumentNullException(
                nameof(client));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    public async Task<AndroidDeviceSyncResultDto>
        SynchronizeAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(organizationId);

        var startedAtUtc = DateTime.UtcNow;

        var configuration =
            await _dbContext
                .AndroidEnterpriseConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                        organizationId,
                    cancellationToken);

        if (configuration is null ||
            string.IsNullOrWhiteSpace(
                configuration.EnterpriseName))
        {
            throw new InvalidOperationException(
                "Android Enterprise no está conectado " +
                "para esta organización.");
        }

        var enterpriseName =
            configuration.EnterpriseName;

        var received = 0;
        var created = 0;
        var updated = 0;
        var markedMissing = 0;
        var failed = 0;

        var errors =
            new List<string>();

        /*
         * Solamente marcaremos dispositivos como ausentes
         * después de haber completado correctamente TODAS
         * las páginas de Google.
         */
        var googleDeviceNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        string? pageToken = null;

        do
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            JsonNode response;

            try
            {
                response =
                    await _client.ListDevicesAsync(
                        enterpriseName,
                        GooglePageSize,
                        pageToken,
                        cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Android device synchronization failed " +
                    "while reading Google page for organization {OrganizationId}.",
                    organizationId);

                throw;
            }

            var devices =
                response["devices"] as JsonArray;

            if (devices is not null)
            {
                foreach (var node in devices)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (node is null)
                        continue;

                    received++;

                    var googleName =
                        GetString(node, "name");

                    if (string.IsNullOrWhiteSpace(
                            googleName))
                    {
                        failed++;

                        errors.Add(
                            $"Google device #{received} " +
                            "does not contain a resource name.");

                        continue;
                    }

                    googleDeviceNames.Add(
                        googleName);

                    try
                    {
                        var wasCreated =
                            await SynchronizeDeviceAsync(
                                organizationId,
                                node,
                                cancellationToken);

                        if (wasCreated)
                            created++;
                        else
                            updated++;
                    }
                    catch (Exception exception)
                    {
                        failed++;

                        var safeName =
                            googleName.Length > 200
                                ? googleName[..200]
                                : googleName;

                        errors.Add(
                            $"{safeName}: " +
                            exception.Message);

                        _logger.LogError(
                            exception,
                            "Could not synchronize Android device {GoogleDeviceName}.",
                            googleName);

                        /*
                         * Eliminamos cambios parciales de este
                         * dispositivo antes de continuar con el
                         * siguiente.
                         */
                        _dbContext.ChangeTracker.Clear();
                    }
                }
            }

            pageToken =
                GetString(
                    response,
                    "nextPageToken");

        } while (!string.IsNullOrWhiteSpace(
            pageToken));

        /*
         * Sólo llegamos aquí si todas las páginas AMAPI
         * fueron obtenidas correctamente.
         */
        var localAndroidDevices =
            await _dbContext
                .AndroidDevices
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId &&
                        !x.IsDeletedInGoogle)
                .ToListAsync(
                    cancellationToken);

        foreach (var androidDevice in
                 localAndroidDevices)
        {
            if (googleDeviceNames.Contains(
                    androidDevice.GoogleDeviceName))
            {
                continue;
            }

            androidDevice.MarkMissingInGoogle();

            var device =
                await _dbContext
                    .Devices
                    .SingleOrDefaultAsync(
                        x =>
                            x.Id ==
                            androidDevice.DeviceId &&
                            x.OrganizationId ==
                            organizationId,
                        cancellationToken);

            device?.MarkAndroidMissing();

            markedMissing++;
        }

        var trackedConfiguration =
            await _dbContext
                .AndroidEnterpriseConfigurations
                .SingleAsync(
                    x =>
                        x.OrganizationId ==
                        organizationId,
                    cancellationToken);

        trackedConfiguration
            .RecordDeviceSynchronization(
                received,
                failed);


        await _dbContext.SaveChangesAsync(
            cancellationToken);

        var completedAtUtc = DateTime.UtcNow;

        _logger.LogInformation(
            "Android synchronization completed for organization {OrganizationId}. " +
            "Received={Received}, Created={Created}, Updated={Updated}, " +
            "Missing={Missing}, Failed={Failed}.",
            organizationId,
            received,
            created,
            updated,
            markedMissing,
            failed);

        return new AndroidDeviceSyncResultDto(
            received,
            created,
            updated,
            markedMissing,
            failed,
            startedAtUtc,
            completedAtUtc,
            errors);
    }

    public async Task<AndroidDeviceInventorySummaryDto>
        GetSummaryAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds = null,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(organizationId);

        var allowedSiteIds =
            NormalizeSiteScope(accessibleSiteIds);

        var rows =
            await (
                from android in
                    _dbContext
                        .AndroidDevices
                        .AsNoTracking()

                join device in
                    _dbContext
                        .Devices
                        .AsNoTracking()
                    on android.DeviceId
                    equals device.Id

                where
                    android.OrganizationId ==
                    organizationId &&
                    device.OrganizationId ==
                    organizationId &&
                    (allowedSiteIds == null ||
                     (device.SiteId != null &&
                      allowedSiteIds.Contains(
                          device.SiteId.Value)))

                select new
                {
                    android.IsDeletedInGoogle,
                    android.ManagementMode,
                    android.LastSynchronizedAtUtc,
                    device.IsManaged,
                    device.ComplianceStatus
                })
                .ToListAsync(
                    cancellationToken);

        var enterpriseLastSync =
     await _dbContext
         .AndroidEnterpriseConfigurations
         .AsNoTracking()
         .Where(
             x =>
                 x.OrganizationId ==
                 organizationId)
         .Select(
             x =>
                 x.LastDeviceSyncAtUtc)
         .SingleOrDefaultAsync(
             cancellationToken);

        var deviceLastSync =
            rows.Count == 0
                ? (DateTime?)null
                : rows.Max(
                    x =>
                        x.LastSynchronizedAtUtc);

        var lastSync =
            enterpriseLastSync ??
            deviceLastSync;

        return new AndroidDeviceInventorySummaryDto(
            Total:
                rows.Count,

            Managed:
                rows.Count(
                    x =>
                        x.IsManaged &&
                        !x.IsDeletedInGoogle),

            MissingInGoogle:
                rows.Count(
                    x =>
                        x.IsDeletedInGoogle),

            FullyManaged:
                rows.Count(
                    x =>
                        ModeEquals(
                            x.ManagementMode,
                            "DEVICE_OWNER") ||
                        ModeEquals(
                            x.ManagementMode,
                            "FULLY_MANAGED")),

            Dedicated:
                rows.Count(
                    x =>
                        ModeEquals(
                            x.ManagementMode,
                            "DEDICATED_DEVICE") ||
                        ModeEquals(
                            x.ManagementMode,
                            "DEDICATED")),

            WorkProfile:
                rows.Count(
                    x =>
                        ModeEquals(
                            x.ManagementMode,
                            "PROFILE_OWNER") ||
                        ModeEquals(
                            x.ManagementMode,
                            "WORK_PROFILE")),

            Compliant:
                rows.Count(
                    x =>
                        x.ComplianceStatus ==
                        ComplianceStatus.Compliant),

            NonCompliant:
                rows.Count(
                    x =>
                        x.ComplianceStatus ==
                        ComplianceStatus.NonCompliant),

            LastSynchronizationUtc:
                lastSync);
    }

    public async Task<AndroidDeviceInventoryPageDto>
        GetInventoryPageAsync(
            Guid organizationId,
            string? search,
            int page,
            int pageSize,
            IReadOnlyCollection<Guid>? accessibleSiteIds = null,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(organizationId);

        var normalizedPage =
            Math.Max(page, 1);

        var normalizedPageSize =
            Math.Clamp(pageSize, 1, 200);

        var allowedSiteIds =
            NormalizeSiteScope(accessibleSiteIds);

        var query =
            from android in
                _dbContext
                    .AndroidDevices
                    .AsNoTracking()

            join device in
                _dbContext
                    .Devices
                    .AsNoTracking()
                on android.DeviceId
                equals device.Id

            where
                android.OrganizationId ==
                organizationId &&
                device.OrganizationId ==
                organizationId &&
                !device.IsDeleted &&
                (allowedSiteIds == null ||
                 (device.SiteId != null &&
                  allowedSiteIds.Contains(
                      device.SiteId.Value)))

            select new
            {
                Device = device,
                Android = android
            };

        var term =
            search?.Trim();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query =
                query.Where(
                    x =>
                        x.Device.DeviceName
                            .Contains(term) ||
                        x.Device.SerialNumber
                            .Contains(term) ||
                        (x.Device.Model != null &&
                         x.Device.Model
                             .Contains(term)) ||
                        (x.Android.AppliedPolicyName != null &&
                         x.Android.AppliedPolicyName
                             .Contains(term)));
        }

        var totalCount =
            await query
                .CountAsync(
                    cancellationToken);

        var rows =
            await query
                .OrderByDescending(
                    x =>
                        x.Android
                            .LastSynchronizedAtUtc)
                .Skip(
                    (normalizedPage - 1) *
                    normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync(
                    cancellationToken);

        var items =
            rows
                .Select(
                    x =>
                        new AndroidInventoryItemDto(
                            x.Device.Id,
                            x.Device.DeviceName,
                            x.Device.SerialNumber,
                            x.Device.Manufacturer,
                            x.Device.Model,
                            x.Device.Status.ToString(),
                            x.Device.ComplianceStatus
                                .ToString(),
                            x.Device.IsManaged,
                            x.Device.LastSeenAtUtc,
                            x.Android.ManagementMode,
                            x.Android.Ownership,
                            x.Android.State,
                            x.Android.AppliedPolicyName,
                            x.Android.AppliedPolicyVersion,
                            x.Android.AppliedPolicyState,
                            x.Android.LastPolicySyncTimeUtc,
                            x.Android.LastSynchronizedAtUtc,
                            x.Android.IsDeletedInGoogle,
                            x.Android.ApiLevel,
                            x.Device.OperatingSystemVersion,
                            x.Android.SecurityPatchLevel))
                .ToList();

        return new AndroidDeviceInventoryPageDto(
            items,
            totalCount,
            normalizedPage,
            normalizedPageSize,
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount /
                    (double)normalizedPageSize));
    }

    public async Task<AndroidDeviceCapabilitiesDto?>
        GetCapabilitiesAsync(
            Guid organizationId,
            Guid deviceId,
            IReadOnlyCollection<Guid>? accessibleSiteIds = null,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(organizationId);

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));
        }

        var allowedSiteIds =
            NormalizeSiteScope(accessibleSiteIds);

        var row =
            await (
                from android in
                    _dbContext
                        .AndroidDevices
                        .AsNoTracking()

                join device in
                    _dbContext
                        .Devices
                        .AsNoTracking()
                    on android.DeviceId
                    equals device.Id

                where
                    android.OrganizationId ==
                        organizationId &&
                    device.OrganizationId ==
                        organizationId &&
                    android.DeviceId ==
                        deviceId &&
                    !device.IsDeleted &&
                    (allowedSiteIds == null ||
                     (device.SiteId != null &&
                      allowedSiteIds.Contains(
                          device.SiteId.Value)))

                select new
                {
                    Device = device,
                    Android = android
                })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (row is null)
        {
            return null;
        }

        var manufacturer =
            row.Device.Manufacturer;

        var requiresAlternativeRoute =
            AndroidRemoteCapabilityResolver
                .IsHuaweiFamily(manufacturer);

        var input =
            new AndroidRemoteCapabilityInput(
                manufacturer,
                row.Device.Model,
                row.Android.ApiLevel,
                row.Android.ManagementMode,
                row.Android.State,
                row.Android.Ownership,
                row.Device.IsManaged,
                row.Android.IsDeletedInGoogle,
                row.Android.LastStatusReportTimeUtc);

        var actions =
            AndroidRemoteCapabilityResolver
                .Resolve(input);

        return new AndroidDeviceCapabilitiesDto(
            row.Device.Id,
            row.Device.DeviceName,
            manufacturer,
            row.Device.Model,
            row.Android.ApiLevel,
            row.Device.OperatingSystemVersion,
            row.Android.SecurityPatchLevel,
            row.Android.ManagementMode,
            row.Android.Ownership,
            row.Device.IsManaged,
            row.Android.IsDeletedInGoogle,
            HasGoogleMobileServices: !requiresAlternativeRoute,
            RequiresAlternativeRoute: requiresAlternativeRoute,
            AlternativeRouteNote: requiresAlternativeRoute
                ? "Huawei/Honor sin Google Mobile Services puede requerir "
                  + "una ruta alternativa no basada en Android Management API."
                : null,
            RemoteDiagnosticsSupported: true,
            RemoteVisionSupported: false,
            RemoteControlSupported: false,
            RemoteSupportNote:
                "La visualización y el control interactivo Android "
                + "requieren un agente con MediaProjection y consentimiento "
                + "explícito del usuario. Hoy no están disponibles; "
                + "el diagnóstico remoto sí lo está.",
            Actions: actions);
    }

    private async Task<bool>
        SynchronizeDeviceAsync(
            Guid organizationId,
            JsonNode googleDevice,
            CancellationToken cancellationToken)
    {
        var googleName =
            RequireString(
                googleDevice,
                "name");

        var googleDeviceId =
            GetResourceId(googleName);

        var hardwareInfo =
            googleDevice["hardwareInfo"];

        var softwareInfo =
            googleDevice["softwareInfo"];

        var networkInfo =
            googleDevice["networkInfo"];

        var securityPostureNode =
            googleDevice["securityPosture"];

        var serialNumber =
            FirstNonEmpty(
                GetString(
                    hardwareInfo,
                    "serialNumber"),
                googleDeviceId,
                googleName);

        var manufacturer =
            GetString(
                hardwareInfo,
                "manufacturer");

        var model =
            GetString(
                hardwareInfo,
                "model");

        var brand =
            GetString(
                hardwareInfo,
                "brand");

        var hardware =
            GetString(
                hardwareInfo,
                "hardware");

        var basebandVersion =
            GetString(
                hardwareInfo,
                "deviceBasebandVersion");

        var bootloaderVersion =
            GetString(
                softwareInfo,
                "bootloaderVersion");

        var androidVersion =
            GetString(
                softwareInfo,
                "androidVersion");

        var apiLevel =
            GetInt32(
                softwareInfo,
                "apiLevel");

        var buildNumber =
            GetString(
                softwareInfo,
                "buildNumber");

        var kernelVersion =
            GetString(
                softwareInfo,
                "kernelVersion");

        var securityPatchLevel =
            GetString(
                softwareInfo,
                "securityPatchLevel");

        var adpVersion =
            FirstNonEmpty(
                GetString(
                    softwareInfo,
                    "androidDevicePolicyVersionName"),
                GetString(
                    softwareInfo,
                    "androidDevicePolicyVersion"));

        var adpVersionCode =
            GetStringOrNumber(
                softwareInfo,
                "androidDevicePolicyVersionCode");

        var imei =
            FirstNonEmpty(
                GetString(
                    networkInfo,
                    "imei"),
                GetFirstStringFromArray(
                    networkInfo,
                    "imei"));

        var macAddress =
            FirstNonEmpty(
                GetString(
                    networkInfo,
                    "wifiMacAddress"),
                GetString(
                    hardwareInfo,
                    "wifiMacAddress"));

        var managementMode =
            GetString(
                googleDevice,
                "managementMode");

        var ownership =
            GetString(
                googleDevice,
                "ownership");

        var state =
            GetString(
                googleDevice,
                "state");

        var appliedPolicyName =
            GetString(
                googleDevice,
                "appliedPolicyName");

        var appliedPolicyVersion =
            GetInt64(
                googleDevice,
                "appliedPolicyVersion");

        var appliedPolicyState =
            GetString(
                googleDevice,
                "appliedState");

        var enrollmentTokenName =
            GetString(
                googleDevice,
                "enrollmentTokenName");

        var userName =
            GetString(
                googleDevice,
                "userName");

        var enrollmentTime =
            GetDateTime(
                googleDevice,
                "enrollmentTime");

        var lastStatusReportTime =
            GetDateTime(
                googleDevice,
                "lastStatusReportTime");

        var lastPolicySyncTime =
            GetDateTime(
                googleDevice,
                "lastPolicySyncTime");

        var securityPosture =
            FirstNonEmpty(
                GetString(
                    securityPostureNode,
                    "devicePosture"),
                GetString(
                    securityPostureNode,
                    "posture"));

        var encryptionStatus =
            FirstNonEmpty(
                GetString(
                    securityPostureNode,
                    "encryptionStatus"),
                GetString(
                    googleDevice,
                    "encryptionStatus"));

        /*
         * La identidad AMAPI es prioritaria.
         */
        var androidDevice =
            await _dbContext
                .AndroidDevices
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                        organizationId &&
                        x.GoogleDeviceName ==
                        googleName,
                    cancellationToken);

        Device device;
        var created = false;

        if (androidDevice is null)
        {
            /*
             * Segundo intento de correlación:
             * serial físico dentro de la misma organización.
             *
             * Esto evita duplicar un registro Android que
             * pudiera haber sido creado anteriormente por
             * otro flujo de TitanMDM.
             */
            device =
                await _dbContext
                    .Devices
                    .SingleOrDefaultAsync(
                        x =>
                            x.OrganizationId ==
                            organizationId &&
                            x.Platform ==
                            DevicePlatform.Android &&
                            x.SerialNumber ==
                            serialNumber,
                        cancellationToken)
                ?? new Device(
                    organizationId,
                    BuildFriendlyDeviceName(
                        manufacturer,
                        model,
                        googleDeviceId),
                    DevicePlatform.Android,
                    serialNumber);

            if (_dbContext.Entry(device).State ==
                EntityState.Detached)
            {
                _dbContext.Devices.Add(device);
            }

            androidDevice =
                new AndroidDevice(
                    organizationId,
                    device.Id,
                    googleName);

            _dbContext
                .AndroidDevices
                .Add(androidDevice);

            created = true;
        }
        else
        {
            device =
                await _dbContext
                    .Devices
                    .SingleAsync(
                        x =>
                            x.Id ==
                            androidDevice.DeviceId &&
                            x.OrganizationId ==
                            organizationId,
                        cancellationToken);

            androidDevice.RestoreFromGoogle();
        }

        var isManaged =
            !string.Equals(
                state,
                "DELETED",
                StringComparison.OrdinalIgnoreCase);

        device.SynchronizeAndroidEnterprise(
            BuildFriendlyDeviceName(
                manufacturer,
                model,
                googleDeviceId),
            manufacturer,
            model,
            androidVersion,
            adpVersion,
            imei,
            macAddress,
            enrollmentTime,
            lastStatusReportTime,
            isManaged);

        if (!string.IsNullOrWhiteSpace(
                userName))
        {
            device.AssignUser(
                userName,
                device.Department);
        }

        androidDevice.Synchronize(
            googleDeviceId,
            managementMode,
            ownership,
            state,
            appliedPolicyName,
            appliedPolicyVersion,
            appliedPolicyState,
            enrollmentTokenName,
            userName,
            brand,
            hardware,
            basebandVersion,
            bootloaderVersion,
            securityPatchLevel,
            apiLevel,
            buildNumber,
            kernelVersion,
            adpVersion,
            adpVersionCode,
            encryptionStatus,
            securityPosture,
            enrollmentTime,
            lastStatusReportTime,
            lastPolicySyncTime);

        await ReconcilePolicyAssignmentAsync(
            organizationId,
            device.Id,
            appliedPolicyName,
            appliedPolicyVersion,
            appliedPolicyState,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return created;


    }

    private async Task ReconcilePolicyAssignmentAsync(
    Guid organizationId,
    Guid deviceId,
    string? appliedPolicyName,
    long? appliedPolicyVersion,
    string? appliedPolicyState,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                appliedPolicyName))
        {
            return;
        }

        var publication =
            await _dbContext
                .AndroidPolicyPublications
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId &&
                        x.GooglePolicyName ==
                        appliedPolicyName)
                .OrderByDescending(
                    x => x.PolicyVersion)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (publication is null)
        {
            return;
        }

        var assignment =
            await _dbContext
                .DevicePolicyAssignments
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                        organizationId &&
                        x.DeviceId ==
                        deviceId &&
                        x.PolicyId ==
                        publication.PolicyId &&
                        x.PolicyVersion ==
                        publication.PolicyVersion,
                    cancellationToken);

        if (assignment is null)
        {
            return;
        }

        /*
         * appliedPolicyName confirma que Google reporta
         * esta política sobre el dispositivo.
         *
         * appliedPolicyVersion, cuando está disponible,
         * se utiliza además para evitar confirmar una
         * versión diferente.
         */
        if (
            appliedPolicyVersion.HasValue &&
            appliedPolicyVersion.Value > 0 &&
            appliedPolicyVersion.Value !=
                publication.PolicyVersion)
        {
            return;
        }

        var state =
            appliedPolicyState?.Trim();

        var successfullyApplied =
            string.Equals(
                state,
                "APPLIED",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                state,
                "APPLIED_STATE_APPLIED",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                state,
                "SUCCESS",
                StringComparison.OrdinalIgnoreCase);

        /*
         * Algunos payloads de AMAPI pueden proporcionar
         * appliedPolicyName sin un estado explícito.
         * No declaramos Applied en ese caso.
         */
        if (!successfullyApplied)
        {
            return;
        }

        assignment.MarkApplied();
    }

    private static string
        BuildFriendlyDeviceName(
            string? manufacturer,
            string? model,
            string googleDeviceId)
    {
        var values =
            new[]
            {
                manufacturer,
                model
            }
            .Where(
                x =>
                    !string.IsNullOrWhiteSpace(x))
            .Select(
                x =>
                    x!.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (values.Length > 0)
        {
            return string.Join(
                " ",
                values);
        }

        return
            $"Android {googleDeviceId}";
    }

    private static string RequireString(
        JsonNode node,
        string propertyName)
    {
        return GetString(
                   node,
                   propertyName)
               ?? throw new InvalidOperationException(
                   $"Google device does not contain '{propertyName}'.");
    }

    private static string? GetString(
        JsonNode? node,
        string propertyName)
    {
        if (node is null)
            return null;

        var value =
            node[propertyName];

        if (value is null)
            return null;

        try
        {
            var text =
                value.GetValue<string>();

            return string.IsNullOrWhiteSpace(text)
                ? null
                : text.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetStringOrNumber(
        JsonNode? node,
        string propertyName)
    {
        if (node is null)
            return null;

        var value =
            node[propertyName];

        if (value is null)
            return null;

        try
        {
            var text =
                value.ToJsonString()
                    .Trim('"');

            return string.IsNullOrWhiteSpace(text)
                ? null
                : text;
        }
        catch
        {
            return null;
        }
    }

    private static string?
        GetFirstStringFromArray(
            JsonNode? node,
            string propertyName)
    {
        if (node?[propertyName]
            is not JsonArray array)
        {
            return null;
        }

        foreach (var item in array)
        {
            if (item is null)
                continue;

            try
            {
                var value =
                    item.GetValue<string>();

                if (!string.IsNullOrWhiteSpace(
                        value))
                {
                    return value.Trim();
                }
            }
            catch
            {
                // Ignore malformed item.
            }
        }

        return null;
    }

    private static int? GetInt32(
        JsonNode? node,
        string propertyName)
    {
        if (node?[propertyName] is null)
            return null;

        try
        {
            return node[propertyName]!
                .GetValue<int>();
        }
        catch
        {
            return int.TryParse(
                GetStringOrNumber(
                    node,
                    propertyName),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : null;
        }
    }

    private static long? GetInt64(
        JsonNode? node,
        string propertyName)
    {
        if (node?[propertyName] is null)
            return null;

        try
        {
            return node[propertyName]!
                .GetValue<long>();
        }
        catch
        {
            return long.TryParse(
                GetStringOrNumber(
                    node,
                    propertyName),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : null;
        }
    }

    private static DateTime? GetDateTime(
        JsonNode? node,
        string propertyName)
    {
        var value =
            GetString(
                node,
                propertyName);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal |
            DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    private static string GetResourceId(
        string resourceName)
    {
        return resourceName
            .Split(
                '/',
                StringSplitOptions
                    .RemoveEmptyEntries)
            .Last();
    }

    private static string FirstNonEmpty(
        params string?[] values)
    {
        return values
            .FirstOrDefault(
                x =>
                    !string.IsNullOrWhiteSpace(x))
            ?.Trim()
            ?? string.Empty;
    }

    private static bool ModeEquals(
        string? actual,
        string expected)
    {
        return string.Equals(
            actual,
            expected,
            StringComparison.OrdinalIgnoreCase);
    }

    private static Guid[]? NormalizeSiteScope(
        IReadOnlyCollection<Guid>? accessibleSiteIds)
    {
        if (accessibleSiteIds is null)
            return null;

        return accessibleSiteIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
    }

    private static void ValidateOrganization(
        Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }
    }
}