using System.Text.Json.Serialization;

namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

internal sealed class DifyWorkflowRequestDto
{
    [JsonPropertyName("inputs")]
    public DifyWorkflowInputsDto Inputs { get; init; } = new();

    [JsonPropertyName("response_mode")]
    public string ResponseMode { get; init; } = "blocking";

    [JsonPropertyName("user")]
    public string User { get; init; } = string.Empty;
}

internal sealed class DifyWorkflowInputsDto
{
    [JsonPropertyName("analysis")]
    public DifyAnalysisInputDto Analysis { get; init; } = new();

    [JsonPropertyName("manual_context")]
    public DifyManualContextDto? ManualContext { get; init; }

    [JsonPropertyName("manual_context_policy")]
    public string ManualContextPolicy { get; init; } = string.Empty;

    [JsonPropertyName("expected_result")]
    public DifyExpectedResultDto ExpectedResult { get; init; } = new();

    [JsonPropertyName("boundary_notice")]
    public DifyBoundaryNoticeDto BoundaryNotice { get; init; } = new();

    [JsonPropertyName("execution")]
    public DifyExecutionMetadataDto Execution { get; init; } = new();
}

internal sealed class DifyAnalysisInputDto
{
    [JsonPropertyName("analysis_id")]
    public string AnalysisId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("original_description")]
    public string OriginalDescription { get; init; } = string.Empty;

    [JsonPropertyName("project_request")]
    public string ProjectRequest { get; init; } = string.Empty;

    [JsonPropertyName("situation_description")]
    public string SituationDescription { get; init; } = string.Empty;

    [JsonPropertyName("change_source")]
    public string ChangeSource { get; init; } = string.Empty;
}

internal sealed class DifyManualContextDto
{
    [JsonPropertyName("combined_text")]
    public string? CombinedText { get; init; }

    [JsonPropertyName("fragments")]
    public IReadOnlyList<DifyManualContextFragmentDto> Fragments { get; init; } = [];
}

internal sealed class DifyManualContextFragmentDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string? FileName { get; init; }
}

internal sealed class DifyExpectedResultDto
{
    [JsonPropertyName("sections")]
    public IReadOnlyList<DifyExpectedResultSectionDto> Sections { get; init; } = [];
}

internal sealed class DifyExpectedResultSectionDto
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("item_type")]
    public string ItemType { get; init; } = string.Empty;

    [JsonPropertyName("is_collection")]
    public bool IsCollection { get; init; }

    [JsonPropertyName("allows_related_context_fragment_ids")]
    public bool AllowsRelatedContextFragmentIds { get; init; }
}

internal sealed class DifyBoundaryNoticeDto
{
    [JsonPropertyName("is_preliminary_analytical_material")]
    public bool IsPreliminaryAnalyticalMaterial { get; init; }

    [JsonPropertyName("ai_does_not_make_management_decision")]
    public bool AiDoesNotMakeManagementDecision { get; init; }

    [JsonPropertyName("human_decision_authority")]
    public string HumanDecisionAuthority { get; init; } = string.Empty;

    [JsonPropertyName("result_use_statement")]
    public string ResultUseStatement { get; init; } = string.Empty;
}

internal sealed class DifyExecutionMetadataDto
{
    [JsonPropertyName("engine_name")]
    public string EngineName { get; init; } = string.Empty;

    [JsonPropertyName("requested_profile_name")]
    public string? RequestedProfileName { get; init; }
}

internal sealed class DifyWorkflowResponseDto
{
    [JsonPropertyName("workflow_run_id")]
    public string? WorkflowRunId { get; init; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; init; }

    [JsonPropertyName("data")]
    public DifyWorkflowResponseDataDto? Data { get; init; }
}

internal sealed class DifyWorkflowResponseDataDto
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("outputs")]
    public DifyWorkflowOutputsDto? Outputs { get; init; }
}

internal sealed class DifyWorkflowOutputsDto
{
    [JsonPropertyName("impact_map")]
    public DifyImpactMapDto? ImpactMap { get; init; }

    [JsonPropertyName("retrieved_context")]
    public IReadOnlyList<DifyRetrievedContextItemDto> RetrievedContext { get; init; } = [];

    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    [JsonPropertyName("metadata")]
    public DifyWorkflowOutputMetadataDto? Metadata { get; init; }
}

internal sealed class DifyWorkflowOutputMetadataDto
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("response_shape")]
    public string? ResponseShape { get; init; }
}

internal sealed class DifyImpactMapDto
{
    [JsonPropertyName("change_summary")]
    public DifyImpactMapItemDto? ChangeSummary { get; init; }

    [JsonPropertyName("affected_requirements")]
    public IReadOnlyList<DifyImpactMapItemDto> AffectedRequirements { get; init; } = [];

    [JsonPropertyName("affected_tasks")]
    public IReadOnlyList<DifyImpactMapItemDto> AffectedTasks { get; init; } = [];

    [JsonPropertyName("affected_project_decisions")]
    public IReadOnlyList<DifyImpactMapItemDto> AffectedProjectDecisions { get; init; } = [];

    [JsonPropertyName("affected_api_interfaces_documents_tests")]
    public IReadOnlyList<DifyImpactMapItemDto> AffectedApiInterfacesDocumentsTests { get; init; } = [];

    [JsonPropertyName("affected_architectural_constraints")]
    public IReadOnlyList<DifyImpactMapItemDto> AffectedArchitecturalConstraints { get; init; } = [];

    [JsonPropertyName("affected_organizational_context_items")]
    public IReadOnlyList<DifyImpactMapItemDto> AffectedOrganizationalContextItems { get; init; } = [];

    [JsonPropertyName("contradictions")]
    public IReadOnlyList<DifyImpactMapItemDto> Contradictions { get; init; } = [];

    [JsonPropertyName("missing_information")]
    public IReadOnlyList<DifyImpactMapItemDto> MissingInformation { get; init; } = [];

    [JsonPropertyName("clarification_questions")]
    public IReadOnlyList<DifyImpactMapItemDto> ClarificationQuestions { get; init; } = [];

    [JsonPropertyName("risks")]
    public IReadOnlyList<DifyImpactMapItemDto> Risks { get; init; } = [];

    [JsonPropertyName("options_for_expert_review")]
    public IReadOnlyList<DifyImpactMapItemDto> OptionsForExpertReview { get; init; } = [];

    [JsonPropertyName("preliminary_assessment")]
    public DifyImpactMapItemDto? PreliminaryAssessment { get; init; }
}

internal sealed class DifyImpactMapItemDto
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("severity")]
    public string? Severity { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("related_context_fragment_ids")]
    public IReadOnlyList<string> RelatedContextFragmentIds { get; init; } = [];
}

internal sealed class DifyRetrievedContextItemDto
{
    [JsonPropertyName("source_title")]
    public string? SourceTitle { get; init; }

    [JsonPropertyName("source_id")]
    public string? SourceId { get; init; }

    [JsonPropertyName("external_reference")]
    public string? ExternalReference { get; init; }

    [JsonPropertyName("fragment_id")]
    public string? FragmentId { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("excerpt")]
    public string? Excerpt { get; init; }

    [JsonPropertyName("url_or_reference")]
    public string? UrlOrReference { get; init; }

    [JsonPropertyName("rank")]
    public int? Rank { get; init; }

    [JsonPropertyName("score")]
    public double? Score { get; init; }
}
