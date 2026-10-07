using System.Net.Http.Json;
using System.Text.Json;

using TitanMDM.WindowsAgent.Contracts;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class EnrollmentService
{
    private readonly HttpClient
        _httpClient;

    private readonly DeviceIdentityStore
        _identityStore;

    private readonly WindowsDeviceInfoProvider
        _deviceInfoProvider;

    private readonly ILogger<EnrollmentService>
        _logger;

    public EnrollmentService(
        HttpClient httpClient,
        DeviceIdentityStore identityStore,
        WindowsDeviceInfoProvider deviceInfoProvider,
        ILogger<EnrollmentService> logger)
    {
        _httpClient =
            httpClient;

        _identityStore =
            identityStore;

        _deviceInfoProvider =
            deviceInfoProvider;

        _logger =
            logger;
    }

    public async Task<DeviceIdentity>
        EnrollAsync(
            string enrollmentToken,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                enrollmentToken))
        {
            throw new EnrollmentException(
                "TOKEN_REQUIRED",
                "El token de inscripción es obligatorio.",
                400);
        }

        /*
         * ========================================================
         * EXISTING LOCAL IDENTITY
         * ========================================================
         *
         * Si device.json ya existe, el dispositivo está inscrito.
         *
         * NO se crea otra identidad.
         * NO se consume otro token.
         * NO se genera otro DeviceId.
         * ========================================================
         */

        var existingIdentity =
            await _identityStore
                .LoadAsync(
                    cancellationToken);

        if (existingIdentity is not null)
        {
            _logger.LogInformation(
                "El dispositivo ya posee identidad TitanMDM. DeviceId: {DeviceId}",
                existingIdentity.DeviceId);

            return existingIdentity;
        }

        /*
         * ========================================================
         * LOCAL DEVICE INFORMATION
         * ========================================================
         */

        var device =
            _deviceInfoProvider
                .GetDeviceInformation();

        if (string.IsNullOrWhiteSpace(
                device.DeviceName))
        {
            throw new EnrollmentException(
                "DEVICE_NAME_UNAVAILABLE",
                "No fue posible determinar el nombre del dispositivo.",
                500);
        }

        if (string.IsNullOrWhiteSpace(
                device.SerialNumber))
        {
            throw new EnrollmentException(
                "SERIAL_UNAVAILABLE",
                "No fue posible determinar el número de serie del dispositivo.",
                500);
        }

        var request =
            new AgentEnrollmentRequest(
                EnrollmentToken:
                    enrollmentToken.Trim(),

                DeviceName:
                    device.DeviceName,

                Platform:
                    "Windows",

                SerialNumber:
                    device.SerialNumber,

                Manufacturer:
                    device.Manufacturer,

                Model:
                    device.Model,

                OperatingSystem:
                    device.OperatingSystem,

                OperatingSystemVersion:
                    device.OperatingSystemVersion,

                AgentVersion:
                    device.AgentVersion);

        _logger.LogInformation(
            "Intentando inscripción TitanMDM. DeviceName: {DeviceName}, Serial: {SerialNumber}",
            request.DeviceName,
            request.SerialNumber);

        HttpResponseMessage response;

        try
        {
            response =
                await _httpClient
                    .PostAsJsonAsync(
                        "/api/enrollment/register",
                        request,
                        cancellationToken);
        }
        catch (
            OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
        {
            throw;
        }
        catch (
            TaskCanceledException ex)
        {
            throw new EnrollmentException(
                "SERVER_TIMEOUT",
                $"Tiempo de espera agotado comunicando con TitanMDM: {ex.Message}",
                408);
        }
        catch (
            HttpRequestException ex)
        {
            throw new EnrollmentException(
                "SERVER_UNAVAILABLE",
                $"No fue posible conectar con TitanMDM: {ex.Message}",
                503);
        }

        using (response)
        {
            if (!response
                    .IsSuccessStatusCode)
            {
                var responseBody =
                    await response
                        .Content
                        .ReadAsStringAsync(
                            cancellationToken);

                var error =
                    ParseError(
                        responseBody);

                _logger.LogWarning(
                    "TitanMDM rechazó inscripción. HTTP {StatusCode}. Code: {Code}. Message: {Message}",
                    (int)response.StatusCode,
                    error.Code,
                    error.Message);

                throw new EnrollmentException(
                    error.Code,
                    error.Message,
                    (int)response.StatusCode);
            }

            AgentEnrollmentResponse?
                enrollment;

            try
            {
                enrollment =
                    await response
                        .Content
                        .ReadFromJsonAsync<
                            AgentEnrollmentResponse>(
                                cancellationToken);
            }
            catch (
                JsonException ex)
            {
                throw new EnrollmentException(
                    "INVALID_SERVER_RESPONSE",
                    $"TitanMDM devolvió JSON inválido: {ex.Message}",
                    500);
            }

            if (enrollment is null)
            {
                throw new EnrollmentException(
                    "EMPTY_RESPONSE",
                    "TitanMDM devolvió una respuesta de inscripción vacía.",
                    500);
            }

            if (
                enrollment.DeviceId ==
                Guid.Empty)
            {
                throw new EnrollmentException(
                    "INVALID_DEVICE_ID",
                    "TitanMDM no devolvió un DeviceId válido.",
                    500);
            }

            if (
                enrollment.OrganizationId ==
                Guid.Empty)
            {
                throw new EnrollmentException(
                    "INVALID_ORGANIZATION_ID",
                    "TitanMDM no devolvió un OrganizationId válido.",
                    500);
            }

            if (
                string.IsNullOrWhiteSpace(
                    enrollment.DeviceSecret))
            {
                throw new EnrollmentException(
                    "INVALID_DEVICE_SECRET",
                    "TitanMDM no devolvió DeviceSecret.",
                    500);
            }

            /*
             * ====================================================
             * PERSIST IDENTITY
             * ====================================================
             *
             * DeviceIdentityStore protege DeviceSecret con DPAPI
             * LocalMachine.
             *
             * Resultado:
             *
             * C:\ProgramData\TitanMDM\device.json
             * ====================================================
             */

            var identity =
                new DeviceIdentity(
                    enrollment.DeviceId,
                    enrollment.DeviceSecret);

            await _identityStore
                .SaveAsync(
                    identity,
                    cancellationToken);

            var persistedIdentity =
                await _identityStore
                    .LoadAsync(
                        cancellationToken);

            if (
                persistedIdentity is null
                ||
                persistedIdentity.DeviceId !=
                    enrollment.DeviceId)
            {
                throw new EnrollmentException(
                    "IDENTITY_PERSISTENCE_FAILED",
                    "La inscripción fue aceptada pero no fue posible persistir la identidad local del agente.",
                    500);
            }

            _logger.LogInformation(
                "Inscripción TitanMDM completada. DeviceId: {DeviceId}, OrganizationId: {OrganizationId}",
                enrollment.DeviceId,
                enrollment.OrganizationId);

            return persistedIdentity;
        }
    }

    private static EnrollmentError
        ParseError(
            string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(
                responseBody))
        {
            return new EnrollmentError(
                "ENROLLMENT_FAILED",
                "TitanMDM rechazó la inscripción sin proporcionar detalles.");
        }

        try
        {
            using var document =
                JsonDocument.Parse(
                    responseBody);

            var root =
                document.RootElement;

            var code =
                TryGetString(
                    root,
                    "code");

            var message =
                TryGetString(
                    root,
                    "message");

            return new EnrollmentError(
                string.IsNullOrWhiteSpace(
                    code)
                    ? "ENROLLMENT_FAILED"
                    : code,

                string.IsNullOrWhiteSpace(
                    message)
                    ? responseBody
                    : message);
        }
        catch (JsonException)
        {
            return new EnrollmentError(
                "ENROLLMENT_FAILED",
                responseBody);
        }
    }

    private static string?
        TryGetString(
            JsonElement element,
            string propertyName)
    {
        if (
            !element.TryGetProperty(
                propertyName,
                out var property))
        {
            return null;
        }

        if (
            property.ValueKind !=
            JsonValueKind.String)
        {
            return null;
        }

        return property
            .GetString();
    }

    private sealed record
        EnrollmentError(
            string Code,
            string Message);
}