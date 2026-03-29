namespace petergraves.ViewModels.SuperControlListingSiteDemo;

public sealed class SuperControlListingSiteDemoResponseViewModel
{
    public SuperControlListingSiteDemoRequestViewModel Request { get; init; } = new();

    public bool Loaded { get; init; }

    public int? AccountId { get; init; }

    public string? Error { get; init; }

    public SuperControlListingSiteDemoStatsViewModel Stats { get; init; } = new();

    public IReadOnlyList<SuperControlListingSiteDemoPropertyViewModel> Properties { get; init; } = [];
}
