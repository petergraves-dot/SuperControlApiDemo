namespace petergraves.ViewModels.SuperControlListingSiteDemo;

public sealed class SuperControlListingSiteDemoPropertyViewModel
{
    public int PropertyId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? SubCaption { get; init; }

    public string DescriptionHtml { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string? HeroImageUrl { get; init; }

    public string? PropertyType { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public decimal? FromPrice { get; init; }

    public decimal? SelectedStayPrice { get; init; }

    public int? SelectedStayNights { get; init; }

    public string? Currency { get; init; }

    public bool IsAvailableForSelectedDates { get; init; }

    public DateTime? NextKnownAvailableDateUtc { get; init; }

    public DateTime LastUpdatedUtc { get; init; }
}
