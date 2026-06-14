using System.Text.Json;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Application.Analysis.External;

public sealed class MockExternalRagAdapter : IExternalRagAdapter
{
    private const string ProviderName = "LocalMockKnowledgeSource";
    private const string AdapterName = nameof(MockExternalRagAdapter);
    private const string ModelName = "local-demo-model";
    private const string WorkflowName = "mock-impact-analysis";
    private const string HappyPathProfileName = "happy-path";
    private const string MetadataOnlyProfileName = "metadata-only";
    private const string UnavailableProfileName = "unavailable";
    private const string PartialProfileName = "partial";
    private const string FailedProfileName = "failed";

    public Task<ExternalRagAdapterResponse> AnalyzeAsync(
        ExternalRagAdapterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var scenario = SelectScenario(request.ExecutionMetadata.RequestedProfileName);

        if (scenario == MockExternalRagScenario.Failed)
        {
            return Task.FromResult(CreateFailedResponse(request));
        }

        var response = new ExternalRagAdapterResponse(
            Status: CreateSuccessfulStatus(scenario),
            ImpactMap: CreateImpactMap(request),
            Metadata: CreateMetadata(scenario),
            RetrievedContextState: CreateRetrievedContextState(scenario),
            RetrievedContextItems: CreateRetrievedContextItems(scenario),
            Warnings: CreateWarnings(scenario),
            Errors: [],
            SanitizedDiagnosticSnapshot: CreateDiagnosticSnapshot(
                request.CorrelationId,
                scenario,
                CreateSuccessfulStatus(scenario),
                CreateRetrievedContextState(scenario),
                CreateRetrievedContextItems(scenario).Count));

        return Task.FromResult(response);
    }

    private static MockExternalRagScenario SelectScenario(string? requestedProfileName)
    {
        var normalizedProfileName = requestedProfileName?.Trim();

        return normalizedProfileName switch
        {
            null or "" or HappyPathProfileName => MockExternalRagScenario.HappyPath,
            MetadataOnlyProfileName => MockExternalRagScenario.MetadataOnly,
            UnavailableProfileName => MockExternalRagScenario.Unavailable,
            PartialProfileName => MockExternalRagScenario.Partial,
            FailedProfileName => MockExternalRagScenario.Failed,
            _ => MockExternalRagScenario.HappyPath
        };
    }

    private static ExternalRagAdapterResponse CreateFailedResponse(ExternalRagAdapterRequest request) =>
        new(
            Status: ExternalRagAdapterResponseStatus.Failed,
            ImpactMap: null,
            Metadata: CreateMetadata(MockExternalRagScenario.Failed),
            RetrievedContextState: RetrievedContextState.Unavailable,
            RetrievedContextItems: [],
            Warnings:
            [
                "Local mock external analysis returned a controlled failed response."
            ],
            Errors:
            [
                new ExternalRagAdapterError(
                    Code: "mock_external_failure",
                    Message: "Local mock external analysis did not produce an impact map.",
                    DiagnosticDetails: "This deterministic scenario is intended for failed response handling tests.")
            ],
            SanitizedDiagnosticSnapshot: CreateDiagnosticSnapshot(
                request.CorrelationId,
                MockExternalRagScenario.Failed,
                ExternalRagAdapterResponseStatus.Failed,
                RetrievedContextState.Unavailable,
                retrievedContextItemCount: 0));

    private static ExternalRagAdapterResponseStatus CreateSuccessfulStatus(MockExternalRagScenario scenario) =>
        scenario switch
        {
            MockExternalRagScenario.MetadataOnly => ExternalRagAdapterResponseStatus.CompletedWithWarnings,
            MockExternalRagScenario.Unavailable => ExternalRagAdapterResponseStatus.CompletedWithWarnings,
            MockExternalRagScenario.Partial => ExternalRagAdapterResponseStatus.Partial,
            _ => ExternalRagAdapterResponseStatus.Completed
        };

    private static RetrievedContextState CreateRetrievedContextState(MockExternalRagScenario scenario) =>
        scenario switch
        {
            MockExternalRagScenario.MetadataOnly => RetrievedContextState.MetadataOnly,
            MockExternalRagScenario.Unavailable => RetrievedContextState.Unavailable,
            MockExternalRagScenario.Partial => RetrievedContextState.Partial,
            _ => RetrievedContextState.Available
        };

