namespace TitanMDM.WindowsAgent.Services;

public sealed class CommandWakeSignal : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Pulse()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Multiple notifications collapse into one queue check.
        }
    }

    public async Task WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(timeout, cancellationToken);
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}
