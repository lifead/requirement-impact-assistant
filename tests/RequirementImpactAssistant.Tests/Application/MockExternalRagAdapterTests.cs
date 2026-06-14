using System.Text.Json;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class MockExternalRagAdapterTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsCompletedHappyPathResponseWithFullRetrievedContext()
    {
        var adapter = new MockExternalRagAdapter();

        var response = await adapter.AnalyzeAsync(CreateRequest());

        Assert.Equal(ExternalRagAdapterResponseStatus.Completed, response.Status);
        Assert.Empty(response.Warnings);
        Assert.Empty(response.Errors);

        Assert.NotNull(response.ImpactMap);
        Assert.Equal("Gateway migration", response.ImpactMap.ChangeSummary.Title);
        Assert.Equal(ImpactSeverity.Medium, response.ImpactMap.ChangeSummary.Severity);
        Assert.Equal("Requires human expert review", response.ImpactMap.PreliminaryAssessment.Title);
        Assert.Single(response.ImpactMap.AffectedRequirements);
        Assert.Single(response.ImpactMap.AffectedProjectDecisions);
        Assert.Single(response.ImpactMap.Risks);

        Assert.Equal("LocalMockKnowledgeSource", response.Metadata.ProviderName);
        Assert.Equal(nameof(MockExternalRagAdapter), response.Metadata.AdapterName);
        Assert.Equal("local-demo-model", response.Metadata.ModelName);
        Assert.Equal("mock-impact-analysis", response.Metadata.WorkflowName);
        Assert.Equal("happy-path", response.Metadata.ProfileName);
        Assert.Equal("local-demo", response.Metadata.SanitizedProperties["execution"]);

        Assert.Equal(RetrievedContextState.Available, response.RetrievedContextState);
        Assert.Collection(
            response.RetrievedContextItems,
            item =>
            {
                Assert.Equal("Local demo requirement catalogue", item.SourceTitle);
                Assert.Equal("local-demo-REQ-001", item.ExternalReference);
                Assert.Equal("Controlled change to an integration boundary requires expert review.", item.Excerpt);
                Assert.Contains("controlled change to an integration boundary", item.Text);
                Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                Assert.Equal(1, item.Rank);
                Assert.Equal(0.94, item.Score);
            },
            item =>
            {
                Assert.Equal("Local demo decision log", item.SourceTitle);
                Assert.Equal("local-demo-ADR-002", item.ExternalReference);
                Assert.Equal("External analytical material is preliminary and separate from the expert conclusion.", item.Excerpt);
                Assert.Contains("preliminary", item.Text);
                Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                Assert.Equal(2, item.Rank);
                Assert.Equal(0.89, item.Score);
            });

        Assert.NotNull(response.SanitizedDiagnosticSnapshot);
        using var document = JsonDocument.Parse(response.SanitizedDiagnosticSnapshot);
        var root = document.RootElement;
        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal("LocalMockKnowledgeSource", root.GetProperty("provider").GetString());
        Assert.Equal(nameof(MockExternalRagAdapter), root.GetProperty("adapter").GetString());
        Assert.Equal("Available", root.GetProperty("retrievedContextState").GetString());
        Assert.Equal(2, root.GetProperty("retrievedContextItemCount").GetInt32());
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", root.GetProperty("correlationId").GetGuid().ToString());
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsDeterministicHappyPathResponse()
    {
        var adapter = new MockExternalRagAdapter();
        var request = CreateRequest();

        var first = await adapter.AnalyzeAsync(request);
        var second = await adapter.AnalyzeAsync(request);

        Assert.Equal(first.SanitizedDiagnosticSnapshot, second.SanitizedDiagnosticSnapshot);
        Assert.Equal(first.Metadata.ProviderName, second.Metadata.ProviderName);
        Assert.Equal(first.Metadata.AdapterName, second.Metadata.AdapterName);
        Assert.Equal(first.Metadata.ModelName, second.Metadata.ModelName);
        Assert.Equal(first.Metadata.WorkflowName, second.Metadata.WorkflowName);
        Assert.Equal(first.Metadata.ProfileName, second.Metadata.ProfileName);
        Assert.Equal(
            first.Metadata.SanitizedProperties.OrderBy(item => item.Key),
            second.Metadata.SanitizedProperties.OrderBy(item => item.Key));
        Assert.Equal(first.RetrievedContextState, second.RetrievedContextState);
        Assert.Equal(first.RetrievedContextItems.Count, second.RetrievedContextItems.Count);
        Assert.Equal(first.RetrievedContextItems[0].Text, second.RetrievedContextItems[0].Text);
        Assert.Equal(first.ImpactMap!.ChangeSummary.Title, second.ImpactMap!.ChangeSummary.Title);
        Assert.Equal(first.ImpactMap.Risks.Single().Description, second.ImpactMap.Risks.Single().Description);
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
                RequestedProfileName: null,
                SanitizedProperties: new Dictionary<string, string>()));
    }
}
