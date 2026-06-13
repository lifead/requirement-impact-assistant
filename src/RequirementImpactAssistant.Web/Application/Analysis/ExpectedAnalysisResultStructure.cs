using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed record ExpectedAnalysisResultStructure(
    IReadOnlyList<ExpectedAnalysisResultSection> Sections)
{
    public static ExpectedAnalysisResultStructure Default { get; } = new(
        [
            CreateSection("changeSummary", ImpactMapItemType.ChangeSummary, isCollection: false),
            CreateSection("affectedRequirements", ImpactMapItemType.AffectedRequirement, isCollection: true),
            CreateSection("affectedTasks", ImpactMapItemType.AffectedTask, isCollection: true),
            CreateSection("affectedProjectDecisions", ImpactMapItemType.AffectedProjectDecision, isCollection: true),
            CreateSection("affectedApiInterfacesDocumentsTests", ImpactMapItemType.AffectedApiInterfaceDocumentTest, isCollection: true),
            CreateSection("affectedArchitecturalConstraints", ImpactMapItemType.AffectedArchitecturalConstraint, isCollection: true),
            CreateSection("affectedOrganizationalContextItems", ImpactMapItemType.AffectedOrganizationalContextItem, isCollection: true),
            CreateSection("contradictions", ImpactMapItemType.Contradiction, isCollection: true),
            CreateSection("missingInformation", ImpactMapItemType.MissingInformation, isCollection: true),
            CreateSection("clarificationQuestions", ImpactMapItemType.ClarificationQuestion, isCollection: true),
            CreateSection("risks", ImpactMapItemType.Risk, isCollection: true),
            CreateSection("optionsForExpertReview", ImpactMapItemType.OptionForExpertReview, isCollection: true),
            CreateSection("preliminaryAssessment", ImpactMapItemType.PreliminaryAssessment, isCollection: false)
        ]);

    private static ExpectedAnalysisResultSection CreateSection(
        string key,
        ImpactMapItemType itemType,
        bool isCollection) =>
        new(
            Key: key,
            ItemType: itemType.ToString(),
            IsCollection: isCollection,
            AllowsRelatedContextFragmentIds: true);
}

public sealed record ExpectedAnalysisResultSection(
    string Key,
    string ItemType,
    bool IsCollection,
    bool AllowsRelatedContextFragmentIds);
