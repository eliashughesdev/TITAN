namespace TitanMDM.WindowsAgent.Services;

public sealed class AgentRetryPolicy
{
    private readonly ILogger<
        AgentRetryPolicy>
        _logger;

    public AgentRetryPolicy(
        ILogger<AgentRetryPolicy> logger)
    {
        _logger =
            logger;
    }

    public async Task<T>
        ExecuteAsync<T>(
            Func<CancellationToken, Task<T>>
                operation,
            string operationName,
            CancellationToken cancellationToken,
            int maximumAttempts = 5)
    {
        Exception?
            lastException =
                null;

        for (
            var attempt = 1;
            attempt <=
                maximumAttempts;
            attempt++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            try
            {
                return await operation(
                    cancellationToken);
            }
            catch (
                OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
                when (
                    IsTransient(
                        ex))
            {
                lastException =
                    ex;

                if (
                    attempt ==
                    maximumAttempts)
                {
                    break;
                }

                var delay =
                    CalculateDelay(
                        attempt);

                _logger.LogWarning(
                    ex,
                    "Operación {Operation} falló. Intento {Attempt}/{MaximumAttempts}. Retry en {DelayMs} ms.",
                    operationName,
                    attempt,
                    maximumAttempts,
                    delay.TotalMilliseconds);

                await Task.Delay(
                    delay,
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"TitanMDM agotó los reintentos para '{operationName}'.",
            lastException);
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task>
            operation,
        string operationName,
        CancellationToken cancellationToken,
        int maximumAttempts = 5)
    {
        await ExecuteAsync(
            async token =>
            {
                await operation(
                    token);

                return true;
            },
            operationName,
            cancellationToken,
            maximumAttempts);
    }

    private static bool IsTransient(
        Exception exception)
    {
        return exception is
            HttpRequestException
            or IOException
            or TimeoutException
            or TaskCanceledException;
    }

    private static TimeSpan
        CalculateDelay(
            int attempt)
    {
        var exponentialSeconds =
            Math.Min(
                60,
                Math.Pow(
                    2,
                    attempt));

        var jitterMilliseconds =
            Random.Shared.Next(
                250,
                1250);

        return TimeSpan
            .FromSeconds(
                exponentialSeconds)
            +
            TimeSpan
                .FromMilliseconds(
                    jitterMilliseconds);
    }
}