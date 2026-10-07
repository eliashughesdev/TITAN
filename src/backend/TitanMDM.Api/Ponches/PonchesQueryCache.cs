using System.Collections.Concurrent;

using Microsoft.Extensions.Caching.Memory;

namespace TitanMDM.Api.Ponches;

public sealed class PonchesQueryCache
    : IPonchesQueryCache
{
    private readonly IMemoryCache
        _cache;

    private readonly ConcurrentDictionary<
        string,
        byte>
        _knownKeys =
            new(
                StringComparer.Ordinal);

    private readonly ConcurrentDictionary<
        string,
        SemaphoreSlim>
        _locks =
            new(
                StringComparer.Ordinal);

    private readonly ILogger<
        PonchesQueryCache>
        _logger;

    public PonchesQueryCache(
        IMemoryCache cache,
        ILogger<PonchesQueryCache> logger)
    {
        _cache =
            cache
            ??
            throw new ArgumentNullException(
                nameof(cache));

        _logger =
            logger
            ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    public async Task<
        PonchesGatewayResponse>
        GetOrCreateAsync(
            string organizationId,
            string cacheKey,
            TimeSpan lifetime,
            Func<Task<
                PonchesGatewayResponse>>
                factory,
            CancellationToken cancellationToken = default)
    {
        ArgumentException
            .ThrowIfNullOrWhiteSpace(
                organizationId);

        ArgumentException
            .ThrowIfNullOrWhiteSpace(
                cacheKey);

        ArgumentNullException
            .ThrowIfNull(
                factory);

        var key =
            BuildKey(
                organizationId,
                cacheKey);

        if (_cache.TryGetValue(
                key,
                out PonchesGatewayResponse?
                    cached)
            &&
            cached is not null)
        {
            return Clone(
                cached);
        }

        var gate =
            _locks.GetOrAdd(
                key,
                _ =>
                    new SemaphoreSlim(
                        1,
                        1));

        await gate.WaitAsync(
            cancellationToken);

        try
        {
            if (_cache.TryGetValue(
                    key,
                    out cached)
                &&
                cached is not null)
            {
                return Clone(
                    cached);
            }

            var result =
                await factory();

            /*
             * Sólo guardamos respuestas exitosas.
             * Nunca cacheamos 4xx/5xx.
             */
            if (result.StatusCode
                is >= 200
                and < 300)
            {
                var cacheCopy =
                    Clone(
                        result);

                _cache.Set(
                    key,
                    cacheCopy,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow =
                            lifetime
                    });

                _knownKeys[
                    key] = 0;
            }

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Remove(
        string organizationId,
        string cacheKey)
    {
        var key =
            BuildKey(
                organizationId,
                cacheKey);

        _cache.Remove(
            key);

        _knownKeys.TryRemove(
            key,
            out _);
    }

    public void RemovePrefix(
        string organizationId,
        string prefix)
    {
        var normalizedPrefix =
            BuildKey(
                organizationId,
                prefix);

        foreach (var key in
                 _knownKeys.Keys)
        {
            if (!key.StartsWith(
                    normalizedPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            _cache.Remove(
                key);

            _knownKeys.TryRemove(
                key,
                out _);
        }
    }

    public void InvalidateOperationalData(
        string organizationId)
    {
        RemovePrefix(
            organizationId,
            "dashboard");

        RemovePrefix(
            organizationId,
            "records");

        RemovePrefix(
            organizationId,
            "employees");

        RemovePrefix(
            organizationId,
            "devices");

        RemovePrefix(
            organizationId,
            "reports");

        _logger.LogInformation(
            "Ponches cache invalidado para OrganizationId={OrganizationId}",
            organizationId);
    }

    private static string BuildKey(
        string organizationId,
        string cacheKey)
    {
        return
            $"ponches:{organizationId.Trim().ToLowerInvariant()}:" +
            cacheKey.Trim().ToLowerInvariant();
    }

    private static PonchesGatewayResponse Clone(
        PonchesGatewayResponse source)
    {
        return new PonchesGatewayResponse(
            StatusCode:
                source.StatusCode,

            Body:
                source.Body.ToArray(),

            ContentType:
                source.ContentType,

            ContentDisposition:
                source.ContentDisposition);
    }
}