using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Options;
using petergraves.Controllers;
using petergraves.Features.SuperControlDemo;
using petergraves.Features.SuperControlListingSiteDemo;
using petergraves.Features.SuperControlProperty;
using petergraves.Integrations.SuperControl;
using petergraves.ViewModels.SuperControlListingSiteDemo;
using petergraves.ViewModels.SuperControlProperty;
using System.Text.Json;

namespace petergraves.Tests;

[TestClass]
public class SuperControlDemoModelTests
{
    [TestMethod]
    public async Task LoadDemoAsync_WhenApiKeyMissing_SetsError()
    {
        var model = new SuperControlDemoViewModel(
            new StubSuperControlClient(),
            new StubSuperControlResponseCache(),
            Options.Create(new SuperControlOptions { ApiKey = " " }));

        await model.LoadDemoAsync(CancellationToken.None);

        Assert.AreEqual("SuperControl API key is not configured. Set SuperControl__ApiKey and try again.", model.Error);
    }

    [TestMethod]
    public async Task OnPostAsync_WhenResponsesSuccessful_PopulatesFeaturedPropertyAndEndpointResults()
    {
        const int accountId = 42;
        const int propertyId = 1001;
        const string contentIndexUrl = "properties/contentindex/42";
        const string listingUrl = "properties/listing/1001";
        const string configurationIndexUrl = "properties/configurationindex/42";
        const string pricesIndexUrl = "properties/pricesindex/42";
        const string availabilityIndexUrl = "properties/availabilityindex/42";
        const string propertyConfigurationUrl = "properties/propertyconfiguration/1001";
        const string pricesListingUrl = "properties/prices/1001";
        const string availabilityListingUrl = "properties/availability/1001";

        var cache = new StubSuperControlResponseCache();
        cache.Add(
            "GET /properties/index|properties/index",
            CreateCachedResponse(
                new AccountsIndexResponse
                {
                    Accounts =
                    [
                        new SuperControlAccount
                        {
                            AccountId = accountId,
                            CompanyName = "Primary",
                            ContentIndexUrl = contentIndexUrl,
                            ConfigurationIndexUrl = configurationIndexUrl,
                            PricesIndexUrl = pricesIndexUrl,
                            AvailabilityIndexUrl = availabilityIndexUrl
                        },
                        new SuperControlAccount
                        {
                            AccountId = accountId,
                            CompanyName = "Duplicate"
                        }
                    ]
                },
                cacheHit: true));
        cache.Add(
            $"GET /properties/contentindex/{{accountId}}|{contentIndexUrl}",
            CreateCachedResponse(
                new ContentIndexResponse
                {
                    AccountId = accountId,
                    Properties =
                    [
                        new SuperControlPropertyIndexEntry
                        {
                            PropertyId = propertyId,
                            Active = true,
                            LastUpdated = DateTimeOffset.Parse("2026-01-15T10:00:00Z"),
                            ListingUrl = listingUrl
                        },
                        new SuperControlPropertyIndexEntry
                        {
                            PropertyId = 2002,
                            Active = false,
                            LastUpdated = DateTimeOffset.Parse("2025-12-01T10:00:00Z"),
                            ListingUrl = "properties/listing/2002"
                        }
                    ]
                }));
        cache.Add(
            $"GET /properties/listing/{{propertyId}}|{listingUrl}",
            CreateCachedResponse(
                new PropertyListingResponse
                {
                    PropertyId = propertyId,
                    AdContent = new PropertyAdContent
                    {
                        PropertyName = "Featured Villa"
                    }
                },
                staleFallback: true));
        cache.Add(
            $"GET /properties/configurationindex/{{accountId}}|{configurationIndexUrl}",
            CreateCachedResponse(
                new ConfigurationIndexResponse
                {
                    AccountId = accountId,
                    Properties =
                    [
                        new PropertyConfigurationIndexEntry
                        {
                            PropertyId = propertyId,
                            LastUpdated = DateTimeOffset.Parse("2026-01-10T10:00:00Z"),
                            ConfigurationContentUrl = propertyConfigurationUrl
                        }
                    ]
                }));
        cache.Add(
            $"GET /properties/pricesindex/{{accountId}}|{pricesIndexUrl}",
            CreateCachedResponse(
                new PricesIndexResponse
                {
                    AccountId = accountId,
                    Properties =
                    [
                        new PricesIndexEntry
                        {
                            PropertyId = propertyId,
                            LastUpdated = DateTimeOffset.Parse("2026-01-10T10:00:00Z"),
                            ListingUrl = pricesListingUrl
                        }
                    ]
                }));
        cache.Add(
            $"GET /properties/availabilityindex/{{accountId}}|{availabilityIndexUrl}",
            CreateCachedResponse(
                new AvailabilityIndexResponse
                {
                    AccountId = accountId,
                    Properties =
                    [
                        new AvailabilityIndexEntry
                        {
                            PropertyId = propertyId,
                            LastUpdated = DateTimeOffset.Parse("2026-01-10T10:00:00Z"),
                            ListingUrl = availabilityListingUrl
                        }
                    ]
                }));
        cache.Add(
            $"GET /properties/propertyconfiguration/{{propertyId}}|{propertyConfigurationUrl}",
            CreateCachedResponse(new PropertyConfigurationListingResponse { PropertyId = propertyId }));
        cache.Add(
            $"GET /properties/prices/{{propertyId}}|{pricesListingUrl}",
            CreateCachedResponse(new PricesListingResponse { PropertyId = propertyId }, cacheHit: true));
        cache.Add(
            $"GET /properties/availability/{{propertyId}}|{availabilityListingUrl}",
            CreateCachedResponse(new AvailabilityListingResponse { PropertyId = propertyId }));

        var model = new SuperControlDemoViewModel(
            new StubSuperControlClient(),
            cache,
            Options.Create(new SuperControlOptions { ApiKey = "test-key", AccountId = accountId }));

        await model.LoadDemoAsync(CancellationToken.None);

        Assert.IsNull(model.Error);
        Assert.IsTrue(model.AccountsIndexSucceeded);
        Assert.AreEqual(1, model.Accounts.Count);
        Assert.AreEqual(accountId, model.Accounts[0].AccountId);
        Assert.AreEqual(true, model.ContentIndexSucceeded);
        Assert.AreEqual(2, model.Properties.Count);
        Assert.AreEqual(1, model.ActivePropertyCount);
        Assert.AreEqual(1, model.InactivePropertyCount);
        Assert.AreEqual(true, model.PropertyListingSucceeded);
        Assert.AreEqual("Featured Villa", model.FeaturedPropertyName);
        Assert.AreEqual(true, model.ConfigurationIndex.Succeeded);
        Assert.AreEqual(true, model.PricesIndex.Succeeded);
        Assert.AreEqual(true, model.AvailabilityIndex.Succeeded);
        Assert.AreEqual(true, model.PropertyConfigurationListing.Succeeded);
        Assert.AreEqual(true, model.PricesListing.Succeeded);
        Assert.AreEqual(true, model.AvailabilityListing.Succeeded);
        StringAssert.Contains(model.AccountsIndexSummary ?? string.Empty, "cache-hit");
        StringAssert.Contains(model.PropertyListingSummary ?? string.Empty, "stale-cache");
        StringAssert.Contains(model.ConfigurationIndex.Summary, "accountId=42");
        StringAssert.Contains(model.PropertyConfigurationListing.Summary, "propertyId=1001");
    }

