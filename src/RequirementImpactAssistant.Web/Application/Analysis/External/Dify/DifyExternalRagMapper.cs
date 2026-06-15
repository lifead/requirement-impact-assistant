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
        var providerStatus = response.Data?.Status;
        if (IsProviderFailureStatus(providerStatus))
        {
            return CreateFailureResponse(
                request,
                options,
                providerStatus: providerStatus,
                errorCode: "dify_provider_error",
                errorMessage: "Dify external RAG provider reported a failed analysis.",
                diagnosticDetails: "Provider status indicated failure.",
                warnings: ["Dify external RAG provider did not complete the analysis."]);
        }

        if (outputs?.ImpactMap is null)
        {
            return CreateFailureResponse(
                request,
                options,
                providerStatus: providerStatus,
                errorCode: "dify_incomplete_response",
                errorMessage: "Dify external RAG provider response did not contain a structured impact map.",
                diagnosticDetails: "Expected structured impact map was missing from provider outputs.",
                warnings: ["Dify external RAG provider returned an incomplete structured response."]);
        }

        var retrievedContextItems = outputs.RetrievedContext
            .Select(MapRetrievedContextItem)
            .ToArray();
        var retrievedContextState = CreateRetrievedContextState(retrievedContextItems);
        var metadata = CreateMetadata(request, response, options);
        var warnings = CreateWarnings(outputs.Warnings, retrievedContextState);
        var status = CreateResponseStatus(retrievedContextState, warnings);

        return new ExternalRagAdapterResponse(
            Status: status,
            ImpactMap: MapImpactMap(outputs.ImpactMap),
            Metadata: metadata,
            RetrievedContextState: retrievedContextState,
            RetrievedContextItems: retrievedContextItems,
            Warnings: warnings,
            Errors: [],
            SanitizedDiagnosticSnapshot: CreateDiagnosticSnapshot(
                request.CorrelationId,
                status,
                metadata,
                response.Data?.Status,
                retrievedContextState,
                retrievedContextItems.Length));
    }

    public static ExternalRagAdapterResponse CreateUnavailableConfigurationResponse(
        ExternalRagAdapterRequest request,
        DifyExternalRagOptions options,
        DifyExternalRagConfigurationStatus configurationStatus)
    {
        var disabled = !configurationStatus.IsEnabled;
        var diagnosticDetails = disabled
            ? "Dify adapter is disabled."
            : string.Join(" ", configurationStatus.Reasons);

        return CreateFailureResponse(
            request,
            options,
            providerStatus: disabled ? "disabled" : "configuration-unavailable",
            errorCode: disabled ? "dify_disabled" : "dify_configuration_unavailable",
            errorMessage: disabled
                ? "Dify external RAG adapter is disabled."
                : "Dify external RAG adapter is unavailable because its configuration is incomplete.",
            diagnosticDetails: diagnosticDetails,
            warnings:
            [
                disabled
                    ? "Dify external RAG adapter is disabled."
                    : "Dify external RAG adapter is unavailable; configuration is incomplete."
            ]);
    }

    public static ExternalRagAdapterResponse CreateFailureResponse(
        ExternalRagAdapterRequest request,
        DifyExternalRagOptions options,
        string? providerStatus,
        string errorCode,
        string errorMessage,
        string? diagnosticDetails,
        IReadOnlyList<string> warnings)
    {
        var metadata = CreateFailureMetadata(request, options, providerStatus);

        return new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.Failed,
            ImpactMap: null,
            Metadata: metadata,
            RetrievedContextState: RetrievedContextState.Unavailable,
            RetrievedContextItems: [],
            Warnings: warnings,
            Errors:
            [
                new ExternalRagAdapterError(
                    Code: errorCode,
                    Message: errorMessage,
                    DiagnosticDetails: NormalizeOptional(diagnosticDetails))
            ],
            SanitizedDiagnosticSnapshot: CreateDiagnosticSnapshot(
                request.CorrelationId,
                ExternalRagAdapterResponseStatus.Failed,
                metadata,
                providerStatus,
                RetrievedContextState.Unavailable,
                retrievedContextItemCount: 0));
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
                ["providerStatus"] = NormalizeProviderStatusForDiagnostics(response.Data?.Status),
                ["responseShape"] = NormalizeOptional(responseShape) ?? "dify-workflow-outputs"
            });
    }

    private static ExternalRagAdapterResponseMetadata CreateFailureMetadata(
        ExternalRagAdapterRequest request,
        DifyExternalRagOptions options,
        string? providerStatus) =>
        new(
            ProviderName: ProviderName,
            AdapterName: AdapterName,
            ModelName: null,
            WorkflowName: NormalizeOptional(options.WorkflowOrAppId),
            ProfileName: NormalizeOptional(request.ExecutionMetadata.RequestedProfileName) ??
                NormalizeOptional(options.ProfileName),
            SanitizedProperties: new Dictionary<string, string>
            {
                ["providerStatus"] = NormalizeProviderStatusForDiagnostics(providerStatus),
                ["responseShape"] = "unavailable"
            });

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

    private static RetrievedContextState CreateRetrievedContextState(
        IReadOnlyList<RetrievedContextItem> retrievedContextItems)
    {
        if (retrievedContextItems.Count == 0)
        {
            return RetrievedContextState.Unavailable;
        }

        if (retrievedContextItems.All(item => item.Completeness == RetrievedContextItemCompleteness.FullText))
        {
            return RetrievedContextState.Available;
        }

        if (retrievedContextItems.All(item => item.Completeness == RetrievedContextItemCompleteness.MetadataOnly))
        {
            return RetrievedContextState.MetadataOnly;
        }

        return RetrievedContextState.Partial;
    }

    private static ExternalRagAdapterResponseStatus CreateResponseStatus(
        RetrievedContextState retrievedContextState,
        IReadOnlyList<string> warnings) =>
        retrievedContextState switch
        {
            RetrievedContextState.Partial => ExternalRagAdapterResponseStatus.Partial,
            RetrievedContextState.MetadataOnly or RetrievedContextState.Unavailable =>
                ExternalRagAdapterResponseStatus.CompletedWithWarnings,
            _ when warnings.Count > 0 => ExternalRagAdapterResponseStatus.CompletedWithWarnings,
            _ => ExternalRagAdapterResponseStatus.Completed
        };

    private static IReadOnlyList<string> CreateWarnings(
        IReadOnlyList<string> providerWarnings,
        RetrievedContextState retrievedContextState)
    {
        var warnings = providerWarnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning.Trim())
            .ToList();

        switch (retrievedContextState)
        {
            case RetrievedContextState.Unavailable:
                warnings.Add("Dify returned structured impact analysis without retrieved context.");
                break;
            case RetrievedContextState.MetadataOnly:
                warnings.Add("Dify returned retrieved context metadata without source text or excerpts.");
                break;
            case RetrievedContextState.Partial:
                warnings.Add("Dify returned partial retrieved context; full source text was not available for every item.");
                break;
        }

        return warnings
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsProviderFailureStatus(string? providerStatus)
    {
        var normalizedStatus = NormalizeProviderStatusForDiagnostics(providerStatus);

        return normalizedStatus is "failed" or "error";
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
            providerStatus = NormalizeProviderStatusForDiagnostics(providerStatus),
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

    private static string NormalizeProviderStatusForDiagnostics(string? providerStatus)
    {
        var normalizedStatus = NormalizeOptional(providerStatus);
        if (normalizedStatus is null)
        {
            return "unknown";
        }

        if (IsSafeHttpStatus(normalizedStatus))
        {
            return normalizedStatus.ToLowerInvariant();
        }

        var safeStatus = normalizedStatus.ToLowerInvariant();
        return safeStatus switch
        {
            "succeeded" or
            "completed" or
            "success" or
            "failed" or
            "error" or
            "stopped" or
            "running" or
            "timeout" or
            "transport-error" or
            "malformed-response" or
            "disabled" or
            "configuration-unavailable" => safeStatus,
            _ => "unknown"
        };
    }

    private static bool IsSafeHttpStatus(string value)
    {
        if (!value.StartsWith("http-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(value.AsSpan("http-".Length), out var statusCode) &&
            statusCode is >= 100 and <= 599;
    }
}
