namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed record AnalysisBoundaryNotice(
    bool IsPreliminaryAnalyticalMaterial,
    bool AiDoesNotMakeManagementDecision,
    string HumanDecisionAuthority,
    string ResultUseStatement)
{
    public static AnalysisBoundaryNotice Default { get; } = new(
        IsPreliminaryAnalyticalMaterial: true,
        AiDoesNotMakeManagementDecision: true,
        HumanDecisionAuthority: "Human expert",
        ResultUseStatement: "AI/LLM output is preliminary analytical material for expert review and is not a management decision.");
}
