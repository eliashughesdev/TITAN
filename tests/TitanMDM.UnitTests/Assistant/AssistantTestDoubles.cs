using System.Text.Json.Nodes;

using TitanMDM.Application.Assistant;

namespace TitanMDM.UnitTests.Assistant;

public sealed class StubTool
    : IAssistantTool
{
    private readonly Func<JsonObject, AssistantToolResult>? _handler;

    public StubTool(
        AssistantToolDescriptor descriptor,
        Func<JsonObject, AssistantToolResult>? handler = null)
    {
        Descriptor =
            descriptor;

        _handler =
            handler;
    }

    public AssistantToolDescriptor Descriptor { get; }

    public int ExecuteCount { get; private set; }

    public JsonObject? CapturedArguments { get; private set; }

    public AssistantToolContext? CapturedContext { get; private set; }

    public Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        ExecuteCount++;

        CapturedArguments =
            arguments;

        CapturedContext =
            context;

        return Task.FromResult(
            _handler?.Invoke(arguments) ??
            AssistantToolResult.Ok("ok"));
    }
}

public sealed class ScriptedProvider
    : IAssistantProvider
{
    private readonly Queue<string> _responses;

    public ScriptedProvider(
        params string[] responses)
    {
        _responses =
            new Queue<string>(responses);
    }

    public string Name =>
        "Test";

    public List<AssistantProviderRequest> Requests { get; } =
        new();

    public int Calls =>
        Requests.Count;

    public Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new AssistantProviderStatus(
                Name,
                true,
                true,
                "test-model"));
    }

    public Task<AssistantProviderResponse> CompleteAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(
            request);

        var next =
            _responses.Count > 0
                ? _responses.Dequeue()
                : "{\"toolCalls\":[],\"answer\":\"\"}";

        return Task.FromResult(
            new AssistantProviderResponse(
                next,
                "test-model",
                Name));
    }
}

public sealed class DelayedProvider
    : IAssistantProvider
{
    public string Name =>
        "Slow";

    public Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new AssistantProviderStatus(
                Name,
                true,
                true,
                "slow-model"));
    }

    public async Task<AssistantProviderResponse> CompleteAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(5),
            cancellationToken);

        return new AssistantProviderResponse(
            "respuesta tardía",
            "slow-model",
            Name);
    }
}

public sealed class StubProviderRegistry
    : IAssistantProviderRegistry
{
    private readonly IAssistantProvider? _provider;

    public StubProviderRegistry(
        IAssistantProvider? provider)
    {
        _provider =
            provider;
    }

    public Task<IAssistantProvider?> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _provider);
    }

    public Task<IReadOnlyList<AssistantProviderStatus>> GetStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AssistantProviderStatus> statuses =
            _provider is null
                ? Array.Empty<AssistantProviderStatus>()
                : new[]
                {
                    new AssistantProviderStatus(
                        _provider.Name,
                        true,
                        true,
                        "test-model")
                };

        return Task.FromResult(
            statuses);
    }
}
