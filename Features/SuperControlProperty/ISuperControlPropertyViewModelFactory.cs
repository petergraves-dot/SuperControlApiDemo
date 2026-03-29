using petergraves.ViewModels.SuperControlProperty;

namespace petergraves.Features.SuperControlProperty;

public interface ISuperControlPropertyViewModelFactory
{
    Task<SuperControlPropertyResponseViewModel> BuildAsync(
        SuperControlPropertyRequestViewModel request,
        CancellationToken cancellationToken = default);
}