    [TestMethod]
    public async Task RefreshCacheAsync_WhenAccountsCadenceConfigured_ReturnsRefreshSummary()
    {
        var cache = new StubSuperControlResponseCache();
        cache.Add(
            "properties/index",
            CreateCachedResponse(
                new AccountsIndexResponse
                {
                    Accounts =
                    [
                        new SuperControlAccount
                        {
                            AccountId = 42,
                            CompanyName = "Primary"
                        }
                    ]
                },
                cacheHit: true));

        var model = new SuperControlDemoViewModel(
            new StubSuperControlClient(),
            cache,
            Options.Create(new SuperControlOptions { ApiKey = "test-key" }))
        {
            CacheRefreshCadence = "accounts"
        };

        await model.RefreshCacheAsync(CancellationToken.None);

        Assert.IsNull(model.Error);
        Assert.AreEqual(true, model.CacheRefreshSucceeded);
        StringAssert.Contains(model.CacheRefreshSummary ?? string.Empty, "accounts");
        StringAssert.Contains(model.CacheRefreshJson ?? string.Empty, "\"AccountCount\": 1");
    }

    private static CachedSuperControlResponse CreateCachedResponse<T>(
        T payload,
        bool cacheHit = false,
        bool staleFallback = false)
    {
        return new CachedSuperControlResponse(
            new SuperControlApiResponse(true, 200, JsonSerializer.Serialize(payload)),
            cacheHit,
            staleFallback);
    }
}

