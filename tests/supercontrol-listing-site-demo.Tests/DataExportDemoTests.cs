using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using petergraves.Features.SuperControlDataExportDemo;
using petergraves.Integrations.SuperControl;
using petergraves.ViewModels.SuperControlDataExportDemo;

namespace supercontrol_listing_site_demo.Tests;

[TestClass]
public class DataExportDemoViewModelFactoryTests
{
    [TestMethod]
    public async Task BuildBookingsAsync_WhenApiReturnsStringError_SetsError()
    {
        var client = new RecordingDataExportClient
        {
            BookingsResponse = new SuperControlApiResponse(true, 200, "<string>Invalid token</string>")
        };
        var factory = CreateFactory(client);

        var response = await factory.BuildBookingsAsync(
            new DataExportBookingsRequestViewModel
            {
                SearchMode = "lastUpdate",
                LastUpdate = new DateOnly(2026, 3, 1)
            },
            CancellationToken.None);

        Assert.IsTrue(response.Loaded);
        Assert.AreEqual("Invalid token", response.Error);
    }

    [TestMethod]
    public async Task BuildBookingsAsync_WhenXmlValid_MapsAndAnonymizesEmail()
    {
        var xml = """
                  <scAPI>
                    <CurrentPage>1</CurrentPage>
                    <TotalPages>3</TotalPages>
                    <Payload>
                      <Booking>
                        <SystemId>ABC</SystemId>
                        <BookingId>123</BookingId>
                        <BookingDate>2026-03-01</BookingDate>
                        <Type>online</Type>
                        <Status>reserved</Status>
                        <Source>website</Source>
                        <Currency>GBP</Currency>
                        <ClientRef>CLIENT-1</ClientRef>
                        <Guest>
                          <FirstName>Jane</FirstName>
                          <LastName>Doe</LastName>
                          <Country>GB</Country>
                          <Email>jane.doe@example.com</Email>
                        </Guest>
                        <Properties>
                          <Property>
                            <Start>2026-04-01</Start>
                            <End>2026-04-08</End>
                            <PropertyId>55</PropertyId>
                            <Status>reserved</Status>
                            <Adults>2</Adults>
                            <Childrens>1</Childrens>
                            <Infants>0</Infants>
                            <Total>123.45</Total>
                          </Property>
                        </Properties>
                      </Booking>
                    </Payload>
                  </scAPI>
                  """;

        var client = new RecordingDataExportClient
        {
            BookingsResponse = new SuperControlApiResponse(true, 200, xml)
        };
        var factory = CreateFactory(client);

        var response = await factory.BuildBookingsAsync(
            new DataExportBookingsRequestViewModel
            {
                SearchMode = "lastUpdate",
                LastUpdate = new DateOnly(2026, 3, 1)
            },
            CancellationToken.None);

        Assert.IsTrue(response.Loaded);
        Assert.AreEqual(1, response.CurrentPage);
        Assert.AreEqual(3, response.TotalPages);
        Assert.AreEqual(1, response.Bookings.Count);
        Assert.AreEqual("j***@e***.com", response.Bookings[0].GuestEmail);
        Assert.IsNotNull(response.RawXml);
        Assert.IsFalse(response.RawXml.Contains("jane.doe@example.com", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(response.RawXml, "j***@e***.com");
    }

    [TestMethod]
    public async Task BuildBookingsAsync_WhenSingleModeMissingIdentifiers_DoesNotCallApi()
    {
        var client = new RecordingDataExportClient();
        var factory = CreateFactory(client);

        var response = await factory.BuildBookingsAsync(
            new DataExportBookingsRequestViewModel { SearchMode = "single" },
            CancellationToken.None);

        Assert.IsFalse(response.Loaded);
        Assert.AreEqual(0, client.BookingsCallCount);
    }

    [TestMethod]
    public async Task DataExportTutorialRoute_ReturnsSuccess()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/supercontrol-data-export-tutorial");

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    private static DataExportDemoViewModelFactory CreateFactory(RecordingDataExportClient client)
    {
        return new DataExportDemoViewModelFactory(
            client,
            Options.Create(new SuperControlOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://api.supercontrol.co.uk/v3/"
            }));
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SuperControl:ApiKey"] = "test-api-key",
                        ["SuperControl:BaseUrl"] = "https://api.supercontrol.co.uk/v3/"
                    });
                });

                builder.ConfigureTestServices(_ => { });
            });
    }

    private sealed class RecordingDataExportClient : ISuperControlClient
    {
        public int BookingsCallCount { get; private set; }

        public SuperControlApiResponse BookingsResponse { get; set; }
            = new(true, 200, "<scAPI><CurrentPage>1</CurrentPage><TotalPages>1</TotalPages><Payload /></scAPI>");

        public Task<SuperControlApiResponse> GetAccountsIndexAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SuperControlApiResponse> GetContentIndexAsync(int accountId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SuperControlApiResponse> GetByRelativeUrlAsync(string relativeUrl, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SuperControlApiResponse> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SuperControlApiResponse> GetDataExportBookingsAsync(string queryString, CancellationToken cancellationToken = default)
        {
            BookingsCallCount++;
            return Task.FromResult(BookingsResponse);
        }

        public Task<SuperControlApiResponse> GetDataExportPropertiesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
