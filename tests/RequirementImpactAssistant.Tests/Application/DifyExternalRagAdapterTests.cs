using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class DifyExternalRagAdapterTests
{
    private const string TestApiKey = "test-dify-api-key";

    [Fact]
    public async Task AnalyzeAsync_SendsAgentStreamingRequestThroughFakeHttpAndParsesAgentAnswerJson()
    {
        var answer = """
            {
              "changeSummary": "Gateway migration impact",
              "affectedRequirements": [
                {
                  "title": "Storage code validation",
                  "description": "Requirement text must mention non-empty storage code validation.",
                  "severity": "High",
                  "notes": "Check requirement wording.",
                  "relatedContextFragmentIds": ["11111111-1111-1111-1111-111111111111"]
                }
              ],
              "affectedTasks": ["Update API validation task"],
              "affectedProjectDecisions": [],
              "affectedApiInterfacesDocumentsTests": [
                {
                  "title": "POST /bins contract",
                  "description": "API documents and tests should cover blank code rejection.",
                  "severity": "Medium"
                }
              ],
              "affectedArchitecturalConstraints": [],
              "affectedOrganizationalContextItems": [],
              "contradictions": [],
              "missingInformation": ["Confirm accepted error message."],
              "clarificationQuestions": ["Should whitespace-only values be rejected?"],
              "risks": [
                {
                  "title": "Validation regression",
                  "description": "Existing clients may rely on permissive input.",
                  "severity": "Medium"
                }
              ],
              "optionsForExpertReview": ["Approve validation scope"],
              "preliminaryAssessment": "Requires expert review",
              "usedSources": ["requirements-demo"],
              "warnings": []
            }
            """;
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            CreateCompleteSsePayload(answer, "msg-123", "conv-456", totalTokens: 17)));
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

        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("Gateway migration impact", response.ImpactMap.ChangeSummary.Title);
        Assert.Contains("preliminary analytical material", response.ImpactMap.ChangeSummary.Notes, StringComparison.Ordinal);
        var affectedRequirement = Assert.Single(response.ImpactMap.AffectedRequirements);
        Assert.Equal("Storage code validation", affectedRequirement.Title);
        Assert.Equal("Requirement text must mention non-empty storage code validation.", affectedRequirement.Description);
        Assert.Equal(ImpactSeverity.High, affectedRequirement.Severity);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), Assert.Single(affectedRequirement.RelatedContextFragmentIds));
        Assert.Equal("Update API validation task", Assert.Single(response.ImpactMap.AffectedTasks).Title);
        Assert.Equal("POST /bins contract", Assert.Single(response.ImpactMap.AffectedApiInterfacesDocumentsTests).Title);
        Assert.Equal("Confirm accepted error message.", Assert.Single(response.ImpactMap.MissingInformation).Title);
        Assert.Equal("Should whitespace-only values be rejected?", Assert.Single(response.ImpactMap.ClarificationQuestions).Title);
        Assert.Equal("Validation regression", Assert.Single(response.ImpactMap.Risks).Title);
        Assert.Equal("Approve validation scope", Assert.Single(response.ImpactMap.OptionsForExpertReview).Title);
        Assert.Equal("Requires expert review", response.ImpactMap.PreliminaryAssessment.Title);
        Assert.Contains("preliminary analytical material", response.ImpactMap.PreliminaryAssessment.Notes, StringComparison.Ordinal);
        Assert.Empty(response.Errors);
        Assert.Equal(RetrievedContextState.Unavailable, response.RetrievedContextState);
        Assert.Empty(response.RetrievedContextItems);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("did not include retriever_resources", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Dify", response.Metadata.ProviderName);
        Assert.Equal(nameof(DifyExternalRagAdapter), response.Metadata.AdapterName);
        Assert.Null(response.Metadata.ModelName);
        Assert.Equal("workflow-from-options", response.Metadata.WorkflowName);
        Assert.Equal("research-profile", response.Metadata.ProfileName);
        Assert.Equal("stream-complete", response.Metadata.SanitizedProperties["providerStatus"]);
        Assert.Equal("dify-agent-answer-json", response.Metadata.SanitizedProperties["responseShape"]);
        Assert.Equal("true", response.Metadata.SanitizedProperties["streamComplete"]);
        Assert.Equal("1", response.Metadata.SanitizedProperties["answerFragmentCount"]);
        Assert.Equal("parsed-json", response.Metadata.SanitizedProperties["answerParseStatus"]);
        Assert.Equal("full-answer-json", response.Metadata.SanitizedProperties["answerParseMode"]);
        Assert.Equal("completedWithWarnings", response.Metadata.SanitizedProperties["adapterResponseStatus"]);
        Assert.Equal("false", response.Metadata.SanitizedProperties["rawAnswerFallbackRetained"]);
        Assert.Equal("1", response.Metadata.SanitizedProperties["usedSourceCount"]);
        Assert.Equal("msg-123", response.Metadata.SanitizedProperties["messageId"]);
        Assert.Equal("conv-456", response.Metadata.SanitizedProperties["conversationId"]);
        Assert.Equal("17", response.Metadata.SanitizedProperties["usage.total_tokens"]);

        Assert.NotNull(response.SanitizedDiagnosticSnapshot);
        using var diagnosticDocument = JsonDocument.Parse(response.SanitizedDiagnosticSnapshot);
        var diagnosticRoot = diagnosticDocument.RootElement;
        Assert.Equal("completedWithWarnings", diagnosticRoot.GetProperty("status").GetString());
        Assert.Equal("stream-complete", diagnosticRoot.GetProperty("providerStatus").GetString());
        Assert.Equal("dify-agent-answer-json", diagnosticRoot.GetProperty("responseShape").GetString());
        Assert.Equal("msg-123", diagnosticRoot.GetProperty("messageId").GetString());
        Assert.Equal("conv-456", diagnosticRoot.GetProperty("conversationId").GetString());
        Assert.Equal("https", diagnosticRoot.GetProperty("endpoint").GetProperty("scheme").GetString());
        Assert.Equal("dify.invalid", diagnosticRoot.GetProperty("endpoint").GetProperty("host").GetString());
        Assert.Equal("/v1/chat-messages", diagnosticRoot.GetProperty("endpoint").GetProperty("path").GetString());
        Assert.Equal("17", diagnosticRoot.GetProperty("usage").GetProperty("total_tokens").GetString());
        Assert.Equal("Unavailable", diagnosticRoot.GetProperty("retrievedContextState").GetString());
        Assert.Equal(0, diagnosticRoot.GetProperty("retrievedContextItemCount").GetInt32());
        Assert.Contains(
            diagnosticRoot.GetProperty("warnings").EnumerateArray(),
            warning => warning.GetString()?.Contains("did not include retriever_resources", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Empty(diagnosticRoot.GetProperty("errors").EnumerateArray());
        AssertSanitized(
            response,
            [TestApiKey, "https://dify.invalid", "Authorization", "Bearer"]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenMessageEndContainsRetrieverResources_MapsRetrievedContextItems()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            CreateCompleteSsePayload(
                CreateStructuredAnswer("Resources mapped impact"),
                "msg-resources",
                "conv-resources",
                totalTokens: 31,
                retrieverResourcesJson: """
                    [
                      {
                        "position": 1,
                        "dataset_id": "dataset-1",
                        "dataset_name": "Requirements knowledge base",
                        "document_id": "document-1",
                        "document_name": "Storage cell requirement",
                        "segment_id": "segment-1",
                        "score": 0.875,
                        "content": "Storage cell codes must reject blank values."
                      }
                    ]
                    """)));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.Equal(RetrievedContextState.Partial, response.RetrievedContextState);
        var item = Assert.Single(response.RetrievedContextItems);
        Assert.Equal("Storage cell requirement", item.SourceTitle);
        Assert.Equal("document-1", item.SourceId);
        Assert.Equal("dataset-1", item.ExternalReference);
        Assert.Equal("segment-1", item.FragmentId);
        Assert.Null(item.Text);
        Assert.Equal("Storage cell codes must reject blank values.", item.Excerpt);
        Assert.Equal(1, item.Rank);
        Assert.Equal(0.875, item.Score);
        Assert.Equal("Dify", item.ProviderName);
        Assert.Equal(nameof(DifyExternalRagAdapter), item.AdapterName);
        Assert.Equal(RetrievedContextItemCompleteness.ExcerptOnly, item.Completeness);
        Assert.Contains("without full source text", item.WarningOrLimitationNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("partial retrieved context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            response.Warnings,
            warning => warning.Contains("did not include retriever_resources", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(response.SanitizedDiagnosticSnapshot);
        using var diagnosticDocument = JsonDocument.Parse(response.SanitizedDiagnosticSnapshot);
        var diagnosticRoot = diagnosticDocument.RootElement;
        Assert.Equal("Partial", diagnosticRoot.GetProperty("retrievedContextState").GetString());
        Assert.Equal(1, diagnosticRoot.GetProperty("retrievedContextItemCount").GetInt32());
    }

    [Fact]
    public async Task AnalyzeAsync_WhenMessageEndContainsEmptyRetrieverResources_ReturnsUnavailableWithWarning()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            CreateCompleteSsePayload(
                CreateStructuredAnswer("Empty resources impact"),
                "msg-empty",
                "conv-empty",
                totalTokens: 19,
                retrieverResourcesJson: "[]")));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.Equal(RetrievedContextState.Unavailable, response.RetrievedContextState);
        Assert.Empty(response.RetrievedContextItems);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("empty retriever_resources", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenRetrieverResourcesContainEmptyObject_IgnoresResourceAndReturnsUnavailable()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            CreateCompleteSsePayload(
                CreateStructuredAnswer("Empty object resources impact"),
                "msg-empty-object",
                "conv-empty-object",
                totalTokens: 20,
                retrieverResourcesJson: """
                    [
                      {}
                    ]
                    """)));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.Equal(RetrievedContextState.Unavailable, response.RetrievedContextState);
        Assert.Empty(response.RetrievedContextItems);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("empty or unrecognized item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenRetrieverResourcesContainOnlyUnknownFields_IgnoresResourceAndReturnsUnavailable()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            CreateCompleteSsePayload(
                CreateStructuredAnswer("Unknown resources impact"),
                "msg-unknown",
                "conv-unknown",
                totalTokens: 22,
                retrieverResourcesJson: """
                    [
                      {
                        "provider_private_field": "ignored",
                        "another_unknown_field": "ignored"
                      }
                    ]
                    """)));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.Equal(RetrievedContextState.Unavailable, response.RetrievedContextState);
        Assert.Empty(response.RetrievedContextItems);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("empty or unrecognized item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenRetrieverResourcesContainSourceMetadataOnly_ReturnsMetadataOnlyState()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            CreateCompleteSsePayload(
                CreateStructuredAnswer("Metadata-only resources impact"),
                "msg-metadata-only",
                "conv-metadata-only",
                totalTokens: 24,
                retrieverResourcesJson: """
                    [
                      {
                        "dataset_id": "dataset-2",
                        "document_id": "document-2",
                        "document_name": "Architecture note",
                        "segment_id": "segment-2",
                        "score": 0.73
                      }
                    ]
                    """)));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.Equal(RetrievedContextState.MetadataOnly, response.RetrievedContextState);
        var item = Assert.Single(response.RetrievedContextItems);
        Assert.Equal("Architecture note", item.SourceTitle);
        Assert.Equal("document-2", item.SourceId);
        Assert.Equal("dataset-2", item.ExternalReference);
        Assert.Equal("segment-2", item.FragmentId);
        Assert.Null(item.Text);
        Assert.Null(item.Excerpt);
        Assert.Equal(0.73, item.Score);
        Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, item.Completeness);
        Assert.Contains("without excerpt or full text", item.WarningOrLimitationNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("metadata without source text or excerpts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenRetrieverResourcesAreMalformed_ReturnsUnavailableWithWarnings()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            CreateCompleteSsePayload(
                CreateStructuredAnswer("Malformed resources impact"),
                "msg-malformed",
                "conv-malformed",
                totalTokens: 23,
                retrieverResourcesJson: """
                    [
                      "not-an-object"
                    ]
                    """)));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.Equal(RetrievedContextState.Unavailable, response.RetrievedContextState);
        Assert.Empty(response.RetrievedContextItems);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("malformed item", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenStructuredAnswerArrivesOnIncompleteStream_ReturnsPartialMappedResponse()
    {
        var answer = """
            {
              "changeSummary": "Partial structured impact",
              "affectedRequirements": ["Storage code requirement"],
              "affectedTasks": [],
              "affectedProjectDecisions": [],
              "affectedApiInterfacesDocumentsTests": [],
              "affectedArchitecturalConstraints": [],
              "affectedOrganizationalContextItems": [],
              "contradictions": [],
              "missingInformation": [],
              "clarificationQuestions": [],
              "risks": [],
              "optionsForExpertReview": [],
              "preliminaryAssessment": "Needs expert review",
              "usedSources": [],
              "warnings": []
            }
            """;
        var agentMessage = JsonSerializer.Serialize(new
        {
            @event = "agent_message",
            answer
        });
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            $"data: {agentMessage}{Environment.NewLine}"));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.Partial, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("Partial structured impact", response.ImpactMap.ChangeSummary.Title);
        Assert.Equal("Storage code requirement", Assert.Single(response.ImpactMap.AffectedRequirements).Title);
        Assert.Equal("Needs expert review", response.ImpactMap.PreliminaryAssessment.Title);
        Assert.Empty(response.Errors);
        Assert.Contains("message_end", string.Join(" ", response.Warnings), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("dify-agent-answer-json", response.Metadata.SanitizedProperties["responseShape"]);
        Assert.Equal("partial", response.Metadata.SanitizedProperties["adapterResponseStatus"]);
        Assert.Equal("parsed-json", response.Metadata.SanitizedProperties["answerParseStatus"]);
        Assert.Equal("false", response.Metadata.SanitizedProperties["streamComplete"]);
        Assert.Equal("false", response.Metadata.SanitizedProperties["rawAnswerFallbackRetained"]);

        Assert.NotNull(response.SanitizedDiagnosticSnapshot);
        using var diagnosticDocument = JsonDocument.Parse(response.SanitizedDiagnosticSnapshot);
        var diagnosticRoot = diagnosticDocument.RootElement;
        Assert.Equal("partial", diagnosticRoot.GetProperty("status").GetString());
        Assert.Equal("stream-incomplete", diagnosticRoot.GetProperty("providerStatus").GetString());
        Assert.Contains(
            diagnosticRoot.GetProperty("warnings").EnumerateArray(),
            warning => warning.GetString()?.Contains("message_end", StringComparison.OrdinalIgnoreCase) == true);
        AssertSanitized(response, [TestApiKey, "https://dify.invalid", "Authorization", "Bearer"]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenStructuredAnswerContainsProviderWarnings_ReturnsCompletedWithSanitizedWarnings()
    {
        var answer = """
            {
              "changeSummary": "Warning structured impact apiKey=synthetic-key",
              "affectedRequirements": [],
              "affectedTasks": [],
              "affectedProjectDecisions": [],
              "affectedApiInterfacesDocumentsTests": [],
              "affectedArchitecturalConstraints": [],
              "affectedOrganizationalContextItems": [],
              "contradictions": [],
              "missingInformation": [],
              "clarificationQuestions": [],
              "risks": [],
              "optionsForExpertReview": [],
              "preliminaryAssessment": "Needs expert review",
              "usedSources": [],
              "warnings": ["Provider warning with cookie=session-value csrf=csrf-value password=password-value session=session-value auth=auth-assignment auth: auth-colon key=synthetic-key key: synthetic-key-colon Authorization: Bearer synthetic-token dify.invalid/private"]
            }
            """;
        var handler = new CapturingHandler(_ => CreateSseResponse(
            HttpStatusCode.OK,
            CreateCompleteSsePayload(answer, "msg-warning", "conv-warning", totalTokens: 21)));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("Warning structured impact [REDACTED]", response.ImpactMap.ChangeSummary.Title);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("Dify provider warning was redacted", StringComparison.Ordinal));
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("did not include retriever_resources", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("completedWithWarnings", response.Metadata.SanitizedProperties["adapterResponseStatus"]);

        Assert.NotNull(response.SanitizedDiagnosticSnapshot);
        using var diagnosticDocument = JsonDocument.Parse(response.SanitizedDiagnosticSnapshot);
        var diagnosticRoot = diagnosticDocument.RootElement;
        Assert.Equal("completedWithWarnings", diagnosticRoot.GetProperty("status").GetString());
        Assert.Equal("msg-warning", diagnosticRoot.GetProperty("messageId").GetString());
        Assert.Equal("conv-warning", diagnosticRoot.GetProperty("conversationId").GetString());
        Assert.Contains(
            "Dify provider warning was redacted",
            string.Join(" ", diagnosticRoot.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString())),
            StringComparison.Ordinal);
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
                "csrf-value",
                "password-value",
                "auth-assignment",
                "auth-colon",
                "synthetic-key-colon",
                "dify.invalid/private"
            ]);
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
        Assert.Equal("External AI/RAG raw answer fallback retained", response.ImpactMap.ChangeSummary.Title);
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
            data: {"event":"agent_message","answer":"Unable to produce JSON. apiKey=synthetic-key Authorization: Bearer synthetic-token cookie=session-value auth=auth-assignment auth: auth-colon key=generic-key-assignment key: generic-key-colon session=session-assignment password=password-assignment password: password-colon dify.invalid/private"}
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456"}

            """));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.Partial, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("External AI/RAG raw answer fallback retained", response.ImpactMap.ChangeSummary.Title);
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
                "generic-key-assignment",
                "generic-key-colon",
                "session-assignment",
                "password-assignment",
                "password-colon",
                "dify.invalid/private"
            ]);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenAgentAnswerJsonHasUnexpectedShape_ReturnsPartialRawFallback()
    {
        var handler = new CapturingHandler(_ => CreateSseResponse(HttpStatusCode.OK, """
            data: {"event":"agent_message","answer":"{\"answer\":\"not expected shape\"}"}
            data: {"event":"message_end","message_id":"msg-unexpected","conversation_id":"conv-unexpected","metadata":{"usage":{"total_tokens":7}}}

            """));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.Partial, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("External AI/RAG raw answer fallback retained", response.ImpactMap.ChangeSummary.Title);
        Assert.Equal("{\"answer\":\"not expected shape\"}", response.ImpactMap.ChangeSummary.Description);
        Assert.Contains(
            response.Warnings,
            warning => warning.Contains("expected structured answer shape", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("dify-agent-raw-answer-fallback", response.Metadata.SanitizedProperties["responseShape"]);
        Assert.Equal("raw-text-fallback", response.Metadata.SanitizedProperties["answerParseStatus"]);
        Assert.Equal("true", response.Metadata.SanitizedProperties["rawAnswerFallbackRetained"]);
        AssertSanitized(response, [TestApiKey, "https://dify.invalid", "Authorization", "Bearer"]);
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

    private static string CreateCompleteSsePayload(
        string answer,
        string messageId,
        string conversationId,
        int totalTokens,
        string? retrieverResourcesJson = null)
    {
        var agentMessage = JsonSerializer.Serialize(new
        {
            @event = "agent_message",
            answer
        });
        var metadata = new Dictionary<string, object?>
        {
            ["usage"] = new Dictionary<string, object?>
            {
                ["prompt_tokens"] = 12,
                ["completion_tokens"] = 5,
                ["total_tokens"] = totalTokens
            }
        };
        if (retrieverResourcesJson is not null)
        {
            using var retrieverResourcesDocument = JsonDocument.Parse(retrieverResourcesJson);
            metadata["retriever_resources"] = retrieverResourcesDocument.RootElement.Clone();
        }

        var messageEnd = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["event"] = "message_end",
            ["message_id"] = messageId,
            ["conversation_id"] = conversationId,
            ["metadata"] = metadata
        });

        return string.Join(
            Environment.NewLine,
            $"data: {agentMessage}",
            $"data: {messageEnd}",
            string.Empty);
    }

    private static string CreateStructuredAnswer(string changeSummary) =>
        JsonSerializer.Serialize(new
        {
            changeSummary,
            affectedRequirements = Array.Empty<object>(),
            affectedTasks = Array.Empty<object>(),
            affectedProjectDecisions = Array.Empty<object>(),
            affectedApiInterfacesDocumentsTests = Array.Empty<object>(),
            affectedArchitecturalConstraints = Array.Empty<object>(),
            affectedOrganizationalContextItems = Array.Empty<object>(),
            contradictions = Array.Empty<object>(),
            missingInformation = Array.Empty<object>(),
            clarificationQuestions = Array.Empty<object>(),
            risks = Array.Empty<object>(),
            optionsForExpertReview = Array.Empty<object>(),
            preliminaryAssessment = "Needs expert review",
            usedSources = Array.Empty<object>(),
            warnings = Array.Empty<string>()
        });

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
