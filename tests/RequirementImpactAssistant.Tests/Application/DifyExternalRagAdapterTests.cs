using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class DifyExternalRagAdapterTests
{
    private const string TestApiKey = "test-dify-api-key";

    [Fact]
    public async Task AnalyzeAsync_SendsAgentStreamingRequestThroughFakeHttpAndParsesAgentAnswerJson()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(HttpStatusCode.OK, """
            data: {"event":"agent_message","answer":"{\"changeSummary\":\""}
            data: {"event":"agent_message","answer":"Gateway migration\"}"}
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456","metadata":{"usage":{"prompt_tokens":12,"completion_tokens":5,"total_tokens":17}}}

            """));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(1, handler.CallCount);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://dify.invalid/v1/chat-messages", handler.LastRequest.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.False(string.IsNullOrWhiteSpace(handler.LastRequest.Headers.Authorization?.Parameter));
        Assert.NotNull(handler.LastRequestBody);

        using var requestDocument = JsonDocument.Parse(handler.LastRequestBody);
        var requestRoot = requestDocument.RootElement;
        var inputs = requestRoot.GetProperty("inputs");
        Assert.Equal("Original requirement", inputs.GetProperty("originalRequirement").GetString());
        Assert.Equal("Current situation", inputs.GetProperty("situation").GetString());
        Assert.Equal("Change source", inputs.GetProperty("source").GetString());
        Assert.Equal("Project request", requestRoot.GetProperty("query").GetString());
        Assert.Equal("streaming", requestRoot.GetProperty("response_mode").GetString());
        Assert.Equal(string.Empty, requestRoot.GetProperty("conversation_id").GetString());
        Assert.Equal("analysis-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", requestRoot.GetProperty("user").GetString());
        Assert.DoesNotContain("blocking", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manual_context", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain(TestApiKey, handler.LastRequestBody, StringComparison.Ordinal);

        Assert.Equal(ExternalRagAdapterResponseStatus.Partial, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("Dify Agent structured answer parsed", response.ImpactMap.ChangeSummary.Title);
        Assert.Empty(response.Errors);
        Assert.Equal(RetrievedContextState.Unavailable, response.RetrievedContextState);
        Assert.Empty(response.RetrievedContextItems);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("structured response mapping", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Dify", response.Metadata.ProviderName);
        Assert.Equal(nameof(DifyExternalRagAdapter), response.Metadata.AdapterName);
        Assert.Null(response.Metadata.ModelName);
        Assert.Equal("workflow-from-options", response.Metadata.WorkflowName);
        Assert.Equal("research-profile", response.Metadata.ProfileName);
        Assert.Equal("stream-complete", response.Metadata.SanitizedProperties["providerStatus"]);
        Assert.Equal("dify-agent-sse-intermediate", response.Metadata.SanitizedProperties["responseShape"]);
        Assert.Equal("true", response.Metadata.SanitizedProperties["streamComplete"]);
        Assert.Equal("2", response.Metadata.SanitizedProperties["answerFragmentCount"]);
        Assert.Equal("parsed-json", response.Metadata.SanitizedProperties["answerParseStatus"]);
        Assert.Equal("full-answer-json", response.Metadata.SanitizedProperties["answerParseMode"]);
        Assert.Equal("false", response.Metadata.SanitizedProperties["rawAnswerFallbackRetained"]);
        Assert.Equal("msg-123", response.Metadata.SanitizedProperties["messageId"]);
        Assert.Equal("conv-456", response.Metadata.SanitizedProperties["conversationId"]);
        Assert.Equal("17", response.Metadata.SanitizedProperties["usage.total_tokens"]);

        Assert.NotNull(response.SanitizedDiagnosticSnapshot);
        using var diagnosticDocument = JsonDocument.Parse(response.SanitizedDiagnosticSnapshot);
        var diagnosticRoot = diagnosticDocument.RootElement;
        Assert.Equal("partial", diagnosticRoot.GetProperty("status").GetString());
        Assert.Equal("stream-complete", diagnosticRoot.GetProperty("providerStatus").GetString());
        Assert.Equal("Unavailable", diagnosticRoot.GetProperty("retrievedContextState").GetString());
        Assert.Equal(0, diagnosticRoot.GetProperty("retrievedContextItemCount").GetInt32());
        AssertSanitized(
            response,
            [TestApiKey, "https://dify.invalid", "Authorization", "Bearer", "Gateway migration"]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenDifyOptionsAreDisabled_ReturnsSanitizedUnavailableResponseWithoutHttpCall()
    {
        var handler = new CapturingHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        var adapter = CreateAdapter(handler, options => options.Enabled = false);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(0, handler.CallCount);
        AssertFailure(
            response,
            expectedCode: "dify_disabled",
            forbiddenTokens: [TestApiKey, "https://dify.invalid", "Authorization", "Bearer"]);
        Assert.Contains("disabled", response.Warnings.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenDifyOptionsAreIncomplete_ReturnsSanitizedUnavailableResponseWithoutHttpCall()
    {
        var handler = new CapturingHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        var adapter = CreateAdapter(handler, options => options.ApiKey = null);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(0, handler.CallCount);
        AssertFailure(
            response,
            expectedCode: "dify_configuration_unavailable",
            forbiddenTokens: [TestApiKey, "https://dify.invalid", "Authorization", "Bearer"]);
        Assert.Contains("configuration is incomplete", response.Warnings.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenProviderReturnsHttpError_ReturnsSanitizedProviderError()
    {
        const string rawProviderBody = "raw provider body with https://dify.invalid/v1/chat-messages and test-dify-api-key";
        var handler = new CapturingHandler(_ => CreateSseResponse(HttpStatusCode.ServiceUnavailable, rawProviderBody));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(1, handler.CallCount);
        AssertFailure(
            response,
            expectedCode: "dify_provider_error",
            forbiddenTokens: [TestApiKey, "https://dify.invalid", rawProviderBody, "Authorization", "Bearer"]);
        Assert.Equal("http-503", response.Metadata.SanitizedProperties["providerStatus"]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenProviderTimesOut_ReturnsSanitizedTimeoutError()
    {
        var handler = new DelayingHandler();
        var adapter = CreateAdapter(handler, options => options.TimeoutSeconds = 1);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(1, handler.CallCount);
        AssertFailure(
            response,
            expectedCode: "dify_timeout",
            forbiddenTokens: [TestApiKey, "https://dify.invalid", "Authorization", "Bearer"]);
        Assert.Equal("timeout", response.Metadata.SanitizedProperties["providerStatus"]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenCallerCancels_PropagatesCancellationWithoutReturningProviderDiagnostics()
    {
        var handler = new DelayingHandler();
        var adapter = CreateAdapter(handler, options => options.TimeoutSeconds = 30);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.AnalyzeAsync(CreateRequest(), cancellationTokenSource.Token));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenStreamIsIncompleteWithAnswer_ReturnsPartialIntermediateResponse()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(HttpStatusCode.OK, """
            data: {"event":"agent_message","answer":"Partial answer"}

            """));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.Partial, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("Dify Agent raw answer fallback retained", response.ImpactMap.ChangeSummary.Title);
        Assert.Equal("Partial answer", response.ImpactMap.ChangeSummary.Description);
        Assert.Empty(response.Errors);
        Assert.Contains("message_end", string.Join(" ", response.Warnings), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("could not be parsed as JSON", string.Join(" ", response.Warnings), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("stream-incomplete", response.Metadata.SanitizedProperties["providerStatus"]);
        Assert.Equal("false", response.Metadata.SanitizedProperties["streamComplete"]);
        Assert.Equal("raw-text-fallback", response.Metadata.SanitizedProperties["answerParseStatus"]);
        Assert.Equal("raw-text-fallback", response.Metadata.SanitizedProperties["answerParseMode"]);
        Assert.Equal("true", response.Metadata.SanitizedProperties["rawAnswerFallbackRetained"]);
        AssertSanitized(response, [TestApiKey, "https://dify.invalid", "Authorization", "Bearer"]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenInvalidAgentAnswerContainsSensitiveLookingFragments_RetainsOnlySanitizedRawFallback()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(HttpStatusCode.OK, """
            data: {"event":"agent_message","answer":"Unable to produce JSON. apiKey=synthetic-key Authorization: Bearer synthetic-token cookie=session-value auth=auth-assignment auth: auth-colon session=session-assignment password=password-assignment password: password-colon dify.invalid/private"}
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456"}

            """));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.Partial, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("Dify Agent raw answer fallback retained", response.ImpactMap.ChangeSummary.Title);
        Assert.Contains("[REDACTED]", response.ImpactMap.ChangeSummary.Description, StringComparison.Ordinal);
        Assert.Contains("[REDACTED_URL]", response.ImpactMap.ChangeSummary.Description, StringComparison.Ordinal);
        Assert.Equal("raw-text-fallback", response.Metadata.SanitizedProperties["answerParseStatus"]);
        Assert.Equal("true", response.Metadata.SanitizedProperties["rawAnswerFallbackRetained"]);
        AssertSanitized(
            response,
            [
                TestApiKey,
                "https://dify.invalid",
                "Authorization",
                "Bearer",
                "synthetic-key",
                "synthetic-token",
                "session-value",
                "auth-assignment",
                "auth-colon",
                "session-assignment",
                "password-assignment",
                "password-colon",
                "dify.invalid/private"
            ]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenStreamIsIncompleteWithoutAnswer_ReturnsSanitizedFailure()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(HttpStatusCode.OK, """
            data: {"event":"agent_thought","thought":"Provider-only thought stays private."}

            """));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        AssertFailure(
            response,
            expectedCode: "dify_empty_stream_response",
            forbiddenTokens:
            [
                TestApiKey,
                "https://dify.invalid",
                "Authorization",
                "Bearer",
                "Provider-only thought stays private."
            ]);
        Assert.Equal("stream-incomplete", response.Metadata.SanitizedProperties["providerStatus"]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenTransportFails_ReturnsSanitizedTransportError()
    {
        var handler = new ThrowingHandler();
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        AssertFailure(
            response,
            expectedCode: "dify_transport_error",
            forbiddenTokens: [TestApiKey, "https://dify.invalid", "Authorization", "Bearer"]);
        Assert.Equal("transport-error", response.Metadata.SanitizedProperties["providerStatus"]);
    }

    private static DifyExternalRagAdapter CreateAdapter(
        HttpMessageHandler handler,
        Action<DifyExternalRagOptions>? configureOptions = null)
    {
        var httpClient = new HttpClient(handler);
        var optionsValue = new DifyExternalRagOptions
        {
            Enabled = true,
            Endpoint = "https://dify.invalid/workflows/run",
            WorkflowOrAppId = "workflow-from-options",
            ApiKey = TestApiKey,
            TimeoutSeconds = 30,
            ProfileName = "options-profile"
        };
        configureOptions?.Invoke(optionsValue);
        var options = Options.Create(optionsValue);

        return new DifyExternalRagAdapter(httpClient, options);
    }

    private static ExternalRagAdapterRequest CreateRequest()
    {
        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Analysis: new AnalysisInputFields(
                Title: "Gateway migration",
                OriginalDescription: "Original requirement",
                ProjectRequest: "Project request",
                SituationDescription: "Current situation",
                ChangeSource: "Change source"),
            ContextFragments:
            [
                new AnalysisContextFragmentSnapshot(
                    Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Type: "Task",
                    Source: "Task tracker",
                    Text: "Task context",
                    FileName: null)
            ]);

        return new ExternalRagAdapterRequest(
            CorrelationId: snapshot.AnalysisId,
            InputSnapshot: snapshot,
            ManualContext: new ExternalRagManualContextBlock(
                ContextFragments: snapshot.ContextFragments,
                CombinedText: "Task context"),
            CanForwardManualContextToExternalAiOrRag: true,
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default,
            ExecutionMetadata: new ExternalRagRequestMetadata(
                EngineName: nameof(ExternalRagAnalysisEngine),
                RequestedProfileName: "research-profile",
                SanitizedProperties: new Dictionary<string, string>()));
    }

    private static HttpResponseMessage CreateSseResponse(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/event-stream")
        };

    private static void AssertFailure(
        ExternalRagAdapterResponse response,
        string expectedCode,
        IReadOnlyList<string> forbiddenTokens)
    {
        Assert.Equal(ExternalRagAdapterResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Equal(RetrievedContextState.Unavailable, response.RetrievedContextState);
        Assert.Empty(response.RetrievedContextItems);
        Assert.NotEmpty(response.Warnings);

        var error = Assert.Single(response.Errors);
        Assert.Equal(expectedCode, error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));

        AssertSanitized(response, forbiddenTokens);
    }

    private static void AssertSanitized(
        ExternalRagAdapterResponse response,
        IReadOnlyList<string> forbiddenTokens)
    {
        var serializedResponse = JsonSerializer.Serialize(response);
        var inspectedStrings = new List<string?>
        {
            serializedResponse,
            response.SanitizedDiagnosticSnapshot,
            response.Metadata.ProviderName,
            response.Metadata.AdapterName,
            response.Metadata.ModelName,
            response.Metadata.WorkflowName,
            response.Metadata.ProfileName
        };

        inspectedStrings.AddRange(response.Warnings);
        inspectedStrings.AddRange(response.Errors.SelectMany(error => new[]
        {
            error.Code,
            error.Message,
            error.DiagnosticDetails
        }));
        inspectedStrings.AddRange(response.Metadata.SanitizedProperties.SelectMany(property => new[]
        {
            property.Key,
            property.Value
        }));

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.All(inspectedStrings, value =>
                Assert.DoesNotContain(forbiddenToken, value ?? string.Empty, StringComparison.Ordinal));
        }
    }

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

    private sealed class DelayingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return CreateSseResponse(HttpStatusCode.OK, string.Empty);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Synthetic transport failure.");
    }
}
