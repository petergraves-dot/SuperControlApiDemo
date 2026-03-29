namespace petergraves.ViewModels.SuperControlListingSiteDemo;

public sealed class SuperControlListingSiteDemoStatsViewModel
{
    public int TotalActiveProperties { get; init; }

    public int ReturnedProperties { get; init; }

    public int CacheHits { get; init; }

    public int CacheMisses { get; init; }

    public int StaleFallbackHits { get; init; }
}
