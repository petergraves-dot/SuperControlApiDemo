using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace supercontrol_listing_site_demo.Tests;

[TestClass]
public sealed class SeoTests
{
    private const string CanonicalBaseUrl = "https://demo.petergraves.co.uk";

    [TestMethod]
    public async Task Robots_AllowsCrawlingAndAdvertisesCanonicalSitemap()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var robots = await client.GetStringAsync("/robots.txt");

        Assert.AreEqual(
            $"User-agent: *\nAllow: /\nSitemap: {CanonicalBaseUrl}/sitemap.xml\n",
            robots);
    }

    [TestMethod]
    public async Task Sitemap_ContainsThePublicDemoPagesAndNoSyntheticMetadata()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var sitemap = await client.GetStringAsync("/sitemap.xml");

        StringAssert.Contains(sitemap, $"<loc>{CanonicalBaseUrl}/</loc>");
        StringAssert.Contains(sitemap, $"<loc>{CanonicalBaseUrl}/supercontrol-demo</loc>");
        StringAssert.Contains(sitemap, $"<loc>{CanonicalBaseUrl}/supercontrol-listing-site-demo</loc>");
        StringAssert.Contains(sitemap, $"<loc>{CanonicalBaseUrl}/supercontrol-property-explorer</loc>");
        StringAssert.Contains(sitemap, $"<loc>{CanonicalBaseUrl}/supercontrol-data-export</loc>");
        StringAssert.Contains(sitemap, $"<loc>{CanonicalBaseUrl}/supercontrol-data-export/properties</loc>");
        Assert.IsFalse(sitemap.Contains("<lastmod>", StringComparison.Ordinal));
        Assert.IsFalse(sitemap.Contains("<changefreq>", StringComparison.Ordinal));
        Assert.IsFalse(sitemap.Contains("<priority>", StringComparison.Ordinal));
        Assert.IsFalse(sitemap.Contains("/supercontrol-listing-site-tutorial</loc>", StringComparison.Ordinal));
        Assert.IsFalse(sitemap.Contains("/supercontrol-property-explorer-tutorial</loc>", StringComparison.Ordinal));
        Assert.IsFalse(sitemap.Contains("/supercontrol-data-export-tutorial</loc>", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("/supercontrol-listing-site-demo/property/123")]
    [DataRow("/supercontrol-property-explorer/search")]
    [DataRow("/supercontrol-listing-site-tests")]
    [DataRow("/supercontrol-data-export-tests")]
    public async Task InteractiveAndTestPages_SendNoIndexHeader(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, path);

        using var response = await client.SendAsync(request);

        Assert.AreEqual("noindex", response.Headers.GetValues("X-Robots-Tag").Single());
    }

    [TestMethod]
    public async Task NoIndexHtml_HasRobotsMetaAndNoCanonical()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/supercontrol-listing-site-tests");
        var html = await response.Content.ReadAsStringAsync();

        StringAssert.Contains(html, "<meta name=\"robots\" content=\"noindex\" />");
        Assert.IsFalse(html.Contains("rel=\"canonical\"", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("/", "/")]
    [DataRow("/supercontrol-demo", "/supercontrol-demo")]
    [DataRow("/supercontrol-listing-site-demo", "/supercontrol-listing-site-demo")]
    [DataRow("/supercontrol-property-explorer", "/supercontrol-property-explorer")]
    [DataRow("/supercontrol-data-export", "/supercontrol-data-export")]
    [DataRow("/supercontrol-data-export/properties", "/supercontrol-data-export/properties")]
    [DataRow("/supercontrol-listing-site-tutorial", "/supercontrol-listing-site-tutorial")]
    [DataRow("/supercontrol-property-explorer-tutorial", "/supercontrol-property-explorer-tutorial")]
    [DataRow("/supercontrol-data-export-tutorial", "/supercontrol-data-export-tutorial")]
    public async Task IndexablePages_HaveDescriptionAndCanonicalWithoutNoIndex(
        string path,
        string canonicalPath)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("X-Robots-Tag"));
        StringAssert.Contains(html, "<meta name=\"description\"");
        StringAssert.Contains(html, $"<link rel=\"canonical\" href=\"{CanonicalBaseUrl}{canonicalPath}\" />");
        Assert.IsFalse(html.Contains("<meta name=\"robots\"", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(
        "/supercontrol-listing-site-tutorial",
        "https://petergraves.co.uk/field-notes/supercontrol-listing-site-demo")]
    [DataRow(
        "/supercontrol-property-explorer-tutorial",
        "https://petergraves.co.uk/field-notes/map-based-property-explorer-with-supercontrol-and-leaflet")]
    [DataRow(
        "/supercontrol-data-export-tutorial",
        "https://petergraves.co.uk/field-notes/supercontrol-data-export-api")]
    public async Task TutorialPages_PermanentlyRedirectToFieldNotesOutsideDevelopment(
        string path,
        string expectedLocation)
    {
        using var factory = CreateFactory("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.AreEqual(expectedLocation, response.Headers.Location?.AbsoluteUri);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment = "Development")
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Site:CanonicalBaseUrl"] = CanonicalBaseUrl,
                        ["SuperControl:ApiKey"] = "test-api-key",
                        ["SuperControl:BaseUrl"] = "https://api.supercontrol.co.uk/v3/"
                    });
                });

                builder.ConfigureTestServices(_ => { });
            });
    }
}
