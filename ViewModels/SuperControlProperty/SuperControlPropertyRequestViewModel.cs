namespace petergraves.ViewModels.SuperControlProperty;

public sealed record SuperControlPropertyRequestViewModel
{
    public int PropertyId { get; init; }

    public DateOnly? CheckIn { get; init; }

    public DateOnly? CheckOut { get; init; }

    public int Guests { get; init; } = 2;

    public string? Where { get; init; }
}
