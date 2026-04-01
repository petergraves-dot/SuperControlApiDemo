using petergraves.ViewModels.SuperControlDataExportDemo;

namespace petergraves.Features.SuperControlDataExportDemo;

public interface IDataExportDemoViewModelFactory
{
    Task<DataExportBookingsResponseViewModel> BuildBookingsAsync(
        DataExportBookingsRequestViewModel request,
        CancellationToken cancellationToken = default);

    Task<DataExportPropertiesResponseViewModel> BuildPropertiesAsync(
        CancellationToken cancellationToken = default);
}
