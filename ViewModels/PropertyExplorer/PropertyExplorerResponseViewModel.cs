namespace petergraves.ViewModels.PropertyExplorer;

public sealed class PropertyExplorerResponseViewModel
{
    public PropertyExplorerRequestViewModel Request { get; init; } = new();

    public bool Loaded { get; init; }

    public string? Error { get; init; }

    public int TotalActiveProperties { get; init; }

    public int MappedPropertyCount => Properties.Count(property => property.HasValidCoordinates);

    public int UnmappedPropertyCount => Properties.Count - MappedPropertyCount;

    public IReadOnlyList<PropertyExplorerPropertyViewModel> Properties { get; init; } = [];

    public IReadOnlyList<PropertyExplorerSearchSuggestionViewModel> Suggestions { get; init; } = [];
}

public sealed record PropertyExplorerSearchSuggestionViewModel(string Value, string Type);

public sealed class PropertyExplorerPropertyViewModel
{
    public int PropertyId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string? HeroImageUrl { get; init; }

    public string? PropertyType { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public bool HasValidCoordinates => Latitude.HasValue && Longitude.HasValue;

    public bool CoordinatesAreApproximate { get; init; }

    public int? MaximumGuests { get; init; }

    public decimal? FromPrice { get; init; }

    public decimal? SelectedStayPrice { get; init; }

    public int? SelectedStayNights { get; init; }

    public string? Currency { get; init; }

    public bool IsAvailableForSelectedDates { get; init; }

    public bool HasSelectedDates { get; init; }

    public string AvailabilityStatus { get; init; } = "not-requested";

    public bool? AllowBookings { get; init; }
}
