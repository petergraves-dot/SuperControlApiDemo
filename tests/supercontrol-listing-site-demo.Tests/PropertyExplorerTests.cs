using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using petergraves.Features.PropertyExplorer;
using petergraves.Integrations.SuperControl;
using petergraves.ViewModels.PropertyExplorer;

namespace petergraves.Tests;

[TestClass]
public class PropertyExplorerTests
{
    [TestMethod]
    public void ParseBounds_WithValidBounds_ReturnsSpatialValue()
    {
        var request = new PropertyExplorerRequestViewModel { Bbox = "-4.12,54.72,-3.70,55.08" };

        var bounds = request.ParseBounds();

        Assert.IsNotNull(bounds);
        Assert.IsTrue(bounds.Contains(54.9, -3.9));
        Assert.IsFalse(bounds.Contains(55.2, -3.9));
    }

    [TestMethod]
    public void ParseBounds_WithInvalidCoordinate_ReturnsNull()
    {
        var request = new PropertyExplorerRequestViewModel { Bbox = "-4.12,95,-3.70,55.08" };

        Assert.IsNull(request.ParseBounds());
    }

    [TestMethod]
    public void PropertyMapBounds_WhenCrossingAntimeridian_MatchesEitherSide()
    {
        var bounds = new PropertyMapBounds(170, -20, -170, 20);

        Assert.IsTrue(bounds.Contains(0, 175));
        Assert.IsTrue(bounds.Contains(0, -175));
        Assert.IsFalse(bounds.Contains(0, 0));
    }

    [TestMethod]
    public async Task Factory_NormalizesGuestsViewAndZoom()
    {
        var service = new ExplorerRecordingService
        {
            Snapshot = new SuperControlListingSiteSnapshot
            {
                Suggestions = [new SuperControlSearchSuggestion("Castle Douglas, GB", "Location")]
            }
        };
        var factory = new PropertyExplorerViewModelFactory(
            service,
            Options.Create(new SuperControlOptions { ApiKey = "test", AccountId = 42 }));

        var response = await factory.BuildAsync(new PropertyExplorerRequestViewModel
        {
            Guests = 80,
            Zoom = 40,
            View = "MAP"
        });

        Assert.IsTrue(response.Loaded);
        Assert.AreEqual(30, response.Request.Guests);
        Assert.AreEqual(19, response.Request.Zoom);
        Assert.AreEqual("map", response.Request.View);
        Assert.AreEqual(30, service.Guests);
        Assert.AreEqual(100, service.MaxResults);
        Assert.AreEqual("Castle Douglas, GB", response.Suggestions.Single().Value);
    }

    [TestMethod]
    public async Task Factory_RejectsReversedDatesWithoutCallingService()
    {
        var service = new ExplorerRecordingService();
        var factory = new PropertyExplorerViewModelFactory(
            service,
            Options.Create(new SuperControlOptions { ApiKey = "test", AccountId = 42 }));

        var response = await factory.BuildAsync(new PropertyExplorerRequestViewModel
        {
            CheckIn = new DateOnly(2026, 11, 9),
            CheckOut = new DateOnly(2026, 11, 6)
        });

        Assert.AreEqual("Check-out must be after check-in.", response.Error);
        Assert.IsFalse(service.WasCalled);
    }

    private sealed class ExplorerRecordingService : ISuperControlListingSiteService
    {
        public bool WasCalled { get; private set; }
        public int Guests { get; private set; }
        public int MaxResults { get; private set; }
        public SuperControlListingSiteSnapshot Snapshot { get; init; } = new();

        public Task<SuperControlListingSiteSnapshot> BuildSnapshotAsync(
            int accountId,
            string? query,
            int guests,
            DateOnly? checkIn,
            DateOnly? checkOut,
            CancellationToken cancellationToken = default,
            PropertyMapBounds? bounds = null,
            int maxResults = 12)
        {
            WasCalled = true;
            Guests = guests;
            MaxResults = maxResults;
            return Task.FromResult(Snapshot);
        }

        public Task<SuperControlPropertyDetailResult> GetPropertyDetailAsync(
            int accountId,
            int propertyId,
            int guests,
            DateOnly? checkIn,
            DateOnly? checkOut,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
