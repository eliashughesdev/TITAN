using System.Diagnostics;

using Microsoft.Extensions.Options;

using TitanMDM.Application.Assistant;

namespace TitanMDM.Api.AI.Assistant;

/// <summary>
/// Adaptador de proveedor basado en el cliente OpenRouter
/// existente. No duplica el transporte HTTP ni maneja la API
/// key: reutiliza <see cref="IOpenRouterAiService"/>.
/// </summary>
public sealed class OpenRouterAssistantProvider
    : IAssistantProvider
{
    private readonly IOpenRouterAiService _ai;
    private readonly OpenRouterOptions _options;

    public OpenRouterAssistantProvider(
        IOpenRouterAiService ai,
        IOptions<OpenRouterOptions> options)
    {
        _ai =
            ai;

        _options =
            options.Value;
    }

    public string Name =>
        "OpenRouter";

    public async Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var enabled =
            _options.Enabled &&
            _options.EnableAssistant;

        if (!enabled || !_ai.IsEnabled)
        {
            return new AssistantProviderStatus(
                Name,
                enabled,
                false,
                _options.ResolveAssistantModel(),
                enabled
                    ? "OpenRouter no tiene una credencial configurada en el servidor."
                    : "OpenRouter deshabilitado por configuración.",
                DateTime.UtcNow,
                0);
        }

        var stopwatch =
            Stopwatch.StartNew();
        var available =
            await _ai.CheckHealthAsync(
                cancellationToken);
        stopwatch.Stop();

        return new AssistantProviderStatus(
            Name,
            true,
            available,
            _options.ResolveAssistantModel(),
            available
                ? null
                : "OpenRouter no respondió a la verificación de conectividad.",
            DateTime.UtcNow,
            stopwatch.ElapsedMilliseconds);
    }

    public async Task<AssistantProviderResponse> CompleteAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var model =
            string.IsNullOrWhiteSpace(
                request.Model)
                ? _options.ResolveAssistantModel()
                : request.Model;

        var content =
            await _ai.CompleteAsync(
                new OpenRouterRequest(
                    request.SystemPrompt,
                    Flatten(request.Messages),
                    model,
                    request.JsonMode,
                    request.Temperature,
                    request.MaxTokens),
                cancellationToken);

        return new AssistantProviderResponse(
            content ?? string.Empty,
            model,
            Name);
    }

    private static string Flatten(
        IReadOnlyList<AssistantProviderMessage> messages)
    {
        return string.Join(
            "\n",
            messages.Select(
                message =>
                    $"{message.Role}: {message.Content}"));
    }
}
