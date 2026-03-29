using System.ComponentModel.DataAnnotations;

namespace petergraves.Integrations.SuperControl;

public sealed class SuperControlOptions
{
    public const string SectionName = "SuperControl";

    [Required]
    public string BaseUrl { get; init; } = "https://api.supercontrol.co.uk/v3/";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? AccountId { get; init; }

    public string CalendarKey { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int DefaultPropertyId { get; init; } = 671777;
}
