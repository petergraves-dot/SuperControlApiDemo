using System.Globalization;
using System.Net;
using System.Text.Json;
using Ganss.Xss;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;

namespace petergraves.Integrations.SuperControl;

public interface ISuperControlListingSiteService
{
    Task<SuperControlListingSiteSnapshot> BuildSnapshotAsync(
        int accountId,
        string? query,
        int guests,
        DateOnly? checkIn,
        DateOnly? checkOut,
        CancellationToken cancellationToken = default);

    Task<SuperControlPropertyDetailResult> GetPropertyDetailAsync(
        int accountId,
        int propertyId,
        int guests,
        DateOnly? checkIn,
        DateOnly? checkOut,
        CancellationToken cancellationToken = default);
}

public sealed class SuperControlListingSiteService : ISuperControlListingSiteService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISuperControlClient _client;
    private readonly IMemoryCache _cache;
    private readonly string _diskCacheRoot;

    public SuperControlListingSiteService(
        ISuperControlClient client,
        IMemoryCache cache,
        IWebHostEnvironment environment)
    {
        _client = client;
        _cache = cache;
        _diskCacheRoot = Path.Combine(environment.ContentRootPath, ".supercontrol-cache");
        Directory.CreateDirectory(_diskCacheRoot);
    }

    public async Task<SuperControlListingSiteSnapshot> BuildSnapshotAsync(
        int accountId,
        string? query,
        int guests,
        DateOnly? checkIn,
        DateOnly? checkOut,
        CancellationToken cancellationToken = default)
    {
        const int maxConcurrentPayloadFetches = 8;
        using var payloadFetchGate = new SemaphoreSlim(maxConcurrentPayloadFetches);

        var account = await ResolveAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            return new SuperControlListingSiteSnapshot
            {
                Search = new SuperControlSearchContext(query, guests, checkIn, checkOut),
                Errors = ["Accounts index could not be loaded."]
            };
        }

        var contentIndexUrl = ResolveIndexUrl(account.ContentIndexUrl, $"properties/contentindex/{account.AccountId}");
        var pricesIndexUrl = ResolveIndexUrl(account.PricesIndexUrl, $"properties/pricesindex/{account.AccountId}");
        var availabilityIndexUrl = ResolveIndexUrl(account.AvailabilityIndexUrl, $"properties/availabilityindex/{account.AccountId}");

        var contentTask = GetTypedByUrlAsync<ContentIndexResponse>(contentIndexUrl, TimeSpan.FromMinutes(2), cancellationToken);
        var pricesIndexTask = GetTypedByUrlAsync<PricesIndexResponse>(pricesIndexUrl, TimeSpan.FromMinutes(2), cancellationToken);
        var availabilityIndexTask = GetTypedByUrlAsync<AvailabilityIndexResponse>(availabilityIndexUrl, TimeSpan.FromMinutes(2), cancellationToken);

        await Task.WhenAll(contentTask, pricesIndexTask, availabilityIndexTask);

        var content = contentTask.Result;
        if (content is null || content.Properties.Count == 0)
        {
            return new SuperControlListingSiteSnapshot
            {
                Search = new SuperControlSearchContext(query, guests, checkIn, checkOut),
                Errors = ["Content index returned no properties or could not be parsed."]
            };
        }

        var availabilityByProperty = availabilityIndexTask.Result?.Properties
            .ToDictionary(property => property.PropertyId, property => property)
            ?? new Dictionary<int, AvailabilityIndexEntry>();

        var pricingMap = pricesIndexTask.Result?.Properties
            .ToDictionary(property => property.PropertyId, property => property)
            ?? new Dictionary<int, PricesIndexEntry>();

        var candidates = content.Properties
            .Where(property => property.Active)
            .OrderByDescending(property => property.LastUpdated)
            .Take(20)
            .ToList();

        var listingTasks = candidates.Select(async property =>
        {
            var listing = await RunWithConcurrencyLimitAsync(
                payloadFetchGate,
                () => GetDeltaCachedPropertyPayloadAsync<PropertyListingResponse>(
                    payloadType: "listing",
                    propertyId: property.PropertyId,
                    payloadUrl: property.ListingUrl,
                    lastUpdatedUtc: property.LastUpdated.UtcDateTime,
                    cancellationToken),
                cancellationToken);
            return (Property: property, Listing: listing.Payload, listing.CacheDisposition);
        });

        var listingResults = await Task.WhenAll(listingTasks);
        var cacheHits = 0;
        var cacheMisses = 0;
        var staleFallbackHits = 0;

        var enrichmentTasks = listingResults.Select(async result =>
        {
            var localCacheHits = 0;
            var localCacheMisses = 0;
            var localStaleFallbackHits = 0;

            if (result.Listing is null)
            {
                return (Card: (SuperControlPropertyCard?)null, localCacheHits, localCacheMisses, localStaleFallbackHits);
            }

            CountCache(result.CacheDisposition, ref localCacheHits, ref localCacheMisses, ref localStaleFallbackHits);

            var address = result.Listing.Location?.Address;
            var location = BuildLocation(result.Listing);
            if (!MatchesQuery(query, result.Listing, location))
            {
                return (Card: (SuperControlPropertyCard?)null, localCacheHits, localCacheMisses, localStaleFallbackHits);
            }

            var card = new SuperControlPropertyCard
            {
                PropertyId = result.Property.PropertyId,
                Name = result.Listing.AdContent?.PropertyName ?? $"Property {result.Property.PropertyId}",
                SubCaption = result.Listing.AdContent?.Subcaption,
                Description = result.Listing.AdContent?.Description
                    ?? result.Listing.AdContent?.AccommodationsSummary,
                DescriptionHtml = BuildSafeDescriptionHtml(
                    result.Listing.AdContent?.Description
                    ?? result.Listing.AdContent?.AccommodationsSummary),
                Location = location,
                City = address?.City,
                Country = address?.Country,
                HeroImageUrl = result.Listing.Images.FirstOrDefault(image => !string.IsNullOrWhiteSpace(image.Url))?.Url,
                Amenities = result.Listing.Property?.Amenities?.Take(6).ToList() ?? [],
                PropertyType = result.Listing.Property?.PropertyType,
                LastUpdatedUtc = result.Property.LastUpdated.UtcDateTime
            };

            Task<DeltaCachedPayloadResult<PricesListingResponse>>? pricesTask = null;
            if (pricingMap.TryGetValue(card.PropertyId, out var pricesEntry))
            {
                pricesTask = RunWithConcurrencyLimitAsync(
                    payloadFetchGate,
                    () => GetDeltaCachedPropertyPayloadAsync<PricesListingResponse>(
                        payloadType: "prices",
                        propertyId: card.PropertyId,
                        payloadUrl: pricesEntry.ListingUrl,
                        lastUpdatedUtc: pricesEntry.LastUpdated.UtcDateTime,
                        cancellationToken),
                    cancellationToken);
            }

            Task<DeltaCachedPayloadResult<AvailabilityListingResponse>>? availabilityTask = null;
            if (availabilityByProperty.TryGetValue(card.PropertyId, out var availabilityEntry))
            {
                availabilityTask = RunWithConcurrencyLimitAsync(
                    payloadFetchGate,
                    () => GetDeltaCachedPropertyPayloadAsync<AvailabilityListingResponse>(
                        payloadType: "availability",
                        propertyId: card.PropertyId,
                        payloadUrl: availabilityEntry.ListingUrl,
                        lastUpdatedUtc: availabilityEntry.LastUpdated.UtcDateTime,
                        cancellationToken),
                    cancellationToken);
            }

            var pricesResult = pricesTask is null ? null : await pricesTask;
            if (pricesResult is not null)
            {
                CountCache(pricesResult.CacheDisposition, ref localCacheHits, ref localCacheMisses, ref localStaleFallbackHits);
                var prices = pricesResult.Payload;
                card.FromPrice = ExtractFromPrice(prices?.Prices?.PriceLos);
                card.Currency = prices?.Prices?.Currency;
                ApplySelectedStayPrice(card, prices?.Prices?.PriceLos, checkIn, checkOut);
            }

            var availabilityResult = availabilityTask is null ? null : await availabilityTask;
            if (availabilityResult is not null)
            {
                CountCache(availabilityResult.CacheDisposition, ref localCacheHits, ref localCacheMisses, ref localStaleFallbackHits);
                var availability = availabilityResult.Payload;
                ApplyAvailability(card, availability?.PropertyAvailability, checkIn, checkOut);
            }

            return (Card: card, localCacheHits, localCacheMisses, localStaleFallbackHits);
        });

        var enrichedResults = await Task.WhenAll(enrichmentTasks);
        var cards = enrichedResults
            .Where(result => result.Card is not null)
            .Select(result => result.Card!)
            .ToList();

        cacheHits += enrichedResults.Sum(result => result.localCacheHits);
        cacheMisses += enrichedResults.Sum(result => result.localCacheMisses);
        staleFallbackHits += enrichedResults.Sum(result => result.localStaleFallbackHits);

        cards = cards
            .OrderByDescending(card => card.IsAvailableForSelectedDates)
            .ThenBy(card => card.SelectedStayPrice ?? decimal.MaxValue)
            .ThenBy(card => card.FromPrice ?? decimal.MaxValue)
            .ThenBy(card => card.Name)
            .Take(12)
            .ToList();

        return new SuperControlListingSiteSnapshot
        {
            Search = new SuperControlSearchContext(query, guests, checkIn, checkOut),
            TotalActiveProperties = content.Properties.Count(property => property.Active),
            ReturnedProperties = cards.Count,
            Properties = cards,
            CacheHits = cacheHits,
            CacheMisses = cacheMisses,
            StaleFallbackHits = staleFallbackHits
        };
    }

    private static async Task<T> RunWithConcurrencyLimitAsync<T>(
        SemaphoreSlim gate,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SuperControlPropertyDetailResult> GetPropertyDetailAsync(
        int accountId,
        int propertyId,
        int guests,
        DateOnly? checkIn,
        DateOnly? checkOut,
        CancellationToken cancellationToken = default)
    {
        var account = await ResolveAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            return new SuperControlPropertyDetailResult
            {
                Error = "Accounts index could not be loaded."
            };
        }

        var contentIndexUrl = ResolveIndexUrl(account.ContentIndexUrl, $"properties/contentindex/{account.AccountId}");
        var content = await GetTypedByUrlAsync<ContentIndexResponse>(contentIndexUrl, TimeSpan.FromMinutes(2), cancellationToken);
        if (content is null)
        {
            return new SuperControlPropertyDetailResult
            {
                Error = "Content index could not be loaded."
            };
        }

        var contentEntry = content.Properties.FirstOrDefault(property => property.PropertyId == propertyId);
        if (contentEntry is null)
        {
            return new SuperControlPropertyDetailResult
            {
                Error = $"Property {propertyId} was not found in account {account.AccountId}."
            };
        }

        var configurationIndexUrl = ResolveIndexUrl(account.ConfigurationIndexUrl, $"properties/configurationindex/{account.AccountId}");
        var pricesIndexUrl = ResolveIndexUrl(account.PricesIndexUrl, $"properties/pricesindex/{account.AccountId}");
        var availabilityIndexUrl = ResolveIndexUrl(account.AvailabilityIndexUrl, $"properties/availabilityindex/{account.AccountId}");
        var configurationIndexTask = GetTypedByUrlAsync<ConfigurationIndexResponse>(configurationIndexUrl, TimeSpan.FromMinutes(2), cancellationToken);
        var pricesIndexTask = GetTypedByUrlAsync<PricesIndexResponse>(pricesIndexUrl, TimeSpan.FromMinutes(2), cancellationToken);
        var availabilityIndexTask = GetTypedByUrlAsync<AvailabilityIndexResponse>(availabilityIndexUrl, TimeSpan.FromMinutes(2), cancellationToken);
        await Task.WhenAll(configurationIndexTask, pricesIndexTask, availabilityIndexTask);

        var listingResult = await GetDeltaCachedPropertyPayloadAsync<PropertyListingResponse>(
            payloadType: "listing",
            propertyId: propertyId,
            payloadUrl: contentEntry.ListingUrl,
            lastUpdatedUtc: contentEntry.LastUpdated.UtcDateTime,
            cancellationToken);

        if (listingResult.Payload is null)
        {
            return new SuperControlPropertyDetailResult
            {
                Error = $"Property listing payload for {propertyId} could not be loaded."
            };
        }

        var detail = BuildDetailCard(contentEntry, listingResult.Payload);

        var configurationIndexEntry = configurationIndexTask.Result?.Properties.FirstOrDefault(property => property.PropertyId == propertyId);
        if (configurationIndexEntry is not null && !string.IsNullOrWhiteSpace(configurationIndexEntry.ConfigurationContentUrl))
        {
            var configurationResult = await GetDeltaCachedPropertyPayloadAsync<PropertyConfigurationListingResponse>(
                payloadType: "propertyconfiguration",
                propertyId: propertyId,
                payloadUrl: configurationIndexEntry.ConfigurationContentUrl,
                lastUpdatedUtc: configurationIndexEntry.LastUpdated.UtcDateTime,
                cancellationToken);
            ApplyConfiguration(detail, configurationResult.Payload?.Configuration);
        }

        var pricesIndexEntry = pricesIndexTask.Result?.Properties.FirstOrDefault(property => property.PropertyId == propertyId);
        if (pricesIndexEntry is not null)
        {
            var pricesResult = await GetDeltaCachedPropertyPayloadAsync<PricesListingResponse>(
                payloadType: "prices",
                propertyId: propertyId,
                payloadUrl: pricesIndexEntry.ListingUrl,
                lastUpdatedUtc: pricesIndexEntry.LastUpdated.UtcDateTime,
                cancellationToken);
            ApplyPrices(detail, pricesResult.Payload);
        }

        var availabilityIndexEntry = availabilityIndexTask.Result?.Properties.FirstOrDefault(property => property.PropertyId == propertyId);
        if (availabilityIndexEntry is not null)
        {
            var availabilityResult = await GetDeltaCachedPropertyPayloadAsync<AvailabilityListingResponse>(
                payloadType: "availability",
                propertyId: propertyId,
                payloadUrl: availabilityIndexEntry.ListingUrl,
                lastUpdatedUtc: availabilityIndexEntry.LastUpdated.UtcDateTime,
                cancellationToken);
            ApplyDetailAvailability(detail, availabilityResult.Payload?.PropertyAvailability, checkIn, checkOut);
        }

        return new SuperControlPropertyDetailResult
        {
            Property = detail
        };
    }

    private async Task<T?> GetTypedAsync<T>(string relativeUrl, TimeSpan ttl, CancellationToken cancellationToken) where T : class
    {
        var response = await GetResponseCachedAsync(relativeUrl, ttl, cancellationToken);
        return Deserialize<T>(response?.Body);
    }

    private async Task<SuperControlAccount?> ResolveAccountAsync(int preferredAccountId, CancellationToken cancellationToken)
    {
        var accounts = await GetTypedAsync<AccountsIndexResponse>("properties/index", TimeSpan.FromMinutes(2), cancellationToken);
        if (accounts?.Accounts is null || accounts.Accounts.Count == 0)
        {
            return null;
        }

        return accounts.Accounts.FirstOrDefault(account => account.AccountId == preferredAccountId)
            ?? accounts.Accounts[0];
    }

    private static string ResolveIndexUrl(string? accountUrl, string fallbackRelativeUrl)
    {
        return string.IsNullOrWhiteSpace(accountUrl)
            ? fallbackRelativeUrl
            : accountUrl;
    }

    private async Task<T?> GetTypedByUrlAsync<T>(string url, TimeSpan ttl, CancellationToken cancellationToken) where T : class
    {
        var response = await GetResponseCachedAsync(url, ttl, cancellationToken);
        return Deserialize<T>(response?.Body);
    }

    private async Task<SuperControlApiResponse?> GetResponseCachedAsync(string url, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var cacheKey = $"sc-api::{url}";
        if (_cache.TryGetValue(cacheKey, out SuperControlApiResponse? cached) && cached is not null)
        {
            return cached;
        }

        var response = Uri.TryCreate(url, UriKind.Absolute, out _)
            ? await _client.GetByUrlAsync(url, cancellationToken)
            : await _client.GetByRelativeUrlAsync(url, cancellationToken);

        if (response.IsSuccess)
        {
            _cache.Set(cacheKey, response, ttl);
        }

        return response;
    }

    private async Task<DeltaCachedPayloadResult<T>> GetDeltaCachedPropertyPayloadAsync<T>(
        string payloadType,
        int propertyId,
        string payloadUrl,
        DateTime lastUpdatedUtc,
        CancellationToken cancellationToken) where T : class
    {
        var sanitizedType = payloadType.ToLowerInvariant();
        var directory = Path.Combine(_diskCacheRoot, sanitizedType);
        Directory.CreateDirectory(directory);

        var bodyPath = Path.Combine(directory, $"{propertyId}.json");
        var metaPath = Path.Combine(directory, $"{propertyId}.meta.json");
        var normalizedLastUpdated = lastUpdatedUtc.ToUniversalTime();
        var memoryCacheKey = BuildDeltaMemoryCacheKey<T>(sanitizedType, propertyId, normalizedLastUpdated, payloadUrl);

        if (_cache.TryGetValue(memoryCacheKey, out T? memoryPayload) && memoryPayload is not null)
        {
            return new DeltaCachedPayloadResult<T>(memoryPayload, CacheDisposition.Hit);
        }

        try
        {
            var cachedMeta = ReadMetadata(metaPath);
            if (cachedMeta is not null
                && cachedMeta.LastUpdatedUtc == normalizedLastUpdated
                && File.Exists(bodyPath))
            {
                var cachedBody = await File.ReadAllTextAsync(bodyPath, cancellationToken);
                var cachedPayload = Deserialize<T>(cachedBody);
                if (cachedPayload is not null)
                {
                    _cache.Set(memoryCacheKey, cachedPayload, TimeSpan.FromMinutes(15));
                    return new DeltaCachedPayloadResult<T>(cachedPayload, CacheDisposition.Hit);
                }
            }
        }
        catch (IOException)
        {
            // Continue with live fetch when local disk cache is unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Continue with live fetch when local disk cache is unavailable.
        }

        var response = await _client.GetByUrlAsync(payloadUrl, cancellationToken);
        if (response.IsSuccess)
        {
            DeltaCacheMetadata metadata = new()
            {
                Url = payloadUrl,
                LastUpdatedUtc = normalizedLastUpdated,
                CachedAtUtc = DateTime.UtcNow
            };

            try
            {
                await File.WriteAllTextAsync(bodyPath, response.Body, cancellationToken);
                await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(metadata), cancellationToken);
            }
            catch (IOException)
            {
                // Continue; in-memory cache still provides short-term reuse.
            }
            catch (UnauthorizedAccessException)
            {
                // Continue; in-memory cache still provides short-term reuse.
            }

            var payload = Deserialize<T>(response.Body);
            if (payload is not null)
            {
                _cache.Set(memoryCacheKey, payload, TimeSpan.FromMinutes(15));
                return new DeltaCachedPayloadResult<T>(payload, CacheDisposition.Miss);
            }
        }

        try
        {
            if (File.Exists(bodyPath))
            {
                var staleBody = await File.ReadAllTextAsync(bodyPath, cancellationToken);
                var stalePayload = Deserialize<T>(staleBody);
                if (stalePayload is not null)
                {
                    _cache.Set(memoryCacheKey, stalePayload, TimeSpan.FromMinutes(2));
                    return new DeltaCachedPayloadResult<T>(stalePayload, CacheDisposition.StaleFallbackHit);
                }
            }
        }
        catch (IOException)
        {
            // Disk not available; fall through.
        }
        catch (UnauthorizedAccessException)
        {
            // Disk not available; fall through.
        }

        return new DeltaCachedPayloadResult<T>(default, CacheDisposition.Miss);
    }

    private static string BuildDeltaMemoryCacheKey<T>(
        string payloadType,
        int propertyId,
        DateTime lastUpdatedUtc,
        string payloadUrl)
    {
        return $"sc-delta::{typeof(T).Name}::{payloadType}::{propertyId}::{lastUpdatedUtc:O}::{payloadUrl}";
    }

    private static DeltaCacheMetadata? ReadMetadata(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<DeltaCacheMetadata>(json, SerializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void CountCache(CacheDisposition disposition, ref int hits, ref int misses, ref int staleHits)
    {
        switch (disposition)
        {
            case CacheDisposition.Hit:
                hits++;
                break;
            case CacheDisposition.StaleFallbackHit:
                staleHits++;
                break;
            default:
                misses++;
                break;
        }
    }

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static bool MatchesQuery(string? query, PropertyListingResponse listing, string location)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var haystack = string.Join(" ", [
            listing.AdContent?.PropertyName,
            listing.AdContent?.Subcaption,
            listing.AdContent?.Description,
            listing.AdContent?.LocationText,
            location
        ]);

        return haystack.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLocation(PropertyListingResponse listing)
    {
        var city = listing.Location?.Address?.City;
        var country = listing.Location?.Address?.Country;
        if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(country))
        {
            return $"{city}, {country}";
        }

        return city ?? country ?? listing.AdContent?.LocationText ?? "Location not set";
    }

    private static decimal? ExtractFromPrice(List<string>? priceLos)
    {
        if (priceLos is null || priceLos.Count == 0) return null;

        decimal? min = null;
        foreach (var row in priceLos.Take(120))
        {
            var parsed = ParseBestLosRate(row);
            if (parsed is null)
            {
                continue;
            }

            min = min is null ? parsed.MinPrice : Math.Min(min.Value, parsed.MinPrice);
        }

        return min;
    }

    private static void ApplyAvailability(
        SuperControlPropertyCard card,
        PropertyAvailabilityData? availability,
        DateOnly? checkIn,
        DateOnly? checkOut)
    {
        if (availability?.Availability is null)
        {
            return;
        }

        var series = availability.Availability;
        card.AvailabilityCoverageStartUtc = availability.StartDate.UtcDateTime;
        card.AvailabilityCoverageEndUtc = availability.EndDate.UtcDateTime;
        card.NextKnownAvailableDateUtc = FindNextAvailableDate(availability.StartDate.UtcDateTime.Date, series);

        if (checkIn is null || checkOut is null || checkOut <= checkIn)
        {
            return;
        }

        var start = checkIn.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Date;
        var end = checkOut.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Date;
        card.IsAvailableForSelectedDates = IsRangeAvailable(availability.StartDate.UtcDateTime.Date, series, start, end);
    }

    private static DateTime? FindNextAvailableDate(DateTime seriesStartUtcDate, string series)
    {
        for (var i = 0; i < series.Length; i++)
        {
            if (series[i] == 'Y')
            {
                return seriesStartUtcDate.AddDays(i);
            }
        }

        return null;
    }

    private static bool IsRangeAvailable(DateTime seriesStartUtcDate, string series, DateTime startUtcDate, DateTime endUtcDateExclusive)
    {
        var fromIndex = (int)(startUtcDate - seriesStartUtcDate).TotalDays;
        var nights = (int)(endUtcDateExclusive - startUtcDate).TotalDays;
        if (fromIndex < 0 || nights <= 0)
        {
            return false;
        }

        if (fromIndex + nights > series.Length)
        {
            return false;
        }

        for (var i = fromIndex; i < fromIndex + nights; i++)
        {
            if (series[i] != 'Y')
            {
                return false;
            }
        }

        return true;
    }

    private static readonly HtmlSanitizer DescriptionSanitizer = CreateDescriptionSanitizer();

    private static HtmlSanitizer CreateDescriptionSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "em", "ul", "ol", "li" })
        {
            sanitizer.AllowedTags.Add(tag);
        }
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        return sanitizer;
    }

    private static string BuildSafeDescriptionHtml(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(source);
        return DescriptionSanitizer.Sanitize(decoded);
    }

    private static SuperControlPropertyDetail BuildDetailCard(
        SuperControlPropertyIndexEntry contentEntry,
        PropertyListingResponse listing)
    {
        var location = BuildLocation(listing);
        var description = listing.AdContent?.Description ?? listing.AdContent?.AccommodationsSummary;

        return new SuperControlPropertyDetail
        {
            PropertyId = contentEntry.PropertyId,
            Name = listing.AdContent?.PropertyName ?? $"Property {contentEntry.PropertyId}",
            SubCaption = listing.AdContent?.Subcaption,
            DescriptionHtml = BuildSafeDescriptionHtml(description),
            Location = location,
            Images = listing.Images
                .Select(image => image.Url)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Cast<string>()
                .ToList(),
            Amenities = listing.Property?.Amenities ?? [],
            PropertyType = listing.Property?.PropertyType,
            LastUpdatedUtc = contentEntry.LastUpdated.UtcDateTime
        };
    }

    private static void ApplyPrices(SuperControlPropertyDetail detail, PricesListingResponse? pricesListing)
    {
        var priceLos = pricesListing?.Prices?.PriceLos;
        detail.FromPrice = ExtractFromPrice(priceLos);
        detail.Currency = pricesListing?.Prices?.Currency;
        detail.SampleRates = BuildSampleRates(priceLos).ToList();
    }

    private static IEnumerable<SampleRateRow> BuildSampleRates(List<string>? priceLos)
    {
        if (priceLos is null)
        {
            yield break;
        }

        var emitted = 0;
        foreach (var row in priceLos)
        {
            if (emitted >= 12)
            {
                yield break;
            }

            var parsed = ParseBestLosRate(row);
            if (parsed is null)
            {
                continue;
            }

            emitted++;
            yield return parsed;
        }
    }

    private static SampleRateRow? ParseBestLosRate(string row)
    {
        var parts = row.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        if (!DateOnly.TryParse(parts[0], out var date))
        {
            return null;
        }

        decimal? min = null;
        var bestNights = 0;

        // Format is documented as: date,pax,los1,los2,los3,...
        for (var i = 2; i < parts.Length; i++)
        {
            if (!decimal.TryParse(parts[i], NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0)
            {
                continue;
            }

            var nights = i - 1;
            if (min is null || price < min.Value)
            {
                min = price;
                bestNights = nights;
            }
        }

        if (!min.HasValue || bestNights <= 0)
        {
            return null;
        }

        return new SampleRateRow(date, bestNights, min.Value);
    }

    private static void ApplyDetailAvailability(
        SuperControlPropertyDetail detail,
        PropertyAvailabilityData? availability,
        DateOnly? checkIn,
        DateOnly? checkOut)
    {
        if (availability?.Availability is null)
        {
            return;
        }

        detail.AvailabilityCoverageStartUtc = availability.StartDate.UtcDateTime;
        detail.AvailabilityCoverageEndUtc = availability.EndDate.UtcDateTime;
        detail.NextKnownAvailableDateUtc = FindNextAvailableDate(availability.StartDate.UtcDateTime.Date, availability.Availability);

        if (checkIn is null || checkOut is null || checkOut <= checkIn)
        {
            return;
        }

        var start = checkIn.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Date;
        var end = checkOut.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Date;
        detail.IsAvailableForSelectedDates = IsRangeAvailable(availability.StartDate.UtcDateTime.Date, availability.Availability, start, end);
    }

    private static void ApplyConfiguration(SuperControlPropertyDetail detail, PropertyConfigurationData? configuration)
    {
        if (configuration is null)
        {
            return;
        }

        detail.CheckInTime = configuration.CheckInTime;
        detail.CheckOutTime = configuration.CheckOutTime;
        detail.ChildrenAllowed = configuration.ChildrenAllowed;
        detail.PetsAllowed = configuration.PetsAllowed;
        detail.SmokingAllowed = configuration.SmokingAllowed;
        detail.AllowBookings = configuration.AllowBookings;
        detail.AllowEnquiries = configuration.AllowEnquiries;
        detail.CancellationPolicy = configuration.CancellationPolicy?.Policy;
        detail.MerchantName = configuration.MerchantName;
        detail.MaximumOccupancyAdults = configuration.MaximumOccupancy?.Adults;
        detail.MaximumOccupancyChildren = configuration.MaximumOccupancy?.Children;
        detail.MaximumOccupancyGuests = configuration.MaximumOccupancy?.Guests;

        detail.AcceptedCardPaymentForms = configuration.AcceptedPaymentForms?.CardPaymentForms
            .Where(card => !string.IsNullOrWhiteSpace(card.CardCode))
            .Select(card => !string.IsNullOrWhiteSpace(card.CardType)
                ? $"{card.CardCode} ({card.CardType})"
                : card.CardCode!)
            .ToList() ?? [];
    }

    private static void ApplySelectedStayPrice(
        SuperControlPropertyCard card,
        List<string>? priceLos,
        DateOnly? checkIn,
        DateOnly? checkOut)
    {
        if (priceLos is null || checkIn is null || checkOut is null || checkOut <= checkIn)
        {
            return;
        }

        var nights = checkOut.Value.DayNumber - checkIn.Value.DayNumber;
        if (nights <= 0)
        {
            return;
        }

        var targetDate = checkIn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var row in priceLos)
        {
            var parts = row.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < nights + 2)
            {
                continue;
            }

            if (!string.Equals(parts[0], targetDate, StringComparison.Ordinal))
            {
                continue;
            }

            var losIndex = nights + 1;
            if (losIndex >= parts.Length)
            {
                continue;
            }

            if (!decimal.TryParse(parts[losIndex], NumberStyles.Number, CultureInfo.InvariantCulture, out var stayPrice) || stayPrice <= 0)
            {
                continue;
            }

            card.SelectedStayNights = nights;
            card.SelectedStayPrice = stayPrice;
            return;
        }
    }
}

