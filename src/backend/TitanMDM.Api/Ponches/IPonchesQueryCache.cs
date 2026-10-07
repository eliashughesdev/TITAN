namespace TitanMDM.Api.Ponches;

public interface IPonchesQueryCache
{
    Task<PonchesGatewayResponse> GetOrCreateAsync(
        string organizationId,
        string cacheKey,
        TimeSpan lifetime,
        Func<Task<PonchesGatewayResponse>> factory,
        CancellationToken cancellationToken = default);

    void Remove(
        string organizationId,
        string cacheKey);

    void RemovePrefix(
        string organizationId,
        string prefix);

    void InvalidateOperationalData(
        string organizationId);
}