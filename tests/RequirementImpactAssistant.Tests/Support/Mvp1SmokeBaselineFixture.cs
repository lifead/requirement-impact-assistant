using System.Text.Json;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using DomainAnalysis = RequirementImpactAssistant.Web.Domain.Analysis;

namespace RequirementImpactAssistant.Tests.Support;

public static class Mvp1SmokeBaselineFixture
{
    public static readonly Guid AnalysisId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    public static readonly Guid ManualRequirementContextFragmentId = Guid.Parse("10000000-0000-4000-8000-000000000101");
    public static readonly Guid ManualDecisionContextFragmentId = Guid.Parse("10000000-0000-4000-8000-000000000102");
    public static readonly Guid ExpertEvaluationId = Guid.Parse("10000000-0000-4000-8000-000000000201");
    public static readonly Guid ExpertConclusionId = Guid.Parse("10000000-0000-4000-8000-000000000301");

    public static readonly DateTimeOffset CreatedAt = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset UpdatedAt = new(2026, 1, 15, 9, 5, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset GeneratedAt = new(2026, 1, 15, 9, 10, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset ExpertFixedAt = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions SnapshotJsonSerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static Mvp1SmokeBaseline Create()
    {
        var analysis = CreateAnalysis();
        var analysisRequest = CreateAnalysisRequest();
        var expectedImpactMap = CreateExpectedImpactMap();
        var expectedExpertEvaluation = CreateExpectedExpertEvaluation();
        var expectedExpertConclusion = CreateExpectedExpertConclusion();
        var externalRequest = CreateExternalHappyPathRequest();
        var externalResponse = CreateExternalHappyPathResponse();

        return new Mvp1SmokeBaseline(
            Analysis: analysis,
            AnalysisRequest: analysisRequest,
            ManualContextFragments: analysis.ContextFragments.ToList(),
            ExpectedImpactMap: expectedImpactMap,
            ExpectedExpertEvaluation: expectedExpertEvaluation,
            ExpectedExpertConclusion: expectedExpertConclusion,
            ExternalHappyPathRequest: externalRequest,
            ExternalHappyPathResponse: externalResponse);
    }

    public static DomainAnalysis CreateAnalysis()
    {
        var analysis = new DomainAnalysis
        {
            Id = AnalysisId,
            Title = "local demo request - example integration boundary",
            Status = AnalysisStatus.ReadyForAnalysis,
            OriginalDescription = "The current local demo request describes a controlled change near an example integration boundary.",
            ProjectRequest = "Assess which anonymized requirement catalogue entries, project decisions, tests, and expert review notes may be affected by the example integration boundary change.",
            SituationDescription = "A demo requirement catalogue entry currently requires stable behavior around an example integration boundary.",
            ChangeSource = "local demo change note",
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };

        analysis.ContextFragments.AddRange(CreateManualContextFragments());

        return analysis;
    }

    public static IReadOnlyList<ContextFragment> CreateManualContextFragments() =>
    [
        new()
        {
            Id = ManualRequirementContextFragmentId,
            AnalysisId = AnalysisId,
            Type = ContextFragmentType.DocumentFragment,
            Source = "demo requirement catalogue",
            Text = "Manual context: demo requirement catalogue states that changes near the example integration boundary must remain traceable to a requirement item.",
            FileName = "demo-requirement-catalogue-note.txt",
            FilePath = null,
            CreatedAt = CreatedAt.AddMinutes(1)
        },
        new()
        {
            Id = ManualDecisionContextFragmentId,
            AnalysisId = AnalysisId,
            Type = ContextFragmentType.PreviousDecision,
            Source = "local demo decision note",
            Text = "Manual context: local demo decision note separates preliminary analytical material from the expert conclusion.",
            FileName = null,
            FilePath = null,
            CreatedAt = CreatedAt.AddMinutes(2)
        }
    ];

    public static AiAnalysisRequest CreateAnalysisRequest()
    {
        var snapshot = CreateInputSnapshot();

        return new AiAnalysisRequest(
            InputSnapshot: snapshot,
            InputSnapshotJson: JsonSerializer.Serialize(snapshot, SnapshotJsonSerializerOptions),
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default);
    }

    public static AnalysisInputSnapshot CreateInputSnapshot() =>
        new(
            AnalysisId: AnalysisId,
            Analysis: new AnalysisInputFields(
                Title: "local demo request - example integration boundary",
                OriginalDescription: "The current local demo request describes a controlled change near an example integration boundary.",
                ProjectRequest: "Assess which anonymized requirement catalogue entries, project decisions, tests, and expert review notes may be affected by the example integration boundary change.",
                SituationDescription: "A demo requirement catalogue entry currently requires stable behavior around an example integration boundary.",
                ChangeSource: "local demo change note"),
            ContextFragments: CreateManualContextFragments()
                .Select(fragment => new AnalysisContextFragmentSnapshot(
                    Id: fragment.Id,
                    Type: fragment.Type.ToString(),
                    Source: fragment.Source,
                    Text: fragment.Text,
                    FileName: fragment.FileName))
                .ToList());

    public static ImpactMap CreateExpectedImpactMap()
    {
        var impactMap = new ImpactMap
        {
            ChangeSummary =
            {
                Title = "Example integration boundary change",
                Description = "Preliminary map for a local demo request that may affect an example integration boundary.",
                Severity = ImpactSeverity.Medium,
                Notes = "Fixture baseline only; later smoke tasks may assert this stable content."
            },
            PreliminaryAssessment =
            {
                Title = "Requires expert review",
                Description = "The material is sufficient for a human expert to review likely requirement and boundary effects.",
                Severity = ImpactSeverity.Medium,
                Notes = "No management decision is made by the AI baseline."
            }
        };

        var requirement = impactMap.AddAffectedRequirement();
        requirement.Title = "Demo requirement catalogue item";
        requirement.Description = "The example integration boundary should stay traceable to a requirement catalogue item.";
        requirement.Severity = ImpactSeverity.Medium;
        requirement.RelatedContextFragmentIds.Add(ManualRequirementContextFragmentId);

        var projectDecision = impactMap.AddAffectedProjectDecision();
        projectDecision.Title = "Local demo decision note";
        projectDecision.Description = "The expert conclusion remains separate from preliminary analytical material.";
        projectDecision.Severity = ImpactSeverity.Low;
        projectDecision.RelatedContextFragmentIds.Add(ManualDecisionContextFragmentId);

        var interfaceDocumentTest = impactMap.AddAffectedApiInterfaceDocumentTest();
        interfaceDocumentTest.Title = "Example integration boundary contract";
        interfaceDocumentTest.Description = "Boundary contract notes and smoke tests may need review after the requirement change.";
        interfaceDocumentTest.Severity = ImpactSeverity.Medium;
        interfaceDocumentTest.RelatedContextFragmentIds.Add(ManualRequirementContextFragmentId);

        var missingInformation = impactMap.AddMissingInformation();
        missingInformation.Title = "Boundary acceptance detail";
        missingInformation.Description = "The baseline expects a later expert pass to confirm acceptance details for the example boundary.";
        missingInformation.Severity = ImpactSeverity.Low;

        var clarification = impactMap.AddClarificationQuestion();
        clarification.Title = "Confirm affected catalogue entries";
        clarification.Description = "Which demo requirement catalogue entries should be considered authoritative for this local request?";
        clarification.Severity = ImpactSeverity.Low;

        var risk = impactMap.AddRisk();
        risk.Title = "Traceability gap";
        risk.Description = "If the catalogue item and boundary note diverge, later implementation tasks may miss required expert checks.";
        risk.Severity = ImpactSeverity.Medium;
        risk.RelatedContextFragmentIds.Add(ManualRequirementContextFragmentId);

        var option = impactMap.AddOptionForExpertReview();
        option.Title = "Proceed with limited scope review";
        option.Description = "Review only the anonymized requirement catalogue and example integration boundary for the smoke baseline.";
        option.Severity = ImpactSeverity.Low;

        return impactMap;
    }

    public static ExpertEvaluation CreateExpectedExpertEvaluation()
    {
        var evaluation = new ExpertEvaluation
        {
            Id = ExpertEvaluationId,
            AnalysisId = AnalysisId,
            ContextSufficiency = ContextSufficiencyRating.PartiallySufficient,
            ResultUsefulness = ResultUsefulnessRating.Useful,
            GeneralComment = "Expert smoke baseline confirms that preliminary material is reviewable but remains non-decisional."
        };

        evaluation.EvaluatedItems.Add(new ExpertEvaluatedItem
        {
            Id = Guid.Parse("10000000-0000-4000-8000-000000000211"),
            TargetType = ExpertEvaluationTargetType.ImpactItem,
            TargetId = "change-summary",
            Mark = ExpertMark.Confirmed,
            Comment = "Change summary is suitable for later smoke assertions.",
            CorrectionText = string.Empty
        });

        evaluation.EvaluatedItems.Add(new ExpertEvaluatedItem
        {
            Id = Guid.Parse("10000000-0000-4000-8000-000000000212"),
            TargetType = ExpertEvaluationTargetType.ImpactItem,
            TargetId = "affected-requirement-001",
            Mark = ExpertMark.NeedsClarification,
            Comment = "The affected requirement remains anonymized and needs human confirmation.",
            CorrectionText = "Confirm authoritative demo requirement catalogue item before implementation."
        });

        evaluation.MissedItems.Add(new ExpertMissedItem
        {
            Id = Guid.Parse("10000000-0000-4000-8000-000000000221"),
            ItemType = ImpactMapItemType.AffectedTask,
            Title = "Review local smoke task boundary",
            Description = "Later smoke tasks should verify that execution paths remain separated.",
            Severity = ImpactSeverity.Low,
            Comment = "Included as expected human-layer data, not as an AI decision."
        });

        evaluation.Corrections.Add(new ExpertCorrection
        {
            Id = Guid.Parse("10000000-0000-4000-8000-000000000231"),
            TargetType = ExpertEvaluationTargetType.ImpactItem,
            TargetId = "risk-001",
            ItemType = ImpactMapItemType.Risk,
            Text = "Keep the risk phrasing limited to traceability inside the anonymized smoke fixture.",
            Comment = "Expert correction baseline for later human-layer smoke."
        });

        return evaluation;
    }

    public static ExpertConclusion CreateExpectedExpertConclusion() =>
        new()
        {
            Id = ExpertConclusionId,
            AnalysisId = AnalysisId,
            ConclusionType = ExpertConclusionType.AcceptWithLimitations,
            Comment = "Accept the preliminary smoke baseline for later automated checks only.",
            Rationale = "The fixture uses anonymized local demo data and keeps final expert authority with a human reviewer.",
            FixedAt = ExpertFixedAt
        };

    public static ExternalRagAdapterRequest CreateExternalHappyPathRequest()
    {
        var snapshot = CreateInputSnapshot();

        return new ExternalRagAdapterRequest(
            CorrelationId: AnalysisId,
            InputSnapshot: snapshot,
            ManualContext: new ExternalRagManualContextBlock(
                ContextFragments: snapshot.ContextFragments,
                CombinedText: string.Join(
                    Environment.NewLine,
                    snapshot.ContextFragments.Select(fragment => fragment.Text))),
            CanForwardManualContextToExternalAiOrRag: true,
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default,
            ExecutionMetadata: new ExternalRagRequestMetadata(
                EngineName: "local-smoke-external-baseline",
                RequestedProfileName: "happy-path",
                SanitizedProperties: new Dictionary<string, string>
                {
                    ["execution"] = "local-demo-fixture",
                    ["network"] = "disabled"
                }));
    }

    public static ExternalRagAdapterResponse CreateExternalHappyPathResponse() =>
        new(
            Status: ExternalRagAdapterResponseStatus.Completed,
            ImpactMap: CreateExpectedImpactMap(),
            Metadata: new ExternalRagAdapterResponseMetadata(
                ProviderName: "LocalMockKnowledgeSource",
                AdapterName: "LocalSmokeFixtureAdapter",
                ModelName: "local-demo-model",
                WorkflowName: "mock-impact-analysis",
                ProfileName: "happy-path",
                SanitizedProperties: new Dictionary<string, string>
                {
                    ["execution"] = "local-demo",
                    ["network"] = "disabled",
                    ["source"] = "test-fixture"
                }),
            RetrievedContextState: RetrievedContextState.Available,
            RetrievedContextItems:
            [
                new RetrievedContextItem
                {
                    SourceTitle = "demo requirement catalogue",
                    SourceId = "demo-req-001",
                    ExternalReference = "local-demo-REQ-001",
                    FragmentId = "retrieved-fragment-001",
                    Text = "Retrieved context: demo requirement catalogue confirms the example integration boundary is affected by controlled requirement changes.",
                    Excerpt = "Demo requirement catalogue confirms the example integration boundary is affected.",
                    UrlOrReference = "local-reference:demo-requirement-catalogue/REQ-001",
                    Rank = 1,
                    Score = 0.94,
                    ProviderName = "LocalMockKnowledgeSource",
                    AdapterName = "LocalSmokeFixtureAdapter",
                    Completeness = RetrievedContextItemCompleteness.FullText
                },
                new RetrievedContextItem
                {
                    SourceTitle = "local demo decision note",
                    SourceId = "demo-decision-002",
                    ExternalReference = "local-demo-DEC-002",
                    FragmentId = "retrieved-fragment-002",
                    Text = "Retrieved context: local demo decision note states that preliminary analytical material must stay separate from the expert conclusion.",
                    Excerpt = "Preliminary analytical material stays separate from the expert conclusion.",
                    UrlOrReference = "local-reference:local-demo-decision-note/DEC-002",
                    Rank = 2,
                    Score = 0.89,
                    ProviderName = "LocalMockKnowledgeSource",
                    AdapterName = "LocalSmokeFixtureAdapter",
                    Completeness = RetrievedContextItemCompleteness.FullText
                }
            ],
            Warnings: [],
            Errors: [],
            SanitizedDiagnosticSnapshot:
                "{\"status\":\"completed\",\"provider\":\"LocalMockKnowledgeSource\",\"adapter\":\"LocalSmokeFixtureAdapter\",\"profile\":\"happy-path\",\"retrievedContextState\":\"Available\",\"retrievedContextItemCount\":2,\"network\":\"disabled\"}");
}

public sealed record Mvp1SmokeBaseline(
    DomainAnalysis Analysis,
    AiAnalysisRequest AnalysisRequest,
    IReadOnlyList<ContextFragment> ManualContextFragments,
    ImpactMap ExpectedImpactMap,
    ExpertEvaluation ExpectedExpertEvaluation,
    ExpertConclusion ExpectedExpertConclusion,
    ExternalRagAdapterRequest ExternalHappyPathRequest,
    ExternalRagAdapterResponse ExternalHappyPathResponse);
