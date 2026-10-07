using Microsoft.Extensions.Options;

using TitanMDM.WindowsAgent.Configuration;
using TitanMDM.WindowsAgent.Contracts;
using TitanMDM.WindowsAgent.Execution;
using TitanMDM.WindowsAgent.Services;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent;

public sealed class Worker
    : BackgroundService
{
    private readonly ILogger<Worker>
        _logger;

    private readonly TitanMdmApiClient
        _apiClient;

    private readonly EnrollmentService
        _enrollmentService;

    private readonly ICommandExecutor
        _commandExecutor;

    private readonly DeviceIdentityStore
        _identityStore;

    private readonly AgentRuntimeSettingsStore
        _runtimeSettingsStore;

    private readonly AgentLifecycleCoordinator
        _lifecycle;

    private readonly AgentRetryPolicy
        _retryPolicy;

    private readonly AgentOptions
        _options;

    public Worker(
        ILogger<Worker> logger,
        TitanMdmApiClient apiClient,
        EnrollmentService enrollmentService,
        ICommandExecutor commandExecutor,
        DeviceIdentityStore identityStore,
        AgentRuntimeSettingsStore runtimeSettingsStore,
        AgentLifecycleCoordinator lifecycle,
        AgentRetryPolicy retryPolicy,
        IOptions<AgentOptions> options)
    {
        _logger =
            logger;

        _apiClient =
            apiClient;

        _enrollmentService =
            enrollmentService;

        _commandExecutor =
            commandExecutor;

        _identityStore =
            identityStore;

        _runtimeSettingsStore =
            runtimeSettingsStore;

        _lifecycle =
            lifecycle;

        _retryPolicy =
            retryPolicy;

        _options =
            options.Value;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _lifecycle.MarkStarting();

        _logger.LogInformation(
            "TitanMDM Windows Agent iniciado.");

        _logger.LogInformation(
            "TitanMDM ServerUrl activo: {ServerUrl}",
            _options.ServerUrl);

        _logger.LogInformation(
            "Archivo de identidad: {IdentityPath}",
            _identityStore
                .GetIdentityFilePath());

        _logger.LogInformation(
            "Archivo de configuración: {SettingsPath}",
            _runtimeSettingsStore
                .GetSettingsFilePath());

        var consecutiveFailures =
            0;

        while (
            !stoppingToken
                .IsCancellationRequested)
        {
            try
            {
                var identity =
                    await EnsureEnrollmentAsync(
                        stoppingToken);

                if (identity is null)
                {
                    consecutiveFailures =
                        0;

                    await DelayAsync(
                        stoppingToken);

                    continue;
                }

                /*
                 * =================================================
                 * COMMAND POLLING
                 * =================================================
                 *
                 * Retry solamente para transporte.
                 *
                 * NO se reintenta automáticamente la ejecución
                 * del comando porque acciones como:
                 *
                 * - shutdown
                 * - restart
                 * - uninstall
                 * - lock
                 *
                 * no deben ejecutarse múltiples veces debido a
                 * un problema de red.
                 * =================================================
                 */

                var commands =
                    await _retryPolicy
                        .ExecuteAsync(
                            token =>
                                _apiClient
                                    .GetCommandsAsync(
                                        token),

                            "consultar comandos",

                            stoppingToken,

                            maximumAttempts:
                                3);

                consecutiveFailures =
                    0;

                _lifecycle.MarkHealthy();

                foreach (
                    var command
                    in commands)
                {
                    if (
                        stoppingToken
                            .IsCancellationRequested)
                    {
                        break;
                    }

                    await ProcessCommandAsync(
                        command,
                        stoppingToken);
                }
            }
            catch (
                OperationCanceledException)
                when (
                    stoppingToken
                        .IsCancellationRequested)
            {
                break;
            }
            catch (
                EnrollmentException ex)
            {
                consecutiveFailures++;

                HandleEnrollmentException(
                    ex);

                _logger.LogWarning(
                    ex,
                    "TitanMDM no pudo completar el enrolamiento. Code={Code}, StatusCode={StatusCode}.",
                    ex.Code,
                    ex.StatusCode);
            }
            catch (
                HttpRequestException ex)
            {
                consecutiveFailures++;

                _lifecycle
                    .MarkServerUnavailable(
                        ex.Message);

                _logger.LogWarning(
                    ex,
                    "TitanMDM API no está disponible actualmente. Fallos consecutivos={FailureCount}.",
                    consecutiveFailures);
            }
            catch (
                TaskCanceledException ex)
                when (
                    !stoppingToken
                        .IsCancellationRequested)
            {
                consecutiveFailures++;

                _lifecycle
                    .MarkServerUnavailable(
                        "Timeout comunicando con TitanMDM.");

                _logger.LogWarning(
                    ex,
                    "Timeout comunicando con TitanMDM. Fallos consecutivos={FailureCount}.",
                    consecutiveFailures);
            }
            catch (
                Exception ex)
            {
                consecutiveFailures++;

                _logger.LogError(
                    ex,
                    "Error durante el ciclo principal del agente TitanMDM.");
            }

            await DelayWithBackoffAsync(
                consecutiveFailures,
                stoppingToken);
        }

        _lifecycle.MarkStopping();

        _logger.LogInformation(
            "TitanMDM Windows Agent detenido.");
    }

    // ============================================================
    // ENROLLMENT
    // ============================================================

    private async Task<DeviceIdentity?>
        EnsureEnrollmentAsync(
            CancellationToken cancellationToken)
    {
        var identity =
            await _identityStore
                .LoadAsync(
                    cancellationToken);

        /*
         * ========================================================
         * EXISTING IDENTITY
         * ========================================================
         *
         * Si device.json ya existe:
         *
         * - jamás volver a enrolar
         * - jamás consumir otro token
         * - limpiar un token residual de reinstall/repair
         * ========================================================
         */

        if (
            identity is
            not null)
        {
            _lifecycle
                .MarkEnrolled();

            await ClearResidualEnrollmentTokenAsync(
                cancellationToken);

            return identity;
        }

        /*
         * No existe identidad.
         */

        if (
            string.IsNullOrWhiteSpace(
                _options.EnrollmentToken))
        {
            _lifecycle
                .MarkEnrollmentRequired(
                    "El dispositivo no posee device.json ni EnrollmentToken.");

            _logger.LogWarning(
                "El equipo todavía no está inscrito y no existe EnrollmentToken.");

            return null;
        }

        _lifecycle.MarkEnrolling();

        _logger.LogInformation(
            "Iniciando inscripción del dispositivo contra {ServerUrl}.",
            _options.ServerUrl);

        try
        {
            identity =
                await _enrollmentService
                    .EnrollAsync(
                        _options.EnrollmentToken,
                        cancellationToken);
        }
        catch (
            EnrollmentException)
        {
            throw;
        }

        _lifecycle.MarkEnrolled();

        _logger.LogInformation(
            "Dispositivo inscrito correctamente. DeviceId={DeviceId}",
            identity.DeviceId);

        await ClearResidualEnrollmentTokenAsync(
            cancellationToken);

        return identity;
    }

    private async Task
        ClearResidualEnrollmentTokenAsync(
            CancellationToken cancellationToken)
    {
        try
        {
            var settings =
                await _runtimeSettingsStore
                    .LoadAsync(
                        cancellationToken);

            if (
                settings is null
                ||
                string.IsNullOrWhiteSpace(
                    settings.EnrollmentToken))
            {
                _options.EnrollmentToken =
                    null;

                return;
            }

            await _runtimeSettingsStore
                .ClearEnrollmentTokenAsync(
                    cancellationToken);

            _options.EnrollmentToken =
                null;

            _logger.LogInformation(
                "EnrollmentToken residual eliminado de agentsettings.json.");
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
            Exception ex)
        {
            /*
             * device.json ya es la credencial definitiva.
             *
             * No invalidamos un equipo enrolado porque falle
             * la limpieza de un token temporal.
             */

            _logger.LogWarning(
                ex,
                "No fue posible limpiar EnrollmentToken residual.");
        }
    }

    private void HandleEnrollmentException(
        EnrollmentException exception)
    {
        switch (
            exception.Code
                .ToUpperInvariant())
        {
            case "TOKEN_EXPIRED":
                _lifecycle
                    .MarkTokenExpired(
                        exception.Message);

                break;

            case "INVALID_TOKEN":

            case "TOKEN_REQUIRED":

            case "TOKEN_REVOKED":

            case "TOKEN_EXHAUSTED":

            case "TOKEN_NOT_ACTIVE":

            case "PLATFORM_MISMATCH":
                _lifecycle
                    .MarkTokenInvalid(
                        exception.Code,
                        exception.Message);

                break;

            case "SERVER_UNAVAILABLE":

            case "SERVER_TIMEOUT":
                _lifecycle
                    .MarkServerUnavailable(
                        exception.Message);

                break;

            default:

                if (
                    exception
                        .IsRecoveryError)
                {
                    _lifecycle
                        .MarkRecoveryRequired(
                            exception.Code,
                            exception.Message);

                    break;
                }

                _lifecycle
                    .MarkRecoveryRequired(
                        exception.Code,
                        exception.Message);

                break;
        }
    }

    // ============================================================
    // COMMAND EXECUTION
    // ============================================================

    private async Task ProcessCommandAsync(
        AgentCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Comando recibido: {CommandId} - {CommandType}",
                command.CommandId,
                command.CommandType);

            /*
             * DELIVERY
             */

            await _retryPolicy
                .ExecuteAsync(
                    token =>
                        _apiClient
                            .MarkDeliveredAsync(
                                command.CommandId,
                                token),

                    $"marcar delivered {command.CommandId}",

                    cancellationToken,

                    maximumAttempts:
                        3);

            /*
             * EXECUTING
             */

            await _retryPolicy
                .ExecuteAsync(
                    token =>
                        _apiClient
                            .MarkExecutingAsync(
                                command.CommandId,
                                token),

                    $"marcar executing {command.CommandId}",

                    cancellationToken,

                    maximumAttempts:
                        3);

            /*
             * Ejecutar EXACTAMENTE UNA VEZ.
             */

            var result =
                await _commandExecutor
                    .ExecuteAsync(
                        command,
                        cancellationToken);

            if (
                result.Success)
            {
                await ReportSuccessAsync(
                    command,
                    result,
                    cancellationToken);

                return;
            }

            await ReportFailureAsync(
                command,
                result.ErrorCode
                    ??
                    "COMMAND_FAILED",
                result.ErrorMessage
                    ??
                    "El comando no pudo ejecutarse.",
                result.ResultJson,
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
            Exception ex)
        {
            _logger.LogError(
                ex,
                "Error procesando comando {CommandId}.",
                command.CommandId);

            /*
             * Intentamos reportar el error.
             *
             * NO se vuelve a ejecutar el comando.
             */

            try
            {
                await ReportFailureAsync(
                    command,
                    "AGENT_EXCEPTION",
                    ex.Message,
                    null,
                    cancellationToken);
            }
            catch (
                Exception reportException)
            {
                _logger.LogError(
                    reportException,
                    "No se pudo reportar el fallo del comando {CommandId}.",
                    command.CommandId);
            }
        }
    }

    private async Task ReportSuccessAsync(
        AgentCommand command,
        CommandExecutionResult result,
        CancellationToken cancellationToken)
    {
        await _retryPolicy
            .ExecuteAsync(
                token =>
                    _apiClient
                        .MarkSuccessAsync(
                            command.CommandId,
                            result.ResultJson,
                            token),

                $"reportar success {command.CommandId}",

                cancellationToken,

                maximumAttempts:
                    3);

        _logger.LogInformation(
            "Comando completado: {CommandId} - {CommandType}.",
            command.CommandId,
            command.CommandType);
    }

    private async Task ReportFailureAsync(
        AgentCommand command,
        string errorCode,
        string errorMessage,
        string? resultJson,
        CancellationToken cancellationToken)
    {
        await _retryPolicy
            .ExecuteAsync(
                token =>
                    _apiClient
                        .MarkFailedAsync(
                            command.CommandId,
                            errorCode,
                            errorMessage,
                            resultJson,
                            token),

                $"reportar failed {command.CommandId}",

                cancellationToken,

                maximumAttempts:
                    3);

        _logger.LogWarning(
            "Comando fallido: {CommandId} - {CommandType} - {ErrorCode}.",
            command.CommandId,
            command.CommandType,
            errorCode);
    }

    // ============================================================
    // DELAYS
    // ============================================================

    private async Task DelayAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        _options
                            .CommandPollingIntervalSeconds,
                        5,
                        300)),
                cancellationToken);
        }
        catch (
            OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
        {
        }
    }

    private async Task DelayWithBackoffAsync(
        int consecutiveFailures,
        CancellationToken cancellationToken)
    {
        if (
            consecutiveFailures <=
            0)
        {
            await DelayAsync(
                cancellationToken);

            return;
        }

        var exponent =
            Math.Min(
                consecutiveFailures - 1,
                6);

        var seconds =
            Math.Min(
                300,
                5 * (1 << exponent));

        var jitterMilliseconds =
            Random.Shared.Next(
                250,
                1500);

        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(
                    seconds)
                +
                TimeSpan.FromMilliseconds(
                    jitterMilliseconds),

                cancellationToken);
        }
        catch (
            OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
        {
        }
    }
}