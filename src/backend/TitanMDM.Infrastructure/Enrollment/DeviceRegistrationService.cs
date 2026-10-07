using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Automation;
using TitanMDM.Application.Enrollment.DeviceRegistration;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Enrollment;

public sealed class DeviceRegistrationService
    : IDeviceRegistrationService
{
    private static readonly TimeSpan
        MinimumRecoveryOfflineWindow =
            TimeSpan.FromMinutes(3);

    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IAutomationEventDispatcher
        _automation;

    public DeviceRegistrationService(
        TitanMdmDbContext dbContext,
        IAutomationEventDispatcher automation)
    {
        _dbContext =
            dbContext;

        _automation =
            automation;
    }

    public async Task<RegisterDeviceResultDto>
        RegisterAsync(
            RegisterDeviceRequest request,
            CancellationToken cancellationToken = default)
    {
        ValidateRequest(
            request);

        var normalizedToken =
            request.EnrollmentToken
                .Trim();

        var tokenHash =
            ComputeSha256(
                normalizedToken);

        var executionStrategy =
            _dbContext
                .Database
                .CreateExecutionStrategy();

        return await executionStrategy
            .ExecuteAsync(
                async () =>
                {
                    await using var transaction =
                        await _dbContext
                            .Database
                            .BeginTransactionAsync(
                                cancellationToken);

                    try
                    {
                        var enrollmentToken =
                            await _dbContext
                                .EnrollmentTokens
                                .SingleOrDefaultAsync(
                                    x =>
                                        x.TokenHash ==
                                        tokenHash,
                                    cancellationToken);

                        if (
                            enrollmentToken is null)
                        {
                            throw new DeviceRegistrationException(
                                "INVALID_TOKEN",
                                "La credencial de inscripción no es válida.");
                        }

                        ValidateEnrollmentToken(
                            enrollmentToken,
                            request.Platform);

                        var serialNumber =
                            request.SerialNumber
                                .Trim();

                        var existingDevice =
                            await _dbContext
                                .Devices
                                .FirstOrDefaultAsync(
                                    x =>
                                        x.OrganizationId ==
                                            enrollmentToken.OrganizationId
                                        &&
                                        x.SerialNumber ==
                                            serialNumber
                                        &&
                                        !x.IsDeleted,
                                    cancellationToken);

                        if (
                            existingDevice is not null)
                        {
                            var result =
                                await RecoverExistingDeviceAsync(
                                    existingDevice,
                                    enrollmentToken,
                                    request,
                                    cancellationToken);

                            await transaction
                                .CommitAsync(
                                    cancellationToken);

                            return result;
                        }

                        var resultNew =
                            await RegisterNewDeviceAsync(
                                enrollmentToken,
                                request,
                                cancellationToken);

                        await transaction
                            .CommitAsync(
                                cancellationToken);

                        return resultNew;
                    }
                    catch
                    {
                        await transaction
                            .RollbackAsync(
                                cancellationToken);

                        throw;
                    }
                });
    }

    // ============================================================
    // NEW DEVICE
    // ============================================================

    private async Task<RegisterDeviceResultDto>
        RegisterNewDeviceAsync(
            EnrollmentToken enrollmentToken,
            RegisterDeviceRequest request,
            CancellationToken cancellationToken)
    {
        var device =
            new Device(
                enrollmentToken.OrganizationId,
                request.DeviceName.Trim(),
                enrollmentToken.Platform,
                request.SerialNumber.Trim());

        UpdateDeviceFromEnrollment(
            device,
            request);

        device.CompleteEnrollment();

        var rawSecret =
            GenerateDeviceSecret();

        var credential =
            new DeviceCredential(
                device.Id,
                device.OrganizationId,
                ComputeSha256(
                    rawSecret));

        enrollmentToken.RegisterUse();

        _dbContext
            .Devices
            .Add(
                device);

        _dbContext
            .DeviceCredentials
            .Add(
                credential);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        await DispatchEnrollmentEventAsync(
            device,
            "DeviceEnrolled",
            cancellationToken);

        return BuildResult(
            device,
            rawSecret);
    }

    // ============================================================
    // EXISTING DEVICE RECOVERY
    // ============================================================

    private async Task<RegisterDeviceResultDto>
        RecoverExistingDeviceAsync(
            Device device,
            EnrollmentToken enrollmentToken,
            RegisterDeviceRequest request,
            CancellationToken cancellationToken)
    {
        /*
         * Recuperación segura:
         *
         * - mismo OrganizationId
         * - misma plataforma
         * - mismo serial
         * - token válido nuevo
         * - dispositivo suficientemente offline
         *
         * Esto evita que un token válido pueda rotar inmediatamente
         * las credenciales de un equipo que sigue comunicándose.
         */

        if (
            device.OrganizationId !=
                enrollmentToken.OrganizationId)
        {
            throw new DeviceRegistrationException(
                "RECOVERY_ORGANIZATION_MISMATCH",
                "El dispositivo pertenece a otra organización.");
        }

        if (
            device.Platform !=
                enrollmentToken.Platform)
        {
            throw new DeviceRegistrationException(
                "RECOVERY_PLATFORM_MISMATCH",
                "La plataforma del dispositivo existente no coincide.");
        }

        var deviceNameMatches =
            string.Equals(
                device.DeviceName,
                request.DeviceName.Trim(),
                StringComparison.OrdinalIgnoreCase);

        if (
            !deviceNameMatches)
        {
            throw new DeviceRegistrationException(
                "RECOVERY_DEVICE_NAME_MISMATCH",
                "El nombre del equipo no coincide con el dispositivo existente.");
        }

        if (
            device.LastSeenAtUtc.HasValue
            &&
            DateTime.UtcNow -
                device.LastSeenAtUtc.Value <
                MinimumRecoveryOfflineWindow)
        {
            throw new DeviceRegistrationException(
                "RECOVERY_DEVICE_STILL_ACTIVE",
                "El dispositivo existente todavía se considera activo. Espere unos minutos o retire primero su identidad anterior.");
        }

        /*
         * Revocar TODAS las credenciales activas anteriores.
         */

        var activeCredentials =
            await _dbContext
                .DeviceCredentials
                .Where(
                    x =>
                        x.DeviceId ==
                            device.Id
                        &&
                        x.IsActive)
                .ToListAsync(
                    cancellationToken);

        foreach (
            var credential
            in activeCredentials)
        {
            credential.Revoke();
        }

        /*
         * Crear credencial nueva.
         */

        var newDeviceSecret =
            GenerateDeviceSecret();

        var newCredential =
            new DeviceCredential(
                device.Id,
                device.OrganizationId,
                ComputeSha256(
                    newDeviceSecret));

        _dbContext
            .DeviceCredentials
            .Add(
                newCredential);

        UpdateDeviceFromEnrollment(
            device,
            request);

        /*
         * Mantener el mismo DeviceId.
         *
         * CompleteEnrollment reactiva el equipo y actualiza LastSeen.
         */

        device.CompleteEnrollment();

        /*
         * Consumimos el token porque una recuperación también es
         * una operación privilegiada de inscripción.
         */

        enrollmentToken.RegisterUse();

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        await DispatchEnrollmentEventAsync(
            device,
            "DeviceReEnrolled",
            cancellationToken);

        return BuildResult(
            device,
            newDeviceSecret);
    }

    // ============================================================
    // UPDATE DEVICE
    // ============================================================

    private static void
        UpdateDeviceFromEnrollment(
            Device device,
            RegisterDeviceRequest request)
    {
        device.UpdateInventory(
            Normalize(
                request.Manufacturer),

            Normalize(
                request.Model),

            Normalize(
                request.OperatingSystem),

            Normalize(
                request.OperatingSystemVersion),

            Normalize(
                request.AgentVersion),

            null,

            Normalize(
                request.MacAddress));

        device.RegisterHeartbeat(
            Normalize(
                request.IpAddress),

            null);
    }

    // ============================================================
    // RESULT
    // ============================================================

    private static RegisterDeviceResultDto
        BuildResult(
            Device device,
            string deviceSecret)
    {
        return new RegisterDeviceResultDto(
            device.Id,
            device.OrganizationId,
            device.DeviceName,
            device.Platform.ToString(),
            device.Status.ToString(),
            device.ComplianceStatus.ToString(),
            device.IsManaged,
            device.EnrolledAtUtc
                ??
                DateTime.UtcNow,
            deviceSecret);
    }

    // ============================================================
    // AUTOMATION
    // ============================================================

    private async Task
        DispatchEnrollmentEventAsync(
            Device device,
            string eventName,
            CancellationToken cancellationToken)
    {
        await _automation
            .DispatchAsync(
                device.OrganizationId,
                device.Id,
                eventName,
                new
                {
                    platform =
                        device.Platform
                            .ToString(),

                    deviceName =
                        device.DeviceName,

                    serialNumber =
                        device.SerialNumber,

                    manufacturer =
                        device.Manufacturer,

                    model =
                        device.Model,

                    operatingSystem =
                        device.OperatingSystem,

                    operatingSystemVersion =
                        device.OperatingSystemVersion,

                    agentVersion =
                        device.AgentVersion,

                    enrolledAtUtc =
                        device.EnrolledAtUtc,

                    lastSeenAtUtc =
                        device.LastSeenAtUtc
                },
                cancellationToken:
                    cancellationToken);
    }

    // ============================================================
    // VALIDATION
    // ============================================================

    private static void
        ValidateRequest(
            RegisterDeviceRequest request)
    {
        if (
            string.IsNullOrWhiteSpace(
                request.EnrollmentToken))
        {
            throw new DeviceRegistrationException(
                "INVALID_REQUEST",
                "EnrollmentToken es obligatorio.");
        }

        if (
            string.IsNullOrWhiteSpace(
                request.DeviceName))
        {
            throw new DeviceRegistrationException(
                "INVALID_REQUEST",
                "DeviceName es obligatorio.");
        }

        if (
            string.IsNullOrWhiteSpace(
                request.SerialNumber))
        {
            throw new DeviceRegistrationException(
                "INVALID_REQUEST",
                "SerialNumber es obligatorio.");
        }

        if (
            !Enum.TryParse<DevicePlatform>(
                request.Platform,
                true,
                out var platform)
            ||
            platform ==
                DevicePlatform.Unknown)
        {
            throw new DeviceRegistrationException(
                "INVALID_PLATFORM",
                "La plataforma debe ser Windows o Android.");
        }
    }

    private static void
        ValidateEnrollmentToken(
            EnrollmentToken token,
            string requestedPlatform)
    {
        if (
            token.Status ==
                EnrollmentStatus.Revoked)
        {
            throw new DeviceRegistrationException(
                "TOKEN_REVOKED",
                "La credencial de inscripción fue revocada.");
        }

        if (
            DateTime.UtcNow >=
                token.ExpiresAtUtc)
        {
            throw new DeviceRegistrationException(
                "TOKEN_EXPIRED",
                "La credencial de inscripción ha expirado.");
        }

        if (
            token.Status ==
                EnrollmentStatus.Completed
            ||
            token.UsedCount >=
                token.MaxUses)
        {
            throw new DeviceRegistrationException(
                "TOKEN_EXHAUSTED",
                "La credencial alcanzó el máximo de usos permitidos.");
        }

        if (
            token.Status !=
                EnrollmentStatus.Active)
        {
            throw new DeviceRegistrationException(
                "TOKEN_NOT_ACTIVE",
                "La credencial de inscripción no está activa.");
        }

        if (
            !Enum.TryParse<DevicePlatform>(
                requestedPlatform,
                true,
                out var platform)
            ||
            platform !=
                token.Platform)
        {
            throw new DeviceRegistrationException(
                "PLATFORM_MISMATCH",
                $"La credencial pertenece a {token.Platform}.");
        }
    }

    // ============================================================
    // CRYPTO
    // ============================================================

    private static string
        GenerateDeviceSecret()
    {
        return Convert
            .ToHexString(
                RandomNumberGenerator
                    .GetBytes(
                        32));
    }

    private static string
        ComputeSha256(
            string value)
    {
        return Convert
            .ToHexString(
                SHA256.HashData(
                    Encoding.UTF8
                        .GetBytes(
                            value)));
    }

    // ============================================================
    // NORMALIZE
    // ============================================================

    private static string?
        Normalize(
            string? value)
    {
        return string
            .IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }
}