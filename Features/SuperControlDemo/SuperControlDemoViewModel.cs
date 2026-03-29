using System.Text.Json;
using Microsoft.Extensions.Options;
using petergraves.Integrations.SuperControl;

namespace petergraves.Features.SuperControlDemo;

public class SuperControlDemoViewModel
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISuperControlClient _superControlClient;
    private readonly ISuperControlResponseCache _responseCache;
    private readonly SuperControlOptions _options;

    public SuperControlDemoViewModel(
        ISuperControlClient superControlClient,
        ISuperControlResponseCache responseCache,
        IOptions<SuperControlOptions> options)
    {
        _superControlClient = superControlClient;
        _responseCache = responseCache;
        _options = options.Value;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public int? AccountId => _options.AccountId;

    public string? AccountsIndexSummary { get; private set; }

    public string? AccountsIndexJson { get; private set; }

    public IReadOnlyList<SuperControlAccount> Accounts { get; private set; } = [];

    public bool AccountsIndexSucceeded { get; private set; }

    public string? ContentIndexSummary { get; private set; }

    public string? ContentIndexJson { get; private set; }

    public IReadOnlyList<SuperControlPropertyIndexEntry> Properties { get; private set; } = [];

    public bool? ContentIndexSucceeded { get; private set; }

    public int ActivePropertyCount => Properties.Count(property => property.Active);

    public int InactivePropertyCount => Properties.Count - ActivePropertyCount;

    public string? PropertyListingSummary { get; private set; }

    public string? PropertyListingJson { get; private set; }

    public bool? PropertyListingSucceeded { get; private set; }

    public string? FeaturedPropertyName { get; private set; }

    public DemoEndpointResult ConfigurationIndex { get; private set; } = new();

    public DemoEndpointResult PricesIndex { get; private set; } = new();

    public DemoEndpointResult AvailabilityIndex { get; private set; } = new();

    public DemoEndpointResult PropertyConfigurationListing { get; private set; } = new();

    public DemoEndpointResult PricesListing { get; private set; } = new();

    public DemoEndpointResult AvailabilityListing { get; private set; } = new();

    public string CacheRefreshCadence { get; set; } = "accounts";

    public string? CacheRefreshSummary { get; private set; }

    public string? CacheRefreshJson { get; private set; }

    public bool? CacheRefreshSucceeded { get; private set; }

    public string? Error { get; private set; }

    public async Task LoadDemoAsync(CancellationToken cancellationToken)
    {
        if (!HasApiKey)
        {
            Error = "SuperControl API key is not configured. Set SuperControl__ApiKey and try again.";
            return;
        }

        try
        {
            var accountsCached = await _responseCache.GetOrFetchAsync(
                scope: "supercontrol-demo",
                cacheKey: "GET /properties/index|properties/index",
                ttl: TimeSpan.FromMinutes(2),
                fetch: ct => _superControlClient.GetAccountsIndexAsync(ct),
                cancellationToken: cancellationToken);
            var accountsIndexResponse = accountsCached.Response;
            AccountsIndexSucceeded = accountsIndexResponse.IsSuccess;
            AccountsIndexSummary = $"HTTP {accountsIndexResponse.StatusCode} {(accountsIndexResponse.IsSuccess ? "Success" : "Error")}{(accountsCached.StaleFallback ? " • stale-cache" : accountsCached.CacheHit ? " • cache-hit" : " • live")}";
            if (accountsIndexResponse.IsSuccess)
            {
                var accounts = TryDeserialize<AccountsIndexResponse>(accountsIndexResponse.Body);
                Accounts = DeduplicateAccounts(accounts?.Accounts ?? []);
                AccountsIndexJson = TryPrettyPrintJson(JsonSerializer.Serialize(new AccountsIndexResponse
                {
                    Accounts = Accounts.ToList()
                }));
            }
            else
            {
                AccountsIndexJson = TryPrettyPrintJson(accountsIndexResponse.Body);
            }

            var configuredOrDefaultAccountId = _options.AccountId ?? Accounts.FirstOrDefault()?.AccountId;
            var account = Accounts.FirstOrDefault(item => item.AccountId == configuredOrDefaultAccountId) ?? Accounts.FirstOrDefault();

            if ((account?.AccountId ?? configuredOrDefaultAccountId) is int resolvedAccountId)
            {
                var contentIndexUrl = !string.IsNullOrWhiteSpace(account?.ContentIndexUrl)
                    ? account.ContentIndexUrl
                    : $"properties/contentindex/{resolvedAccountId}";
                var contentCached = await _responseCache.GetOrFetchAsync(
                    scope: "supercontrol-demo",
                    cacheKey: $"GET /properties/contentindex/{{accountId}}|{contentIndexUrl}",
                    ttl: TimeSpan.FromMinutes(2),
                    fetch: ct => _superControlClient.GetByUrlAsync(contentIndexUrl, ct),
                    cancellationToken: cancellationToken);
                var contentIndexResponse = contentCached.Response;
                ContentIndexSucceeded = contentIndexResponse.IsSuccess;
                ContentIndexSummary = $"HTTP {contentIndexResponse.StatusCode} {(contentIndexResponse.IsSuccess ? "Success" : "Error")} (accountId={resolvedAccountId}){(contentCached.StaleFallback ? " • stale-cache" : contentCached.CacheHit ? " • cache-hit" : " • live")}";
                ContentIndexJson = TryPrettyPrintJson(contentIndexResponse.Body);
                var content = contentIndexResponse.IsSuccess
                    ? TryDeserialize<ContentIndexResponse>(contentIndexResponse.Body)
                    : null;
                if (content is not null)
                {
                    Properties = content.Properties;
                }

                var featuredProperty = Properties
                    .OrderByDescending(property => property.Active)
                    .ThenByDescending(property => property.LastUpdated)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(featuredProperty?.ListingUrl))
                {
                    var listingCached = await _responseCache.GetOrFetchAsync(
                        scope: "supercontrol-demo",
                        cacheKey: $"GET /properties/listing/{{propertyId}}|{featuredProperty.ListingUrl}",
                        ttl: TimeSpan.FromMinutes(10),
                        fetch: ct => _superControlClient.GetByUrlAsync(featuredProperty.ListingUrl, ct),
                        cancellationToken: cancellationToken);
                    var listingResponse = listingCached.Response;
                    PropertyListingSucceeded = listingResponse.IsSuccess;
                    PropertyListingSummary = $"HTTP {listingResponse.StatusCode} {(listingResponse.IsSuccess ? "Success" : "Error")} (propertyId={featuredProperty.PropertyId}){(listingCached.StaleFallback ? " • stale-cache" : listingCached.CacheHit ? " • cache-hit" : " • live")}";
                    PropertyListingJson = TryPrettyPrintJson(listingResponse.Body);

                    if (listingResponse.IsSuccess)
                    {
                        var listing = TryDeserialize<PropertyListingResponse>(listingResponse.Body);
                        FeaturedPropertyName = listing?.AdContent?.PropertyName;
                    }
                }

                var configurationIndexUrl = !string.IsNullOrWhiteSpace(account?.ConfigurationIndexUrl)
                    ? account.ConfigurationIndexUrl
                    : $"properties/configurationindex/{resolvedAccountId}";
                ConfigurationIndex = await ExecuteEndpointAsync(
                    "GET /properties/configurationindex/{accountId}",
                    configurationIndexUrl,
                    cancellationToken,
                    resolvedAccountId);
                var configurationIndexData = ConfigurationIndex.Succeeded == true
                    ? TryDeserialize<ConfigurationIndexResponse>(ConfigurationIndex.RawBody)
                    : null;

                var pricesIndexUrl = !string.IsNullOrWhiteSpace(account?.PricesIndexUrl)
                    ? account.PricesIndexUrl
                    : $"properties/pricesindex/{resolvedAccountId}";
                PricesIndex = await ExecuteEndpointAsync(
                    "GET /properties/pricesindex/{accountId}",
                    pricesIndexUrl,
                    cancellationToken,
                    resolvedAccountId);
                var pricesIndexData = PricesIndex.Succeeded == true
                    ? TryDeserialize<PricesIndexResponse>(PricesIndex.RawBody)
                    : null;

                var availabilityIndexUrl = !string.IsNullOrWhiteSpace(account?.AvailabilityIndexUrl)
                    ? account.AvailabilityIndexUrl
                    : $"properties/availabilityindex/{resolvedAccountId}";
                AvailabilityIndex = await ExecuteEndpointAsync(
                    "GET /properties/availabilityindex/{accountId}",
                    availabilityIndexUrl,
                    cancellationToken,
                    resolvedAccountId);
                var availabilityIndexData = AvailabilityIndex.Succeeded == true
                    ? TryDeserialize<AvailabilityIndexResponse>(AvailabilityIndex.RawBody)
                    : null;

                var featuredId = featuredProperty?.PropertyId ?? 0;

                var configurationListing = configurationIndexData?.Properties
                    .FirstOrDefault(property => property.PropertyId == featuredId)
                    ?? configurationIndexData?.Properties.FirstOrDefault();
                PropertyConfigurationListing = await ExecuteEndpointAsync(
                    "GET /properties/propertyconfiguration/{propertyId}",
                    configurationListing?.ConfigurationContentUrl,
                    cancellationToken,
                    configurationListing?.PropertyId);

                var pricesListing = pricesIndexData?.Properties
                    .FirstOrDefault(property => property.PropertyId == featuredId)
                    ?? pricesIndexData?.Properties.FirstOrDefault();
                PricesListing = await ExecuteEndpointAsync(
                    "GET /properties/prices/{propertyId}",
                    pricesListing?.ListingUrl,
                    cancellationToken,
                    pricesListing?.PropertyId);

                var availabilityListing = availabilityIndexData?.Properties
                    .FirstOrDefault(property => property.PropertyId == featuredId)
                    ?? availabilityIndexData?.Properties.FirstOrDefault();
                AvailabilityListing = await ExecuteEndpointAsync(
                    "GET /properties/availability/{propertyId}",
                    availabilityListing?.ListingUrl,
                    cancellationToken,
                    availabilityListing?.PropertyId);
            }
            else
            {
                ContentIndexSucceeded = null;
                ContentIndexSummary = "Skipped (no account id found from config or accounts index).";
            }
        }
        catch (Exception exception)
        {
            Error = exception.Message;
        }

        return;
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken)
    {
        if (!HasApiKey)
        {
            Error = "SuperControl API key is not configured. Set SuperControl__ApiKey and try again.";
            return;
        }

        var cadence = ResolveCadence(CacheRefreshCadence);
        if (cadence is null)
        {
            Error = "Invalid cache refresh cadence. Use accounts, content-config, prices-availability, or all.";
            return;
        }

        var summary = new DemoCacheRefreshSummary(cadence.Name);

        try
        {
            var accountsIndex = await _responseCache.GetOrFetchAsync(
                scope: "index",
                cacheKey: "properties/index",
                ttl: TimeSpan.FromHours(12),
                fetch: ct => _superControlClient.GetAccountsIndexAsync(ct),
                cancellationToken: cancellationToken);

            AddRefreshStats(summary, "accounts-index", accountsIndex);

            if (accountsIndex.Response.IsSuccess)
            {
                var parsedAccounts = TryDeserialize<AccountsIndexResponse>(accountsIndex.Response.Body);
                var accounts = parsedAccounts?.Accounts ?? [];
                summary.AccountCount = accounts.Count;

                if (cadence.IncludeContentConfiguration)
                {
                    foreach (var account in accounts)
                    {
                        await RefreshCacheIndexAsync(
                            summary,
                            "content-index",
                            $"properties/contentindex/{account.AccountId}",
                            TimeSpan.FromHours(6),
                            cancellationToken);

                        await RefreshCacheIndexAsync(
                            summary,
                            "configuration-index",
                            $"properties/configurationindex/{account.AccountId}",
                            TimeSpan.FromHours(6),
                            cancellationToken);
                    }
                }

                if (cadence.IncludePricesAvailability)
                {
                    foreach (var account in accounts)
                    {
                        await RefreshCacheIndexAsync(
                            summary,
                            "prices-index",
                            $"properties/pricesindex/{account.AccountId}",
                            TimeSpan.FromMinutes(30),
                            cancellationToken);

                        await RefreshCacheIndexAsync(
                            summary,
                            "availability-index",
                            $"properties/availabilityindex/{account.AccountId}",
                            TimeSpan.FromMinutes(30),
                            cancellationToken);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Error = exception.Message;
        }

        summary.CompletedAtUtc = DateTime.UtcNow;
        CacheRefreshSucceeded = summary.Failures == 0;
        CacheRefreshSummary = $"Cadence {summary.Cadence}: {summary.Successes}/{summary.Requests} succeeded • cache-hit {summary.CacheHits} • live {summary.CacheMisses}";
        CacheRefreshJson = TryPrettyPrintJson(JsonSerializer.Serialize(summary));

        return;
    }

    private static string TryPrettyPrintJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static T? TryDeserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static List<SuperControlAccount> DeduplicateAccounts(IEnumerable<SuperControlAccount> accounts)
    {
        return accounts
            .GroupBy(account => account.AccountId)
            .Select(group => group.First())
            .ToList();
    }

    private async Task<DemoEndpointResult> ExecuteEndpointAsync(
        string label,
        string? url,
        CancellationToken cancellationToken,
        int? propertyOrAccountId = null)
    {
            if (string.IsNullOrWhiteSpace(url))
            {
                return new DemoEndpointResult
            {
                Label = label,
                Summary = "Skipped (endpoint URL not available).",
                Succeeded = null
            };
        }

        var cached = await _responseCache.GetOrFetchAsync(
            scope: "supercontrol-demo",
            cacheKey: $"{label}|{url}",
            ttl: TimeSpan.FromMinutes(2),
            fetch: ct => _superControlClient.GetByUrlAsync(url, ct),
            cancellationToken: cancellationToken);
        var response = cached.Response;
        var idSuffix = propertyOrAccountId is int id ? $" ({(label.Contains("accountId", StringComparison.OrdinalIgnoreCase) ? "accountId" : "propertyId")}={id})" : string.Empty;
        var cacheSuffix = cached.StaleFallback
            ? " • stale-cache"
            : cached.CacheHit ? " • cache-hit" : " • live";
        return new DemoEndpointResult
        {
            Label = label,
            EndpointUrl = url,
            RawBody = response.Body,
            Json = TryPrettyPrintJson(response.Body),
            Succeeded = response.IsSuccess,
            Summary = $"HTTP {response.StatusCode} {(response.IsSuccess ? "Success" : "Error")}{idSuffix}{cacheSuffix}"
        };
    }

    private static CacheRefreshCadence? ResolveCadence(string? cadence)
    {
        var value = cadence?.Trim().ToLowerInvariant() ?? "accounts";
        return value switch
        {
            "accounts" => new CacheRefreshCadence("accounts", false, false),
            "content-config" => new CacheRefreshCadence("content-config", true, false),
            "prices-availability" => new CacheRefreshCadence("prices-availability", false, true),
            "all" => new CacheRefreshCadence("all", true, true),
            _ => null
        };
    }

    private async Task RefreshCacheIndexAsync(
        DemoCacheRefreshSummary summary,
        string indexName,
        string cacheKey,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var cached = await _responseCache.GetOrFetchAsync(
            scope: "index",
            cacheKey: cacheKey,
            ttl: ttl,
            fetch: ct => _superControlClient.GetByRelativeUrlAsync(cacheKey, ct),
            cancellationToken: cancellationToken);

        AddRefreshStats(summary, indexName, cached);
    }

    private static void AddRefreshStats(
        DemoCacheRefreshSummary summary,
        string indexName,
        CachedSuperControlResponse response)
    {
        summary.Requests++;
        if (response.Response.IsSuccess)
        {
            summary.Successes++;
        }
        else
        {
            summary.Failures++;
        }

        if (response.CacheHit)
        {
            summary.CacheHits++;
        }
        else
        {
            summary.CacheMisses++;
        }

        if (response.StaleFallback)
        {
            summary.StaleFallbacks++;
        }

        summary.IndexBreakdown[indexName] = summary.IndexBreakdown.TryGetValue(indexName, out var existing)
            ? existing with
            {
                Requests = existing.Requests + 1,
                Successes = existing.Successes + (response.Response.IsSuccess ? 1 : 0),
                Failures = existing.Failures + (response.Response.IsSuccess ? 0 : 1),
                CacheHits = existing.CacheHits + (response.CacheHit ? 1 : 0),
                CacheMisses = existing.CacheMisses + (response.CacheHit ? 0 : 1),
                StaleFallbacks = existing.StaleFallbacks + (response.StaleFallback ? 1 : 0)
            }
            : new DemoCacheRefreshIndexStats(
                Requests: 1,
                Successes: response.Response.IsSuccess ? 1 : 0,
                Failures: response.Response.IsSuccess ? 0 : 1,
                CacheHits: response.CacheHit ? 1 : 0,
                CacheMisses: response.CacheHit ? 0 : 1,
                StaleFallbacks: response.StaleFallback ? 1 : 0);
    }
}

public sealed class DemoEndpointResult
{
    public string Label { get; init; } = string.Empty;

    public string? EndpointUrl { get; init; }

    public string Summary { get; init; } = string.Empty;

    public bool? Succeeded { get; init; }

    public string Json { get; init; } = string.Empty;

    public string RawBody { get; init; } = string.Empty;
}

public sealed record CacheRefreshCadence(
    string Name,
    bool IncludeContentConfiguration,
    bool IncludePricesAvailability);

public sealed class DemoCacheRefreshSummary
{
    public DemoCacheRefreshSummary(string cadence)
    {
        Cadence = cadence;
        StartedAtUtc = DateTime.UtcNow;
    }

    public string Cadence { get; }

    public DateTime StartedAtUtc { get; }

    public DateTime? CompletedAtUtc { get; set; }

    public int AccountCount { get; set; }

    public int Requests { get; set; }

    public int Successes { get; set; }

    public int Failures { get; set; }

    public int CacheHits { get; set; }

    public int CacheMisses { get; set; }

    public int StaleFallbacks { get; set; }

    public Dictionary<string, DemoCacheRefreshIndexStats> IndexBreakdown { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record DemoCacheRefreshIndexStats(
    int Requests,
    int Successes,
    int Failures,
    int CacheHits,
    int CacheMisses,
    int StaleFallbacks);
