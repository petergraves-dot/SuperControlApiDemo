using Microsoft.Extensions.Options;
using petergraves.Integrations.SuperControl;
using petergraves.ViewModels.PropertyExplorer;

namespace petergraves.Features.PropertyExplorer;

public sealed class PropertyExplorerViewModelFactory : IPropertyExplorerViewModelFactory
{
    private readonly ISuperControlListingSiteService _listingSiteService;
    private readonly SuperControlOptions _options;

    public PropertyExplorerViewModelFactory(
        ISuperControlListingSiteService listingSiteService,
        IOptions<SuperControlOptions> options)
    {
        _listingSiteService = listingSiteService;
        _options = options.Value;
    }

    public async Task<PropertyExplorerResponseViewModel> BuildAsync(
        PropertyExplorerRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = request with
        {
            Guests = Math.Clamp(request.Guests, 1, 30),
            Zoom = request.Zoom.HasValue ? Math.Clamp(request.Zoom.Value, 2, 19) : null,
            View = string.Equals(request.View, "map", StringComparison.OrdinalIgnoreCase) ? "map" : "list"
        };

        if (normalizedRequest.CheckIn.HasValue
            && normalizedRequest.CheckOut.HasValue
            && normalizedRequest.CheckOut <= normalizedRequest.CheckIn)
        {
            return new PropertyExplorerResponseViewModel
            {
                Request = normalizedRequest,
                Error = "Check-out must be after check-in."
            };
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new PropertyExplorerResponseViewModel
            {
                Request = normalizedRequest,
                Error = "SuperControl__ApiKey is not configured."
            };
        }

        if (_options.AccountId is not int accountId)
        {
            return new PropertyExplorerResponseViewModel
            {
                Request = normalizedRequest,
                Error = "SuperControl__AccountId is not configured."
            };
        }

        var snapshot = await _listingSiteService.BuildSnapshotAsync(
            accountId,
            normalizedRequest.Where,
            normalizedRequest.Guests,
            normalizedRequest.CheckIn,
            normalizedRequest.CheckOut,
            cancellationToken,
            normalizedRequest.ParseBounds(),
            maxResults: 100);

        var error = snapshot.Errors.Count > 0 ? string.Join(" ", snapshot.Errors) : null;
        var hasSelectedDates = normalizedRequest.CheckIn.HasValue && normalizedRequest.CheckOut.HasValue;

        return new PropertyExplorerResponseViewModel
        {
            Request = normalizedRequest,
            Loaded = error is null,
            Error = error,
            TotalActiveProperties = snapshot.TotalActiveProperties,
            Suggestions = snapshot.Suggestions
                .Select(suggestion => new PropertyExplorerSearchSuggestionViewModel(suggestion.Value, suggestion.Type))
                .ToList(),
            Properties = snapshot.Properties.Select(property => new PropertyExplorerPropertyViewModel
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
                SelectedStayPrice = property.SelectedStayPrice,
                SelectedStayNights = property.SelectedStayNights,
                Currency = property.Currency,
                IsAvailableForSelectedDates = property.IsAvailableForSelectedDates,
                HasSelectedDates = hasSelectedDates,
                AvailabilityStatus = GetAvailabilityStatus(property, hasSelectedDates),
                AllowBookings = property.AllowBookings
            }).ToList()
        };
    }

    private static string GetAvailabilityStatus(SuperControlPropertyCard property, bool hasSelectedDates)
    {
        if (!hasSelectedDates)
        {
            return "not-requested";
        }

        if (!property.HasAvailabilityData)
        {
            return "unknown";
        }

        if (!property.SelectedDatesWithinAvailabilityCoverage)
        {
            return "outside-coverage";
        }

        return property.IsAvailableForSelectedDates ? "available" : "unavailable";
    }
}
