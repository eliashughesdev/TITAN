using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Audit;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Android;

public sealed class AndroidRemoteActionService
    : IAndroidRemoteActionService
{
    private const string AuditTargetType =
        "AndroidDevice";

    private readonly TitanMdmDbContext _dbContext;
    private readonly AndroidManagementClient _client;
    private readonly IAdministrativeAuditWriter _auditWriter;
    private readonly IScopeAccessService _scopeAccess;
    private readonly AndroidManagementOptions _options;
    private readonly ILogger<AndroidRemoteActionService> _logger;

    public AndroidRemoteActionService(
        TitanMdmDbContext dbContext,
        AndroidManagementClient client,
        IAdministrativeAuditWriter auditWriter,
        IScopeAccessService scopeAccess,
        IOptions<AndroidManagementOptions> options,
        ILogger<AndroidRemoteActionService> logger)
    {
        _dbContext = dbContext;
        _client = client;
        _auditWriter = auditWriter;
        _scopeAccess = scopeAccess;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AndroidRemoteActionResponse> ExecuteAsync(
        Guid organizationId,
        Guid userId,
        Guid deviceId,
        AndroidRemoteActionRequest request,
        string correlationId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new AndroidRemoteActionException(
                "INVALID_ORGANIZATION",
                "La organización no es válida.");
        }

        if (userId == Guid.Empty)
        {
            throw new AndroidRemoteActionException(
                "INVALID_USER",
                "El usuario no es válido.");
        }

        if (deviceId == Guid.Empty)
        {
            throw new AndroidRemoteActionException(
                "INVALID_DEVICE",
                "El dispositivo es obligatorio.");
        }

        ArgumentNullException.ThrowIfNull(request);

        if (!_options.EnableDeviceCommands)
        {
            throw new AndroidRemoteActionException(
                "REMOTE_ACTIONS_DISABLED",
                "Las acciones remotas Android están deshabilitadas.");
        }

        var action =
            ParseAction(request.Action);

        if (!await _scopeAccess.CanAccessDeviceAsync(
                organizationId,
                userId,
                deviceId,
                cancellationToken))
        {
            throw new AndroidRemoteActionException(
                "DEVICE_NOT_FOUND",
                "El dispositivo no existe o no pertenece a la organización.");
        }

        var device =
            await _dbContext
                .Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == deviceId &&
                        x.OrganizationId == organizationId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (device is null)
        {
            throw new AndroidRemoteActionException(
                "DEVICE_NOT_FOUND",
                "El dispositivo no existe o no pertenece a la organización.");
        }

        if (device.Platform != DevicePlatform.Android)
        {
            throw new AndroidRemoteActionException(
                "NOT_ANDROID_DEVICE",
                "La acción remota solo aplica a dispositivos Android.");
        }

        var androidDevice =
            await _dbContext
                .AndroidDevices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.DeviceId == deviceId,
                    cancellationToken);

        if (androidDevice is null ||
            string.IsNullOrWhiteSpace(
                androidDevice.GoogleDeviceName))
        {
            throw new AndroidRemoteActionException(
                "ANDROID_DEVICE_NOT_ENROLLED",
                "El dispositivo todavía no está registrado en Android Enterprise.");
        }

        ValidateActionRequest(
            action,
            request);

        await EnsureNotDuplicateAsync(
            organizationId,
            deviceId,
            action,
            cancellationToken);

        var auditAction =
            BuildAuditAction(action);

        try
        {
            var commandName =
                await IssueCommandAsync(
                    action,
                    androidDevice.GoogleDeviceName,
                    request,
                    cancellationToken);

            await WriteAuditAsync(
                organizationId,
                userId,
                auditAction,
                deviceId,
                "Success",
                correlationId,
                ipAddress,
                cancellationToken);

            return new AndroidRemoteActionResponse(
                deviceId,
                action.ToString(),
                action == AndroidRemoteActionType.Delete
                    ? "DeletionRequested"
                    : "Issued",
                commandName,
                correlationId,
                DateTime.UtcNow);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Android remote action failed. OrganizationId={OrganizationId}, DeviceId={DeviceId}, Action={Action}, ErrorType={ErrorType}",
                organizationId,
                deviceId,
                action,
                exception.GetType().Name);

            await WriteAuditAsync(
                organizationId,
                userId,
                auditAction,
                deviceId,
                "Failure",
                correlationId,
                ipAddress,
                cancellationToken);

            throw new AndroidRemoteActionException(
                "ANDROID_REMOTE_ACTION_FAILED",
                "No fue posible ejecutar la acción remota Android.");
        }
    }

    // ============================================================
    // COMMAND ISSUE
    // ============================================================

    private async Task<string?> IssueCommandAsync(
        AndroidRemoteActionType action,
        string deviceName,
        AndroidRemoteActionRequest request,
        CancellationToken cancellationToken)
    {
        switch (action)
        {
            case AndroidRemoteActionType.Lock:
                return NameOf(
                    await _client.LockDeviceAsync(
                        deviceName,
                        cancellationToken));

            case AndroidRemoteActionType.Reboot:
                return NameOf(
                    await _client.RebootDeviceAsync(
                        deviceName,
                        cancellationToken));

            case AndroidRemoteActionType.ResetPassword:
                return NameOf(
                    await _client.ResetPasswordAsync(
                        deviceName,
                        request.NewPassword!,
                        cancellationToken));

            case AndroidRemoteActionType.StartLostMode:
            {
                var command =
                    new JsonObject
                    {
                        ["type"] =
                            "START_LOST_MODE"
                    };

                if (!string.IsNullOrWhiteSpace(
                        request.LostModeMessage))
                {
                    command["lostModeMessage"] =
                        request.LostModeMessage.Trim();
                }

                if (!string.IsNullOrWhiteSpace(
                        request.LostModePhoneNumber))
                {
                    command["lostModePhoneNumber"] =
                        request.LostModePhoneNumber.Trim();
                }

                return NameOf(
                    await _client.StartLostModeAsync(
                        deviceName,
                        command,
                        cancellationToken));
            }

            case AndroidRemoteActionType.StopLostMode:
                return NameOf(
                    await _client.StopLostModeAsync(
                        deviceName,
                        cancellationToken));

            case AndroidRemoteActionType.Wipe:
                return NameOf(
                    await _client.WipeDeviceAsync(
                        deviceName,
                        null,
                        cancellationToken));

            case AndroidRemoteActionType.Delete:
                await _client.DeleteDeviceAsync(
                    deviceName,
                    cancellationToken:
                        cancellationToken);

                return null;

            default:
                throw new AndroidRemoteActionException(
                    "UNSUPPORTED_ACTION",
                    "La acción remota solicitada no está soportada.");
        }
    }

    private static string? NameOf(
        JsonNode? node)
    {
        return node?["name"]?
            .GetValue<string>();
    }

    // ============================================================
    // DUPLICATE PROTECTION
    // ============================================================

    private async Task EnsureNotDuplicateAsync(
        Guid organizationId,
        Guid deviceId,
        AndroidRemoteActionType action,
        CancellationToken cancellationToken)
    {
        var window =
            _options.RemoteActionDeduplicationSeconds;

        if (window <= 0)
        {
            return;
        }

        var threshold =
            DateTime.UtcNow.AddSeconds(-window);

        var targetId =
            deviceId.ToString();

        var auditAction =
            BuildAuditAction(action);

        var duplicate =
            await _dbContext
                .AdministrativeAuditEvents
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId &&
                        x.Action == auditAction &&
                        x.TargetType ==
                            AuditTargetType &&
                        x.TargetId == targetId &&
                        x.Result == "Success" &&
                        x.CreatedAtUtc >= threshold,
                    cancellationToken);

        if (duplicate)
        {
            throw new AndroidRemoteActionException(
                "DUPLICATE_REMOTE_ACTION",
                "Ya se envió esta misma acción hace instantes. Espere antes de reintentar.");
        }
    }

    // ============================================================
    // AUDIT
    // ============================================================

    private async Task WriteAuditAsync(
        Guid organizationId,
        Guid userId,
        string auditAction,
        Guid deviceId,
        string result,
        string correlationId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditWriter.WriteAsync(
                new AdministrativeAuditEntry(
                    organizationId,
                    userId,
                    auditAction,
                    AuditTargetType,
                    deviceId.ToString(),
                    result,
                    correlationId,
                    ipAddress),
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "No fue posible persistir la auditoría de acción remota Android. Action={Action}, DeviceId={DeviceId}",
                auditAction,
                deviceId);
        }
    }

    private static string BuildAuditAction(
        AndroidRemoteActionType action)
    {
        return
            $"Android.RemoteAction.{action}";
    }

    // ============================================================
    // PARSING
    // ============================================================

    private static void ValidateActionRequest(
        AndroidRemoteActionType action,
        AndroidRemoteActionRequest request)
    {
        if (action == AndroidRemoteActionType.ResetPassword &&
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new AndroidRemoteActionException(
                "PASSWORD_REQUIRED",
                "Debe indicar la nueva contraseña.");
        }
    }

    private static AndroidRemoteActionType ParseAction(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AndroidRemoteActionException(
                "ACTION_REQUIRED",
                "La acción remota es obligatoria.");
        }

        var normalized =
            new string(
                value
                    .Where(
                        character =>
                            char.IsLetterOrDigit(
                                character))
                    .ToArray());

        if (!Enum.TryParse<AndroidRemoteActionType>(
                normalized,
                true,
                out var action))
        {
            throw new AndroidRemoteActionException(
                "UNSUPPORTED_ACTION",
                "La acción remota solicitada no está soportada.");
        }

        return action;
    }
}
