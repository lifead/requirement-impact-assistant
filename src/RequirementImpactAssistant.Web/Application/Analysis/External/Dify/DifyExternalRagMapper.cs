using System.Text.Json;
using System.Text.RegularExpressions;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

internal static class DifyExternalRagMapper
{
    public const string ProviderName = "Dify";
    public const string AdapterName = nameof(DifyExternalRagAdapter);

    private static readonly JsonSerializerOptions DiagnosticJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex SensitiveProviderWarningPattern = new(
        @"(authorization\s*:|bearer\s+\S+|\bapi[-_\s]*key\b|\b(?:access|refresh|auth|api)?[-_\s]*token\s*[:=]\s*\S+|\bsecret\b\s*[:=]\s*\S+|\bendpoint\b\s*[:=]\s*\S+|https?://\S+|s[k]-[A-Za-z0-9][A-Za-z0-9._-]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex SensitiveMappedAssignmentPattern = new(
        @"\b(?:api[-_\s]*key|apikey|key|access[-_\s]*token|refresh[-_\s]*token|auth[-_\s]*token|auth|token|password|secret|cookie|csrf|session[-_\s]*id|session)\b\s*[:=]\s*[^\s,;}\]]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex SchemeLessUrlPattern = new(
        @"(?<![@\w.-])(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,}(?::\d{2,5})?(?:/[^\s,;}\]]*)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private const string SanitizedProviderWarning =
        "Dify provider warning was redacted because it contained sensitive diagnostic content.";
    private const string PreliminaryMaterialNotice =
        "External AI/RAG output is preliminary analytical material and does not replace human expert review.";

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
                options,
                response.Data?.Status,
                retrievedContextState,
                retrievedContextItems.Length,
                warnings,
                []));
    }

    public static ExternalRagAdapterResponse CreateStreamingResponse(
        ExternalRagAdapterRequest request,
        DifyAgentSseParseResult streamResult,
        DifyExternalRagOptions options)
    {
        var providerStatus = streamResult.IsComplete ? "stream-complete" : "stream-incomplete";
        if (string.IsNullOrWhiteSpace(streamResult.Answer))
        {
            return CreateFailureResponse(
                request,
                options,
                providerStatus,
                errorCode: "dify_empty_stream_response",
                errorMessage: "Dify external RAG provider stream did not contain an answer.",
                diagnosticDetails: "The SSE stream did not produce a usable agent_message answer fragment.",
                warnings: CreateStreamingWarningsWithoutAnswer(
                    streamResult,
                    "Dify external RAG provider stream did not contain an answer."));
        }

        const RetrievedContextState retrievedContextState = RetrievedContextState.Unavailable;
        var answerParseResult = DifyAgentAnswerJsonParser.Parse(streamResult.Answer);
        var warnings = CreateStreamingWarnings(streamResult, answerParseResult);
        var status = CreateStreamingStatus(streamResult, answerParseResult, warnings);
        var impactMap = answerParseResult.StructuredAnswer is null
            ? CreateRawFallbackImpactMap(streamResult, answerParseResult)
            : MapImpactMap(answerParseResult.StructuredAnswer);
        var metadata = CreateStreamingMetadata(
            request,
            streamResult,
            answerParseResult,
            options,
            providerStatus,
            status);

        return new ExternalRagAdapterResponse(
            Status: status,
            ImpactMap: impactMap,
            Metadata: metadata,
            RetrievedContextState: retrievedContextState,
            RetrievedContextItems: [],
            Warnings: warnings,
            Errors: [],
            SanitizedDiagnosticSnapshot: CreateDiagnosticSnapshot(
                request.CorrelationId,
                status,
                metadata,
                options,
                providerStatus,
                retrievedContextState,
                retrievedContextItemCount: 0,
                warnings,
                []));
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
        var error = new ExternalRagAdapterError(
            Code: errorCode,
            Message: errorMessage,
            DiagnosticDetails: NormalizeOptional(diagnosticDetails));

        return new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.Failed,
            ImpactMap: null,
            Metadata: metadata,
            RetrievedContextState: RetrievedContextState.Unavailable,
            RetrievedContextItems: [],
            Warnings: warnings,
            Errors:
            [
                error
            ],
            SanitizedDiagnosticSnapshot: CreateDiagnosticSnapshot(
                request.CorrelationId,
                ExternalRagAdapterResponseStatus.Failed,
                metadata,
                options,
                providerStatus,
                RetrievedContextState.Unavailable,
                retrievedContextItemCount: 0,
                warnings,
                [
                    error
                ]));
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

    private static ExternalRagAdapterResponseMetadata CreateStreamingMetadata(
        ExternalRagAdapterRequest request,
        DifyAgentSseParseResult streamResult,
        DifyAgentAnswerParseResult answerParseResult,
        DifyExternalRagOptions options,
        string providerStatus,
        ExternalRagAdapterResponseStatus responseStatus)
    {
        var sanitizedProperties = new Dictionary<string, string>
        {
            ["providerStatus"] = NormalizeProviderStatusForDiagnostics(providerStatus),
            ["responseShape"] = answerParseResult.HasStructuredJson
                ? "dify-agent-answer-json"
                : "dify-agent-raw-answer-fallback",
            ["streamComplete"] = streamResult.IsComplete ? bool.TrueString.ToLowerInvariant() : bool.FalseString.ToLowerInvariant(),
            ["answerFragmentCount"] = streamResult.AnswerFragments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["hasMalformedEvents"] = streamResult.HasMalformedEvents ? bool.TrueString.ToLowerInvariant() : bool.FalseString.ToLowerInvariant(),
            ["agentThoughtEventCount"] = streamResult.AgentThoughtEventCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["unknownEventCount"] = streamResult.UnknownEventCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["answerParseStatus"] = answerParseResult.HasStructuredJson ? "parsed-json" : "raw-text-fallback",
            ["answerParseMode"] = CreateAnswerParseMode(answerParseResult.ParseMode),
            ["adapterResponseStatus"] = CreateDiagnosticStatus(responseStatus),
            ["rawAnswerFallbackRetained"] = answerParseResult.SanitizedRawText is null
                ? bool.FalseString.ToLowerInvariant()
                : bool.TrueString.ToLowerInvariant()
        };

        if (answerParseResult.StructuredAnswer is not null)
        {
            sanitizedProperties["usedSourceCount"] =
                answerParseResult.StructuredAnswer.UsedSources.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(answerParseResult.SanitizedRawText))
        {
            sanitizedProperties["rawAnswerFallbackText"] = answerParseResult.SanitizedRawText;
        }

        if (!string.IsNullOrWhiteSpace(streamResult.MessageEndMetadata?.MessageId))
        {
            sanitizedProperties["messageId"] = streamResult.MessageEndMetadata.MessageId;
        }

        if (!string.IsNullOrWhiteSpace(streamResult.MessageEndMetadata?.ConversationId))
        {
            sanitizedProperties["conversationId"] = streamResult.MessageEndMetadata.ConversationId;
        }

        foreach (var usageEntry in streamResult.MessageEndMetadata?.Usage ?? new Dictionary<string, string>())
        {
            sanitizedProperties[$"usage.{usageEntry.Key}"] = usageEntry.Value;
        }

        return new ExternalRagAdapterResponseMetadata(
            ProviderName: ProviderName,
            AdapterName: AdapterName,
            ModelName: null,
            WorkflowName: NormalizeOptional(options.WorkflowOrAppId),
            ProfileName: NormalizeOptional(request.ExecutionMetadata.RequestedProfileName) ??
                NormalizeOptional(options.ProfileName),
            SanitizedProperties: sanitizedProperties);
    }

    private static ImpactMap CreateRawFallbackImpactMap(
        DifyAgentSseParseResult streamResult,
        DifyAgentAnswerParseResult answerParseResult)
    {
        var impactMap = new ImpactMap();
        impactMap.ChangeSummary.Title = "External AI/RAG raw answer fallback retained";
        impactMap.ChangeSummary.Description =
            answerParseResult.SanitizedRawText ?? "The adapter retained no raw answer text because the Agent answer was empty.";
        impactMap.ChangeSummary.Severity = ImpactSeverity.NotSpecified;
        impactMap.ChangeSummary.Notes =
            $"SSE answer fragments received: {streamResult.AnswerFragments.Count}. Answer parse mode: {CreateAnswerParseMode(answerParseResult.ParseMode)}.";

        impactMap.PreliminaryAssessment.Title = "Requires human expert review";
        impactMap.PreliminaryAssessment.Description = PreliminaryMaterialNotice;
        impactMap.PreliminaryAssessment.Severity = ImpactSeverity.NotSpecified;

        return impactMap;
    }

    private static IReadOnlyList<string> CreateStreamingWarnings(
        DifyAgentSseParseResult streamResult,
        DifyAgentAnswerParseResult answerParseResult)
    {
        var warnings = streamResult.Warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning.Trim())
            .ToList();

        warnings.AddRange(answerParseResult.Warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning.Trim()));

        if (answerParseResult.StructuredAnswer is null)
        {
            warnings.Add("Dify Agent answer was not structured; sanitized raw answer fallback was retained.");
        }
        else
        {
            warnings.AddRange(answerParseResult.StructuredAnswer.Warnings
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Select(SanitizeProviderWarning));

            if (streamResult.IsComplete)
            {
                warnings.Add("Dify Agent answer was mapped without retrieved context resources; retrieved context mapping is unavailable until the dedicated retriever resources task.");
            }
        }

        return warnings
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static ExternalRagAdapterResponseStatus CreateStreamingStatus(
        DifyAgentSseParseResult streamResult,
        DifyAgentAnswerParseResult answerParseResult,
        IReadOnlyList<string> warnings)
    {
        if (answerParseResult.StructuredAnswer is null || !streamResult.IsComplete)
        {
            return ExternalRagAdapterResponseStatus.Partial;
        }

        return warnings.Count > 0
            ? ExternalRagAdapterResponseStatus.CompletedWithWarnings
            : ExternalRagAdapterResponseStatus.Completed;
    }

    private static IReadOnlyList<string> CreateStreamingWarningsWithoutAnswer(
        DifyAgentSseParseResult streamResult,
        string adapterWarning)
    {
        var warnings = streamResult.Warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning.Trim())
            .ToList();

        warnings.Add(adapterWarning);

        return warnings
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string CreateAnswerParseMode(DifyAgentAnswerParseMode parseMode) =>
        parseMode switch
        {
            DifyAgentAnswerParseMode.FullAnswerJson => "full-answer-json",
            DifyAgentAnswerParseMode.JsonSubstringFallback => "json-substring-fallback",
            DifyAgentAnswerParseMode.RawTextFallback => "raw-text-fallback",
            _ => "none"
        };

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

    private static ImpactMap MapImpactMap(DifyAgentAnswerDto source)
    {
        var impactMap = new ImpactMap();

        ApplyText(source.ChangeSummary, impactMap.ChangeSummary);
        impactMap.ChangeSummary.Notes = AppendNotice(impactMap.ChangeSummary.Notes, PreliminaryMaterialNotice);
        ApplyText(source.PreliminaryAssessment, impactMap.PreliminaryAssessment);
        impactMap.PreliminaryAssessment.Notes = AppendNotice(
            impactMap.PreliminaryAssessment.Notes,
            PreliminaryMaterialNotice);
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

    private static void AddItems(
        IEnumerable<JsonElement> sourceItems,
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

    private static void Apply(JsonElement source, ImpactMapItem target)
    {
        if (TryGetScalarText(source, out var scalarText))
        {
            target.Title = SanitizeMappedText(scalarText);
            target.Severity = ImpactSeverity.NotSpecified;
            return;
        }

        if (source.ValueKind != JsonValueKind.Object)
        {
            target.Title = SanitizeMappedText(source.GetRawText());
            target.Severity = ImpactSeverity.NotSpecified;
            return;
        }

        target.Title = SanitizeMappedText(
            GetFirstStringProperty(source, "title", "name", "summary", "text", "description") ?? string.Empty);
        target.Description = SanitizeMappedText(
            GetFirstStringProperty(source, "description", "details", "text", "summary") ?? string.Empty);
        target.Severity = MapSeverity(GetFirstStringProperty(source, "severity", "impactSeverity", "riskLevel"));
        target.Notes = SanitizeMappedText(GetFirstStringProperty(source, "notes", "rationale", "comment") ?? string.Empty);

        foreach (var relatedId in GetRelatedContextFragmentIds(source))
        {
            if (Guid.TryParse(relatedId, out var parsedId))
            {
                target.RelatedContextFragmentIds.Add(parsedId);
            }
        }
    }

    private static void ApplyText(string? source, ImpactMapItem target)
    {
        target.Title = SanitizeMappedText(source ?? string.Empty);
        target.Severity = ImpactSeverity.NotSpecified;
    }

    private static bool TryGetScalarText(JsonElement source, out string value)
    {
        value = string.Empty;
        switch (source.ValueKind)
        {
            case JsonValueKind.String:
                value = source.GetString() ?? string.Empty;
                return true;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                value = source.GetRawText();
                return true;
            default:
                return false;
        }
    }

    private static string? GetFirstStringProperty(JsonElement source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (source.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static IEnumerable<string> GetRelatedContextFragmentIds(JsonElement source)
    {
        foreach (var propertyName in new[] { "relatedContextFragmentIds", "related_context_fragment_ids" })
        {
            if (!source.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    yield return item.GetString()!;
                }
            }
        }
    }

    private static string AppendNotice(string notes, string notice)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return notice;
        }

        return string.Concat(notes.Trim(), " ", notice);
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
            .Select(SanitizeProviderWarning)
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

    private static string SanitizeProviderWarning(string warning)
    {
        var normalizedWarning = warning.Trim();

        return SensitiveProviderWarningPattern.IsMatch(normalizedWarning) ||
            SensitiveMappedAssignmentPattern.IsMatch(normalizedWarning) ||
            SchemeLessUrlPattern.IsMatch(normalizedWarning)
            ? SanitizedProviderWarning
            : normalizedWarning;
    }

    private static string SanitizeMappedText(string value)
    {
        var normalizedValue = value.Trim();

        var sanitizedValue = SensitiveMappedAssignmentPattern.Replace(normalizedValue, "[REDACTED]");
        sanitizedValue = SensitiveProviderWarningPattern.Replace(sanitizedValue, "[REDACTED]");
        return SchemeLessUrlPattern.Replace(sanitizedValue, "[REDACTED_URL]");
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
        DifyExternalRagOptions options,
        string? providerStatus,
        RetrievedContextState retrievedContextState,
        int retrievedContextItemCount,
        IReadOnlyList<string> warnings,
        IReadOnlyList<ExternalRagAdapterError> errors)
    {
        var snapshot = new
        {
            status = CreateDiagnosticStatus(status),
            provider = metadata.ProviderName,
            adapter = metadata.AdapterName,
            endpoint = CreateEndpointDiagnostic(options.Endpoint),
            workflow = metadata.WorkflowName,
            profile = metadata.ProfileName,
            providerStatus = NormalizeProviderStatusForDiagnostics(providerStatus),
            messageId = GetMetadataValue(metadata, "messageId"),
            conversationId = GetMetadataValue(metadata, "conversationId"),
            usage = CreateUsageDiagnostic(metadata),
            responseShape = GetMetadataValue(metadata, "responseShape"),
            retrievedContextState = retrievedContextState.ToString(),
            retrievedContextItemCount,
            warnings = warnings
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Select(SanitizeMappedText)
                .ToArray(),
            errors = errors
                .Select(error => new
                {
                    code = SanitizeMappedText(error.Code),
                    message = SanitizeMappedText(error.Message),
                    diagnosticDetails = SanitizeOptionalDiagnostic(error.DiagnosticDetails)
                })
                .ToArray(),
            correlationId
        };

        return JsonSerializer.Serialize(snapshot, DiagnosticJsonOptions);
    }

    private static object? CreateEndpointDiagnostic(string? endpoint)
    {
        var normalizedEndpoint = DifyAgentRequestContract.NormalizeChatMessagesEndpoint(endpoint);
        if (normalizedEndpoint is null)
        {
            return null;
        }

        return new
        {
            scheme = normalizedEndpoint.Scheme,
            host = normalizedEndpoint.Host,
            port = normalizedEndpoint.IsDefaultPort ? (int?)null : normalizedEndpoint.Port,
            path = normalizedEndpoint.AbsolutePath
        };
    }

    private static IReadOnlyDictionary<string, string> CreateUsageDiagnostic(
        ExternalRagAdapterResponseMetadata metadata) =>
        metadata.SanitizedProperties
            .Where(property => property.Key.StartsWith("usage.", StringComparison.Ordinal))
            .ToDictionary(
                property => property.Key["usage.".Length..],
                property => property.Value,
                StringComparer.Ordinal);

    private static string? GetMetadataValue(ExternalRagAdapterResponseMetadata metadata, string key) =>
        metadata.SanitizedProperties.TryGetValue(key, out var value)
            ? value
            : null;

    private static string? SanitizeOptionalDiagnostic(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : SanitizeMappedText(value);

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
            "stream-complete" or
            "stream-incomplete" or
            "stream-read-error" or
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
