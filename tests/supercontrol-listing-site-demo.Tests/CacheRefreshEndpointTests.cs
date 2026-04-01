using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http.Json;
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

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/supercontrol/cache-refresh?cadence=not-a-cadence");
        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains(json, "Invalid cadence");
    }

    [TestMethod]
    public async Task CacheRefreshEndpoint_WhenAccountsCadence_ReturnsRefreshSummary()
    {
        using var factory = CreateFactory(CreateAccountsIndexCachedResponse());
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/supercontrol/cache-refresh?cadence=accounts");
        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.AreEqual("accounts", root.GetProperty("cadence").GetString());
        Assert.AreEqual(1, root.GetProperty("accountCount").GetInt32());
        Assert.AreEqual(1, root.GetProperty("requests").GetInt32());
        Assert.AreEqual(1, root.GetProperty("successes").GetInt32());
    }

    [TestMethod]
    public async Task CacheRefreshEndpoint_WhenGetUsed_ReturnsMethodNotAllowed()
    {
        using var factory = CreateFactory(CreateAccountsIndexCachedResponse());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/internal/supercontrol/cache-refresh?cadence=accounts");

        Assert.AreEqual(System.Net.HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [TestMethod]
    public async Task CacheRefreshEndpoint_WhenRapidPostsExceedLimit_ReturnsTooManyRequests()
    {
        using var factory = CreateFactory(CreateAccountsIndexCachedResponse());
        using var client = factory.CreateClient();

        const int permitLimit = 6;
        for (var i = 0; i < permitLimit; i++)
        {
            using var allowedRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/supercontrol/cache-refresh?cadence=accounts");
            var allowedResponse = await client.SendAsync(allowedRequest);
            Assert.AreEqual(System.Net.HttpStatusCode.OK, allowedResponse.StatusCode);
        }

        using var blockedRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/supercontrol/cache-refresh?cadence=accounts");
        var blockedResponse = await client.SendAsync(blockedRequest);

        Assert.AreEqual((System.Net.HttpStatusCode)429, blockedResponse.StatusCode);
    }

    [TestMethod]
    public async Task DemoRefreshCacheEndpoint_WhenPostedAsJsonWithAntiforgeryToken_ReturnsOk()
    {
        using var factory = CreateFactory(CreateAccountsIndexCachedResponse());
        using var client = factory.CreateClient();

        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/supercontrol-demo/refresh-cache")
        {
            Content = JsonContent.Create(new
            {
                cacheRefreshCadence = "prices-availability",
                __RequestVerificationToken = antiforgeryToken
            })
        };

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(html, "Cadence prices-availability:");
    }

    [TestMethod]
    public async Task DemoRefreshCacheEndpoint_WhenPostedAsJsonWithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var factory = CreateFactory(CreateAccountsIndexCachedResponse());
        using var client = factory.CreateClient();

        await client.GetAsync("/supercontrol-demo");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/supercontrol-demo/refresh-cache")
        {
            Content = JsonContent.Create(new
            {
                cacheRefreshCadence = "prices-availability"
            })
        };

        var response = await client.SendAsync(request);

        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
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

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/supercontrol-demo");
        var html = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);

        Assert.IsTrue(match.Success, "Antiforgery token field was not rendered on /supercontrol-demo.");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
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

            if (cacheKey.StartsWith("properties/pricesindex/", StringComparison.Ordinal)
                || cacheKey.StartsWith("properties/availabilityindex/", StringComparison.Ordinal)
                || cacheKey.StartsWith("properties/contentindex/", StringComparison.Ordinal)
                || cacheKey.StartsWith("properties/configurationindex/", StringComparison.Ordinal))
            {
                return Task.FromResult(new CachedSuperControlResponse(
                    new SuperControlApiResponse(true, 200, "{}"),
                    CacheHit: false,
                    StaleFallback: false));
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

        public Task<SuperControlApiResponse> GetDataExportBookingsAsync(string queryString, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SuperControlApiResponse> GetDataExportPropertiesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
