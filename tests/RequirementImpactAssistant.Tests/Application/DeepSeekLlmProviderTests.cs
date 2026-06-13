using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Extensions;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class DeepSeekLlmProviderTests
{
    [Fact]
    public void ApplicationAnalysisRegistration_SelectsDeepSeekProviderFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiAnalysis:Provider"] = LlmProviderNames.DeepSeek,
                ["AiAnalysis:DeepSeek:Model"] = "deepseek-chat",
                ["AiAnalysis:DeepSeek:BaseUrl"] = "https://api.deepseek.com",
                ["AiAnalysis:DeepSeek:ApiKey"] = "test-api-key"
            })
            .Build();

        services.AddApplicationAnalysis(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var provider = scope.ServiceProvider.GetRequiredService<ILlmProvider>();
        var engine = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngine>();

        Assert.IsType<DeepSeekLlmProvider>(provider);
        Assert.IsType<DirectLlmAnalysisEngine>(engine);
    }

    [Fact]
    public async Task CompleteAsync_MapsRequestToDeepSeekChatCompletionsWithoutNetwork()
    {
        var handler = new CapturingHandler(_ => CreateJsonResponse(HttpStatusCode.OK, """
            {
              "id": "test-response",
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": "{\"changeSummary\":{\"title\":\"Gateway migration\"}}"
                  }
                }
              ]
            }
            """));
        var provider = CreateProvider(handler);

        var response = await provider.CompleteAsync(CreateProviderRequest());

        Assert.Equal(LlmProviderResponseStatus.Succeeded, response.Status);
        Assert.Equal(1, handler.CallCount);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://api.deepseek.com/chat/completions", handler.LastRequest.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("test-api-key", handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.NotNull(handler.LastRequestBody);

        using var document = JsonDocument.Parse(handler.LastRequestBody);
        var root = document.RootElement;

        Assert.Equal("deepseek-chat", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());

        var messages = root.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("Do not make management decisions.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("Analyze the anonymized gateway migration request.", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task CompleteAsync_MapsSuccessfulDeepSeekResponseToRawProviderResponse()
    {
        const string rawDeepSeekResponse = """
            {
              "id": "deepseek-test-id",
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": "preliminary analytical material"
                  }
                }
              ]
            }
            """;
        var provider = CreateProvider(new CapturingHandler(_ => CreateJsonResponse(HttpStatusCode.OK, rawDeepSeekResponse)));

        var response = await provider.CompleteAsync(CreateProviderRequest());

        Assert.Equal(LlmProviderResponseStatus.Succeeded, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Empty(response.Errors);
        Assert.Equal(rawDeepSeekResponse, response.RawResponse);
    }

    [Fact]
    public async Task CompleteAsync_MapsHttpFailureToFailedProviderResponse()
    {
        var provider = CreateProvider(new CapturingHandler(_ => CreateJsonResponse(
            HttpStatusCode.Unauthorized,
            """{"error":{"message":"invalid api key"}}""",
            "Unauthorized")));

        var response = await provider.CompleteAsync(CreateProviderRequest());

        Assert.Equal(LlmProviderResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Contains("invalid api key", response.RawResponse);
        Assert.Contains(response.Errors, error => error.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CompleteAsync_MapsTransportFailureToFailedProviderResponse()
    {
        var provider = CreateProvider(new CapturingHandler(_ => throw new HttpRequestException("Synthetic transport failure.")));

        var response = await provider.CompleteAsync(CreateProviderRequest());

        Assert.Equal(LlmProviderResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Empty(response.RawResponse);
        Assert.Contains("DeepSeek provider call failed", response.Errors[0]);
        Assert.Contains("Synthetic transport failure.", response.Errors[1]);
    }

    [Fact]
    public async Task CompleteAsync_WithoutApiKeyFailsBeforeHttpCall()
    {
        var handler = new CapturingHandler(_ => CreateJsonResponse(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider(handler, apiKey: null);

        var response = await provider.CompleteAsync(CreateProviderRequest());

        Assert.Equal(LlmProviderResponseStatus.Failed, response.Status);
        Assert.Equal(0, handler.CallCount);
        Assert.Contains("API key is not configured", response.Errors[0]);
    }

    private static DeepSeekLlmProvider CreateProvider(
        HttpMessageHandler handler,
        string? apiKey = "test-api-key",
        string? baseUrl = "https://api.deepseek.com",
        string model = "deepseek-chat")
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AiAnalysisOptions
        {
            Provider = LlmProviderNames.DeepSeek,
            DeepSeek = new DeepSeekLlmProviderOptions
            {
                ApiKey = apiKey,
                BaseUrl = baseUrl,
                Model = model
            }
        });

        return new DeepSeekLlmProvider(httpClient, options);
    }

    private static LlmProviderRequest CreateProviderRequest() =>
        new(
            LlmProviderNames.DeepSeek,
            "Analyze the anonymized gateway migration request.",
            CreateAnalysisRequest());

    private static AiAnalysisRequest CreateAnalysisRequest()
    {
        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Analysis: new AnalysisInputFields(
                Title: "Gateway migration",
                OriginalDescription: "Original requirement for authentication gateway.",
                ProjectRequest: "Move the gateway authentication flow to the new service.",
                SituationDescription: "Current gateway is shared by several integrations.",
                ChangeSource: "Architecture review"),
            ContextFragments: []);

        return new AiAnalysisRequest(
            InputSnapshot: snapshot,
            InputSnapshotJson: "{}",
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default);
    }

    private static HttpResponseMessage CreateJsonResponse(
        HttpStatusCode statusCode,
        string content,
        string? reasonPhrase = null) =>
        new(statusCode)
        {
            ReasonPhrase = reasonPhrase,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request);
        }
    }
}
