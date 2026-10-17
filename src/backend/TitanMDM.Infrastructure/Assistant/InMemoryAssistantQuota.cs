using Microsoft.Extensions.Options;

using TitanMDM.Application.Assistant;

namespace TitanMDM.Infrastructure.Assistant;

/// <summary>
/// Cuota en memoria por usuario con ventana deslizante.
/// Fundacional: no requiere persistencia y protege el consumo
/// del proveedor de IA por cuenta.
/// </summary>
public sealed class InMemoryAssistantQuota
    : IAssistantQuota
{
    private readonly AssistantOptions _options;
    private readonly object _gate = new();

    private readonly Dictionary<string, Queue<DateTime>>
        _windows = new();

    public InMemoryAssistantQuota(
        IOptions<AssistantOptions> options)
    {
        _options =
            options.Value;
    }

    public bool TryConsume(
        Guid organizationId,
        Guid userId)
    {
        var max =
            Math.Clamp(
                _options.MaxTurnsPerWindow,
                1,
                10000);

        var window =
            TimeSpan.FromSeconds(
                Math.Clamp(
                    _options.QuotaWindowSeconds,
                    5,
                    3600));

        var now =
            DateTime.UtcNow;

        var key =
            organizationId.ToString("N") +
            ":" +
            userId.ToString("N");

        lock (_gate)
        {
            if (!_windows.TryGetValue(
                    key,
                    out var timestamps))
            {
                timestamps =
                    new Queue<DateTime>();

                _windows[key] =
                    timestamps;
            }

            var threshold =
                now - window;

            while (timestamps.Count > 0 &&
                   timestamps.Peek() < threshold)
            {
                timestamps.Dequeue();
            }

            if (timestamps.Count >= max)
            {
                return false;
            }

            timestamps.Enqueue(
                now);

            return true;
        }
    }
}
