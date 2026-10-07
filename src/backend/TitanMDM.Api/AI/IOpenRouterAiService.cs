namespace TitanMDM.Api.AI;

public interface IOpenRouterAiService
{
    bool IsEnabled { get; }

    Task<string> CompleteAsync(
        OpenRouterRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record OpenRouterRequest(
    string SystemPrompt,
    string UserPrompt,
    string? Model = null,
    bool JsonMode = false,
    double? Temperature = null,
    int? MaxTokens = null);

public sealed class OpenRouterUnavailableException
    : Exception
{
    public OpenRouterUnavailableException(
        string message)
        : base(message)
    {
    }

    public OpenRouterUnavailableException(
        string message,
        Exception innerException)
        : base(
            message,
            innerException)
    {
    }
}