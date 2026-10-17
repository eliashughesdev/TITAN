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

    public Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var enabled =
            _options.Enabled &&
            _options.EnableAssistant;

        var status =
            new AssistantProviderStatus(
                Name,
                enabled,
                enabled && _ai.IsEnabled,
                _options.ResolveAssistantModel(),
                enabled
                    ? null
                    : "OpenRouter deshabilitado por configuración.");

        return Task.FromResult(
            status);
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
