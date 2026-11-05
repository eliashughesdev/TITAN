using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TitanMDM.Application.Android.Policies;
using TitanMDM.Application.Commands;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Android.Policies;

public sealed class AndroidPolicyAssignmentService
    : IAndroidPolicyAssignmentService
{
    private readonly TitanMdmDbContext _dbContext;
    private readonly AndroidManagementClient _androidManagementClient;
    private readonly IDeviceCommandService? _commandService;
    private readonly ILogger<AndroidPolicyAssignmentService> _logger;

    /// <summary>
    /// Comando que entrega al TitanMDM Agent (Kotlin) la configuración
    /// del Titan Launcher cuando el equipo no está en Android Enterprise.
    /// </summary>
    public const string AgentLauncherCommandType = "APPLY_LAUNCHER_POLICY";

    private const string AgentPackageName = "com.titanmdm.agent";

    // Máximo permitido por DeviceCommandService (24 h): el equipo lo
    // recibe en su siguiente sincronización.
    private const int AgentCommandExpirationMinutes = 1440;

    public AndroidPolicyAssignmentService(
        TitanMdmDbContext dbContext,
        AndroidManagementClient androidManagementClient,
        ILogger<AndroidPolicyAssignmentService> logger,
        IDeviceCommandService? commandService = null)
    {
        _commandService =
            commandService;

        _dbContext =
            dbContext ??
            throw new ArgumentNullException(nameof(dbContext));

        _androidManagementClient =
            androidManagementClient ??
            throw new ArgumentNullException(
                nameof(androidManagementClient));

        _logger =
            logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AndroidPolicyAssignmentDto> AssignAsync(
        Guid organizationId,
        Guid policyId,
        Guid deviceId,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        CancellationToken cancellationToken = default,
        Guid? actorUserId = null)
    {
        ValidateIdentifiers(
            organizationId,
            policyId,
            deviceId);

        var policy =
            await _dbContext.Policies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Id == policyId,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "La política TitanMDM no existe.");

        if (policy.Platform != PolicyPlatform.Android)
        {
            throw new InvalidOperationException(
                "La política seleccionada no pertenece a Android.");
        }

        /*
         * Equipos inscritos por TitanMDM Agent (Kotlin) no tienen
         * fila en AndroidDevices: la política se entrega por la cola
         * de comandos SQL y el agente la aplica en su sincronización.
         */
        var isAgentDevice =
            !await _dbContext.AndroidDevices
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.DeviceId == deviceId,
                    cancellationToken);

        if (isAgentDevice)
        {
            return await AssignToAgentAsync(
                organizationId,
                policy,
                deviceId,
                accessibleSiteIds,
                actorUserId,
                cancellationToken);
        }

        var publication =
            await _dbContext.AndroidPolicyPublications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policyId &&
                        x.PolicyVersion == policy.CurrentVersion,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "La versión actual de la política no está publicada en Android Enterprise.");

        if (publication.Status !=
            AndroidPolicyPublicationStatus.Published)
        {
            throw new InvalidOperationException(
                $"La publicación Android se encuentra en estado {publication.Status}.");
        }

        if (string.IsNullOrWhiteSpace(
                publication.GooglePolicyName))
        {
            throw new InvalidOperationException(
                "La publicación Android no contiene GooglePolicyName.");
        }

        var device =
            await _dbContext.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Id == deviceId &&
                        !x.IsDeleted,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "El dispositivo TitanMDM no existe.");

        EnsureDeviceInScope(
            accessibleSiteIds,
            device.SiteId);

        var androidDevice =
            await _dbContext.AndroidDevices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.DeviceId == device.Id,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "El dispositivo no está sincronizado con Android Enterprise.");

        if (androidDevice.IsDeletedInGoogle)
        {
            throw new InvalidOperationException(
                "El dispositivo está marcado como ausente en Google Android Enterprise.");
        }

        if (string.IsNullOrWhiteSpace(
                androidDevice.GoogleDeviceName))
        {
            throw new InvalidOperationException(
                "El dispositivo Android no contiene GoogleDeviceName.");
        }

        var assignment =
            await _dbContext.DevicePolicyAssignments
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policyId &&
                        x.DeviceId == deviceId,
                    cancellationToken);

        /*
         * Una asignación correspondiente a otra versión no debe
         * reutilizarse como si perteneciera a CurrentVersion.
         */
        if (assignment is not null &&
            assignment.PolicyVersion != policy.CurrentVersion)
        {
            _dbContext.DevicePolicyAssignments.Remove(
                assignment);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            assignment = null;
        }

        if (assignment is null)
        {
            assignment =
                new DevicePolicyAssignment(
                    organizationId,
                    policyId,
                    deviceId,
                    policy.CurrentVersion);

            _dbContext.DevicePolicyAssignments.Add(
                assignment);
        }

        assignment.MarkApplying();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        try
        {
            /*
             * Android no utiliza el APPLY_POLICY genérico de
             * Windows.
             *
             * Se establece directamente policyName sobre el
             * recurso devices/{deviceId} de Google AMAPI.
             */
            var googleDevice =
                await _androidManagementClient.PatchDeviceAsync(
                    androidDevice.GoogleDeviceName,
                    new JsonObject
                    {
                        ["policyName"] =
                            publication.GooglePolicyName
                    },
                    "policyName",
                    cancellationToken);

            _logger.LogInformation(
                "Android policy assignment accepted by Google AMAPI. " +
                "OrganizationId={OrganizationId}, " +
                "PolicyId={PolicyId}, " +
                "PolicyVersion={PolicyVersion}, " +
                "DeviceId={DeviceId}, " +
                "GoogleDeviceName={GoogleDeviceName}, " +
                "GooglePolicyName={GooglePolicyName}",
                organizationId,
                policyId,
                policy.CurrentVersion,
                deviceId,
                androidDevice.GoogleDeviceName,
                publication.GooglePolicyName);

            /*
             * No usamos MarkApplied() aquí.
             *
             * devices.patch confirma que Google aceptó policyName,
             * pero no que Android Device Policy haya terminado de
             * aplicar la configuración.
             *
             * El estado permanece Applying hasta que el inventario
             * sincronizado reporte appliedPolicyName/version/state.
             */

            return MapAssignment(
                assignment,
                androidDevice,
                publication.GooglePolicyName,
                googleDevice);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            assignment.MarkFailed(
                exception.Message);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            _logger.LogError(
                exception,
                "Android policy assignment failed. " +
                "OrganizationId={OrganizationId}, " +
                "PolicyId={PolicyId}, " +
                "DeviceId={DeviceId}",
                organizationId,
                policyId,
                deviceId);

            throw;
        }
    }

    public async Task<AndroidPolicyAssignmentDto?> GetAsync(
        Guid organizationId,
        Guid policyId,
        Guid deviceId,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty ||
            policyId == Guid.Empty ||
            deviceId == Guid.Empty)
        {
            return null;
        }

        var policy =
            await _dbContext.Policies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Id == policyId,
                    cancellationToken);

        if (policy is null ||
            policy.Platform != PolicyPlatform.Android)
        {
            return null;
        }

        var assignment =
            await _dbContext.DevicePolicyAssignments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policyId &&
                        x.DeviceId == deviceId,
                    cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        var androidDevice =
            await _dbContext.AndroidDevices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.DeviceId == deviceId,
                    cancellationToken);

        var deviceSiteId =
            await _dbContext.Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Id == deviceId)
                .Select(x => x.SiteId)
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (!IsInScope(
                accessibleSiteIds,
                deviceSiteId))
        {
            return null;
        }

        var publication =
            await _dbContext.AndroidPolicyPublications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policyId &&
                        x.PolicyVersion ==
                            assignment.PolicyVersion,
                    cancellationToken);

        if (androidDevice is null)
        {
            return MapAgentAssignment(
                assignment,
                publication?.GooglePolicyName);
        }

        if (publication is null ||
            string.IsNullOrWhiteSpace(
                publication.GooglePolicyName))
        {
            return null;
        }

        return MapAssignment(
            assignment,
            androidDevice,
            publication.GooglePolicyName,
            googleDevice: null);
    }

    public async Task<AndroidPolicyAssignmentPageDto> GetPageAsync(
        Guid organizationId,
        Guid policyId,
        string? search,
        IReadOnlyCollection<Guid>? accessibleSiteIds = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        pageSize =
            Math.Clamp(pageSize, 1, 200);

        if (organizationId == Guid.Empty ||
            policyId == Guid.Empty)
        {
            return new AndroidPolicyAssignmentPageDto(
                Array.Empty<AndroidPolicyAssignmentDeviceDto>(),
                0,
                page,
                pageSize,
                0);
        }

        // LEFT JOIN: incluye equipos del TitanMDM Agent (sin AMAPI).
        var query =
            from device in
                _dbContext.Devices.AsNoTracking()
            join androidRow in
                _dbContext.AndroidDevices
                    .AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId)
                on device.Id equals androidRow.DeviceId
                into androidRows
            from android in androidRows.DefaultIfEmpty()
            where
                device.OrganizationId == organizationId &&
                !device.IsDeleted &&
                device.Platform == DevicePlatform.Android
            select new
            {
                Device = device,
                Android = android
            };

        if (accessibleSiteIds is not null)
        {
            var allowedSiteIds =
                accessibleSiteIds
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToArray();

            query = allowedSiteIds.Length == 0
                ? query.Where(_ => false)
                : query.Where(
                    x =>
                        x.Device.SiteId.HasValue &&
                        allowedSiteIds.Contains(
                            x.Device.SiteId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term =
                search.Trim();

            if (term.Length > 200)
            {
                term = term[..200];
            }

            query = query.Where(
                x =>
                    x.Device.DeviceName.Contains(term) ||
                    x.Device.SerialNumber.Contains(term) ||
                    (x.Device.Manufacturer != null &&
                        x.Device.Manufacturer.Contains(term)) ||
                    (x.Device.Model != null &&
                        x.Device.Model.Contains(term)));
        }

        var totalCount =
            await query.CountAsync(cancellationToken);

        var rows =
            await query
                .OrderBy(x => x.Device.DeviceName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Device,
                    x.Android,
                    Assignment =
                        _dbContext.DevicePolicyAssignments
                            .AsNoTracking()
                            .Where(
                                assignment =>
                                    assignment.OrganizationId ==
                                        organizationId &&
                                    assignment.PolicyId ==
                                        policyId &&
                                    assignment.DeviceId ==
                                        x.Device.Id)
                            .OrderByDescending(
                                assignment =>
                                    assignment.UpdatedAtUtc)
                            .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

        var items =
            rows
                .Select(
                    row =>
                    {
                        var assignment =
                            row.Assignment;

                        if (row.Android is null)
                        {
                            return MapAgentDeviceRow(
                                row.Device,
                                assignment);
                        }

                        return new AndroidPolicyAssignmentDeviceDto(
                            row.Device.Id,
                            row.Device.DeviceName,
                            row.Device.SerialNumber,
                            row.Device.Manufacturer,
                            row.Device.Model,
                            row.Device.Status.ToString(),
                            row.Device.ComplianceStatus.ToString(),
                            row.Device.IsManaged,
                            assignment?.Id,
                            assignment?.PolicyVersion,
                            assignment?.Status.ToString() ??
                                "NotAssigned",
                            assignment?.AssignedAtUtc,
                            assignment?.UpdatedAtUtc,
                            assignment?.AppliedAtUtc,
                            row.Android.AppliedPolicyName,
                            row.Android.AppliedPolicyVersion,
                            row.Android.AppliedPolicyState,
                            row.Android.LastPolicySyncTimeUtc,
                            assignment?.ErrorMessage,
                            row.Android.LauncherProfileId,
                            row.Android.LauncherPublicationId,
                            row.Android.LauncherConfigVersion,
                            row.Android.LauncherAppliedAtUtc,
                            row.Android.LauncherSyncStatus,
                            row.Android.LauncherRestrictionsRead);
                    })
                .ToArray();

        var totalPages =
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount / (double)pageSize);

        return new AndroidPolicyAssignmentPageDto(
            items,
            totalCount,
            page,
            pageSize,
            totalPages);
    }

    // ============================================================
    // TITANMDM AGENT (KOTLIN) — SIN ANDROID ENTERPRISE
    // ============================================================

    private async Task<AndroidPolicyAssignmentDto> AssignToAgentAsync(
        Guid organizationId,
        Policy policy,
        Guid deviceId,
        IReadOnlyCollection<Guid>? accessibleSiteIds,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        if (_commandService is null)
        {
            throw new InvalidOperationException(
                "La cola de comandos no está disponible para el agente TitanMDM.");
        }

        if (actorUserId is not Guid actor ||
            actor == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Se requiere el usuario que asigna la política.");
        }

        var device =
            await _dbContext.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Id == deviceId &&
                        !x.IsDeleted,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "El dispositivo TitanMDM no existe.");

        EnsureDeviceInScope(
            accessibleSiteIds,
            device.SiteId);

        if (device.Platform != DevicePlatform.Android)
        {
            throw new InvalidOperationException(
                "El dispositivo no es Android.");
        }

        if (!device.IsManaged)
        {
            throw new InvalidOperationException(
                "El dispositivo no está administrado por el agente TitanMDM.");
        }

        /*
         * Se reutiliza la política compilada al publicar (sellada con
         * la identidad de publicación y con los recursos del launcher
         * ya resueltos). Se acepta aunque Google la haya rechazado:
         * el agente no depende de Android Management API.
         */
        var publication =
            await _dbContext.AndroidPolicyPublications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policy.Id &&
                        x.PolicyVersion == policy.CurrentVersion,
                    cancellationToken);

        if (publication is null ||
            string.IsNullOrWhiteSpace(
                publication.CompiledPolicyJson))
        {
            throw new InvalidOperationException(
                "Publique la versión actual de la política antes de asignarla.");
        }

        var launcherConfiguration =
            ExtractAgentLauncherConfiguration(
                publication.CompiledPolicyJson);

        var assignment =
            await _dbContext.DevicePolicyAssignments
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policy.Id &&
                        x.DeviceId == deviceId,
                    cancellationToken);

        if (assignment is not null &&
            assignment.PolicyVersion != policy.CurrentVersion)
        {
            _dbContext.DevicePolicyAssignments.Remove(
                assignment);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            assignment = null;
        }

        if (assignment is null)
        {
            assignment =
                new DevicePolicyAssignment(
                    organizationId,
                    policy.Id,
                    deviceId,
                    policy.CurrentVersion);

            _dbContext.DevicePolicyAssignments.Add(
                assignment);

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        var payload =
            new JsonObject
            {
                ["assignmentId"] =
                    assignment.Id.ToString("D"),
                ["policyId"] =
                    policy.Id.ToString("D"),
                ["policyName"] =
                    policy.Name,
                ["policyVersion"] =
                    policy.CurrentVersion,
                ["publicationId"] =
                    publication.Id.ToString("D"),
                ["launcher"] =
                    launcherConfiguration
            };

        var command =
            await _commandService.CreateAsync(
                organizationId,
                actor,
                new CreateDeviceCommandRequest(
                    deviceId,
                    AgentLauncherCommandType,
                    payload.ToJsonString(),
                    AgentCommandExpirationMinutes),
                cancellationToken);

        assignment.Queue(
            command.Id);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Android policy queued for TitanMDM Agent. " +
            "OrganizationId={OrganizationId}, PolicyId={PolicyId}, " +
            "PolicyVersion={PolicyVersion}, DeviceId={DeviceId}, " +
            "CommandId={CommandId}, HasLauncher={HasLauncher}",
            organizationId,
            policy.Id,
            policy.CurrentVersion,
            deviceId,
            command.Id,
            launcherConfiguration is not null);

        return MapAgentAssignment(
            assignment,
            publication.GooglePolicyName);
    }

    /// <summary>
    /// Devuelve la managed configuration del launcher (paquete del
    /// agente) contenida en la política compilada, o null si la
    /// política no configura el Titan Launcher.
    /// </summary>
    public static JsonObject? ExtractAgentLauncherConfiguration(
        string compiledPolicyJson)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(compiledPolicyJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        if (root is not JsonObject policy ||
            policy["applications"] is not JsonArray applications)
        {
            return null;
        }

        var configuration =
            applications
                .OfType<JsonObject>()
                .Where(application =>
                    string.Equals(
                        application["packageName"]?.GetValue<string>(),
                        AgentPackageName,
                        StringComparison.Ordinal))
                .Select(application =>
                    application["managedConfiguration"] as JsonObject)
                .FirstOrDefault(value => value is not null);

        return configuration?.DeepClone() as JsonObject;
    }

    private static AndroidPolicyAssignmentDto MapAgentAssignment(
        DevicePolicyAssignment assignment,
        string? googlePolicyName)
    {
        var applied =
            assignment.Status == PolicyAssignmentStatus.Applied;

        return new AndroidPolicyAssignmentDto(
            assignment.Id,
            assignment.PolicyId,
            assignment.DeviceId,
            Guid.Empty,
            assignment.PolicyVersion,
            string.Empty,
            googlePolicyName ?? string.Empty,
            assignment.Status.ToString(),
            assignment.AssignedAtUtc,
            assignment.UpdatedAtUtc,
            assignment.AppliedAtUtc,
            AppliedPolicyName: null,
            AppliedPolicyVersion: applied ? assignment.PolicyVersion : null,
            AppliedPolicyState: applied ? "APPLIED" : null,
            LastPolicySyncTimeUtc: assignment.AppliedAtUtc,
            assignment.ErrorMessage,
            LauncherProfileId: assignment.PolicyId,
            LauncherPublicationId: null,
            LauncherConfigVersion: applied ? assignment.PolicyVersion : null,
            LauncherAppliedAtUtc: assignment.AppliedAtUtc,
            LauncherSyncStatus: AgentLauncherStatus(assignment.Status),
            LauncherRestrictionsRead: applied);
    }

    private static AndroidPolicyAssignmentDeviceDto MapAgentDeviceRow(
        Device device,
        DevicePolicyAssignment? assignment)
    {
        var applied =
            assignment?.Status == PolicyAssignmentStatus.Applied;

        return new AndroidPolicyAssignmentDeviceDto(
            device.Id,
            device.DeviceName,
            device.SerialNumber,
            device.Manufacturer,
            device.Model,
            device.Status.ToString(),
            device.ComplianceStatus.ToString(),
            device.IsManaged,
            assignment?.Id,
            assignment?.PolicyVersion,
            assignment?.Status.ToString() ?? "NotAssigned",
            assignment?.AssignedAtUtc,
            assignment?.UpdatedAtUtc,
            assignment?.AppliedAtUtc,
            AppliedPolicyName: null,
            AppliedPolicyVersion: applied ? assignment!.PolicyVersion : null,
            AppliedPolicyState: assignment is null
                ? "Agente TitanMDM"
                : applied ? "APPLIED" : null,
            LastPolicySyncTimeUtc: assignment?.AppliedAtUtc,
            assignment?.ErrorMessage,
            LauncherProfileId: assignment?.PolicyId,
            LauncherPublicationId: null,
            LauncherConfigVersion: applied ? assignment!.PolicyVersion : null,
            LauncherAppliedAtUtc: assignment?.AppliedAtUtc,
            LauncherSyncStatus: assignment is null
                ? null
                : AgentLauncherStatus(assignment.Status),
            LauncherRestrictionsRead: applied);
    }

    private static string AgentLauncherStatus(
        PolicyAssignmentStatus status) =>
        status switch
        {
            PolicyAssignmentStatus.Applied => "Applied",
            PolicyAssignmentStatus.Failed => "Failed",
            _ => "Pending"
        };

    private static AndroidPolicyAssignmentDto MapAssignment(
        DevicePolicyAssignment assignment,
        AndroidDevice androidDevice,
        string googlePolicyName,
        JsonNode? googleDevice)
    {
        return new AndroidPolicyAssignmentDto(
            assignment.Id,
            assignment.PolicyId,
            assignment.DeviceId,
            androidDevice.Id,
            assignment.PolicyVersion,
            androidDevice.GoogleDeviceName,
            googlePolicyName,
            assignment.Status.ToString(),
            assignment.AssignedAtUtc,
            assignment.UpdatedAtUtc,
            assignment.AppliedAtUtc,
            GetJsonString(
                googleDevice,
                "appliedPolicyName") ??
                androidDevice.AppliedPolicyName,
            GetJsonLong(
                googleDevice,
                "appliedPolicyVersion") ??
                androidDevice.AppliedPolicyVersion,
            GetJsonString(
                googleDevice,
                "appliedPolicyState") ??
                androidDevice.AppliedPolicyState,
            androidDevice.LastPolicySyncTimeUtc,
            assignment.ErrorMessage,
            androidDevice.LauncherProfileId,
            androidDevice.LauncherPublicationId,
            androidDevice.LauncherConfigVersion,
            androidDevice.LauncherAppliedAtUtc,
            androidDevice.LauncherSyncStatus,
            androidDevice.LauncherRestrictionsRead);
    }

    private static void EnsureDeviceInScope(
        IReadOnlyCollection<Guid>? accessibleSiteIds,
        Guid? deviceSiteId)
    {
        if (IsInScope(
                accessibleSiteIds,
                deviceSiteId))
        {
            return;
        }

        /*
         * El llamador solo puede operar sobre dispositivos
         * dentro de su ámbito de sitios. La falta de permiso se
         * comunica igual que un dispositivo inexistente para no
         * filtrar la existencia de dispositivos de otros sitios.
         */
        throw new InvalidOperationException(
            "El dispositivo TitanMDM no existe.");
    }

    private static bool IsInScope(
        IReadOnlyCollection<Guid>? accessibleSiteIds,
        Guid? deviceSiteId)
    {
        /*
         * null = ámbito organizacional (sin restricción de sitio).
         * Conjunto explícito = solo los sitios autorizados.
         */
        if (accessibleSiteIds is null)
        {
            return true;
        }

        return deviceSiteId is Guid siteId &&
            accessibleSiteIds.Contains(siteId);
    }

    private static void ValidateIdentifiers(
        Guid organizationId,
        Guid policyId,
        Guid deviceId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }

        if (policyId == Guid.Empty)
        {
            throw new ArgumentException(
                "PolicyId is required.",
                nameof(policyId));
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));
        }
    }

    private static string? GetJsonString(
        JsonNode? node,
        string propertyName)
    {
        if (node?[propertyName] is not
            JsonValue value)
        {
            return null;
        }

        return value.TryGetValue<string>(
            out var result)
            ? result
            : null;
    }

    private static long? GetJsonLong(
        JsonNode? node,
        string propertyName)
    {
        if (node?[propertyName] is not
            JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<long>(
                out var longValue))
        {
            return longValue;
        }

        if (value.TryGetValue<int>(
                out var intValue))
        {
            return intValue;
        }

        if (value.TryGetValue<string>(
                out var stringValue) &&
            long.TryParse(
                stringValue,
                out var parsedValue))
        {
            return parsedValue;
        }

        return null;
    }
}