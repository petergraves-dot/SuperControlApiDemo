namespace petergraves.ViewModels.SuperControlDataExportDemo;

public sealed record DataExportBookingsRequestViewModel
{
    /// <summary>One of: single, dateRange, lastUpdate</summary>
    public string SearchMode { get; init; } = "lastUpdate";

    // Single booking identifiers (SearchMode = "single")
    public int? BookingId { get; init; }
    public int? OwnerBookingId { get; init; }
    public string? OwnerRef { get; init; }

    // Date range (SearchMode = "dateRange")
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }

    // Last update (SearchMode = "lastUpdate")
    public DateOnly? LastUpdate { get; init; }

    // Common filters
    public string? BookingStatus { get; init; }

    // Pagination
    public int Limit { get; init; } = 100;
    public int Page { get; init; } = 1;
}
