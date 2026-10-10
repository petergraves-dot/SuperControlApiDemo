using petergraves.ViewModels.PropertyExplorer;

namespace petergraves.Features.PropertyExplorer;

public interface IPropertyExplorerViewModelFactory
{
    Task<PropertyExplorerResponseViewModel> BuildAsync(
        PropertyExplorerRequestViewModel request,
        CancellationToken cancellationToken = default);
}
