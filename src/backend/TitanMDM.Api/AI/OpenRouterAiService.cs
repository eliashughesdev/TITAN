using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Options;

namespace TitanMDM.Api.AI;

public sealed class OpenRouterAiService
    : IOpenRouterAiService
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly ILogger<OpenRouterAiService> _logger;

    public OpenRouterAiService(
        HttpClient httpClient,
        IOptions<OpenRouterOptions> options,
        ILogger<OpenRouterAiService> logger)
    {
        _httpClient =
            httpClient
            ?? throw new ArgumentNullException(
                nameof(httpClient));

        _options =
            options?.Value
            ?? throw new ArgumentNullException(
                nameof(options));

        _logger =
            logger
            ?? throw new ArgumentNullException(
                nameof(logger));
    }

    // ============================================================
    // AVAILABILITY
    // ============================================================

    public bool IsEnabled =>
        _options.Enabled
        &&
        !string.IsNullOrWhiteSpace(
            _options.ApiKey);

    // ============================================================
    // COMPLETION
    // ============================================================

    public async Task<string> CompleteAsync(
        OpenRouterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (!_options.Enabled)
        {
            throw new OpenRouterUnavailableException(
                "OpenRouter está deshabilitado.");
        }

        if (string.IsNullOrWhiteSpace(
                _options.ApiKey))
        {
            throw new OpenRouterUnavailableException(
                "AI:OpenRouter:ApiKey no está configurada.");
        }

        if (string.IsNullOrWhiteSpace(
                request.UserPrompt))
        {
            throw new ArgumentException(
                "El prompt del usuario es obligatorio.",
                nameof(request));
        }

        var model =
            ResolveModel(
                request);

        var temperature =
            request.Temperature
            ??
            _options.Temperature;

        var maxTokens =
            request.MaxTokens
            ??
            _options.MaxTokens;

        var maxAttempts =
            Math.Max(
                1,
                _options.MaxRetries + 1);

        Exception? lastFailure =
            null;

        for (
            var attempt = 1;
            attempt <= maxAttempts;
            attempt++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            try
            {
                using var httpRequest =
                    CreateHttpRequest(
                        request,
                        model,
                        temperature,
                        maxTokens);

                using var response =
                    await _httpClient.SendAsync(
                        httpRequest,
                        HttpCompletionOption
                            .ResponseHeadersRead,
                        cancellationToken);

                var body =
                    await response.Content
                        .ReadAsStringAsync(
                            cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return ExtractContent(
                        body);
                }

                var statusCode =
                    (int)response.StatusCode;

                if (!ShouldRetry(
                        response.StatusCode))
                {
                    throw new OpenRouterUnavailableException(
                        BuildFailureMessage(
                            statusCode,
                            body));
                }

                lastFailure =
                    new OpenRouterUnavailableException(
                        BuildFailureMessage(
                            statusCode,
                            body));

                if (attempt >= maxAttempts)
                {
                    break;
                }

                _logger.LogWarning(
                    "OpenRouter respondió HTTP {StatusCode}. " +
                    "Reintento {Attempt}/{MaxAttempts}.",
                    statusCode,
                    attempt,
                    maxAttempts);
            }
            catch (
                OperationCanceledException)
                when (
                    !cancellationToken
                        .IsCancellationRequested)
            {
                lastFailure =
                    new TimeoutException(
                        "OpenRouter excedió el tiempo máximo de espera.");

                if (attempt >= maxAttempts)
                {
                    break;
                }

                _logger.LogWarning(
                    "Timeout comunicando con OpenRouter. " +
                    "Reintento {Attempt}/{MaxAttempts}.",
                    attempt,
                    maxAttempts);
            }
            catch (
                HttpRequestException ex)
            {
                lastFailure =
                    ex;

                if (attempt >= maxAttempts)
                {
                    break;
                }

                _logger.LogWarning(
                    ex,
                    "Error de comunicación con OpenRouter. " +
                    "Reintento {Attempt}/{MaxAttempts}.",
                    attempt,
                    maxAttempts);
            }
            catch (
                JsonException ex)
            {
                throw new OpenRouterUnavailableException(
                    "OpenRouter devolvió una respuesta JSON inválida.",
                    ex);
            }

            var delay =
                CalculateRetryDelay(
                    attempt);

            await Task.Delay(
                delay,
                cancellationToken);
        }

        throw new OpenRouterUnavailableException(
            "OpenRouter no estuvo disponible después de varios intentos.",
            lastFailure
            ??
            new InvalidOperationException(
                "No se recibió una respuesta válida."));
    }

    // ============================================================
    // HTTP REQUEST
    // ============================================================

    private HttpRequestMessage CreateHttpRequest(
        OpenRouterRequest request,
        string model,
        double temperature,
        int maxTokens)
    {
        var payload =
            new Dictionary<string, object?>
            {
                ["model"] =
                    model,

                ["messages"] =
                    new object[]
                    {
                        new
                        {
                            role =
                                "system",

                            content =
                                request.SystemPrompt
                                ??
                                string.Empty
                        },

                        new
                        {
                            role =
                                "user",

                            content =
                                request.UserPrompt
                        }
                    },

                ["temperature"] =
                    temperature,

                ["max_tokens"] =
                    Math.Max(
                        1,
                        maxTokens)
            };

        if (request.JsonMode)
        {
            payload[
                "response_format"] =
                new
                {
                    type =
                        "json_object"
                };
        }

        var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "chat/completions");

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.ApiKey.Trim());

        if (!string.IsNullOrWhiteSpace(
                _options.SiteUrl))
        {
            httpRequest.Headers
                .TryAddWithoutValidation(
                    "HTTP-Referer",
                    _options.SiteUrl.Trim());
        }

        var title =
            !string.IsNullOrWhiteSpace(
                _options.ApplicationTitle)
                ? _options
                    .ApplicationTitle
                    .Trim()
                : _options
                    .AppName
                    .Trim();

        if (!string.IsNullOrWhiteSpace(
                title))
        {
            httpRequest.Headers
                .TryAddWithoutValidation(
                    "X-Title",
                    title);
        }

        httpRequest.Content =
            JsonContent.Create(
                payload);

        return httpRequest;
    }

    // ============================================================
    // MODEL RESOLUTION
    // ============================================================

    private string ResolveModel(
        OpenRouterRequest request)
    {
        if (!string.IsNullOrWhiteSpace(
                request.Model))
        {
            return request.Model.Trim();
        }

        return _options
            .ResolveAssistantModel();
    }

    // ============================================================
    // RESPONSE
    // ============================================================

    private static string ExtractContent(
        string body)
    {
        if (string.IsNullOrWhiteSpace(
                body))
        {
            throw new OpenRouterUnavailableException(
                "OpenRouter devolvió una respuesta vacía.");
        }

        using var document =
            JsonDocument.Parse(
                body);

        var root =
            document.RootElement;

        if (!root.TryGetProperty(
                "choices",
                out var choices)
            ||
            choices.ValueKind !=
                JsonValueKind.Array
            ||
            choices.GetArrayLength() == 0)
        {
            throw new OpenRouterUnavailableException(
                "OpenRouter no devolvió ninguna respuesta utilizable.");
        }

        var firstChoice =
            choices[0];

        if (!firstChoice.TryGetProperty(
                "message",
                out var message))
        {
            throw new OpenRouterUnavailableException(
                "OpenRouter no devolvió el objeto message.");
        }

        if (!message.TryGetProperty(
                "content",
                out var content))
        {
            throw new OpenRouterUnavailableException(
                "OpenRouter no devolvió contenido.");
        }

        var result =
            content.GetString();

        if (string.IsNullOrWhiteSpace(
                result))
        {
            throw new OpenRouterUnavailableException(
                "OpenRouter devolvió contenido vacío.");
        }

        return result.Trim();
    }

    // ============================================================
    // RETRY POLICY
    // ============================================================

    private static bool ShouldRetry(
        HttpStatusCode statusCode)
    {
        return statusCode
                   is HttpStatusCode
                       .RequestTimeout
               ||
               statusCode ==
                   (HttpStatusCode)429
               ||
               (int)statusCode >=
                   500;
    }

    private TimeSpan CalculateRetryDelay(
        int attempt)
    {
        var baseDelay =
            Math.Max(
                250,
                _options
                    .RetryBaseDelayMilliseconds);

        var multiplier =
            Math.Pow(
                2,
                Math.Max(
                    0,
                    attempt - 1));

        var milliseconds =
            Math.Min(
                10_000,
                baseDelay *
                multiplier);

        return TimeSpan
            .FromMilliseconds(
                milliseconds);
    }

    // ============================================================
    // SAFE ERROR
    // ============================================================

    private static string BuildFailureMessage(
        int statusCode,
        string responseBody)
    {
        var safeBody =
            string.IsNullOrWhiteSpace(
                responseBody)
                ? "Sin detalle."
                : responseBody.Trim();

        if (safeBody.Length >
            800)
        {
            safeBody =
                safeBody[..800]
                +
                "...";
        }

        return
            $"OpenRouter respondió HTTP {statusCode}. {safeBody}";
    }
}