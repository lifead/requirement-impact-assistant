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

        var configurationStatus = _options.GetConfigurationStatus();
        if (!configurationStatus.IsConfigured)
        {
            return DifyExternalRagMapper.CreateUnavailableConfigurationResponse(
                request,
                _options,
                configurationStatus);
        }

        var endpoint = CreateEndpoint();
        if (endpoint is null)
        {
            return DifyExternalRagMapper.CreateFailureResponse(
                request,
                _options,
                providerStatus: null,
                errorCode: "dify_configuration_unavailable",
                errorMessage: "Dify external RAG adapter is unavailable because its configuration is incomplete.",
                diagnosticDetails: "Configured endpoint is not a valid absolute HTTP or HTTPS URI.",
                warnings: ["Dify external RAG adapter is unavailable; configuration is incomplete."]);
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return DifyExternalRagMapper.CreateFailureResponse(
                request,
                _options,
                providerStatus: null,
                errorCode: "dify_configuration_unavailable",
                errorMessage: "Dify external RAG adapter is unavailable because its configuration is incomplete.",
                diagnosticDetails: "Configured API key is missing.",
                warnings: ["Dify external RAG adapter is unavailable; configuration is incomplete."]);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(DifyExternalRagMapper.CreateRequest(request), JsonOptions),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        using var timeoutCts = CreateTimeoutTokenSource();
        using var linkedCts = timeoutCts is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var effectiveCancellationToken = linkedCts?.Token ?? cancellationToken;

        try
        {
            using var httpResponse = await _httpClient
                .SendAsync(httpRequest, effectiveCancellationToken)
                .ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return DifyExternalRagMapper.CreateFailureResponse(
                    request,
                    _options,
                    providerStatus: $"http-{(int)httpResponse.StatusCode}",
                    errorCode: "dify_provider_error",
                    errorMessage: "Dify external RAG provider returned an unsuccessful response.",
                    diagnosticDetails: $"HTTP status {(int)httpResponse.StatusCode}.",
                    warnings: ["Dify external RAG provider did not complete the analysis."]);
            }

            var responseBody = await httpResponse.Content
                .ReadAsStringAsync(effectiveCancellationToken)
                .ConfigureAwait(false);
            var difyResponse = JsonSerializer.Deserialize<DifyWorkflowResponseDto>(responseBody, JsonOptions)
                ?? new DifyWorkflowResponseDto();

            return DifyExternalRagMapper.CreateResponse(request, difyResponse, _options);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true &&
            !cancellationToken.IsCancellationRequested)
        {
            return DifyExternalRagMapper.CreateFailureResponse(
                request,
                _options,
                providerStatus: "timeout",
                errorCode: "dify_timeout",
                errorMessage: "Dify external RAG provider did not respond before the configured timeout.",
                diagnosticDetails: "The provider request timed out.",
                warnings: ["Dify external RAG provider timed out."]);
        }
        catch (HttpRequestException)
        {
            return DifyExternalRagMapper.CreateFailureResponse(
                request,
                _options,
                providerStatus: "transport-error",
                errorCode: "dify_transport_error",
                errorMessage: "Dify external RAG provider could not be reached.",
                diagnosticDetails: "The provider request failed at the HTTP transport layer.",
                warnings: ["Dify external RAG provider is unavailable."]);
        }
        catch (JsonException)
        {
            return DifyExternalRagMapper.CreateFailureResponse(
                request,
                _options,
                providerStatus: "malformed-response",
                errorCode: "dify_malformed_response",
                errorMessage: "Dify external RAG provider returned a malformed response.",
                diagnosticDetails: "The provider response could not be parsed as the expected JSON shape.",
                warnings: ["Dify external RAG provider returned a malformed response."]);
        }
    }

    private Uri? CreateEndpoint()
    {
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return null;
        }

        return endpoint;
    }

    private CancellationTokenSource? CreateTimeoutTokenSource()
    {
        if (_options.TimeoutSeconds is null)
        {
            return null;
        }

        return new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds.Value));
    }
}
