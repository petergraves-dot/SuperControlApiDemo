namespace petergraves.Integrations.SuperControl;

public interface ISuperControlClient
{
    Task<SuperControlApiResponse> GetAccountsIndexAsync(CancellationToken cancellationToken = default);

    Task<SuperControlApiResponse> GetContentIndexAsync(int accountId, CancellationToken cancellationToken = default);

    Task<SuperControlApiResponse> GetByRelativeUrlAsync(string relativeUrl, CancellationToken cancellationToken = default);

    Task<SuperControlApiResponse> GetByUrlAsync(string url, CancellationToken cancellationToken = default);

    Task<SuperControlApiResponse> GetDataExportBookingsAsync(string queryString, CancellationToken cancellationToken = default);

    Task<SuperControlApiResponse> GetDataExportPropertiesAsync(CancellationToken cancellationToken = default);
}
