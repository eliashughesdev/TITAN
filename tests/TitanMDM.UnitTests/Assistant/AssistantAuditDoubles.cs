using TitanMDM.Application.Assistant;
using TitanMDM.Application.Audit;

namespace TitanMDM.UnitTests.Assistant;

public sealed class RecordingAuditWriter
    : IAdministrativeAuditWriter
{
    public List<AdministrativeAuditEntry> Entries { get; } =
        new();

    public Task WriteAsync(
        AdministrativeAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        Entries.Add(
            entry);

        return Task.CompletedTask;
    }
}

public sealed class ThrowingAuditWriter
    : IAdministrativeAuditWriter
{
    public Task WriteAsync(
        AdministrativeAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Auditoría no disponible.");
    }
}

public sealed class FixedQuota
    : IAssistantQuota
{
    private readonly int _limit;
    private int _used;

    public FixedQuota(
        int limit)
    {
        _limit =
            Math.Max(0, limit);
    }

    public bool TryConsume(
        Guid organizationId,
        Guid userId)
    {
        if (_used >= _limit)
        {
            return false;
        }

        _used++;

        return true;
    }
}

public sealed class CountingQuota
    : IAssistantQuota
{
    public int Consumed { get; private set; }

    public int Limit { get; set; } =
        int.MaxValue;

    public bool TryConsume(
        Guid organizationId,
        Guid userId)
    {
        if (Consumed >= Limit)
        {
            return false;
        }

        Consumed++;

        return true;
    }
}

public sealed class ThrowingProvider
    : IAssistantProvider
{
    public string Name =>
        "Broken";

    public Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new AssistantProviderStatus(
                Name,
                true,
                true,
                "broken-model"));
    }

    public Task<AssistantProviderResponse> CompleteAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new HttpRequestException(
            "El proveedor no está disponible.");
    }
}

public sealed class ConstantProvider
    : IAssistantProvider
{
    private readonly string _answer;

    public ConstantProvider(
        string answer)
    {
        _answer =
            answer;
    }

    public string Name =>
        "Constant";

    public Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new AssistantProviderStatus(
                Name,
                true,
                true,
                "constant-model"));
    }

    public Task<AssistantProviderResponse> CompleteAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new AssistantProviderResponse(
                _answer,
                "constant-model",
                Name));
    }
}

public sealed class CancellingProvider
    : IAssistantProvider
{
    public string Name =>
        "Cancelling";

    public Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new AssistantProviderStatus(
                Name,
                true,
                true,
                "cancel-model"));
    }

    public async Task<AssistantProviderResponse> CompleteAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(5),
            cancellationToken);

        return new AssistantProviderResponse(
            "no debería llegar",
            "cancel-model",
            Name);
    }
}