public sealed class SuperControlListingSiteSnapshot
{
    public SuperControlSearchContext Search { get; init; } = new(null, 2, null, null);

    public int TotalActiveProperties { get; init; }

    public int ReturnedProperties { get; init; }

    public IReadOnlyList<SuperControlPropertyCard> Properties { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public int CacheHits { get; init; }

    public int CacheMisses { get; init; }

    public int StaleFallbackHits { get; init; }
}

public sealed record SuperControlSearchContext(
    string? Query,
    int Guests,
    DateOnly? CheckIn,
    DateOnly? CheckOut
);

public sealed class SuperControlPropertyCard
{
    public int PropertyId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? SubCaption { get; init; }

    public string? Description { get; init; }

    public string DescriptionHtml { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string? City { get; init; }

    public string? Country { get; init; }

    public string? HeroImageUrl { get; init; }

    public string? PropertyType { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public decimal? FromPrice { get; set; }

    public decimal? SelectedStayPrice { get; set; }

    public int? SelectedStayNights { get; set; }

    public string? Currency { get; set; }

    public bool IsAvailableForSelectedDates { get; set; }

    public DateTime? NextKnownAvailableDateUtc { get; set; }

    public DateTime? AvailabilityCoverageStartUtc { get; set; }

    public DateTime? AvailabilityCoverageEndUtc { get; set; }

    public DateTime LastUpdatedUtc { get; init; }
}

public sealed class DeltaCacheMetadata
{
    public string Url { get; init; } = string.Empty;

    public DateTime LastUpdatedUtc { get; init; }

    public DateTime CachedAtUtc { get; init; }
}

public enum CacheDisposition
{
    Hit,
    Miss,
    StaleFallbackHit
}

public sealed record DeltaCachedPayloadResult<T>(
    T? Payload,
    CacheDisposition CacheDisposition
);

public sealed class SuperControlPropertyDetailResult
{
    public SuperControlPropertyDetail? Property { get; init; }

    public string? Error { get; init; }
}

public sealed class SuperControlPropertyDetail
{
    public int PropertyId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? SubCaption { get; init; }

    public string DescriptionHtml { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string? PropertyType { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public IReadOnlyList<string> Images { get; init; } = [];

    public decimal? FromPrice { get; set; }

    public string? Currency { get; set; }

    public bool IsAvailableForSelectedDates { get; set; }

    public DateTime? NextKnownAvailableDateUtc { get; set; }

    public DateTime? AvailabilityCoverageStartUtc { get; set; }

    public DateTime? AvailabilityCoverageEndUtc { get; set; }

    public DateTime LastUpdatedUtc { get; init; }

    public IReadOnlyList<SampleRateRow> SampleRates { get; set; } = [];

    public string? CheckInTime { get; set; }

    public string? CheckOutTime { get; set; }

    public bool? ChildrenAllowed { get; set; }

    public bool? PetsAllowed { get; set; }

    public bool? SmokingAllowed { get; set; }

    public bool? AllowBookings { get; set; }

    public bool? AllowEnquiries { get; set; }

    public string? CancellationPolicy { get; set; }

    public string? MerchantName { get; set; }

    public int? MaximumOccupancyAdults { get; set; }

    public int? MaximumOccupancyGuests { get; set; }

    public int? MaximumOccupancyChildren { get; set; }

    public IReadOnlyList<string> AcceptedCardPaymentForms { get; set; } = [];
}

public sealed record SampleRateRow(DateOnly ArrivalDate, int Nights, decimal MinPrice);
