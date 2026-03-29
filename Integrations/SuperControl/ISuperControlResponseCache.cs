namespace petergraves.Integrations.SuperControl;

public interface ISuperControlResponseCache
{
    Task<CachedSuperControlResponse> GetOrFetchAsync(
        string scope,
        string cacheKey,
        TimeSpan ttl,
        Func<CancellationToken, Task<SuperControlApiResponse>> fetch,
        CancellationToken cancellationToken = default);
}

public sealed record CachedSuperControlResponse(
    SuperControlApiResponse Response,
    bool CacheHit,
    bool StaleFallback
);
