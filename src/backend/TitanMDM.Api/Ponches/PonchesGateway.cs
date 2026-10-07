using Microsoft.Extensions.Options;

namespace TitanMDM.Api.Ponches;

public sealed class PonchesGateway
    : IPonchesGateway
{
    private readonly HttpClient
        _httpClient;

    private readonly PonchesOptions
        _options;

    private readonly ILogger<PonchesGateway>
        _logger;

    public PonchesGateway(
        HttpClient httpClient,
        IOptions<PonchesOptions> options,
        ILogger<PonchesGateway> logger)
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

    public async Task<PonchesGatewayResponse>
        SendAsync(
            PonchesGatewayRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (!_options.IsConfigured)
        {
            throw new PonchesNotConfiguredException(
                "La integración interna de Ponches no está configurada.");
        }

        var baseUri =
            _options.GetBaseUri();

        if (!request.Route.StartsWith(
                "/",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "La ruta interna de Ponches debe comenzar con '/'.",
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
                    baseUri,
                    request.Route));

        outgoing.Headers.TryAddWithoutValidation(
            "X-Titan-Integration-Key",
            _options.IntegrationKey.Trim());

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

        outgoing.Headers.TryAddWithoutValidation(
            "X-Titan-Operations",
            string.Join(
                ",",
                request.Operations
                    .Where(
                        value =>
                            !string.IsNullOrWhiteSpace(
                                value))
                    .Distinct(
                        StringComparer.Ordinal)
                    .OrderBy(
                        value =>
                            value)));

        if (request.Body is not null)
        {
            outgoing.Content =
                new StreamContent(
                    request.Body);

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

            return new PonchesGatewayResponse(
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
                "Timeout consultando Ponches. Route={Route} Timeout={Timeout}s",
                request.Route.Split('?')[0],
                timeoutSeconds);

            throw new PonchesTimeoutException(
                "El servicio de Ponches superó el tiempo de espera.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "No fue posible conectar con el servicio interno de Ponches. Route={Route}",
                request.Route.Split('?')[0]);

            throw new PonchesUnavailableException(
                "El servicio interno de Ponches no está disponible.",
                exception);
        }
    }
}

public sealed class PonchesNotConfiguredException
    : Exception
{
    public PonchesNotConfiguredException(
        string message)
        : base(message)
    {
    }
}

public sealed class PonchesUnavailableException
    : Exception
{
    public PonchesUnavailableException(
        string message,
        Exception? innerException = null)
        : base(
            message,
            innerException)
    {
    }
}

public sealed class PonchesTimeoutException
    : Exception
{
    public PonchesTimeoutException(
        string message)
        : base(message)
    {
    }
}