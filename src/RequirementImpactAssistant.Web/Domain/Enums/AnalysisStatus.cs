namespace RequirementImpactAssistant.Web.Domain.Enums;

public enum AnalysisStatus
{
    Draft = 0,
    InputIncomplete = 1,
    ReadyForAnalysis = 2,
    LlmAnalysisRunning = 3,
    LlmAnalysisCompleted = 4,
    LlmAnalysisFailed = 5,
    NeedsExpertEvaluation = 6,
    ReturnedForClarification = 7,
    NeedsReanalysis = 8,
    ExpertConclusionFixed = 9,
    Exported = 10
}
