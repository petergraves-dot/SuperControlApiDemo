using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using petergraves.Integrations.SuperControl;

namespace supercontrol_listing_site_demo.Tests;

[TestClass]
public sealed class SuperControlClientTests
{
    [TestMethod]
    public async Task GetByUrlAsync_WhenUrlUsesConfiguredOrigin_SendsToken()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.supercontrol.co.uk/v3/")
        };
        var client = CreateClient(httpClient);

        var response = await client.GetByUrlAsync("https://api.supercontrol.co.uk/v3/properties/index");

        Assert.IsTrue(response.IsSuccess);
        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual("test-token", handler.LastToken);
    }

    [TestMethod]
    public async Task GetByUrlAsync_WhenUrlUsesAnotherOrigin_RejectsWithoutSendingToken()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.supercontrol.co.uk/v3/")
        };
        var client = CreateClient(httpClient);

        var response = await client.GetByUrlAsync("https://example.com/collect");

        Assert.IsFalse(response.IsSuccess);
        Assert.AreEqual(400, response.StatusCode);
        Assert.AreEqual(0, handler.RequestCount);
    }

    [TestMethod]
    public async Task GetByUrlAsync_WhenUrlIsProtocolRelative_RejectsWithoutSendingToken()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.supercontrol.co.uk/v3/")
        };
        var client = CreateClient(httpClient);

        var response = await client.GetByUrlAsync("//example.com/collect");

        Assert.IsFalse(response.IsSuccess);
        Assert.AreEqual(0, handler.RequestCount);
    }

    private static SuperControlClient CreateClient(HttpClient httpClient)
    {
        return new SuperControlClient(
            httpClient,
            Options.Create(new SuperControlOptions
            {
                ApiKey = "test-token",
                AccountId = 42,
                BaseUrl = "https://api.supercontrol.co.uk/v3/"
            }));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string? LastToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastToken = request.Headers.TryGetValues("SC-TOKEN", out var values)
                ? values.SingleOrDefault()
                : null;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }
}
