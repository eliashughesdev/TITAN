using Microsoft.Extensions.Options;

using TitanMDM.Application.Assistant;
using TitanMDM.Infrastructure.Assistant;

namespace TitanMDM.Api.AI.Assistant;

/// <summary>
/// Selecciona el proveedor activo del asistente según la
/// configuración. En modo "Auto" prefiere OpenRouter cuando
/// está disponible y recurre a Ollama solo si está habilitado.
/// </summary>
public sealed class AssistantProviderRegistry
    : IAssistantProviderRegistry
{
    private readonly IReadOnlyList<IAssistantProvider> _providers;
    private readonly AssistantOptions _options;

    public AssistantProviderRegistry(
        IEnumerable<IAssistantProvider> providers,
        IOptions<AssistantOptions> options)
    {
        _providers =
            providers.ToList();

        _options =
            options.Value;
    }

    public async Task<IAssistantProvider?> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var preference =
            (_options.Provider ?? "Auto").Trim();

        if (preference.Equals(
                "OpenRouter",
                StringComparison.OrdinalIgnoreCase))
        {
            return Find("OpenRouter");
        }

        if (preference.Equals(
                "Ollama",
                StringComparison.OrdinalIgnoreCase))
        {
            return Find("Ollama");
        }

        var statuses =
            await GetStatusesAsync(
                cancellationToken);

        var available =
            statuses
                .Where(
                    status =>
                        status.Available)
                .Select(
                    status =>
                        status.Name)
                .ToList();

        if (available.Contains(
                "OpenRouter",
                StringComparer.OrdinalIgnoreCase))
        {
            return Find("OpenRouter");
        }

        if (available.Contains(
                "Ollama",
                StringComparer.OrdinalIgnoreCase))
        {
            return Find("Ollama");
        }

        return Find("OpenRouter") ??
            Find("Ollama");
    }

    public async Task<IReadOnlyList<AssistantProviderStatus>> GetStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var statuses =
            new List<AssistantProviderStatus>();

        foreach (var provider in _providers)
        {
            try
            {
                statuses.Add(
                    await provider.GetStatusAsync(
                        cancellationToken));
            }
            catch (Exception)
                when (!cancellationToken
                    .IsCancellationRequested)
            {
                statuses.Add(
                    new AssistantProviderStatus(
                        provider.Name,
                        false,
                        false,
                        string.Empty,
                        "El proveedor no está disponible."));
            }
        }

        return statuses;
    }

    private IAssistantProvider? Find(
        string name)
    {
        return _providers.FirstOrDefault(
            provider =>
                provider.Name.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase));
    }
}
