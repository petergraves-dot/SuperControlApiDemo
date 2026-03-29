namespace petergraves.ViewModels.SuperControlListingSiteDemo;

public sealed record SuperControlListingSiteDemoRequestViewModel
{
    public string? Where { get; init; }

    public DateOnly? CheckIn { get; init; }

    public DateOnly? CheckOut { get; init; }

    public int Guests { get; init; } = 2;
}
