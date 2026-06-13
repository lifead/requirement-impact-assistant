namespace RequirementImpactAssistant.Web.Domain.Enums;

public enum AiAnalysisResultStatus
{
    NotStarted = 0,
    Running = 1,
    Completed = 2,
    CompletedWithWarnings = 3,
    Failed = 4,
    InvalidResponse = 5
}
