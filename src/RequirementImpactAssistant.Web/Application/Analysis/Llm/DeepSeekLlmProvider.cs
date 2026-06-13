using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RequirementImpactAssistant.Web.Application.Analysis.Llm;

public sealed class DeepSeekLlmProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly DeepSeekLlmProviderOptions _options;

    public DeepSeekLlmProvider(
        HttpClient httpClient,
        IOptions<AiAnalysisOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value.DeepSeek;
    }

    public async Task<LlmProviderResponse> CompleteAsync(
        LlmProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Failed("DeepSeek API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            return Failed("DeepSeek model is not configured.");
        }

        var endpoint = CreateEndpoint();
        if (endpoint is null)
        {
            return Failed("DeepSeek base URL is not configured as a valid absolute URL.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(CreateRequestBody(request), JsonOptions),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);

            using var httpResponse = await _httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            var rawResponse = await httpResponse.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return new LlmProviderResponse(
                    LlmProviderResponseStatus.Failed,
                    ImpactMap: null,
                    RawResponse: rawResponse,
                    Errors:
                    [
                        $"DeepSeek provider returned HTTP {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase})."
                    ]);
            }

            return new LlmProviderResponse(
                LlmProviderResponseStatus.Succeeded,
                ImpactMap: null,
                RawResponse: rawResponse,
                Errors: []);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new LlmProviderResponse(
                LlmProviderResponseStatus.Failed,
                ImpactMap: null,
                RawResponse: string.Empty,
                Errors:
                [
                    "DeepSeek provider call failed before a response was returned.",
                    exception.Message
                ]);
        }
    }

    private Uri? CreateEndpoint()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.deepseek.com"
            : _options.BaseUrl.Trim();

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        return new Uri(baseUri, "/chat/completions");
    }

    private object CreateRequestBody(LlmProviderRequest request) =>
        new
        {
            model = _options.Model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "Prepare preliminary requirement impact analysis material only. Do not make management decisions."
                },
                new
                {
                    role = "user",
                    content = request.Prompt
                }
            },
            stream = false
        };

    private static LlmProviderResponse Failed(string error) =>
        new(
            LlmProviderResponseStatus.Failed,
            ImpactMap: null,
            RawResponse: string.Empty,
            Errors: [error]);
}
