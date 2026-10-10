using System.Globalization;
using petergraves.Integrations.SuperControl;

namespace petergraves.ViewModels.PropertyExplorer;

public sealed record PropertyExplorerRequestViewModel
{
    public string? Where { get; init; }

    public DateOnly? CheckIn { get; init; }

    public DateOnly? CheckOut { get; init; }

    public int Guests { get; init; } = 2;

    public string? Bbox { get; init; }

    public int? Zoom { get; init; }

    public string View { get; init; } = "list";

    public PropertyMapBounds? ParseBounds()
    {
        if (string.IsNullOrWhiteSpace(Bbox))
        {
            return null;
        }

        var values = Bbox.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 4
            || !double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var west)
            || !double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var south)
            || !double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var east)
            || !double.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var north))
        {
            return null;
        }

        var bounds = new PropertyMapBounds(west, south, east, north);
        return bounds.IsValid ? bounds : null;
    }
}
