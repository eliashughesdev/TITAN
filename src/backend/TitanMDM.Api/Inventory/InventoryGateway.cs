using Microsoft.Extensions.Options;

namespace TitanMDM.Api.Inventory;

public sealed class InventoryGateway
    : IInventoryGateway
{
    private readonly HttpClient
        _httpClient;

    private readonly InventoryOptions
        _options;

    private readonly ILogger<InventoryGateway>
        _logger;

    public InventoryGateway(
        HttpClient httpClient,
        IOptions<InventoryOptions> options,
        ILogger<InventoryGateway> logger)
    {
        _httpClient =
            httpClient
            ??
            throw new ArgumentNullException(
                nameof(httpClient));

        _options =
            options?.Value
            ??
            throw new ArgumentNullException(
                nameof(options));

        _logger =
            logger
            ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    public async Task<InventoryGatewayResponse>
        SendAsync(
            InventoryGatewayRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (!_options.IsConfigured)
        {
            throw new InventoryNotConfiguredException(
                "La integración interna de Inventario no está configurada.");
        }

        var baseUri =
            _options.BaseUri;

        if (string.IsNullOrWhiteSpace(
            baseUri))
        {
            throw new InvalidOperationException(
                "La base URI de Inventario no está configurada.");
        }

        var integrationKey =
            _options.IntegrationKey;

        if (string.IsNullOrWhiteSpace(
            integrationKey))
        {
            throw new InventoryNotConfiguredException(
                "La clave de integración de Inventario no está configurada.");
        }

        if (!request.Route.StartsWith(
                "/",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "La ruta interna de Inventario debe comenzar con '/'.",
                nameof(request));
        }

        var timeoutSeconds =
            request.DeviceOperation
                ? Math.Clamp(
                    _options.DeviceOperationTimeoutSeconds,
                    30,
                    600)
                : Math.Clamp(
                    _options.TimeoutSeconds,
                    10,
                    300);

        using var timeout =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeout.CancelAfter(
            TimeSpan.FromSeconds(
                timeoutSeconds));

        using var outgoing =
            new HttpRequestMessage(
                request.Method,
                new Uri(
                    request.Route,
                    UriKind.RelativeOrAbsolute));

        outgoing.Headers.TryAddWithoutValidation(
            "X-Titan-Integration-Key",
            integrationKey.Trim());

        outgoing.Headers.TryAddWithoutValidation(
            "X-Titan-Actor",
            request.ActorUserId);

        outgoing.Headers.TryAddWithoutValidation(
            "X-Titan-Organization",
            request.OrganizationId);

        outgoing.Headers.TryAddWithoutValidation(
            "X-Titan-Access",
            request.IsAdministrator
                ? "manage"
                : "delegate");

        var ops = request.Operations;
        if (ops is not null)
        {
            outgoing.Headers.TryAddWithoutValidation(
                "X-Titan-Operations",
                string.Join(
                    ",",
                    ops
                        .Where(
                            value => !string.IsNullOrWhiteSpace(value))
                        .Distinct()
                        .OrderBy(value => value)));
        }

        if (request.Body is not null)
        {
            outgoing.Content =
                new StreamContent(
                    new MemoryStream(request.Body));

            if (!string.IsNullOrWhiteSpace(
                    request.ContentType))
            {
                outgoing.Content.Headers
                    .TryAddWithoutValidation(
                        "Content-Type",
                        request.ContentType);
            }
        }

        try
        {
            using var incoming =
                await _httpClient.SendAsync(
                    outgoing,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);

            var bytes =
                await incoming.Content
                    .ReadAsByteArrayAsync(
                        timeout.Token);

            return new InventoryGatewayResponse(
                (int)incoming.StatusCode,
                bytes,
                incoming.Content.Headers.ContentType
                    ?.ToString()
                    ??
                    "application/json",
                incoming.Content.Headers.ContentDisposition
                    ?.ToString());
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Timeout consultando Inventario. Route={Route} Timeout={Timeout}s",
                request.Route.Split('?')[0],
                timeoutSeconds);

            throw new InventoryTimeoutException(
                "El servicio de Inventario superó el tiempo de espera.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "No fue posible conectar con el servicio interno de Inventario. Route={Route}",
                request.Route.Split('?')[0]);

            throw new InventoryUnavailableException(
                "El servicio interno de Inventario no está disponible.",
                exception);
        }
    }
}