namespace petergraves.ViewModels;

public sealed class ErrorPageViewModel
{
    public string? RequestId { get; init; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
