namespace petergraves.ViewModels.SuperControlProperty;

public sealed class SuperControlPropertyDetailViewModel
{
    public int PropertyId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? SubCaption { get; init; }

    public string DescriptionHtml { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string? PropertyType { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public IReadOnlyList<string> Images { get; init; } = [];

    public decimal? FromPrice { get; init; }

    public string? Currency { get; init; }

    public bool IsAvailableForSelectedDates { get; init; }

    public DateTime? NextKnownAvailableDateUtc { get; init; }

    public DateTime? AvailabilityCoverageStartUtc { get; init; }

    public DateTime? AvailabilityCoverageEndUtc { get; init; }

    public DateTime LastUpdatedUtc { get; init; }

    public IReadOnlyList<SuperControlPropertySampleRateViewModel> SampleRates { get; init; } = [];

    public string? CheckInTime { get; init; }

    public string? CheckOutTime { get; init; }

    public bool? ChildrenAllowed { get; init; }

    public bool? PetsAllowed { get; init; }

    public bool? SmokingAllowed { get; init; }

    public bool? AllowBookings { get; init; }

    public bool? AllowEnquiries { get; init; }

    public string? CancellationPolicy { get; init; }

    public string? MerchantName { get; init; }

    public int? MaximumOccupancyAdults { get; init; }

    public int? MaximumOccupancyGuests { get; init; }

    public int? MaximumOccupancyChildren { get; init; }

    public IReadOnlyList<string> AcceptedCardPaymentForms { get; init; } = [];
}
