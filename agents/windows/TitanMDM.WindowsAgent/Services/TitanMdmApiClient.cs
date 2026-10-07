using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using TitanMDM.WindowsAgent.Contracts;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class TitanMdmApiClient
{
    private readonly HttpClient
        _httpClient;

    private readonly DeviceIdentityStore
        _identityStore;

    private readonly ILogger<TitanMdmApiClient>
        _logger;

    public TitanMdmApiClient(
        HttpClient httpClient,
        DeviceIdentityStore identityStore,
        ILogger<TitanMdmApiClient> logger)
    {
        _httpClient =
            httpClient;

        _identityStore =
            identityStore;

        _logger =
            logger;
    }

    // ============================================================
    // HEARTBEAT
    // ============================================================

    public async Task<HeartbeatResponse>
        SendHeartbeatAsync(
            CancellationToken cancellationToken = default)
    {
        var identity =
            await _identityStore
                .LoadAsync(
                    cancellationToken);

        ValidateIdentity(
            identity);

        var ipAddress =
            GetPrimaryIpAddress();

        using var request =
            await CreateAuthenticatedRequestAsync(
                HttpMethod.Post,
                "/api/device/heartbeat",
                cancellationToken);

        request.Content =
            JsonContent.Create(
                new
                {
                    deviceId =
                        identity!.DeviceId,

                    deviceSecret =
                        identity.DeviceSecret,

                    ipAddress,

                    batteryLevel =
                        (int?)null,

                    agentVersion =
                        typeof(
                            TitanMdmApiClient)
                            .Assembly
                            .GetName()
                            .Version?
                            .ToString(),

                    operatingSystemVersion =
                        Environment
                            .OSVersion
                            .Version
                            .ToString()
                });

        using var response =
            await _httpClient
                .SendAsync(
                    request,
                    cancellationToken);

        await EnsureSuccessfulAsync(
            response,
            "enviar heartbeat",
            cancellationToken);

        var heartbeat =
            await response
                .Content
                .ReadFromJsonAsync<
                    HeartbeatResponse>(
                        cancellationToken:
                            cancellationToken);

        if (
            heartbeat is null)
        {
            throw new InvalidOperationException(
                "TitanMDM devolvió una respuesta de heartbeat vacía.");
        }

        return heartbeat;
    }

    // ============================================================
    // COMMANDS
    // ============================================================

    public async Task<
        IReadOnlyCollection<AgentCommand>>
        GetCommandsAsync(
            CancellationToken cancellationToken = default)
    {
        using var request =
            await CreateAuthenticatedRequestAsync(
                HttpMethod.Get,
                "/api/device/commands",
                cancellationToken);

        using var response =
            await _httpClient
                .SendAsync(
                    request,
                    cancellationToken);

        await EnsureSuccessfulAsync(
            response,
            "consultar comandos",
            cancellationToken);

        var commands =
            await response
                .Content
                .ReadFromJsonAsync<
                    List<AgentCommand>>(
                        cancellationToken:
                            cancellationToken);

        return commands
            ??
            [];
    }

    public async Task MarkDeliveredAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await SendCommandStatusAsync(
            commandId,
            "delivered",
            null,
            cancellationToken);
    }

    public async Task MarkExecutingAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await SendCommandStatusAsync(
            commandId,
            "executing",
            null,
            cancellationToken);
    }

    public async Task MarkSuccessAsync(
        Guid commandId,
        string? resultJson,
        CancellationToken cancellationToken = default)
    {
        var content =
            JsonContent.Create(
                new CommandSuccessRequest(
                    resultJson));

        await SendCommandStatusAsync(
            commandId,
            "success",
            content,
            cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid commandId,
        string errorCode,
        string errorMessage,
        string? resultJson,
        CancellationToken cancellationToken = default)
    {
        var content =
            JsonContent.Create(
                new CommandFailedRequest(
                    errorCode,
                    errorMessage,
                    resultJson));

        await SendCommandStatusAsync(
            commandId,
            "failed",
            content,
            cancellationToken);
    }

    // ============================================================
    // COMMAND STATUS
    // ============================================================

    private async Task SendCommandStatusAsync(
        Guid commandId,
        string status,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (
            commandId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "CommandId no puede estar vacío.",
                nameof(
                    commandId));
        }

        using var request =
            await CreateAuthenticatedRequestAsync(
                HttpMethod.Post,
                $"/api/device/commands/{commandId}/{status}",
                cancellationToken);

        request.Content =
            content;

        using var response =
            await _httpClient
                .SendAsync(
                    request,
                    cancellationToken);

        await EnsureSuccessfulAsync(
            response,
            $"reportar estado '{status}' del comando",
            cancellationToken);
    }

    // ============================================================
    // AUTHENTICATED REQUEST
    // ============================================================

    private async Task<HttpRequestMessage>
        CreateAuthenticatedRequestAsync(
            HttpMethod method,
            string relativeUrl,
            CancellationToken cancellationToken)
    {
        var identity =
            await _identityStore
                .LoadAsync(
                    cancellationToken);

        ValidateIdentity(
            identity);

        var request =
            new HttpRequestMessage(
                method,
                relativeUrl);

        request.Headers
            .TryAddWithoutValidation(
                "X-Titan-Device-Id",
                identity!.DeviceId
                    .ToString());

        request.Headers
            .TryAddWithoutValidation(
                "X-Titan-Device-Secret",
                identity.DeviceSecret);

        return request;
    }

    // ============================================================
    // IDENTITY VALIDATION
    // ============================================================

    private static void ValidateIdentity(
        DeviceIdentity? identity)
    {
        if (
            identity is null)
        {
            throw new InvalidOperationException(
                "El agente todavía no posee una identidad TitanMDM.");
        }

        if (
            identity.DeviceId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "La identidad TitanMDM contiene un DeviceId inválido.");
        }

        if (
            string.IsNullOrWhiteSpace(
                identity.DeviceSecret))
        {
            throw new InvalidOperationException(
                "La identidad TitanMDM no contiene DeviceSecret.");
        }
    }

    // ============================================================
    // PRIMARY IP
    // ============================================================

    private static string?
        GetPrimaryIpAddress()
    {
        try
        {
            var candidates =
                NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(
                        nic =>
                            nic.OperationalStatus ==
                                OperationalStatus.Up
                            &&
                            nic.NetworkInterfaceType !=
                                NetworkInterfaceType.Loopback
                            &&
                            nic.NetworkInterfaceType !=
                                NetworkInterfaceType.Tunnel)
                    .SelectMany(
                        nic =>
                        {
                            try
                            {
                                var properties =
                                    nic.GetIPProperties();

                                var hasGateway =
                                    properties
                                        .GatewayAddresses
                                        .Any(
                                            gateway =>
                                                gateway.Address
                                                    .AddressFamily ==
                                                AddressFamily
                                                    .InterNetwork
                                                &&
                                                !IPAddress
                                                    .IsLoopback(
                                                        gateway.Address));

                                return properties
                                    .UnicastAddresses
                                    .Where(
                                        address =>
                                            address.Address
                                                .AddressFamily ==
                                            AddressFamily
                                                .InterNetwork
                                            &&
                                            !IPAddress
                                                .IsLoopback(
                                                    address.Address))
                                    .Select(
                                        address =>
                                            new
                                            {
                                                Address =
                                                    address.Address,

                                                HasGateway =
                                                    hasGateway
                                            });
                            }
                            catch
                            {
                                return [];
                            }
                        })
                    .OrderByDescending(
                        x =>
                            x.HasGateway)
                    .ToList();

            return candidates
                .FirstOrDefault()
                ?.Address
                .ToString();
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // HTTP ERROR HANDLING
    // ============================================================

    private async Task EnsureSuccessfulAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (
            response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody =
            await response
                .Content
                .ReadAsStringAsync(
                    cancellationToken);

        _logger.LogWarning(
            "TitanMDM rechazó la operación {Operation}. HTTP {StatusCode}.",
            operation,
            (int)response.StatusCode);

        throw new HttpRequestException(
            $"TitanMDM rechazó la operación '{operation}'. " +
            $"HTTP {(int)response.StatusCode}. " +
            responseBody);
    }
}