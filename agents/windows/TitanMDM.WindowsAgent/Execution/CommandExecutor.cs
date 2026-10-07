using System.Text.Json;

using TitanMDM.WindowsAgent.Contracts;
using TitanMDM.WindowsAgent.Services;

namespace TitanMDM.WindowsAgent.Execution;

public sealed class CommandExecutor
    : ICommandExecutor
{
    private readonly ILogger<CommandExecutor>
        _logger;

    private readonly WindowsSoftwarePackageDownloader
    _packageDownloader;

    private readonly WindowsInventoryProvider
        _inventoryProvider;

    private readonly WindowsSecurityProvider
        _securityProvider;

    private readonly WindowsComplianceProvider
        _complianceProvider;

    private readonly WindowsUpdateProvider
        _updateProvider;

    private readonly WindowsActionExecutor
        _actionExecutor;

    private readonly WindowsServiceManager
        _serviceManager;

    private readonly WindowsScriptExecutor
        _scriptExecutor;

    private readonly WindowsSoftwareManager
        _softwareManager;

    private readonly WindowsPolicyExecutor
    _policyExecutor;

    public CommandExecutor(
    ILogger<CommandExecutor> logger,
    WindowsInventoryProvider inventoryProvider,
    WindowsSecurityProvider securityProvider,
    WindowsComplianceProvider complianceProvider,
    WindowsUpdateProvider updateProvider,
    WindowsActionExecutor actionExecutor,
    WindowsServiceManager serviceManager,
    WindowsScriptExecutor scriptExecutor,
    WindowsSoftwareManager softwareManager,
    WindowsSoftwarePackageDownloader packageDownloader,
    WindowsPolicyExecutor policyExecutor)
{
    _logger =
        logger;

    _inventoryProvider =
        inventoryProvider;

    _securityProvider =
        securityProvider;

    _complianceProvider =
        complianceProvider;

    _updateProvider =
        updateProvider;

    _actionExecutor =
        actionExecutor;

    _serviceManager =
        serviceManager;

    _scriptExecutor =
        scriptExecutor;

    _softwareManager =
        softwareManager;

    _packageDownloader =
        packageDownloader;

    _policyExecutor =
    policyExecutor;
}
    public async Task<CommandExecutionResult>
        ExecuteAsync(
            AgentCommand command,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        if (
            command.CommandId ==
            Guid.Empty)
        {
            return Failure(
                "INVALID_COMMAND_ID",
                "El comando no contiene un CommandId válido.");
        }

        if (
            string.IsNullOrWhiteSpace(
                command.CommandType))
        {
            return Failure(
                "INVALID_COMMAND_TYPE",
                "El comando no contiene un tipo válido.");
        }

        if (
            command.ExpiresAtUtc <=
            DateTime.UtcNow)
        {
            return Failure(
                "COMMAND_EXPIRED",
                "El comando expiró antes de poder ejecutarse.");
        }

        var type =
            command.CommandType
                .Trim()
                .ToUpperInvariant();

        _logger.LogInformation(
            "Ejecutando {CommandId} - {CommandType}.",
            command.CommandId,
            type);

        try
        {
            var json =
                type switch
                {
                    /*
                     * ============================================
                     * DIAGNOSTICS
                     * ============================================
                     */

                    "PING" =>
                        ExecutePing(),

                    "DEVICE_INFO" =>
                        Serialize(
                            _inventoryProvider
                                .CollectDevice()),

                    "DEVICE_INVENTORY" =>
                        Serialize(
                            _inventoryProvider
                                .Collect()),

                    "APP_INVENTORY" =>
                        Serialize(
                            _inventoryProvider
                                .CollectApplications()),

                    "PROCESS_INVENTORY" =>
                        Serialize(
                            _inventoryProvider
                                .CollectProcesses()),

                    "SERVICE_INVENTORY" =>
                        Serialize(
                            _inventoryProvider
                                .CollectServices()),

                    "NETWORK_INFO" =>
                        Serialize(
                            _inventoryProvider
                                .CollectNetwork()),

                    /*
                     * ============================================
                     * SECURITY
                     * ============================================
                     */

                    "SECURITY_STATUS" =>
                        Serialize(
                            await _securityProvider
                                .CollectAsync(
                                    cancellationToken)),

                    "COMPLIANCE_CHECK" =>
                        Serialize(
                            await _complianceProvider
                                .EvaluateAsync(
                                    cancellationToken)),

                    /*
                     * ============================================
                     * WINDOWS UPDATE
                     * ============================================
                     */

                    "WINDOWS_UPDATE_STATUS" =>
                        Serialize(
                            await _updateProvider
                                .CollectAsync(
                                    cancellationToken)),

                    "WINDOWS_UPDATE_SCAN" =>
                        await _updateProvider
                            .TriggerScanAsync(
                                cancellationToken),

                    "WINDOWS_UPDATE_INSTALL" =>
                        await _updateProvider
                            .InstallAvailableAsync(
                                command.PayloadJson,
                                cancellationToken),

                    /*
                     * ============================================
                     * DEVICE ACTIONS
                     * ============================================
                     */

                    "LOCK_DEVICE" =>
                        await _actionExecutor
                            .LockDeviceAsync(
                                cancellationToken),

                    "RESTART_DEVICE" =>
                        await _actionExecutor
                            .RestartDeviceAsync(
                                cancellationToken),

                    "SHUTDOWN_DEVICE" =>
                        await _actionExecutor
                            .ShutdownDeviceAsync(
                                cancellationToken),

                    /*
                     * ============================================
                     * PROCESS CONTROL
                     * ============================================
                     */

                    "PROCESS_TERMINATE" =>
                        await ExecuteProcessTerminateAsync(
                            command.PayloadJson,
                            cancellationToken),

                    /*
                     * ============================================
                     * SERVICE CONTROL
                     * ============================================
                     */

                    "SERVICE_START" =>
                        await ExecuteServiceAsync(
                            command.PayloadJson,
                            "start",
                            cancellationToken),

                    "SERVICE_STOP" =>
                        await ExecuteServiceAsync(
                            command.PayloadJson,
                            "stop",
                            cancellationToken),

                    "SERVICE_RESTART" =>
                        await ExecuteServiceAsync(
                            command.PayloadJson,
                            "restart",
                            cancellationToken),

                    /*
                     * ============================================
                     * SCRIPT
                     * ============================================
                     */

                    "SCRIPT_EXECUTE" =>
                        await ExecuteScriptAsync(
                            command.PayloadJson,
                            cancellationToken),
                    
                    /*
                    * ============================================
                    * POLICY
                    * ============================================
                    */

                    "APPLY_POLICY" =>
                        await _policyExecutor
                            .ApplyAsync(
                                command.PayloadJson,
                                cancellationToken),

                    /*
                     * ============================================
                     * SOFTWARE
                     * ============================================
                     */

                    "SOFTWARE_INSTALL" =>
                        await ExecuteSoftwareInstallAsync(
                            command.PayloadJson,
                            cancellationToken),

                    "SOFTWARE_UNINSTALL" =>
                        await ExecuteSoftwareUninstallAsync(
                            command.PayloadJson,
                            cancellationToken),

                    /*
                     * ============================================
                     * UNKNOWN
                     * ============================================
                     */

                    _ =>
                        throw new UnsupportedCommandException(
                            type)
                };

            return Success(
                json);
        }
        catch (
            UnsupportedCommandException ex)
        {
            return Failure(
                "UNSUPPORTED_COMMAND",
                ex.Message);
        }
        catch (
            JsonException ex)
        {
            return Failure(
                "INVALID_PAYLOAD",
                ex.Message);
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
            TimeoutException ex)
        {
            return Failure(
                "COMMAND_TIMEOUT",
                ex.Message);
        }
        catch (
            UnauthorizedAccessException ex)
        {
            return Failure(
                "ACCESS_DENIED",
                ex.Message);
        }
        catch (
            FileNotFoundException ex)
        {
            return Failure(
                "FILE_NOT_FOUND",
                ex.Message);
        }
        catch (
            InvalidOperationException ex)
        {
            return Failure(
                "INVALID_OPERATION",
                ex.Message);
        }
        catch (
            Exception ex)
        {
            _logger.LogError(
                ex,
                "Error ejecutando comando {CommandId}.",
                command.CommandId);

            return Failure(
                "COMMAND_EXECUTION_ERROR",
                ex.Message);
        }
    }

    /*
     * ============================================================
     * PROCESS
     * ============================================================
     */

    private async Task<string>
        ExecuteProcessTerminateAsync(
            string payloadJson,
            CancellationToken cancellationToken)
    {
        var payload =
            Deserialize<
                ProcessActionPayload>(
                    payloadJson);

        if (
            payload.ProcessId <=
            0)
        {
            throw new JsonException(
                "ProcessId debe ser mayor que cero.");
        }

        return await _actionExecutor
            .TerminateProcessAsync(
                payload.ProcessId,
                cancellationToken);
    }

    /*
     * ============================================================
     * SERVICE
     * ============================================================
     */

    private async Task<string>
        ExecuteServiceAsync(
            string payloadJson,
            string action,
            CancellationToken cancellationToken)
    {
        var payload =
            Deserialize<
                ServiceActionPayload>(
                    payloadJson);

        if (
            string.IsNullOrWhiteSpace(
                payload.ServiceName))
        {
            throw new JsonException(
                "ServiceName es obligatorio.");
        }

        return action switch
        {
            "start" =>
                await _serviceManager
                    .StartAsync(
                        payload.ServiceName,
                        cancellationToken),

            "stop" =>
                await _serviceManager
                    .StopAsync(
                        payload.ServiceName,
                        cancellationToken),

            "restart" =>
                await _serviceManager
                    .RestartAsync(
                        payload.ServiceName,
                        cancellationToken),

            _ =>
                throw new InvalidOperationException(
                    "Acción de servicio inválida.")
        };
    }

    /*
     * ============================================================
     * SCRIPT
     * ============================================================
     */

    private async Task<string>
        ExecuteScriptAsync(
            string payloadJson,
            CancellationToken cancellationToken)
    {
        var payload =
            Deserialize<
                ScriptExecutePayload>(
                    payloadJson);

        return await _scriptExecutor
            .ExecuteAsync(
                new WindowsScriptExecutionRequest(
                    payload.ScriptPath,
                    payload.ExpectedSha256,
                    payload.TimeoutSeconds),
                cancellationToken);
    }

    /*
     * ============================================================
     * SOFTWARE INSTALL
     * ============================================================
     */

    private async Task<string>
    ExecuteSoftwareInstallAsync(
        string payloadJson,
        CancellationToken cancellationToken)
{
    var payload =
        Deserialize<
            SoftwareInstallPayload>(
                payloadJson);

    var packagePath =
        payload.PackagePath;

    var downloaded =
        false;

    if (
        string.IsNullOrWhiteSpace(
            packagePath)
        &&
        !string.IsNullOrWhiteSpace(
            payload.DownloadUrl)
        &&
        !string.IsNullOrWhiteSpace(
            payload.FileName))
    {
        packagePath =
            await _packageDownloader
                .DownloadAsync(
                    payload.DownloadUrl,
                    payload.FileName,
                    cancellationToken);

        downloaded =
            true;
    }

    if (
        string.IsNullOrWhiteSpace(
            packagePath))
    {
        throw new InvalidOperationException(
            "No se especificó PackagePath ni DownloadUrl.");
    }

    try
    {
        return await _softwareManager
            .InstallAsync(
                new WindowsSoftwareInstallRequest(
                    packagePath,
                    payload.ExpectedSha256,
                    payload.Arguments,
                    payload.TimeoutSeconds),
                cancellationToken);
    }
    finally
    {
        if (
            downloaded
            &&
            File.Exists(
                packagePath))
        {
            try
            {
                File.Delete(
                    packagePath);
            }
            catch
            {
                // El staging se limpiará posteriormente.
            }
        }
    }
}

    /*
     * ============================================================
     * SOFTWARE UNINSTALL
     * ============================================================
     */

    private async Task<string>
        ExecuteSoftwareUninstallAsync(
            string payloadJson,
            CancellationToken cancellationToken)
    {
        var payload =
            Deserialize<
                SoftwareUninstallPayload>(
                    payloadJson);

        return await _softwareManager
            .UninstallAsync(
                new WindowsSoftwareUninstallRequest(
                    payload.ProductCode,
                    payload.UninstallExecutable,
                    payload.Arguments,
                    payload.TimeoutSeconds),
                cancellationToken);
    }

    /*
     * ============================================================
     * JSON
     * ============================================================
     */

    private static T Deserialize<T>(
        string payloadJson)
    {
        if (
            string.IsNullOrWhiteSpace(
                payloadJson))
        {
            throw new JsonException(
                "El comando requiere PayloadJson.");
        }

        return JsonSerializer
            .Deserialize<T>(
                payloadJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive =
                        true
                })
            ??
            throw new JsonException(
                "PayloadJson no contiene un objeto válido.");
    }

    private static string ExecutePing()
    {
        return Serialize(
            new
            {
                response =
                    "PONG",

                computerName =
                    Environment.MachineName,

                timestampUtc =
                    DateTime.UtcNow
            });
    }

    private static string Serialize<T>(
        T value)
    {
        return JsonSerializer
            .Serialize(
                value);
    }

    /*
     * ============================================================
     * RESULTS
     * ============================================================
     */

    private static CommandExecutionResult
        Success(
            string? resultJson)
    {
        return new CommandExecutionResult(
            true,
            resultJson,
            null,
            null);
    }

    private static CommandExecutionResult
        Failure(
            string errorCode,
            string errorMessage)
    {
        return new CommandExecutionResult(
            false,
            null,
            errorCode,
            errorMessage);
    }

    /*
     * ============================================================
     * PAYLOADS
     * ============================================================
     */

    private sealed record
        ProcessActionPayload(
            int ProcessId);

    private sealed record
        ServiceActionPayload(
            string ServiceName);

    private sealed record
        ScriptExecutePayload(
            string ScriptPath,
            string ExpectedSha256,
            int TimeoutSeconds = 300);

    private sealed record
    SoftwareInstallPayload(
        string? PackagePath = null,
        Guid? PackageId = null,
        string? DownloadUrl = null,
        string? FileName = null,
        string ExpectedSha256 = "",
        string? Arguments = null,
        int TimeoutSeconds = 1800);

    private sealed record
        SoftwareUninstallPayload(
            string? ProductCode = null,
            string? UninstallExecutable = null,
            string? Arguments = null,
            int TimeoutSeconds = 1800);

    /*
     * ============================================================
     * EXCEPTION
     * ============================================================
     */

    private sealed class
        UnsupportedCommandException
        : Exception
    {
        public UnsupportedCommandException(
            string commandType)
            : base(
                $"El comando '{commandType}' no está soportado por esta versión del agente.")
        {
        }
    }
}