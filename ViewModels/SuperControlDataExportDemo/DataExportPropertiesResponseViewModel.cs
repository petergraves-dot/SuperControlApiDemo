namespace petergraves.ViewModels.SuperControlDataExportDemo;

public sealed class DataExportPropertiesResponseViewModel
{
    public bool Loaded { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<DataExportPropertyViewModel> Properties { get; init; } = [];
    public string? RawXml { get; init; }
}

public sealed class DataExportPropertyViewModel
{
    public string SupercontrolId { get; init; } = string.Empty;
    public string PropertyName { get; init; } = string.Empty;
    public string Arrive { get; init; } = string.Empty;
    public string Depart { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Town { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Longitude { get; init; } = string.Empty;
    public string Latitude { get; init; } = string.Empty;
}
