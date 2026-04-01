namespace petergraves.ViewModels.SuperControlDataExportDemo;

public sealed class DataExportBookingsResponseViewModel
{
    public DataExportBookingsRequestViewModel Request { get; init; } = new();
    public bool Loaded { get; init; }
    public string? Error { get; init; }
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<DataExportBookingViewModel> Bookings { get; init; } = [];
    public string? RawXml { get; init; }
}

public sealed class DataExportBookingViewModel
{
    public int BookingId { get; init; }
    public string BookingDate { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string ClientRef { get; init; } = string.Empty;
    public string GuestName { get; init; } = string.Empty;
    public string GuestEmail { get; init; } = string.Empty;
    public string GuestCountry { get; init; } = string.Empty;
    public IReadOnlyList<DataExportBookingPropertyViewModel> Properties { get; init; } = [];
}

public sealed class DataExportBookingPropertyViewModel
{
    public int PropertyId { get; init; }
    public string Start { get; init; } = string.Empty;
    public string End { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Adults { get; init; }
    public int Children { get; init; }
    public int Infants { get; init; }
    public decimal Total { get; init; }
}
