using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

public sealed class DifyExternalRagAdapter : IExternalRagAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly DifyExternalRagOptions _options;

    public DifyExternalRagAdapter(
        HttpClient httpClient,
        IOptions<DifyExternalRagOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ExternalRagAdapterResponse> AnalyzeAsync(
        ExternalRagAdapterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var endpoint = CreateEndpoint();
        if (endpoint is null)
        {
            throw new InvalidOperationException("Dify endpoint is not configured as a valid absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Dify API key is not configured.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(DifyExternalRagMapper.CreateRequest(request), JsonOptions),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        using var httpResponse = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();

        var responseBody = await httpResponse.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        var difyResponse = JsonSerializer.Deserialize<DifyWorkflowResponseDto>(responseBody, JsonOptions)
            ?? new DifyWorkflowResponseDto();

        return DifyExternalRagMapper.CreateResponse(request, difyResponse, _options);
    }

    private Uri? CreateEndpoint()
    {
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return null;
        }

        return endpoint;
    }
}