[TestClass]
public class SuperControlListingSiteDemoViewModelFactoryTests
{
    [TestMethod]
    public async Task BuildAsync_WhenApiKeyMissing_ReturnsErrorResponse()
    {
        var factory = CreateFactory(new SuperControlOptions { ApiKey = "" });

        var response = await factory.BuildAsync(new SuperControlListingSiteDemoRequestViewModel(), CancellationToken.None);

        Assert.IsFalse(response.Loaded);
        Assert.AreEqual("SuperControl__ApiKey is not configured.", response.Error);
    }

    [TestMethod]
    public async Task BuildAsync_WhenAccountIdMissing_ReturnsErrorResponse()
    {
        var factory = CreateFactory(new SuperControlOptions { ApiKey = "test-key" });

        var response = await factory.BuildAsync(new SuperControlListingSiteDemoRequestViewModel(), CancellationToken.None);

        Assert.IsFalse(response.Loaded);
        Assert.AreEqual("SuperControl__AccountId is not configured.", response.Error);
    }

    [DataTestMethod]
    [DataRow(0, 1)]
    [DataRow(31, 30)]
    public async Task BuildAsync_ClampsGuestsIntoRangeAndCallsService(int requestedGuests, int expectedGuests)
    {
        var service = new RecordingListingSiteService();
        var factory = CreateFactory(
            new SuperControlOptions { ApiKey = "test-key", AccountId = 42 },
            service);

        var response = await factory.BuildAsync(new SuperControlListingSiteDemoRequestViewModel { Guests = requestedGuests }, CancellationToken.None);

        Assert.IsTrue(response.Loaded);
        Assert.AreEqual(expectedGuests, response.Request.Guests);
        Assert.AreEqual(42, service.LastAccountId);
        Assert.AreEqual(expectedGuests, service.LastGuests);
    }

    [TestMethod]
    public async Task Index_WhenInvoked_ReturnsViewResultWithResponseModel()
    {
        var expected = new SuperControlListingSiteDemoResponseViewModel
        {
            Loaded = true,
            Request = new SuperControlListingSiteDemoRequestViewModel { Guests = 2 }
        };
        var controller = new SuperControlListingSiteDemoController(new StubListingSiteDemoViewModelFactory(expected));

        var result = await controller.Index(new SuperControlListingSiteDemoRequestViewModel { Guests = 2 }, CancellationToken.None);

        var viewResult = result as ViewResult;
        Assert.IsNotNull(viewResult);
        Assert.AreSame(expected, viewResult.Model);
    }

    private static ISuperControlListingSiteDemoViewModelFactory CreateFactory(
        SuperControlOptions options,
        RecordingListingSiteService? service = null)
    {
        return new SuperControlListingSiteDemoViewModelFactory(
            service ?? new RecordingListingSiteService(),
            Options.Create(options));
    }
}

[TestClass]
public class SuperControlPropertyViewModelFactoryTests
{
    [TestMethod]
    public async Task BuildAsync_WhenPropertyIdInvalid_ReturnsErrorResponse()
    {
        var factory = CreateFactory(new SuperControlOptions { ApiKey = "test-key", AccountId = 42 });

        var response = await factory.BuildAsync(new SuperControlPropertyRequestViewModel { PropertyId = 0 }, CancellationToken.None);

        Assert.AreEqual("Invalid property id.", response.Error);
    }

    [DataTestMethod]
    [DataRow(0, 1)]
    [DataRow(31, 30)]
    public async Task BuildAsync_ClampsGuestsAndCallsService(int requestedGuests, int expectedGuests)
    {
        var service = new RecordingListingSiteService();
        var factory = CreateFactory(
            new SuperControlOptions { ApiKey = "test-key", AccountId = 42 },
            service);

        var response = await factory.BuildAsync(
            new SuperControlPropertyRequestViewModel { PropertyId = 1001, Guests = requestedGuests },
            CancellationToken.None);

        Assert.AreEqual(expectedGuests, response.Request.Guests);
        Assert.AreEqual(42, service.LastAccountId);
        Assert.AreEqual(1001, service.LastPropertyId);
        Assert.AreEqual(expectedGuests, service.LastGuests);
    }

