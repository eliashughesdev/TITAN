using System.Net.Http.Json;
using System.Text.Json;
using TitanMDM.WindowsAgent.Contracts;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class RemoteSupportApiClient
{
    private readonly HttpClient
        _httpClient;

    private readonly DeviceIdentityStore
        _identityStore;

    private readonly ILogger<
        RemoteSupportApiClient> _logger;

    public RemoteSupportApiClient(
        HttpClient httpClient,
        DeviceIdentityStore identityStore,
        ILogger<RemoteSupportApiClient> logger)
    {
        _httpClient =
            httpClient;

        _identityStore =
            identityStore;

        _logger =
            logger;
    }

    public async Task<
        IReadOnlyList<RemoteSupportRequest>>
        GetPendingAsync(
            CancellationToken cancellationToken = default)
    {
        using var request =
            await CreateRequestAsync(
                HttpMethod.Get,
                "api/device/remote-support/pending",
                cancellationToken);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var sessions =
            await response.Content
                .ReadFromJsonAsync<
                    List<RemoteSupportRequest>>(
                    cancellationToken:
                        cancellationToken);

        return sessions
            ?? [];
    }

    public Task MarkConnectingAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return PostAsync(
            $"api/device/remote-support/{sessionId}/connecting",
            null,
            cancellationToken);
    }

    public Task MarkConnectedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return PostAsync(
            $"api/device/remote-support/{sessionId}/connected",
            null,
            cancellationToken);
    }

    public Task MarkCompletedAsync(
        Guid sessionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        return PostAsync(
            $"api/device/remote-support/{sessionId}/completed",
            new
            {
                reason
            },
            cancellationToken);
    }

    public Task MarkFailedAsync(
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return PostAsync(
            $"api/device/remote-support/{sessionId}/failed",
            new
            {
                reason
            },
            cancellationToken);
    }

    private async Task PostAsync(
        string uri,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request =
            await CreateRequestAsync(
                HttpMethod.Post,
                uri,
                cancellationToken);

        if (payload is not null)
        {
            request.Content =
                JsonContent.Create(
                    payload);
        }

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken);

            _logger.LogWarning(
                "Remote Support API error {StatusCode}: {Body}",
                response.StatusCode,
                body);
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage>
        CreateRequestAsync(
            HttpMethod method,
            string uri,
            CancellationToken cancellationToken)
    {
        var identity =
            await _identityStore
                .LoadAsync(
                    cancellationToken);

        if (identity is null)
        {
            throw new InvalidOperationException(
                "El agente todavía no posee identidad TitanMDM.");
        }

        var request =
            new HttpRequestMessage(
                method,
                uri);

        request.Headers.Add(
            "X-Titan-Device-Id",
            identity.DeviceId.ToString());

        request.Headers.Add(
            "X-Titan-Device-Secret",
            identity.DeviceSecret);

        return request;
    }

public async Task<RemoteHostBootstrap>
    CreateHostBootstrapAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
{
    using var request =
        await CreateRequestAsync(
            HttpMethod.Post,
            $"api/device/remote-support/{sessionId}/host-bootstrap",
            cancellationToken);

    using var response =
        await _httpClient.SendAsync(
            request,
            cancellationToken);

    response.EnsureSuccessStatusCode();

    var bootstrap =
        await response.Content
            .ReadFromJsonAsync<
                RemoteHostBootstrap>(
                cancellationToken:
                    cancellationToken);

    return bootstrap
        ?? throw new InvalidOperationException(
            "TitanMDM no devolvió la configuración del Remote Host.");
}
}