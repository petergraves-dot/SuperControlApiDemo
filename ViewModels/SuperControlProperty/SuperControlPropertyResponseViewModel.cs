namespace petergraves.ViewModels.SuperControlProperty;

public sealed class SuperControlPropertyResponseViewModel
{
    public SuperControlPropertyRequestViewModel Request { get; init; } = new();

    public SuperControlPropertyDetailViewModel? Property { get; init; }

    public string CalendarKey { get; init; } = string.Empty;

    public bool HasCalendarKey { get; init; }

    public int CalendarPropertyId { get; init; }

    public string? Error { get; init; }
}
