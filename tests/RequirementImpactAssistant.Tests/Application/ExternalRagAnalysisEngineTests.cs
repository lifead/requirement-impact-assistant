using System.Text.Json;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class ExternalRagAnalysisEngineTests
{
    private const string FakeSecretLikeToken = "sk-test-external-rag-secret";
    private const string FakeAuthorizationHeader = "Authorization: Bearer";
    private const string FakeSecretEndpoint = "https://external-rag-secret.invalid";

    [Fact]
    public async Task AnalyzeAsync_WithoutAdapterReturnsControlledUnavailableFailure()
    {
        var engine = new ExternalRagAnalysisEngine();

        var response = await engine.AnalyzeAsync(CreateRequest());

        Assert.Equal(AiAnalysisResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Empty(response.RawResponse);
        Assert.Contains("External AI/RAG adapter is not configured", response.Errors.Single());
        Assert.True(response.BoundaryNotice.AiDoesNotMakeManagementDecision);

        Assert.NotNull(response.ResultMetadata);
        Assert.Equal(AnalysisMode.ExternalRag, response.ResultMetadata.AnalysisMode);
        Assert.Equal(nameof(ExternalRagAnalysisEngine), response.ResultMetadata.EngineName);
        Assert.Equal(RetrievedContextState.Unavailable, response.ResultMetadata.RetrievedContextState);
        Assert.False(response.ResultMetadata.ManualContextForwardedToExternalAiOrRag);
        Assert.Empty(response.ResultMetadata.RetrievedContextItems);
        Assert.Contains("External AI/RAG adapter is not configured", response.ResultMetadata.Warnings.Single());
    }

    [Fact]
    public async Task AnalyzeAsync_PassesNeutralRequestToAdapterAndMapsCompletedResponse()
    {
        var impactMap = CreateImpactMap();
        var retrievedContextItem = new RetrievedContextItem
        {
            SourceTitle = "Integration inventory",
            ExternalReference = "inventory-record-42",
            Completeness = RetrievedContextItemCompleteness.MetadataOnly,
            ProviderName = "neutral-provider",
            AdapterName = "neutral-adapter"
        };
        var adapter = new CapturingExternalRagAdapter(new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.Completed,
            ImpactMap: impactMap,
            Metadata: CreateResponseMetadata(),
            RetrievedContextState: RetrievedContextState.MetadataOnly,
            RetrievedContextItems: [retrievedContextItem],
            Warnings: [],
            Errors: [],
            SanitizedDiagnosticSnapshot: "{\"status\":\"completed\"}"));
        var engine = new ExternalRagAnalysisEngine(adapter);
        var request = CreateRequest();

        var response = await engine.AnalyzeAsync(request);

        Assert.Equal(1, adapter.CallCount);
        Assert.NotNull(adapter.LastRequest);
        Assert.Equal(request.InputSnapshot.AnalysisId, adapter.LastRequest.CorrelationId);
        Assert.Same(request.InputSnapshot, adapter.LastRequest.InputSnapshot);
        Assert.Same(request.ExpectedResult, adapter.LastRequest.ExpectedResult);
        Assert.Same(request.BoundaryNotice, adapter.LastRequest.BoundaryNotice);
        Assert.Equal(nameof(ExternalRagAnalysisEngine), adapter.LastRequest.ExecutionMetadata.EngineName);
        Assert.True(adapter.LastRequest.CanForwardManualContextToExternalAiOrRag);
        Assert.NotNull(adapter.LastRequest.ManualContext);
        Assert.Equal("Task context", adapter.LastRequest.ManualContext.CombinedText);
        Assert.Single(adapter.LastRequest.ManualContext.ContextFragments);

        Assert.Equal(AiAnalysisResponseStatus.Succeeded, response.Status);
        Assert.Same(impactMap, response.ImpactMap);
        Assert.Equal("{\"status\":\"completed\"}", response.RawResponse);
        Assert.Empty(response.Errors);

        Assert.NotNull(response.ResultMetadata);
        Assert.Equal(AnalysisMode.ExternalRag, response.ResultMetadata.AnalysisMode);
        Assert.Equal(nameof(ExternalRagAnalysisEngine), response.ResultMetadata.EngineName);
        Assert.Equal("neutral-provider", response.ResultMetadata.ProviderName);
        Assert.Equal("neutral-adapter", response.ResultMetadata.AdapterName);
        Assert.Equal("neutral-model / impact-workflow / research-profile", response.ResultMetadata.ModelWorkflowProfileName);
        Assert.Equal(RetrievedContextState.MetadataOnly, response.ResultMetadata.RetrievedContextState);
        Assert.True(response.ResultMetadata.ManualContextForwardedToExternalAiOrRag);
        Assert.Same(retrievedContextItem, response.ResultMetadata.RetrievedContextItems.Single());
    }

    [Fact]
    public async Task AnalyzeAsync_MapsPartialResponseWarningsToDiagnostics()
    {
        var adapter = new CapturingExternalRagAdapter(new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.Partial,
            ImpactMap: CreateImpactMap(),
            Metadata: CreateResponseMetadata(),
            RetrievedContextState: RetrievedContextState.Partial,
            RetrievedContextItems: [],
            Warnings: ["Only partial external context metadata was available."],
            Errors: [],
            SanitizedDiagnosticSnapshot: "{\"status\":\"partial\"}"));
        var engine = new ExternalRagAnalysisEngine(adapter);

        var response = await engine.AnalyzeAsync(CreateRequest());

        Assert.Equal(AiAnalysisResponseStatus.Partial, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("{\"status\":\"partial\"}", response.RawResponse);
        Assert.Equal(["Only partial external context metadata was available."], response.Errors);
        Assert.NotNull(response.ResultMetadata);
        Assert.Equal(RetrievedContextState.Partial, response.ResultMetadata.RetrievedContextState);
        Assert.Equal(["Only partial external context metadata was available."], response.ResultMetadata.Warnings);
        Assert.Empty(response.ResultMetadata.RetrievedContextItems);
    }

    [Fact]
    public async Task AnalyzeAsync_MapsFailedAdapterResponseWithoutRetrievedContextItems()
    {
        var adapter = new CapturingExternalRagAdapter(new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.Failed,
            ImpactMap: null,
            Metadata: CreateResponseMetadata(),
            RetrievedContextState: RetrievedContextState.Unavailable,
            RetrievedContextItems: [],
            Warnings: ["External analysis was unavailable."],
            Errors:
            [
                new ExternalRagAdapterError(
                    Code: "external_unavailable",
                    Message: "No analytical result was returned.",
                    DiagnosticDetails: "Retry can be considered after adapter configuration is reviewed.")
            ],
            SanitizedDiagnosticSnapshot: "{\"status\":\"failed\"}"));
        var engine = new ExternalRagAnalysisEngine(adapter);

        var response = await engine.AnalyzeAsync(CreateRequest());

        Assert.Equal(AiAnalysisResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Equal("{\"status\":\"failed\"}", response.RawResponse);
        Assert.Contains("External analysis was unavailable.", response.Errors);
        Assert.Contains(
            response.Errors,
            error => error.Contains("external_unavailable: No analytical result was returned.", StringComparison.Ordinal));

        Assert.NotNull(response.ResultMetadata);
        Assert.Equal(AnalysisMode.ExternalRag, response.ResultMetadata.AnalysisMode);
        Assert.Equal(RetrievedContextState.Unavailable, response.ResultMetadata.RetrievedContextState);
        Assert.Equal(["External analysis was unavailable."], response.ResultMetadata.Warnings);
        Assert.Empty(response.ResultMetadata.RetrievedContextItems);
    }

    [Fact]
    public async Task AnalyzeAsync_AdapterExceptionReturnsSanitizedFailure()
    {
        var engine = new ExternalRagAnalysisEngine(new ThrowingExternalRagAdapter());

        var response = await engine.AnalyzeAsync(CreateRequest());

        Assert.Equal(AiAnalysisResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Empty(response.RawResponse);
        var error = Assert.Single(response.Errors);
        Assert.Contains("External adapter failed before returning an analytical result.", error);
        Assert.Contains(nameof(InvalidOperationException), error);
        AssertSanitized(
            response,
            [FakeSecretLikeToken, FakeAuthorizationHeader, FakeSecretEndpoint, "sensitive diagnostic"]);

        Assert.NotNull(response.ResultMetadata);
        Assert.Equal(AnalysisMode.ExternalRag, response.ResultMetadata.AnalysisMode);
        Assert.Equal(RetrievedContextState.Unavailable, response.ResultMetadata.RetrievedContextState);
        Assert.Empty(response.ResultMetadata.RetrievedContextItems);
    }

    private static AiAnalysisRequest CreateRequest()
    {
        var analysisId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var fragmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: analysisId,
            Analysis: new AnalysisInputFields(
                Title: "Gateway migration",
                ProjectRequestType: "ApiOrIntegrationChange",
                OriginalDescription: "Original requirement",
                ProjectRequest: "Project request",
                SituationDescription: "Current situation",
                ChangeSource: "Change source"),
            ContextFragments:
            [
                new AnalysisContextFragmentSnapshot(
                    Id: fragmentId,
                    Type: "Task",
                    Source: "Task tracker",
                    Text: "Task context",
                    FileName: null)
            ]);

        return new AiAnalysisRequest(
            InputSnapshot: snapshot,
            InputSnapshotJson: "{\"analysisId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}",
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default);
    }

    private static ExternalRagAdapterResponseMetadata CreateResponseMetadata() =>
        new(
            ProviderName: "neutral-provider",
            AdapterName: "neutral-adapter",
            ModelName: "neutral-model",
            WorkflowName: "impact-workflow",
            ProfileName: "research-profile",
            SanitizedProperties: new Dictionary<string, string>());

    private static ImpactMap CreateImpactMap() =>
        new()
        {
            ChangeSummary =
            {
                Title = "Potential migration impact",
                Description = "Gateway requirements may change.",
                Severity = ImpactSeverity.Medium
            },
            PreliminaryAssessment =
            {
                Title = "Requires human expert review",
                Description = "Preliminary material only.",
                Severity = ImpactSeverity.Medium
            }
        };

    private static void AssertSanitized(
        AiAnalysisResponse response,
        IReadOnlyList<string> forbiddenTokens)
    {
        var serializedResponse = JsonSerializer.Serialize(response);
        var inspectedStrings = new List<string?>
        {
            serializedResponse,
            response.RawResponse,
            response.ResultMetadata?.ProviderName,
            response.ResultMetadata?.AdapterName,
            response.ResultMetadata?.ModelWorkflowProfileName
        };

        inspectedStrings.AddRange(response.Errors);
        if (response.ResultMetadata is not null)
        {
            inspectedStrings.AddRange(response.ResultMetadata.Warnings);
            inspectedStrings.AddRange(response.ResultMetadata.RetrievedContextItems.SelectMany(item => new[]
            {
                item.SourceTitle,
                item.SourceId,
                item.ExternalReference,
                item.FragmentId,
                item.Text,
                item.Excerpt,
                item.UrlOrReference,
                item.ProviderName,
                item.AdapterName,
                item.WarningOrLimitationNote
            }));
        }

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.All(inspectedStrings, value =>
                Assert.DoesNotContain(forbiddenToken, value ?? string.Empty, StringComparison.Ordinal));
        }
    }

    private sealed class CapturingExternalRagAdapter(ExternalRagAdapterResponse response) : IExternalRagAdapter
    {
        public int CallCount { get; private set; }

        public ExternalRagAdapterRequest? LastRequest { get; private set; }

        public Task<ExternalRagAdapterResponse> AnalyzeAsync(
            ExternalRagAdapterRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;

            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingExternalRagAdapter : IExternalRagAdapter
    {
        public Task<ExternalRagAdapterResponse> AnalyzeAsync(
            ExternalRagAdapterRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                $"sensitive diagnostic should not be surfaced: {FakeAuthorizationHeader} {FakeSecretLikeToken} {FakeSecretEndpoint}");
        }
    }
}
