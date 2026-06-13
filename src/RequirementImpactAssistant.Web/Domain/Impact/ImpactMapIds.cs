namespace RequirementImpactAssistant.Web.Domain.Impact;

public static class ImpactMapIds
{
    public static string GetSectionId(ImpactMapItemType itemType) => itemType switch
    {
        ImpactMapItemType.ChangeSummary => "change-summary",
        ImpactMapItemType.AffectedRequirement => "affected-requirements",
        ImpactMapItemType.AffectedTask => "affected-tasks",
        ImpactMapItemType.AffectedProjectDecision => "affected-project-decisions",
        ImpactMapItemType.AffectedApiInterfaceDocumentTest => "affected-api-interfaces-documents-tests",
        ImpactMapItemType.AffectedArchitecturalConstraint => "affected-architectural-constraints",
        ImpactMapItemType.AffectedOrganizationalContextItem => "affected-organizational-context-items",
        ImpactMapItemType.Contradiction => "contradictions",
        ImpactMapItemType.MissingInformation => "missing-information",
        ImpactMapItemType.ClarificationQuestion => "clarification-questions",
        ImpactMapItemType.Risk => "risks",
        ImpactMapItemType.OptionForExpertReview => "options-for-expert-review",
        ImpactMapItemType.PreliminaryAssessment => "preliminary-assessment",
        ImpactMapItemType.Other => "other",
        _ => "other"
    };

    public static string CreateItemId(ImpactMapItemType itemType, int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ordinal);

        if (IsSingletonItemType(itemType))
        {
            return GetSectionId(itemType);
        }

        return $"{GetItemSlug(itemType)}-{ordinal:000}";
    }

    private static string GetItemSlug(ImpactMapItemType itemType) => itemType switch
    {
        ImpactMapItemType.ChangeSummary => "change-summary",
        ImpactMapItemType.AffectedRequirement => "affected-requirement",
        ImpactMapItemType.AffectedTask => "affected-task",
        ImpactMapItemType.AffectedProjectDecision => "affected-project-decision",
        ImpactMapItemType.AffectedApiInterfaceDocumentTest => "affected-api-interface-document-test",
        ImpactMapItemType.AffectedArchitecturalConstraint => "affected-architectural-constraint",
        ImpactMapItemType.AffectedOrganizationalContextItem => "affected-organizational-context-item",
        ImpactMapItemType.Contradiction => "contradiction",
        ImpactMapItemType.MissingInformation => "missing-information-item",
        ImpactMapItemType.ClarificationQuestion => "clarification-question",
        ImpactMapItemType.Risk => "risk",
        ImpactMapItemType.OptionForExpertReview => "option-for-expert-review",
        ImpactMapItemType.PreliminaryAssessment => "preliminary-assessment",
        ImpactMapItemType.Other => "other-item",
        _ => "other-item"
    };

    private static bool IsSingletonItemType(ImpactMapItemType itemType) =>
        itemType is ImpactMapItemType.ChangeSummary or ImpactMapItemType.PreliminaryAssessment;
}
