namespace RequirementImpactAssistant.Web.Domain.Enums;

public enum ExpertConclusionType
{
    NotSet = 0,
    Accept = 1,
    AcceptWithLimitations = 2,
    SendForClarification = 3,
    SplitIntoSeveralTasks = 4,
    Reject = 5,
    ReturnForReanalysis = 6
}
