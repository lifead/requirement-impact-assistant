using System.Text.Json;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

internal static class DifyExternalRagMapper
{
    public const string ProviderName = "Dify";
    public const string AdapterName = nameof(DifyExternalRagAdapter);

    private static readonly JsonSerializerOptions DiagnosticJsonOptions = new(JsonSerializerDefaults.Web);

    public static DifyWorkflowRequestDto CreateRequest(ExternalRagAdapterRequest request)
    {
        var analysis = request.InputSnapshot.Analysis;

        return new DifyWorkflowRequestDto
        {
            Inputs = new DifyWorkflowInputsDto
            {
                Analysis = new DifyAnalysisInputDto
                {
                    AnalysisId = request.InputSnapshot.AnalysisId.ToString(),
                    Title = analysis.Title,
                    OriginalDescription = analysis.OriginalDescription,
                    ProjectRequest = analysis.ProjectRequest,
                    SituationDescription = analysis.SituationDescription,
                    ChangeSource = analysis.ChangeSource
                },
                ManualContext = request.CanForwardManualContextToExternalAiOrRag
                    ? CreateManualContext(request.ManualContext)
                    : null,
                ManualContextPolicy = request.CanForwardManualContextToExternalAiOrRag
                    ? "forwarded_when_available"
                    : "not_forwarded",
                ExpectedResult = new DifyExpectedResultDto
                {
                    Sections = request.ExpectedResult.Sections
                        .Select(section => new DifyExpectedResultSectionDto
                        {
                            Key = section.Key,
                            ItemType = section.ItemType,
                            IsCollection = section.IsCollection,
                            AllowsRelatedContextFragmentIds = section.AllowsRelatedContextFragmentIds
                        })
                        .ToArray()
                },
                BoundaryNotice = new DifyBoundaryNoticeDto
                {
                    IsPreliminaryAnalyticalMaterial = request.BoundaryNotice.IsPreliminaryAnalyticalMaterial,
                    AiDoesNotMakeManagementDecision = request.BoundaryNotice.AiDoesNotMakeManagementDecision,
                    HumanDecisionAuthority = request.BoundaryNotice.HumanDecisionAuthority,
                    ResultUseStatement = request.BoundaryNotice.ResultUseStatement
                },
                Execution = new DifyExecutionMetadataDto
                {
                    EngineName = request.ExecutionMetadata.EngineName,
                    RequestedProfileName = request.ExecutionMetadata.RequestedProfileName
                }
            },
            User = $"analysis-{request.CorrelationId:N}"
        };
    }

