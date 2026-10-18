using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TitanMDM.Application.Assistant;
using TitanMDM.Infrastructure.Assistant;

namespace TitanMDM.Api.AI.Assistant;

/// <summary>
/// Adaptador de proveedor local basado en Ollama. Está
/// deshabilitado por defecto y nunca se activa mediante el
/// frontend: únicamente por configuración del servidor.
/// </summary>
public sealed class OllamaAssistantProvider
    : IAssistantProvider
{
    public const string HttpClientName =
        "AssistantOllama";

    private readonly HttpClient _httpClient;
    private readonly OllamaAssistantOptions _options;
    private readonly ILogger<OllamaAssistantProvider> _logger;

    public OllamaAssistantProvider(
        HttpClient httpClient,
        IOptions<OllamaAssistantOptions> options,
        ILogger<OllamaAssistantProvider> logger)
    {
        _httpClient =
            httpClient;

        _options =
            options.Value;

        _logger =
            logger;
    }

    public string Name =>
        "Ollama";

    public async Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new AssistantProviderStatus(
                Name,
                false,
                false,
                _options.Model,
                "Ollama deshabilitado por configuración.",
                DateTime.UtcNow,
                0);
        }

        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            using var response =
                await _httpClient.GetAsync(
                    "api/tags",
                    cancellationToken);

            stopwatch.Stop();

            return new AssistantProviderStatus(
                Name,
                true,
                response.IsSuccessStatusCode,
                _options.Model,
                response.IsSuccessStatusCode
                    ? null
                    : "Ollama no responde en el endpoint configurado.",
                DateTime.UtcNow,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or
                TaskCanceledException)
        {
            stopwatch.Stop();

            return new AssistantProviderStatus(
                Name,
                true,
                false,
                _options.Model,
                "Ollama no está accesible.",
                DateTime.UtcNow,
                stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<AssistantProviderResponse> CompleteAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var model =
            string.IsNullOrWhiteSpace(
                request.Model)
                ? _options.Model
                : request.Model;

        var messages =
            new List<OllamaMessage>
            {
                new(
                    "system",
                    request.SystemPrompt)
            };

        messages.AddRange(
            request.Messages.Select(
                message =>
                    new OllamaMessage(
                        message.Role,
                        message.Content)));

        var payload =
            new OllamaChatRequest(
                model,
                messages,
                Stream: false,
                Format: request.JsonMode
                    ? "json"
                    : null,
                Options: new OllamaGenerationOptions(
                    request.Temperature,
                    request.MaxTokens));

        using var response =
            await _httpClient.PostAsJsonAsync(
                "api/chat",
                payload,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var body =
            await response.Content
                .ReadFromJsonAsync<OllamaChatResponse>(
                    cancellationToken: cancellationToken);

        var content =
            body?.Message?.Content ??
            string.Empty;

        _logger.LogDebug(
            "Respuesta de Ollama recibida. Modelo={Model}",
            model);

        return new AssistantProviderResponse(
            content,
            model,
            Name);
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaMessage> Messages,
        bool Stream,
        string? Format,
        OllamaGenerationOptions Options);

    private sealed record OllamaGenerationOptions(
        double Temperature,
        int NumPredict);

    private sealed record OllamaMessage(
        string Role,
        string Content);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")]
        OllamaMessage? Message);
}