    [TestMethod]
    public async Task Index_WhenInvoked_ReturnsViewResultWithResponseModel()
    {
        var expected = new SuperControlPropertyResponseViewModel
        {
            Request = new SuperControlPropertyRequestViewModel { PropertyId = 123 }
        };
        var controller = new SuperControlPropertyController(new StubSuperControlPropertyViewModelFactory(expected));

        var result = await controller.Index(
            123,
            new SuperControlPropertyRequestViewModel { Guests = 2 },
            CancellationToken.None);

        var viewResult = result as ViewResult;
        Assert.IsNotNull(viewResult);
        Assert.AreSame(expected, viewResult.Model);
    }

    private static ISuperControlPropertyViewModelFactory CreateFactory(
        SuperControlOptions options,
        RecordingListingSiteService? service = null)
    {
        return new SuperControlPropertyViewModelFactory(
            service ?? new RecordingListingSiteService(),
            Options.Create(options));
    }
}

internal sealed class StubSuperControlClient : ISuperControlClient
{
    public Task<SuperControlApiResponse> GetAccountsIndexAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<SuperControlApiResponse> GetContentIndexAsync(int accountId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<SuperControlApiResponse> GetByRelativeUrlAsync(string relativeUrl, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<SuperControlApiResponse> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}

internal sealed class StubSuperControlResponseCache : ISuperControlResponseCache
{
    private readonly Dictionary<string, CachedSuperControlResponse> _responses = new(StringComparer.Ordinal);

    public void Add(string cacheKey, CachedSuperControlResponse response)
    {
        _responses[cacheKey] = response;
    }

    public Task<CachedSuperControlResponse> GetOrFetchAsync(
        string scope,
        string cacheKey,
        TimeSpan ttl,
        Func<CancellationToken, Task<SuperControlApiResponse>> fetch,
        CancellationToken cancellationToken = default)
    {
        if (_responses.TryGetValue(cacheKey, out var response))
        {
            return Task.FromResult(response);
        }

        throw new InvalidOperationException($"Unexpected cache key: {cacheKey}");
    }
}

internal sealed class RecordingListingSiteService : ISuperControlListingSiteService
{
    public int? LastAccountId { get; private set; }

    public int? LastPropertyId { get; private set; }

    public int? LastGuests { get; private set; }

    public Task<SuperControlListingSiteSnapshot> BuildSnapshotAsync(
        int accountId,
        string? query,
        int guests,
        DateOnly? checkIn,
        DateOnly? checkOut,
        CancellationToken cancellationToken = default)
    {
        LastAccountId = accountId;
        LastGuests = guests;
        return Task.FromResult(new SuperControlListingSiteSnapshot());
    }

    public Task<SuperControlPropertyDetailResult> GetPropertyDetailAsync(
        int accountId,
        int propertyId,
        int guests,
        DateOnly? checkIn,
        DateOnly? checkOut,
        CancellationToken cancellationToken = default)
    {
        LastAccountId = accountId;
        LastPropertyId = propertyId;
        LastGuests = guests;
        return Task.FromResult(new SuperControlPropertyDetailResult());
    }
}

internal sealed class StubListingSiteDemoViewModelFactory : ISuperControlListingSiteDemoViewModelFactory
{
    private readonly SuperControlListingSiteDemoResponseViewModel _response;

    public StubListingSiteDemoViewModelFactory(SuperControlListingSiteDemoResponseViewModel response)
    {
        _response = response;
    }

    public Task<SuperControlListingSiteDemoResponseViewModel> BuildAsync(
        SuperControlListingSiteDemoRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_response);
    }
}

internal sealed class StubSuperControlPropertyViewModelFactory : ISuperControlPropertyViewModelFactory
{
    private readonly SuperControlPropertyResponseViewModel _response;

    public StubSuperControlPropertyViewModelFactory(SuperControlPropertyResponseViewModel response)
    {
        _response = response;
    }

    public Task<SuperControlPropertyResponseViewModel> BuildAsync(
        SuperControlPropertyRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_response);
    }
}
