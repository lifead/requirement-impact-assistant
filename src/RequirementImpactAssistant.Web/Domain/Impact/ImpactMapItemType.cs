namespace RequirementImpactAssistant.Web.Domain.Impact;

public enum ImpactMapItemType
{
    Other = 0,
    ChangeSummary = 1,
    AffectedRequirement = 2,
    AffectedTask = 3,
    AffectedProjectDecision = 4,
    AffectedApiInterfaceDocumentTest = 5,
    AffectedArchitecturalConstraint = 6,
    AffectedOrganizationalContextItem = 7,
    Contradiction = 8,
    MissingInformation = 9,
    ClarificationQuestion = 10,
    Risk = 11,
    OptionForExpertReview = 12,
    PreliminaryAssessment = 13
}