    private static ExternalRagAdapterResponseMetadata CreateMetadata(MockExternalRagScenario scenario) =>
        new(
            ProviderName: ProviderName,
            AdapterName: AdapterName,
            ModelName: ModelName,
            WorkflowName: WorkflowName,
            ProfileName: CreateProfileName(scenario),
            SanitizedProperties: new Dictionary<string, string>
            {
                ["execution"] = "local-demo",
                ["responseShape"] = CreateResponseShape(scenario)
            });

    private static string CreateProfileName(MockExternalRagScenario scenario) =>
        scenario switch
        {
            MockExternalRagScenario.MetadataOnly => MetadataOnlyProfileName,
            MockExternalRagScenario.Unavailable => UnavailableProfileName,
            MockExternalRagScenario.Partial => PartialProfileName,
            MockExternalRagScenario.Failed => FailedProfileName,
            _ => HappyPathProfileName
        };

    private static string CreateResponseShape(MockExternalRagScenario scenario) =>
        scenario switch
        {
            MockExternalRagScenario.MetadataOnly => "completed-with-metadata-only-context",
            MockExternalRagScenario.Unavailable => "completed-with-unavailable-context",
            MockExternalRagScenario.Partial => "partial-with-warnings",
            MockExternalRagScenario.Failed => "failed-with-sanitized-error",
            _ => "completed-with-full-context"
        };

    private static IReadOnlyList<string> CreateWarnings(MockExternalRagScenario scenario) =>
        scenario switch
        {
            MockExternalRagScenario.MetadataOnly =>
            [
                "Retrieved context contains source metadata only; source text was not returned."
            ],
            MockExternalRagScenario.Unavailable =>
            [
                "Structured impact analysis is available, but retrieved context is unavailable in this mock scenario."
            ],
            MockExternalRagScenario.Partial =>
            [
                "Retrieved context is partial; full source text was not returned."
            ],
            _ => []
        };

    private static IReadOnlyList<RetrievedContextItem> CreateRetrievedContextItems(MockExternalRagScenario scenario) =>
        scenario switch
        {
            MockExternalRagScenario.MetadataOnly => CreateMetadataOnlyRetrievedContextItems(),
            MockExternalRagScenario.Unavailable => [],
            MockExternalRagScenario.Partial => CreatePartialRetrievedContextItems(),
            _ => CreateFullRetrievedContextItems()
        };

    private static IReadOnlyList<RetrievedContextItem> CreateFullRetrievedContextItems() =>
        [
            new()
            {
                SourceTitle = "Local demo requirement catalogue",
                SourceId = "mock-source-requirements",
                ExternalReference = "local-demo-REQ-001",
                FragmentId = "mock-fragment-requirements-001",
                Text = "The affected requirement describes a controlled change to an integration boundary and requires expert review before implementation planning.",
                Excerpt = "Controlled change to an integration boundary requires expert review.",
                UrlOrReference = "local-demo://requirements/REQ-001",
                Rank = 1,
                Score = 0.94,
                ProviderName = ProviderName,
                AdapterName = AdapterName,
                Completeness = RetrievedContextItemCompleteness.FullText
            },
            new()
            {
                SourceTitle = "Local demo decision log",
                SourceId = "mock-source-decisions",
                ExternalReference = "local-demo-ADR-002",
                FragmentId = "mock-fragment-decisions-002",
                Text = "The demo decision log records that external analytical material is preliminary and must remain separated from the human expert conclusion.",
                Excerpt = "External analytical material is preliminary and separate from the expert conclusion.",
                UrlOrReference = "local-demo://decisions/ADR-002",
                Rank = 2,
                Score = 0.89,
                ProviderName = ProviderName,
                AdapterName = AdapterName,
                Completeness = RetrievedContextItemCompleteness.FullText
            }
        ];