    public static ExternalRagAdapterResponse CreateResponse(
        ExternalRagAdapterRequest request,
        DifyWorkflowResponseDto response,
        DifyExternalRagOptions options)
    {
        var outputs = response.Data?.Outputs;
        if (outputs?.ImpactMap is null)
        {
            throw new InvalidOperationException("Dify response did not contain a structured impact map.");
        }

        var retrievedContextItems = outputs.RetrievedContext
            .Select(MapRetrievedContextItem)
            .ToArray();
        var retrievedContextState = retrievedContextItems.Length == 0
            ? RetrievedContextState.Unavailable
            : RetrievedContextState.Available;
        var metadata = CreateMetadata(request, response, options);

        return new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.Completed,
            ImpactMap: MapImpactMap(outputs.ImpactMap),
            Metadata: metadata,
            RetrievedContextState: retrievedContextState,
            RetrievedContextItems: retrievedContextItems,
            Warnings: outputs.Warnings,
            Errors: [],
            SanitizedDiagnosticSnapshot: CreateDiagnosticSnapshot(
                request.CorrelationId,
                ExternalRagAdapterResponseStatus.Completed,
                metadata,
                response.Data?.Status,
                retrievedContextState,
                retrievedContextItems.Length));
    }

    private static DifyManualContextDto? CreateManualContext(ExternalRagManualContextBlock? manualContext)
    {
        if (manualContext is null)
        {
            return null;
        }

        return new DifyManualContextDto
        {
            CombinedText = manualContext.CombinedText,
            Fragments = manualContext.ContextFragments
                .Select(fragment => new DifyManualContextFragmentDto
                {
                    Id = fragment.Id.ToString(),
                    Type = fragment.Type,
                    Source = fragment.Source,
                    Text = fragment.Text,
                    FileName = fragment.FileName
                })
                .ToArray()
        };
    }

    private static ExternalRagAdapterResponseMetadata CreateMetadata(
        ExternalRagAdapterRequest request,
        DifyWorkflowResponseDto response,
        DifyExternalRagOptions options)
    {
        var responseShape = response.Data?.Outputs?.Metadata?.ResponseShape;

        return new ExternalRagAdapterResponseMetadata(
            ProviderName: ProviderName,
            AdapterName: AdapterName,
            ModelName: NormalizeOptional(response.Data?.Outputs?.Metadata?.Model),
            WorkflowName: NormalizeOptional(response.Data?.WorkflowId) ?? NormalizeOptional(options.WorkflowOrAppId),
            ProfileName: NormalizeOptional(request.ExecutionMetadata.RequestedProfileName) ??
                NormalizeOptional(options.ProfileName),
            SanitizedProperties: new Dictionary<string, string>
            {
                ["providerStatus"] = NormalizeOptional(response.Data?.Status) ?? "unknown",
                ["responseShape"] = NormalizeOptional(responseShape) ?? "dify-workflow-outputs"
            });
    }

    private static ImpactMap MapImpactMap(DifyImpactMapDto source)
    {
        var impactMap = new ImpactMap();

        Apply(source.ChangeSummary, impactMap.ChangeSummary);
        Apply(source.PreliminaryAssessment, impactMap.PreliminaryAssessment);
        AddItems(source.AffectedRequirements, impactMap.AddAffectedRequirement);
        AddItems(source.AffectedTasks, impactMap.AddAffectedTask);
        AddItems(source.AffectedProjectDecisions, impactMap.AddAffectedProjectDecision);
        AddItems(source.AffectedApiInterfacesDocumentsTests, impactMap.AddAffectedApiInterfaceDocumentTest);
        AddItems(source.AffectedArchitecturalConstraints, impactMap.AddAffectedArchitecturalConstraint);
        AddItems(source.AffectedOrganizationalContextItems, impactMap.AddAffectedOrganizationalContextItem);
        AddItems(source.Contradictions, impactMap.AddContradiction);
        AddItems(source.MissingInformation, impactMap.AddMissingInformation);
        AddItems(source.ClarificationQuestions, impactMap.AddClarificationQuestion);
        AddItems(source.Risks, impactMap.AddRisk);
        AddItems(source.OptionsForExpertReview, impactMap.AddOptionForExpertReview);

        return impactMap;
    }

    private static void AddItems(
        IEnumerable<DifyImpactMapItemDto> sourceItems,
        Func<ImpactMapItem> addItem)
    {
        foreach (var sourceItem in sourceItems)
        {
            Apply(sourceItem, addItem());
        }
    }

    private static void Apply(DifyImpactMapItemDto? source, ImpactMapItem target)
    {
        if (source is null)
        {
            return;
        }

        target.Title = source.Title?.Trim() ?? string.Empty;
        target.Description = source.Description?.Trim() ?? string.Empty;
        target.Severity = MapSeverity(source.Severity);
        target.Notes = source.Notes?.Trim() ?? string.Empty;

        foreach (var relatedId in source.RelatedContextFragmentIds)
        {
            if (Guid.TryParse(relatedId, out var parsedId))
            {
                target.RelatedContextFragmentIds.Add(parsedId);
            }
        }
    }

    private static ImpactSeverity MapSeverity(string? value) =>
        Enum.TryParse<ImpactSeverity>(value, ignoreCase: true, out var severity)
            ? severity
            : ImpactSeverity.NotSpecified;

    private static RetrievedContextItem MapRetrievedContextItem(DifyRetrievedContextItemDto source)
    {
        var completeness = CreateCompleteness(source);

        return new RetrievedContextItem
        {
            SourceTitle = NormalizeOptional(source.SourceTitle) ?? "Dify retrieved context",
            SourceId = NormalizeOptional(source.SourceId),
            ExternalReference = NormalizeOptional(source.ExternalReference),
            FragmentId = NormalizeOptional(source.FragmentId),
            Text = NormalizeOptional(source.Text),
            Excerpt = NormalizeOptional(source.Excerpt),
            UrlOrReference = NormalizeOptional(source.UrlOrReference),
            Rank = source.Rank,
            Score = source.Score,
            ProviderName = ProviderName,
            AdapterName = AdapterName,
            Completeness = completeness,
            WarningOrLimitationNote = completeness == RetrievedContextItemCompleteness.MetadataOnly
                ? "Dify returned source metadata without excerpt or full text."
                : null
        };
    }

    private static RetrievedContextItemCompleteness CreateCompleteness(DifyRetrievedContextItemDto source)
    {
        if (!string.IsNullOrWhiteSpace(source.Text))
        {
            return RetrievedContextItemCompleteness.FullText;
        }

        if (!string.IsNullOrWhiteSpace(source.Excerpt))
        {
            return RetrievedContextItemCompleteness.ExcerptOnly;
        }

        return RetrievedContextItemCompleteness.MetadataOnly;
    }

    private static string CreateDiagnosticSnapshot(
        Guid correlationId,
        ExternalRagAdapterResponseStatus status,
        ExternalRagAdapterResponseMetadata metadata,
        string? providerStatus,
        RetrievedContextState retrievedContextState,
        int retrievedContextItemCount)
    {
        var snapshot = new
        {
            status = CreateDiagnosticStatus(status),
            provider = metadata.ProviderName,
            adapter = metadata.AdapterName,
            workflow = metadata.WorkflowName,
            profile = metadata.ProfileName,
            providerStatus = NormalizeOptional(providerStatus) ?? "unknown",
            retrievedContextState = retrievedContextState.ToString(),
            retrievedContextItemCount,
            correlationId
        };

        return JsonSerializer.Serialize(snapshot, DiagnosticJsonOptions);
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
