namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed record AnalysisInputSnapshot(
    Guid AnalysisId,
    AnalysisInputFields Analysis,
    IReadOnlyList<AnalysisContextFragmentSnapshot> ContextFragments);

public sealed record AnalysisInputFields(
    string Title,
    string OriginalDescription,
    string ProjectRequest,
    string SituationDescription,
    string ChangeSource);

public sealed record AnalysisContextFragmentSnapshot(
    Guid Id,
    string Type,
    string Source,
    string Text,
    string? FileName);
