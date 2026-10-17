using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace petergraves.UiTests;

[TestClass]
public sealed class ConsentBrowserTests : PageTest
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string _baseUrl = string.Empty;
    private int _analyticsLoads;

    [TestInitialize]
    public async Task StartApplication()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseUrls("http://127.0.0.1:0");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Site:CanonicalBaseUrl"] = "https://demo.petergraves.co.uk",
                ["urls"] = "http://127.0.0.1:0",
                ["SuperControl:ApiKey"] = "test-api-key",
                ["SuperControl:BaseUrl"] = "https://api.supercontrol.co.uk/v3/"
            }));
        });
        _factory.UseKestrel(0);
        _client = _factory.CreateClient();
        _baseUrl = _factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        // Synthetic analytics verifies loading and removal without sending visits to Google.
        await Context.RouteAsync("https://www.googletagmanager.com/**", async route =>
        {
            _analyticsLoads++;
            await route.FulfillAsync(new()
            {
                ContentType = "application/javascript",
                Body = "document.cookie='demo_ga=synthetic; path=/; SameSite=Lax'; document.cookie='demo_ga_2B9VN4NJYY=synthetic; path=/; SameSite=Lax';"
            });
        });
        await Context.RouteAsync("https://*.google-analytics.com/**", route => route.AbortAsync());
    }

    [TestCleanup]
    public void StopApplication()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [TestMethod]
    public async Task Rejection_PersistsAcrossNavigationWithoutLoadingAnalytics()
    {
        await Page.GotoAsync(_baseUrl + "/cookies");
        await Expect(Page.Locator("#stcm-banner")).ToBeVisibleAsync();
        Assert.AreEqual(0, _analyticsLoads);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reject analytics cookies", Exact = true }).ClickAsync();
        await Page.GotoAsync(_baseUrl + "/");
        await Expect(Page.Locator("#stcm-banner")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#stcm-icon")).ToBeVisibleAsync();
        Assert.AreEqual(0, _analyticsLoads);
        Assert.IsFalse((await Context.CookiesAsync()).Any(cookie => cookie.Name.StartsWith("demo_ga", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AcceptanceAndWithdrawal_OnlyAffectDemoAnalyticsCookies()
    {
        await Context.AddCookiesAsync(new[] { new Cookie { Name = "_ga", Value = "main-site-synthetic", Url = _baseUrl } });
        await Page.GotoAsync(_baseUrl + "/cookies?test=synthetic#choices-title");
        Assert.IsTrue((await Context.CookiesAsync()).Any(cookie => cookie.Name == "_ga"));
        Assert.AreEqual(0, _analyticsLoads);
        await Page.Locator("#stcm-banner").GetByRole(AriaRole.Button, new() { Name = "Accept analytics cookies", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("() => document.cookie.includes('demo_ga=')");
        Assert.AreEqual(1, _analyticsLoads);
        Assert.AreEqual("demo", await Page.EvaluateAsync<string>("() => Array.from(dataLayer.find(item => item[0] === 'config'))[2].cookie_prefix"));
        Assert.AreEqual("none", await Page.EvaluateAsync<string>("() => Array.from(dataLayer.find(item => item[0] === 'config'))[2].cookie_domain"));
        Assert.AreEqual(_baseUrl + "/cookies", await Page.EvaluateAsync<string>("() => Array.from(dataLayer.find(item => item[0] === 'config'))[2].page_location"));
        await Page.ReloadAsync();
        await Page.WaitForFunctionAsync("() => document.cookie.includes('demo_ga=')");
        Assert.AreEqual(2, _analyticsLoads);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Change cookie preferences", Exact = true }).ClickAsync();
        await Page.Locator("#stcm-modal").GetByRole(AriaRole.Button, new() { Name = "Reject analytics cookies", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("() => !document.cookie.includes('demo_ga=')");
        await Page.WaitForTimeoutAsync(300);
        await Page.ReloadAsync();
        Assert.AreEqual(2, _analyticsLoads);
        Assert.IsFalse((await Context.CookiesAsync()).Any(cookie => cookie.Name.StartsWith("demo_ga", StringComparison.Ordinal)));
        Assert.IsTrue((await Context.CookiesAsync()).Any(cookie => cookie.Name == "_ga"));
    }
}
