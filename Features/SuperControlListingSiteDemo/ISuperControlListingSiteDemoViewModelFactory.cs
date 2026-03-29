using petergraves.ViewModels.SuperControlListingSiteDemo;

namespace petergraves.Features.SuperControlListingSiteDemo;

public interface ISuperControlListingSiteDemoViewModelFactory
{
    Task<SuperControlListingSiteDemoResponseViewModel> BuildAsync(
        SuperControlListingSiteDemoRequestViewModel request,
        CancellationToken cancellationToken = default);
}