    private static IReadOnlyList<RetrievedContextItem> CreateMetadataOnlyRetrievedContextItems() =>
        [
            new()
            {
                SourceTitle = "Local demo integration inventory",
                SourceId = "mock-source-inventory",
                ExternalReference = "local-demo-INV-003",
                FragmentId = "mock-fragment-inventory-003",
                UrlOrReference = "local-demo://inventory/INV-003",
                Rank = 1,
                Score = 0.82,
                ProviderName = ProviderName,
                AdapterName = AdapterName,
                Completeness = RetrievedContextItemCompleteness.MetadataOnly,
                WarningOrLimitationNote = "Only source metadata is available in this mock scenario."
            }
        ];

    private static IReadOnlyList<RetrievedContextItem> CreatePartialRetrievedContextItems() =>
        [
            new()
            {
                SourceTitle = "Local demo requirement excerpt",
                SourceId = "mock-source-requirements",
                ExternalReference = "local-demo-REQ-004",
                FragmentId = "mock-fragment-requirements-004",
                Excerpt = "The requested change may affect an integration boundary.",
                UrlOrReference = "local-demo://requirements/REQ-004",
                Rank = 1,
                Score = 0.76,
                ProviderName = ProviderName,
                AdapterName = AdapterName,
                Completeness = RetrievedContextItemCompleteness.ExcerptOnly,
                WarningOrLimitationNote = "Full source text is not available in this mock scenario."
            }
        ];

    private static ImpactMap CreateImpactMap(ExternalRagAdapterRequest request)
    {
        var analysis = request.InputSnapshot.Analysis;
        var impactMap = new ImpactMap
        {
            ChangeSummary =
            {
                Title = CreateTitle("Local mock external impact summary", analysis.Title),
                Description = "Deterministic local external AI/RAG demo result for a proposed requirement change.",
                Severity = ImpactSeverity.Medium,
                Notes = "Generated by the local mock external adapter."
            },
            PreliminaryAssessment =
            {
                Title = "Requires human expert review",
                Description = "The material is preliminary and does not replace expert assessment or management review.",
                Severity = ImpactSeverity.Medium,
                Notes = "No external service is used by this mock adapter."
            }
        };

        var requirement = impactMap.AddAffectedRequirement();
        requirement.Title = "Review requirement boundary";
        requirement.Description = "Check whether the requested change modifies the documented requirement boundary.";
        requirement.Severity = ImpactSeverity.Medium;

        var decision = impactMap.AddAffectedProjectDecision();
        decision.Title = "Confirm implementation approach";
        decision.Description = "Verify that existing project decisions still support the proposed change.";
        decision.Severity = ImpactSeverity.Low;

        var risk = impactMap.AddRisk();
        risk.Title = "Incomplete downstream impact review";
        risk.Description = "Related tasks, documents, or tests may still require expert confirmation.";
        risk.Severity = ImpactSeverity.Medium;

        return impactMap;
    }

    private static string CreateDiagnosticSnapshot(
        Guid correlationId,
        MockExternalRagScenario scenario,
        ExternalRagAdapterResponseStatus status,
        RetrievedContextState retrievedContextState,
        int retrievedContextItemCount)
    {
        var snapshot = new
        {
            status = CreateDiagnosticStatus(status),
            provider = ProviderName,
            adapter = AdapterName,
            profile = CreateProfileName(scenario),
            retrievedContextState = retrievedContextState.ToString(),
            retrievedContextItemCount,
            correlationId
        };

        return JsonSerializer.Serialize(snapshot);
    }

    private static string CreateDiagnosticStatus(ExternalRagAdapterResponseStatus status) =>
        status switch
        {
            ExternalRagAdapterResponseStatus.Completed => "completed",
            ExternalRagAdapterResponseStatus.CompletedWithWarnings => "completedWithWarnings",
            ExternalRagAdapterResponseStatus.Partial => "partial",
            ExternalRagAdapterResponseStatus.Failed => "failed",
            _ => "unknown"
        };

    private static string CreateTitle(string fallback, string title) =>
        string.IsNullOrWhiteSpace(title) ? fallback : title.Trim();

    private enum MockExternalRagScenario
    {
        HappyPath,
        MetadataOnly,
        Unavailable,
        Partial,
        Failed
    }
}
