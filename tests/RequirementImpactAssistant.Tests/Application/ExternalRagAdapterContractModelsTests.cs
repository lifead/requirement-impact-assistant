using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class ExternalRagAdapterContractModelsTests
{
    [Fact]
    public void AdapterContract_UsesOnlyNeutralRequestResponseModels()
    {
        var method = Assert.Single(typeof(IExternalRagAdapter).GetMethods());

        Assert.Equal(nameof(IExternalRagAdapter.AnalyzeAsync), method.Name);
        Assert.Equal(typeof(Task<ExternalRagAdapterResponse>), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Collection(
            parameters,
            parameter => Assert.Equal(typeof(ExternalRagAdapterRequest), parameter.ParameterType),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
    }

    [Fact]
    public void AdapterContract_DoesNotDependOnProviderUiOrExportTypes()
    {
        var forbiddenNamespaces = new[]
        {
            "RequirementImpactAssistant.Web.Application.Analysis.Llm",
            "RequirementImpactAssistant.Web.Application.Export",
            "RequirementImpactAssistant.Web.Pages",
            "Microsoft.AspNetCore.Mvc.RazorPages",
            "System.Net.Http"
        };

        var violations = GetReferencedTypes(typeof(IExternalRagAdapter))
            .Where(type => forbiddenNamespaces.Any(
                forbiddenNamespace => type.Namespace == forbiddenNamespace
                    || type.Namespace?.StartsWith(forbiddenNamespace + ".", StringComparison.Ordinal) == true))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(ExternalRagAdapterResponseStatus.Completed, "Completed")]
    [InlineData(ExternalRagAdapterResponseStatus.CompletedWithWarnings, "CompletedWithWarnings")]
    [InlineData(ExternalRagAdapterResponseStatus.Partial, "Partial")]
    [InlineData(ExternalRagAdapterResponseStatus.Failed, "Failed")]
    public void ResponseStatus_HasStableStringRoundTrip(
        ExternalRagAdapterResponseStatus status,
        string expectedName)
    {
        Assert.Equal(expectedName, status.ToString());
        Assert.True(Enum.TryParse<ExternalRagAdapterResponseStatus>(expectedName, ignoreCase: false, out var parsedStatus));
        Assert.Equal(status, parsedStatus);
    }

    [Fact]
    public void Request_CanRepresentInputExpectedResultBoundaryNoticeAndManualContextPolicy()
    {
        var request = new ExternalRagAdapterRequest(
            CorrelationId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            InputSnapshot: CreateInputSnapshot(),
            ManualContext: new ExternalRagManualContextBlock(
                ContextFragments:
                [
                    new AnalysisContextFragmentSnapshot(
                        Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        Type: "Task",
                        Source: "Task tracker",
                        Text: "Manual task context",
                        FileName: null)
                ],
                CombinedText: "Manual task context"),
            CanForwardManualContextToExternalAiOrRag: false,
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default,
            ExecutionMetadata: new ExternalRagRequestMetadata(
                EngineName: "neutral-external-analysis-boundary",
                RequestedProfileName: "research-profile",
                SanitizedProperties: new Dictionary<string, string>
                {
                    ["tenant"] = "local-demo"
                }));

        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), request.CorrelationId);
        Assert.Equal("Gateway migration", request.InputSnapshot.Analysis.Title);
        Assert.NotNull(request.ManualContext);
        Assert.Single(request.ManualContext.ContextFragments);
        Assert.False(request.CanForwardManualContextToExternalAiOrRag);
        Assert.Contains(request.ExpectedResult.Sections, section => section.Key == "changeSummary");
        Assert.True(request.BoundaryNotice.AiDoesNotMakeManagementDecision);
        Assert.Equal("neutral-external-analysis-boundary", request.ExecutionMetadata.EngineName);
        Assert.Equal("research-profile", request.ExecutionMetadata.RequestedProfileName);
    }

    [Fact]
    public void Response_CanRepresentMetadataOnlyRetrievedContextWithoutText()
    {
        var response = new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.CompletedWithWarnings,
            ImpactMap: new ImpactMap(),
            Metadata: CreateResponseMetadata(),
            RetrievedContextState: RetrievedContextState.MetadataOnly,
            RetrievedContextItems:
            [
                new RetrievedContextItem
                {
                    SourceTitle = "Integration inventory",
                    ExternalReference = "inventory-record-42",
                    UrlOrReference = "kb://inventory/42",
                    Rank = 1,
                    Score = 0.86,
                    ProviderName = "ExternalKnowledgeProvider",
                    AdapterName = "NeutralExternalAdapter",
                    Completeness = RetrievedContextItemCompleteness.MetadataOnly
                }
            ],
            Warnings: ["Only source metadata was returned."],
            Errors: [],
            SanitizedDiagnosticSnapshot: "{\"state\":\"metadataOnly\"}");

        var item = Assert.Single(response.RetrievedContextItems);
        Assert.Equal(ExternalRagAdapterResponseStatus.CompletedWithWarnings, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal("ExternalKnowledgeProvider", response.Metadata.ProviderName);
        Assert.Equal(RetrievedContextState.MetadataOnly, response.RetrievedContextState);
        Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, item.Completeness);
        Assert.Null(item.Text);
        Assert.Null(item.Excerpt);
        Assert.Equal(["Only source metadata was returned."], response.Warnings);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void Response_CanRepresentUnavailableOutcomeWithoutRetrievedContextText()
    {
        var response = new ExternalRagAdapterResponse(
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
                    Message: "External analysis was unavailable.",
                    DiagnosticDetails: "No analytical result was returned.")
            ],
            SanitizedDiagnosticSnapshot: null);

        Assert.Equal(ExternalRagAdapterResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Equal(RetrievedContextState.Unavailable, response.RetrievedContextState);
        Assert.Empty(response.RetrievedContextItems);
        Assert.Equal(["External analysis was unavailable."], response.Warnings);
        var error = Assert.Single(response.Errors);
        Assert.Equal("external_unavailable", error.Code);
        Assert.Equal("External analysis was unavailable.", error.Message);
    }

    [Fact]
    public void Response_CanRepresentPartialResultWithoutFullRetrievedContext()
    {
        var impactMap = new ImpactMap();

        var response = new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.Partial,
            ImpactMap: impactMap,
            Metadata: CreateResponseMetadata(),
            RetrievedContextState: RetrievedContextState.Partial,
            RetrievedContextItems:
            [
                new RetrievedContextItem
                {
                    SourceTitle = "Requirement summary",
                    SourceId = "REQ-21",
                    Excerpt = "Only an excerpt was returned.",
                    Completeness = RetrievedContextItemCompleteness.ExcerptOnly,
                    WarningOrLimitationNote = "Full source text was not returned."
                }
            ],
            Warnings: ["Retrieved context is partial."],
            Errors: [],
            SanitizedDiagnosticSnapshot: "{\"state\":\"partial\"}");

        var item = Assert.Single(response.RetrievedContextItems);
        Assert.Equal(ExternalRagAdapterResponseStatus.Partial, response.Status);
        Assert.Same(impactMap, response.ImpactMap);
        Assert.Equal(RetrievedContextState.Partial, response.RetrievedContextState);
        Assert.Equal("Only an excerpt was returned.", item.Excerpt);
        Assert.Null(item.Text);
        Assert.Equal(["Retrieved context is partial."], response.Warnings);
    }

    private static AnalysisInputSnapshot CreateInputSnapshot() =>
        new(
            AnalysisId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Analysis: new AnalysisInputFields(
                Title: "Gateway migration",
                OriginalDescription: "Original requirement",
                ProjectRequest: "Project request",
                SituationDescription: "Current situation",
                ChangeSource: "Change source"),
            ContextFragments: []);

    private static ExternalRagAdapterResponseMetadata CreateResponseMetadata() =>
        new(
            ProviderName: "ExternalKnowledgeProvider",
            AdapterName: "NeutralExternalAdapter",
            ModelName: "external-analysis-model",
            WorkflowName: "impact-analysis",
            ProfileName: "research-profile",
            SanitizedProperties: new Dictionary<string, string>
            {
                ["diagnosticLevel"] = "summary"
            });

    private static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        var method = Assert.Single(type.GetMethods());

        foreach (var referencedType in ExpandType(method.ReturnType))
        {
            yield return referencedType;
        }

        foreach (var parameter in method.GetParameters())
        {
            foreach (var referencedType in ExpandType(parameter.ParameterType))
            {
                yield return referencedType;
            }
        }
    }

    private static IEnumerable<Type> ExpandType(Type? type)
    {
        if (type is null)
        {
            yield break;
        }

        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var referencedType in ExpandType(genericArgument))
            {
                yield return referencedType;
            }
        }
    }
}
