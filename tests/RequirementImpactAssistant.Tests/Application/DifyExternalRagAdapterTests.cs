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
    public async Task AnalyzeAsync_SendsDifyRequestThroughFakeHttpAndMapsHappyPathResponse()
    {
        var handler = new CapturingHandler(_ => CreateJsonResponse(HttpStatusCode.OK, """
            {
              "workflow_run_id": "run-1",
              "task_id": "task-1",
              "data": {
                "workflow_id": "workflow-from-response",
                "status": "succeeded",
                "outputs": {
                  "metadata": {
                    "model": "dify-workflow-model",
                    "response_shape": "structured-impact-map"
                  },
                  "impact_map": {
                    "change_summary": {
                      "title": "Gateway migration",
                      "description": "Authentication gateway change affects integration boundaries.",
                      "severity": "High",
                      "notes": "Preliminary analytical material."
                    },
                    "affected_requirements": [
                      {
                        "title": "Review authentication requirement",
                        "description": "Check whether the gateway migration changes the requirement boundary.",
                        "severity": "Medium",
                        "related_context_fragment_ids": ["11111111-1111-1111-1111-111111111111"]
                      }
                    ],
                    "affected_project_decisions": [
                      {
                        "title": "Confirm gateway architecture decision",
                        "description": "Validate the accepted integration approach.",
                        "severity": "Low"
                      }
                    ],
                    "risks": [
                      {
                        "title": "Downstream integration regression",
                        "description": "Dependent clients may require additional regression checks.",
                        "severity": "High"
                      }
                    ],
                    "preliminary_assessment": {
                      "title": "Requires expert review",
                      "description": "The response is not a management decision.",
                      "severity": "Medium"
                    }
                  },
                  "retrieved_context": [
                    {
                      "source_title": "Integration requirements catalogue",
                      "source_id": "requirements",
                      "external_reference": "REQ-42",
                      "fragment_id": "fragment-42",
                      "text": "Gateway changes that affect integration boundaries require expert review.",
                      "excerpt": "Gateway changes require expert review.",
                      "url_or_reference": "kb://requirements/REQ-42",
                      "rank": 1,
                      "score": 0.91
                    },
                    {
                      "source_title": "Architecture decision log",
                      "source_id": "decisions",
                      "external_reference": "ADR-7",
                      "fragment_id": "fragment-7",
                      "excerpt": "External analytical output remains preliminary.",
                      "url_or_reference": "kb://decisions/ADR-7",
                      "rank": 2,
                      "score": 0.84
                    }
                  ],
                  "warnings": []
                }
              }
            }
            """));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(1, handler.CallCount);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://dify.invalid/workflows/run", handler.LastRequest.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal(TestApiKey, handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.NotNull(handler.LastRequestBody);

        using var requestDocument = JsonDocument.Parse(handler.LastRequestBody);
        var requestRoot = requestDocument.RootElement;
        var inputs = requestRoot.GetProperty("inputs");
        Assert.Equal("blocking", requestRoot.GetProperty("response_mode").GetString());
        Assert.Equal("analysis-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", requestRoot.GetProperty("user").GetString());
        Assert.Equal("Gateway migration", inputs.GetProperty("analysis").GetProperty("title").GetString());
        Assert.Equal("Task context", inputs.GetProperty("manual_context").GetProperty("combined_text").GetString());
        Assert.Equal("forwarded_when_available", inputs.GetProperty("manual_context_policy").GetString());
        Assert.True(inputs.GetProperty("boundary_notice").GetProperty("ai_does_not_make_management_decision").GetBoolean());
        Assert.DoesNotContain(TestApiKey, handler.LastRequestBody, StringComparison.Ordinal);

        Assert.Equal(ExternalRagAdapterResponseStatus.Completed, response.Status);
        Assert.Empty(response.Warnings);
        Assert.Empty(response.Errors);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("Gateway migration", response.ImpactMap.ChangeSummary.Title);
        Assert.Equal(ImpactSeverity.High, response.ImpactMap.ChangeSummary.Severity);
        Assert.Equal("Requires expert review", response.ImpactMap.PreliminaryAssessment.Title);
        Assert.Single(response.ImpactMap.AffectedRequirements);
        Assert.Single(response.ImpactMap.AffectedProjectDecisions);
        Assert.Single(response.ImpactMap.Risks);
        Assert.Equal(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            response.ImpactMap.AffectedRequirements.Single().RelatedContextFragmentIds.Single());

        Assert.Equal("Dify", response.Metadata.ProviderName);
        Assert.Equal(nameof(DifyExternalRagAdapter), response.Metadata.AdapterName);
        Assert.Equal("dify-workflow-model", response.Metadata.ModelName);
        Assert.Equal("workflow-from-response", response.Metadata.WorkflowName);
        Assert.Equal("research-profile", response.Metadata.ProfileName);
        Assert.Equal("succeeded", response.Metadata.SanitizedProperties["providerStatus"]);
        Assert.Equal("structured-impact-map", response.Metadata.SanitizedProperties["responseShape"]);

        Assert.Equal(RetrievedContextState.Available, response.RetrievedContextState);
        Assert.Collection(
            response.RetrievedContextItems,
            item =>
            {
                Assert.Equal("Integration requirements catalogue", item.SourceTitle);
                Assert.Equal("REQ-42", item.ExternalReference);
                Assert.Equal("Gateway changes require expert review.", item.Excerpt);
                Assert.Contains("integration boundaries", item.Text);
                Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                Assert.Equal("Dify", item.ProviderName);
                Assert.Equal(nameof(DifyExternalRagAdapter), item.AdapterName);
                Assert.Equal(1, item.Rank);
                Assert.Equal(0.91, item.Score);
            },
            item =>
            {
                Assert.Equal("Architecture decision log", item.SourceTitle);
                Assert.Equal("ADR-7", item.ExternalReference);
                Assert.Equal("External analytical output remains preliminary.", item.Excerpt);
                Assert.Null(item.Text);
                Assert.Equal(RetrievedContextItemCompleteness.ExcerptOnly, item.Completeness);
                Assert.Equal(2, item.Rank);
                Assert.Equal(0.84, item.Score);
            });

        Assert.NotNull(response.SanitizedDiagnosticSnapshot);
        Assert.DoesNotContain(TestApiKey, response.SanitizedDiagnosticSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("https://dify.invalid", response.SanitizedDiagnosticSnapshot, StringComparison.Ordinal);
        using var diagnosticDocument = JsonDocument.Parse(response.SanitizedDiagnosticSnapshot);
        var diagnosticRoot = diagnosticDocument.RootElement;
        Assert.Equal("completed", diagnosticRoot.GetProperty("status").GetString());
        Assert.Equal("Dify", diagnosticRoot.GetProperty("provider").GetString());
        Assert.Equal(nameof(DifyExternalRagAdapter), diagnosticRoot.GetProperty("adapter").GetString());
        Assert.Equal("workflow-from-response", diagnosticRoot.GetProperty("workflow").GetString());
        Assert.Equal("research-profile", diagnosticRoot.GetProperty("profile").GetString());
        Assert.Equal("Available", diagnosticRoot.GetProperty("retrievedContextState").GetString());
        Assert.Equal(2, diagnosticRoot.GetProperty("retrievedContextItemCount").GetInt32());
    }

    [Fact]
    public async Task AnalyzeAsync_WhenManualContextCannotBeForwarded_SendsPolicyWithoutManualText()
    {
        var handler = new CapturingHandler(_ => CreateJsonResponse(HttpStatusCode.OK, """
            {
              "data": {
                "workflow_id": "workflow-from-response",
                "status": "succeeded",
                "outputs": {
                  "impact_map": {
                    "change_summary": {
                      "title": "Gateway migration"
                    },
                    "preliminary_assessment": {
                      "title": "Requires expert review"
                    }
                  },
                  "retrieved_context": [
                    {
                      "source_title": "Requirement catalogue",
                      "excerpt": "Gateway change context.",
                      "rank": 1
                    }
                  ],
                  "warnings": []
                }
              }
            }
            """));
        var adapter = CreateAdapter(handler);

        var response = await adapter.AnalyzeAsync(CreateRequest(canForwardManualContext: false));

        Assert.Equal(ExternalRagAdapterResponseStatus.Completed, response.Status);
        Assert.NotNull(handler.LastRequestBody);
        using var requestDocument = JsonDocument.Parse(handler.LastRequestBody);
        var inputs = requestDocument.RootElement.GetProperty("inputs");
        Assert.Equal("not_forwarded", inputs.GetProperty("manual_context_policy").GetString());
        Assert.Equal(JsonValueKind.Null, inputs.GetProperty("manual_context").ValueKind);
        Assert.DoesNotContain("Task context", handler.LastRequestBody, StringComparison.Ordinal);
    }

    private static DifyExternalRagAdapter CreateAdapter(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new DifyExternalRagOptions
        {
            Enabled = true,
            Endpoint = "https://dify.invalid/workflows/run",
            WorkflowOrAppId = "workflow-from-options",
            ApiKey = TestApiKey,
            TimeoutSeconds = 30,
            ProfileName = "options-profile"
        });

        return new DifyExternalRagAdapter(httpClient, options);
    }

    private static ExternalRagAdapterRequest CreateRequest(bool canForwardManualContext = true)
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
            CanForwardManualContextToExternalAiOrRag: canForwardManualContext,
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default,
            ExecutionMetadata: new ExternalRagRequestMetadata(
                EngineName: nameof(ExternalRagAnalysisEngine),
                RequestedProfileName: "research-profile",
                SanitizedProperties: new Dictionary<string, string>()));
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
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
