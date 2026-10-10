using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using petergraves.Features.PropertyExplorer;
using petergraves.ViewModels.PropertyExplorer;

namespace petergraves.UiTests;

internal sealed class PropertyExplorerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPropertyExplorerViewModelFactory>();
            services.AddSingleton<IPropertyExplorerViewModelFactory, StubPropertyExplorerViewModelFactory>();
        });
    }
}

internal sealed class StubPropertyExplorerViewModelFactory : IPropertyExplorerViewModelFactory
{
    private static readonly IReadOnlyList<PropertyExplorerPropertyViewModel> Properties =
    [
        new()
        {
            PropertyId = 101,
            Name = "Cutlars Lodge",
            Location = "Castle Douglas, GB",
            PropertyType = "Cottage",
            Latitude = 54.9408,
            Longitude = -3.9306,
            MaximumGuests = 6,
            FromPrice = 202,
            Currency = "GBP",
            AvailabilityStatus = "not-requested"
        },
        new()
        {
            PropertyId = 102,
            Name = "Abbotsway Cottage",
            Location = "Castle Douglas, GB",
            PropertyType = "Cottage",
            Latitude = 54.9411,
            Longitude = -3.9310,
            CoordinatesAreApproximate = true,
            MaximumGuests = 8,
            FromPrice = 420,
            Currency = "GBP",
            AvailabilityStatus = "not-requested"
        },
        new()
        {
            PropertyId = 103,
            Name = "Harbour House",
            Location = "Kirkcudbright, GB",
            PropertyType = "House",
            Latitude = 54.8376,
            Longitude = -4.0492,
            MaximumGuests = 4,
            FromPrice = 315,
            Currency = "GBP",
            AvailabilityStatus = "not-requested"
        },
        new()
        {
            PropertyId = 104,
            Name = "Orroland Estate",
            Location = "Kirkcudbright, GB",
            PropertyType = "Country house",
            MaximumGuests = 22,
            FromPrice = 1800,
            Currency = "GBP",
            AvailabilityStatus = "not-requested"
        }
    ];

    private static readonly IReadOnlyList<PropertyExplorerSearchSuggestionViewModel> Suggestions =
    [
        new("Castle Douglas, GB", "Location"),
        new("Kirkcudbright, GB", "Location"),
        new("Abbotsway Cottage", "Property"),
        new("Cutlars Lodge", "Property"),
        new("Harbour House", "Property"),
        new("Orroland Estate", "Property")
    ];

    public Task<PropertyExplorerResponseViewModel> BuildAsync(
        PropertyExplorerRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = request with
        {
            Guests = Math.Clamp(request.Guests, 1, 30),
            View = string.Equals(request.View, "map", StringComparison.OrdinalIgnoreCase) ? "map" : "list"
        };

        IEnumerable<PropertyExplorerPropertyViewModel> matches = Properties;
        if (!string.IsNullOrWhiteSpace(request.Where))
        {
            matches = matches.Where(property =>
                property.Name.Contains(request.Where, StringComparison.OrdinalIgnoreCase)
                || property.Location.Contains(request.Where, StringComparison.OrdinalIgnoreCase));
        }

        var result = matches.Select(property => WithSearchState(property, normalizedRequest)).ToArray();
        return Task.FromResult(new PropertyExplorerResponseViewModel
        {
            Request = normalizedRequest,
            Loaded = true,
            TotalActiveProperties = Properties.Count,
            Properties = result,
            Suggestions = Suggestions
        });
    }

    private static PropertyExplorerPropertyViewModel WithSearchState(
        PropertyExplorerPropertyViewModel property,
        PropertyExplorerRequestViewModel request)
    {
        var hasDates = request.CheckIn.HasValue && request.CheckOut.HasValue;
        var nights = hasDates ? request.CheckOut!.Value.DayNumber - request.CheckIn!.Value.DayNumber : (int?)null;

        return new PropertyExplorerPropertyViewModel
        {
            PropertyId = property.PropertyId,
            Name = property.Name,
            Location = property.Location,
            HeroImageUrl = property.HeroImageUrl,
            PropertyType = property.PropertyType,
            Latitude = property.Latitude,
            Longitude = property.Longitude,
            CoordinatesAreApproximate = property.CoordinatesAreApproximate,
            MaximumGuests = property.MaximumGuests,
            FromPrice = property.FromPrice,
            SelectedStayPrice = hasDates ? property.FromPrice + 75 : null,
            SelectedStayNights = nights,
            Currency = property.Currency,
            IsAvailableForSelectedDates = hasDates,
            HasSelectedDates = hasDates,
            AvailabilityStatus = hasDates ? "available" : "not-requested",
            AllowBookings = true
        };
    }
}
