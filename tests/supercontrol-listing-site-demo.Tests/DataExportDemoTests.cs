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
    public async Task BuildBookingsAsync_WhenXmlValid_MapsAndAnonymizesPii()
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
                                                    <Address1>4 Market Street</Address1>
                                                    <Town>Dumfries</Town>
                                                    <Postcode>DG71BE</Postcode>
                                                    <TelMain>+447555666699</TelMain>
                          <Country>GB</Country>
                          <Email>jane.doe@example.com</Email>
                                                    <GuestId>121579778</GuestId>
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
        Assert.AreEqual("J*** D***", response.Bookings[0].GuestName);
        Assert.AreEqual("j***@e***.com", response.Bookings[0].GuestEmail);
        Assert.AreEqual("C***1", response.Bookings[0].ClientRef);
        Assert.IsNotNull(response.RawXml);
        Assert.IsFalse(response.RawXml.Contains("jane.doe@example.com", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("Jane", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("Doe", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("4 Market Street", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("Dumfries", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("DG71BE", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("+447555666699", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("121579778", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(response.RawXml, "j***@e***.com");
        StringAssert.Contains(response.RawXml, "J***");
        StringAssert.Contains(response.RawXml, "D***");
        StringAssert.Contains(response.RawXml, "DG***E");
        StringAssert.Contains(response.RawXml, "***99");
        StringAssert.Contains(response.RawXml, "1***8");
        }

        [TestMethod]
        public async Task BuildPropertiesAsync_WhenXmlValid_MapsAndAnonymizesPii()
        {
        var xml = """
              <scdata>
                <property>
                  <supercontrolID>55</supercontrolID>
                  <propertyname>The Cottage</propertyname>
                  <arrive>Friday</arrive>
                  <depart>Friday</depart>
                  <address>4 market st castle douglas</address>
                  <town>Dumfries</town>
                  <postcode>DG71BE</postcode>
                  <country>GB</country>
                  <longitude>-3.61</longitude>
                  <latitude>55.07</latitude>
                </property>
              </scdata>
              """;

        var client = new RecordingDataExportClient
        {
            PropertiesResponse = new SuperControlApiResponse(true, 200, xml)
        };
        var factory = CreateFactory(client);

        var response = await factory.BuildPropertiesAsync(CancellationToken.None);

        Assert.IsTrue(response.Loaded);
        Assert.AreEqual(1, response.Properties.Count);
        Assert.AreEqual("4***", response.Properties[0].Address);
        Assert.AreEqual("D***", response.Properties[0].Town);
        Assert.AreEqual("DG***E", response.Properties[0].Postcode);
        Assert.IsNotNull(response.RawXml);
        Assert.IsFalse(response.RawXml.Contains("4 market st castle douglas", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("Dumfries", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.RawXml.Contains("DG71BE", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(response.RawXml, "<address>4***</address>");
        StringAssert.Contains(response.RawXml, "<town>D***</town>");
        StringAssert.Contains(response.RawXml, "<postcode>DG***E</postcode>");
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

    [TestMethod]
    public async Task DataExportTestsRoute_ReturnsSuccess()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/supercontrol-data-export-tests");

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task ListingSiteTutorialRoute_ReturnsSuccess()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/supercontrol-listing-site-tutorial");

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task ListingSiteTestsRoute_ReturnsSuccess()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/supercontrol-listing-site-tests");

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

        public int PropertiesCallCount { get; private set; }

        public SuperControlApiResponse BookingsResponse { get; set; }
            = new(true, 200, "<scAPI><CurrentPage>1</CurrentPage><TotalPages>1</TotalPages><Payload /></scAPI>");

        public SuperControlApiResponse PropertiesResponse { get; set; }
            = new(true, 200, "<scdata />");

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
        {
            PropertiesCallCount++;
            return Task.FromResult(PropertiesResponse);
        }
    }
}
