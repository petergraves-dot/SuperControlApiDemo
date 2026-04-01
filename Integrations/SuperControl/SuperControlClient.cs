using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace petergraves.Integrations.SuperControl;

public sealed class SuperControlClient : ISuperControlClient
{
    private readonly HttpClient _httpClient;
    private readonly SuperControlOptions _options;

    public SuperControlClient(HttpClient httpClient, IOptions<SuperControlOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<SuperControlApiResponse> GetAccountsIndexAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync("properties/index", cancellationToken);
    }

    public Task<SuperControlApiResponse> GetContentIndexAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return GetAsync($"properties/contentindex/{accountId}", cancellationToken);
    }

    public Task<SuperControlApiResponse> GetByRelativeUrlAsync(string relativeUrl, CancellationToken cancellationToken = default)
    {
        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Expected a relative URL.", nameof(relativeUrl));
        }

        return GetAsync(relativeUrl, cancellationToken);
    }

    public Task<SuperControlApiResponse> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return GetAsync(url, cancellationToken);
    }

    public Task<SuperControlApiResponse> GetDataExportBookingsAsync(string queryString, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrEmpty(queryString)
            ? "DataExport/Bookings"
            : $"DataExport/Bookings?{queryString}";
        return GetXmlAsync(path, cancellationToken);
    }

    public Task<SuperControlApiResponse> GetDataExportPropertiesAsync(CancellationToken cancellationToken = default)
    {
        return GetXmlAsync("DataExport/Properties", cancellationToken);
    }

    private async Task<SuperControlApiResponse> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("SC-TOKEN", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new SuperControlApiResponse(response.IsSuccessStatusCode, (int)response.StatusCode, body);
    }

    private async Task<SuperControlApiResponse> GetXmlAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.TryAddWithoutValidation("SC-TOKEN", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new SuperControlApiResponse(response.IsSuccessStatusCode, (int)response.StatusCode, body);
    }
}
