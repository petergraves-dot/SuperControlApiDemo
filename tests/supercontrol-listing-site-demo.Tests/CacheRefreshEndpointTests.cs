using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using petergraves.Integrations.SuperControl;

namespace supercontrol_listing_site_demo.Tests;

[TestClass]
public class CacheRefreshEndpointTests
{
    [TestMethod]
    public async Task CacheRefreshEndpoint_WhenCadenceInvalid_ReturnsBadRequest()
    {
        using var factory = CreateFactory(CreateAccountsIndexCachedResponse());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/internal/supercontrol/cache-refresh?cadence=not-a-cadence");
        var json = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(json, "Invalid cadence");
    }

    [TestMethod]
    public async Task CacheRefreshEndpoint_WhenAccountsCadence_ReturnsRefreshSummary()
    {
        using var factory = CreateFactory(CreateAccountsIndexCachedResponse());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/internal/supercontrol/cache-refresh?cadence=accounts");
        var json = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.AreEqual("accounts", root.GetProperty("cadence").GetString());
        Assert.AreEqual(1, root.GetProperty("accountCount").GetInt32());
        Assert.AreEqual(1, root.GetProperty("requests").GetInt32());
        Assert.AreEqual(1, root.GetProperty("successes").GetInt32());
    }

    private static WebApplicationFactory<Program> CreateFactory(CachedSuperControlResponse accountsIndexResponse)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SuperControl:ApiKey"] = "test-api-key",
                        ["SuperControl:BaseUrl"] = "https://api.supercontrol.co.uk/v3/",
                        ["SuperControl:AccountId"] = "42"
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<ISuperControlClient, TestSuperControlClient>();
                    services.AddSingleton<ISuperControlResponseCache>(
                        new TestSuperControlResponseCache(accountsIndexResponse));
                });
            });
    }

    private static CachedSuperControlResponse CreateAccountsIndexCachedResponse()
    {
        var payload = JsonSerializer.Serialize(new AccountsIndexResponse
        {
            Accounts =
            [
                new SuperControlAccount
                {
                    AccountId = 42,
                    CompanyName = "Demo Account"
                }
            ]
        });

        return new CachedSuperControlResponse(
            new SuperControlApiResponse(true, 200, payload),
            CacheHit: true,
            StaleFallback: false);
    }

    private sealed class TestSuperControlResponseCache : ISuperControlResponseCache
    {
        private readonly CachedSuperControlResponse _accountsIndexResponse;

        public TestSuperControlResponseCache(CachedSuperControlResponse accountsIndexResponse)
        {
            _accountsIndexResponse = accountsIndexResponse;
        }

        public Task<CachedSuperControlResponse> GetOrFetchAsync(
            string scope,
            string cacheKey,
            TimeSpan ttl,
            Func<CancellationToken, Task<SuperControlApiResponse>> fetch,
            CancellationToken cancellationToken = default)
        {
            if (cacheKey == "properties/index")
            {
                return Task.FromResult(_accountsIndexResponse);
            }

            throw new InvalidOperationException($"Unexpected cache key in test: {cacheKey}");
        }
    }

    private sealed class TestSuperControlClient : ISuperControlClient
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
}
