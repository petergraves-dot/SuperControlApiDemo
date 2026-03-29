using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;

namespace petergraves.Integrations.SuperControl;

public sealed class SuperControlResponseCache : ISuperControlResponseCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMemoryCache _memoryCache;
    private readonly string _cacheRoot;

    public SuperControlResponseCache(IWebHostEnvironment environment, IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
        _cacheRoot = Path.Combine(environment.ContentRootPath, ".supercontrol-cache", "responses");

        try
        {
            Directory.CreateDirectory(_cacheRoot);
        }
        catch (IOException)
        {
            // Continue without disk cache when filesystem is unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Continue without disk cache when filesystem is unavailable.
        }
    }

    public async Task<CachedSuperControlResponse> GetOrFetchAsync(
        string scope,
        string cacheKey,
        TimeSpan ttl,
        Func<CancellationToken, Task<SuperControlApiResponse>> fetch,
        CancellationToken cancellationToken = default)
    {
        var safeScope = string.IsNullOrWhiteSpace(scope) ? "default" : scope.Trim().ToLowerInvariant();
        var scopeDirectory = Path.Combine(_cacheRoot, safeScope);
        var memoryCacheKey = $"sc-response::{safeScope}::{cacheKey}";
        if (_memoryCache.TryGetValue(memoryCacheKey, out SuperControlApiResponse? memoryCached) && memoryCached is not null)
        {
            return new CachedSuperControlResponse(memoryCached, CacheHit: true, StaleFallback: false);
        }

        var diskAvailable = true;
        try
        {
            Directory.CreateDirectory(scopeDirectory);
        }
        catch (IOException)
        {
            diskAvailable = false;
        }
        catch (UnauthorizedAccessException)
        {
            diskAvailable = false;
        }

        var hash = HashKey(cacheKey);
        var bodyPath = Path.Combine(scopeDirectory, $"{hash}.json");
        var metaPath = Path.Combine(scopeDirectory, $"{hash}.meta.json");
        ResponseCacheMeta? meta = null;
        if (diskAvailable)
        {
            meta = ReadMeta(metaPath);
        }

        if (diskAvailable
            && meta is not null
            && DateTime.UtcNow - meta.CachedAtUtc <= ttl
            && File.Exists(bodyPath))
        {
            try
            {
                var cachedBody = await File.ReadAllTextAsync(bodyPath, cancellationToken);
                var cachedResponse = new SuperControlApiResponse(true, meta.StatusCode, cachedBody);
                _memoryCache.Set(memoryCacheKey, cachedResponse, ttl);
                return new CachedSuperControlResponse(
                    cachedResponse,
                    CacheHit: true,
                    StaleFallback: false);
            }
            catch (IOException)
            {
                // Ignore disk read failure and continue to live fetch.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore disk read failure and continue to live fetch.
            }
        }

        var live = await fetch(cancellationToken);
        if (live.IsSuccess)
        {
            _memoryCache.Set(memoryCacheKey, live, ttl);
            if (diskAvailable)
            {
                try
                {
                    await File.WriteAllTextAsync(bodyPath, live.Body, cancellationToken);
                    await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(new ResponseCacheMeta
                    {
                        StatusCode = live.StatusCode,
                        CachedAtUtc = DateTime.UtcNow
                    }), cancellationToken);
                }
                catch (IOException)
                {
                    // Ignore disk write failures and continue with live response.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore disk write failures and continue with live response.
                }
            }

            return new CachedSuperControlResponse(live, CacheHit: false, StaleFallback: false);
        }

        if (diskAvailable && File.Exists(bodyPath) && meta is not null)
        {
            try
            {
                var staleBody = await File.ReadAllTextAsync(bodyPath, cancellationToken);
                var staleResponse = new SuperControlApiResponse(true, meta.StatusCode, staleBody);
                _memoryCache.Set(memoryCacheKey, staleResponse, TimeSpan.FromSeconds(30));
                return new CachedSuperControlResponse(
                    staleResponse,
                    CacheHit: true,
                    StaleFallback: true);
            }
            catch (IOException)
            {
                // Ignore disk read failures and continue returning live response.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore disk read failures and continue returning live response.
            }
        }

        return new CachedSuperControlResponse(live, CacheHit: false, StaleFallback: false);
    }

    private static ResponseCacheMeta? ReadMeta(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ResponseCacheMeta>(File.ReadAllText(path), SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string HashKey(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    private sealed class ResponseCacheMeta
    {
        public int StatusCode { get; init; }

        public DateTime CachedAtUtc { get; init; }
    }
}
