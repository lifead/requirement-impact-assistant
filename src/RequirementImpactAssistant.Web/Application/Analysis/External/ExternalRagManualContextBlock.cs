namespace RequirementImpactAssistant.Web.Application.Analysis.External;

public sealed record ExternalRagManualContextBlock(
    IReadOnlyList<AnalysisContextFragmentSnapshot> ContextFragments,
    string? CombinedText);
