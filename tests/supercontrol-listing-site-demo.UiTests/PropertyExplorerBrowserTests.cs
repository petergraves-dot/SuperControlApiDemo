using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace petergraves.UiTests;

[TestClass]
public sealed class PropertyExplorerBrowserTests : PageTest
{
    private static readonly byte[] TransparentPixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAF/gL+Xh9WAAAAAElFTkSuQmCC");

    private PropertyExplorerWebApplicationFactory? _factory;
    private HttpClient? _client;
    private string _baseUrl = string.Empty;

    [TestInitialize]
    public async Task StartApplicationAsync()
    {
        _factory = new PropertyExplorerWebApplicationFactory();
        _factory.UseKestrel(0);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var server = _factory.Services.GetRequiredService<IServer>();
        _baseUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        await Page.RouteAsync("https://tile.openstreetmap.org/**", route =>
            route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "image/png",
                BodyBytes = TransparentPixel
            }));
    }

    [TestCleanup]
    public void StopApplication()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [TestMethod]
    public async Task Explorer_RendersCardsMapAndClusters()
    {
        await OpenExplorerAsync();

        await Expect(Page.Locator("[data-property-id]")).ToHaveCountAsync(4);
        await Expect(Page.Locator("[data-visible-count]")).ToHaveTextAsync("4");
        await Expect(Page.Locator("[data-unmapped-note]")).ToContainTextAsync("1 without a map location");
        await Expect(Page.Locator("#property-map")).ToHaveClassAsync(new Regex("leaflet-container"));
        await Expect(Page.Locator(".marker-cluster")).ToHaveCountAsync(1);
    }

    [TestMethod]
    public async Task MovingMap_FiltersListAndOffersSearchWithinBounds()
    {
        await OpenExplorerAsync();

        await Page.Locator(".leaflet-control-zoom-in").ClickAsync();
        await Expect(Page.Locator("[data-search-area]")).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-property-id='104']")).ToBeHiddenAsync();
        await Expect(Page.Locator("[data-unmapped-note]")).ToBeHiddenAsync();
        await Expect(Page.Locator("[data-result-noun]")).ToContainTextAsync("map area");
    }

    [TestMethod]
    public async Task SearchThisArea_WritesBoundsAndZoomToTheUrl()
    {
        await OpenExplorerAsync();

        await Page.Locator(".leaflet-control-zoom-in").ClickAsync();
        await Expect(Page.Locator("[data-search-area]")).ToBeVisibleAsync();
        await Page.Locator("[data-search-area]").ClickAsync();

        await Page.WaitForURLAsync(new Regex(@"[?&]bbox="));
        var url = new Uri(Page.Url);
        var query = QueryHelpers.ParseQuery(url.Query);
        Assert.IsFalse(string.IsNullOrWhiteSpace(query["bbox"]));
        Assert.IsFalse(string.IsNullOrWhiteSpace(query["zoom"]));
        Assert.AreEqual("list", query["view"].ToString());
    }

    [TestMethod]
    public async Task LocationSearch_SupportsKeyboardSuggestions()
    {
        await OpenExplorerAsync("?bbox=-4.2,54.7,-3.7,55.1&zoom=9");
        var where = Page.Locator("[data-location-search]");

        await where.FillAsync("Abbots");
        await Expect(Page.Locator("[data-search-suggestions]")).ToBeVisibleAsync();
        await Expect(Page.Locator("[role='option']")).ToHaveCountAsync(1);
        await where.PressAsync("ArrowDown");
        await where.PressAsync("Enter");

        await Expect(where).ToHaveValueAsync("Abbotsway Cottage");
        await Expect(Page.Locator("[data-bbox-input]")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("[data-zoom-input]")).ToHaveValueAsync(string.Empty);
    }

    [TestMethod]
    public async Task StableUrl_RestoresDatesGuestsLocationAndView()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await OpenExplorerAsync("?where=Castle%20Douglas&checkIn=2026-11-06&checkOut=2026-11-09&guests=5&view=map");

        await Expect(Page.Locator("[name='where']")).ToHaveValueAsync("Castle Douglas");
        await Expect(Page.Locator("[name='checkIn']")).ToHaveValueAsync("2026-11-06");
        await Expect(Page.Locator("[name='checkOut']")).ToHaveValueAsync("2026-11-09");
        await Expect(Page.Locator("[name='guests']")).ToHaveValueAsync("5");
        await Expect(Page.Locator("[data-property-explorer]")).ToHaveAttributeAsync("data-active-view", "map");
        await Expect(Page.Locator("[data-map-panel]")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task SelectedDates_ShowAvailabilityPriceCapacityAndBookingHandoff()
    {
        await OpenExplorerAsync("?where=Cutlars&checkIn=2026-11-06&checkOut=2026-11-09&guests=5");
        var card = Page.Locator("[data-property-id='101']");

        await Expect(card.Locator(".pe-status")).ToHaveTextAsync("Available");
        await Expect(card).ToContainTextAsync("Sleeps 6");
        await Expect(card).ToContainTextAsync("3-night total");
        await Expect(card).ToContainTextAsync("277 GBP");

        var handoffUrl = await card.GetByRole(AriaRole.Link, new() { Name = "View property" }).GetAttributeAsync("href");
        Assert.IsNotNull(handoffUrl);
        StringAssert.Contains(handoffUrl, "/supercontrol-listing-site-demo/property/101");
        StringAssert.Contains(handoffUrl, "checkIn=2026-11-06");
        StringAssert.Contains(handoffUrl, "checkOut=2026-11-09");
        StringAssert.Contains(handoffUrl, "guests=5");
    }

    [TestMethod]
    public async Task ResetSearch_ClearsAllSearchState()
    {
        await OpenExplorerAsync("?where=Castle%20Douglas&checkIn=2026-11-06&checkOut=2026-11-09&guests=5&bbox=-4.2,54.7,-3.7,55.1&zoom=9&view=map");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Reset search" }).ClickAsync();
        await Page.WaitForURLAsync($"{_baseUrl}/supercontrol-property-explorer");

        Assert.AreEqual(string.Empty, new Uri(Page.Url).Query);
        await Expect(Page.Locator("[name='where']")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("[name='checkIn']")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("[name='checkOut']")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("[name='guests']")).ToHaveValueAsync("2");
    }

    [TestMethod]
    public async Task MobileViewSwitch_TogglesPanelsAndPersistsChoiceInUrl()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await OpenExplorerAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Map", Exact = true }).ClickAsync();
        await Expect(Page.Locator("[data-property-explorer]")).ToHaveAttributeAsync("data-active-view", "map");
        await Expect(Page.Locator("[data-map-panel]")).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-list-panel]")).ToBeHiddenAsync();
        StringAssert.Contains(Page.Url, "view=map");

        await Page.GetByRole(AriaRole.Button, new() { Name = "List", Exact = true }).ClickAsync();
        await Expect(Page.Locator("[data-list-panel]")).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-map-panel]")).ToBeHiddenAsync();
        StringAssert.Contains(Page.Url, "view=list");
    }

    [TestMethod]
    public async Task MarkerClick_SelectsTheMatchingPropertyCard()
    {
        await OpenExplorerAsync();

        var marker = Page.Locator(".leaflet-marker-icon[title^='Harbour House']");
        await Expect(marker).ToBeVisibleAsync();
        await marker.ClickAsync();
        await Expect(Page.Locator("[data-property-id='103']")).ToHaveClassAsync(new Regex("is-selected"));
    }

    private async Task OpenExplorerAsync(string query = "")
    {
        var response = await Page.GotoAsync(
            $"{_baseUrl}/supercontrol-property-explorer{query}",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);
        await Expect(Page.Locator("#property-map")).ToHaveClassAsync(new Regex("leaflet-container"));
    }
}
