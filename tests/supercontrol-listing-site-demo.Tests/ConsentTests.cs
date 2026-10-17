using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using petergraves.ViewModels;

namespace supercontrol_listing_site_demo.Tests;

[TestClass]
public sealed class ConsentTests
{
    [TestMethod]
    [DataRow("Development", false)]
    [DataRow("Production", true)]
    public async Task ConsentAssets_FollowTheEnvironment(string environment, bool expected)
    {
        using var factory = CreateFactory(environment);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        foreach (var path in new[] { "/", "/cookies" })
        {
            var html = await client.GetStringAsync(path);
            Assert.AreEqual(expected, html.Contains("/js/consent.js?v=", StringComparison.Ordinal));
            Assert.AreEqual(expected, html.Contains("silktide-consent-manager.js?v=", StringComparison.Ordinal));
            Assert.AreEqual(expected, html.Contains("silktide-consent-manager.css?v=", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("<environment", StringComparison.Ordinal));
            StringAssert.Contains(html, "href=\"/cookies\"");
            Assert.IsFalse(html.Contains("<script src=\"https://www.googletagmanager.com", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task CookiePolicy_RendersTheReviewedInventoryAndCanonicalUrl()
    {
        using var factory = CreateFactory("Production");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/cookies");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        StringAssert.Contains(html, "<link rel=\"canonical\" href=\"https://demo.petergraves.co.uk/cookies\" />");
        StringAssert.Contains(html, "data-cookie-preferences");
        foreach (var item in CookiePolicy.Current.Items)
        {
            StringAssert.Contains(html, item.Name);
            StringAssert.Contains(html, item.Purpose);
            StringAssert.Contains(html, item.Duration);
        }
        var sitemap = await client.GetStringAsync("/sitemap.xml");
        StringAssert.Contains(sitemap, "<loc>https://demo.petergraves.co.uk/cookies</loc>");
    }

    [TestMethod]
    public async Task Csp_AllowsAnalyticsWithoutInlineScriptExecution()
    {
        using var factory = CreateFactory("Production");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/cookies");
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        var scripts = policy.Split(';').Single(value => value.TrimStart().StartsWith("script-src ", StringComparison.Ordinal));
        StringAssert.Contains(scripts, "https://www.googletagmanager.com");
        Assert.IsFalse(scripts.Contains("'unsafe-inline'", StringComparison.Ordinal));
        Assert.IsFalse(scripts.Contains("'unsafe-eval'", StringComparison.Ordinal));
        StringAssert.Contains(policy, "https://*.google-analytics.com");
        StringAssert.Contains(policy, "frame-ancestors 'none'");
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Site:CanonicalBaseUrl"] = "https://demo.petergraves.co.uk",
                ["SuperControl:ApiKey"] = "test-api-key",
                ["SuperControl:BaseUrl"] = "https://api.supercontrol.co.uk/v3/"
            }));
        });
}
