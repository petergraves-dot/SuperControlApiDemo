using Microsoft.Extensions.Options;
using petergraves.Integrations.SuperControl;
using petergraves.ViewModels.SuperControlProperty;

namespace petergraves.Features.SuperControlProperty;

public sealed class SuperControlPropertyViewModelFactory : ISuperControlPropertyViewModelFactory
{
    private readonly ISuperControlListingSiteService _listingSiteService;
    private readonly SuperControlOptions _options;

    public SuperControlPropertyViewModelFactory(
        ISuperControlListingSiteService listingSiteService,
        IOptions<SuperControlOptions> options)
    {
        _listingSiteService = listingSiteService;
        _options = options.Value;
    }

    public async Task<SuperControlPropertyResponseViewModel> BuildAsync(
        SuperControlPropertyRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = request with { Guests = Math.Clamp(request.Guests, 1, 30) };

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return CreateBaseResponse(normalizedRequest, "SuperControl__ApiKey is not configured.");
        }

        if (_options.AccountId is not int accountId)
        {
            return CreateBaseResponse(normalizedRequest, "SuperControl__AccountId is not configured.");
        }

        if (normalizedRequest.PropertyId <= 0)
        {
            return CreateBaseResponse(normalizedRequest, "Invalid property id.");
        }

        var result = await _listingSiteService.GetPropertyDetailAsync(
            accountId,
            normalizedRequest.PropertyId,
            normalizedRequest.Guests,
            normalizedRequest.CheckIn,
            normalizedRequest.CheckOut,
            cancellationToken);

        return new SuperControlPropertyResponseViewModel
        {
            Request = normalizedRequest,
            Error = result.Error,
            Property = result.Property is null ? null : MapDetail(result.Property),
            CalendarKey = _options.CalendarKey,
            HasCalendarKey = !string.IsNullOrWhiteSpace(_options.CalendarKey),
            CalendarPropertyId = result.Property?.PropertyId
                ?? (normalizedRequest.PropertyId > 0 ? normalizedRequest.PropertyId : _options.DefaultPropertyId)
        };
    }

    private SuperControlPropertyResponseViewModel CreateBaseResponse(
        SuperControlPropertyRequestViewModel request,
        string error)
    {
        return new SuperControlPropertyResponseViewModel
        {
            Request = request,
            Error = error,
            CalendarKey = _options.CalendarKey,
            HasCalendarKey = !string.IsNullOrWhiteSpace(_options.CalendarKey),
            CalendarPropertyId = request.PropertyId > 0 ? request.PropertyId : _options.DefaultPropertyId
        };
    }

    private static SuperControlPropertyDetailViewModel MapDetail(SuperControlPropertyDetail property)
    {
        return new SuperControlPropertyDetailViewModel
        {
            PropertyId = property.PropertyId,
            Name = property.Name,
            SubCaption = property.SubCaption,
            DescriptionHtml = property.DescriptionHtml,
            Location = property.Location,
            PropertyType = property.PropertyType,
            Amenities = property.Amenities,
            Images = property.Images,
            FromPrice = property.FromPrice,
            Currency = property.Currency,
            IsAvailableForSelectedDates = property.IsAvailableForSelectedDates,
            NextKnownAvailableDateUtc = property.NextKnownAvailableDateUtc,
            AvailabilityCoverageStartUtc = property.AvailabilityCoverageStartUtc,
            AvailabilityCoverageEndUtc = property.AvailabilityCoverageEndUtc,
            LastUpdatedUtc = property.LastUpdatedUtc,
            SampleRates = property.SampleRates
                .Select(rate => new SuperControlPropertySampleRateViewModel
                {
                    ArrivalDate = rate.ArrivalDate,
                    Nights = rate.Nights,
                    MinPrice = rate.MinPrice
                })
                .ToList(),
            CheckInTime = property.CheckInTime,
            CheckOutTime = property.CheckOutTime,
            ChildrenAllowed = property.ChildrenAllowed,
            PetsAllowed = property.PetsAllowed,
            SmokingAllowed = property.SmokingAllowed,
            AllowBookings = property.AllowBookings,
            AllowEnquiries = property.AllowEnquiries,
            CancellationPolicy = property.CancellationPolicy,
            MerchantName = property.MerchantName,
            MaximumOccupancyAdults = property.MaximumOccupancyAdults,
            MaximumOccupancyGuests = property.MaximumOccupancyGuests,
            MaximumOccupancyChildren = property.MaximumOccupancyChildren,
            AcceptedCardPaymentForms = property.AcceptedCardPaymentForms
        };
    }
}
