using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Options;

namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

public sealed class DifyExternalRagAdapter : IExternalRagAdapter
{
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

        var endpoint = DifyAgentRequestContract.NormalizeChatMessagesEndpoint(_options.Endpoint);
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
            DifyAgentRequestContract.SerializeRequest(request),
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
                .SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    effectiveCancellationToken)
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

            await using var responseStream = await httpResponse.Content
                .ReadAsStreamAsync(effectiveCancellationToken)
                .ConfigureAwait(false);
            using var streamReader = new StreamReader(
                responseStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: false);
            var ssePayload = await streamReader
                .ReadToEndAsync(effectiveCancellationToken)
                .ConfigureAwait(false);
            var streamResult = DifyAgentSseStreamParser.Parse(ssePayload);

            return DifyExternalRagMapper.CreateStreamingResponse(
                request,
                streamResult,
                _options);
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
        catch (IOException)
        {
            return DifyExternalRagMapper.CreateFailureResponse(
                request,
                _options,
                providerStatus: "stream-read-error",
                errorCode: "dify_stream_read_error",
                errorMessage: "Dify external RAG provider stream could not be read.",
                diagnosticDetails: "The provider response failed while reading the SSE stream.",
                warnings: ["Dify external RAG provider stream could not be read."]);
        }
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
