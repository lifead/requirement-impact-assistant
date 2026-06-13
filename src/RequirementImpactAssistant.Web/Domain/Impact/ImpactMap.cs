namespace RequirementImpactAssistant.Web.Domain.Impact;

public sealed class ImpactMap
{
    private readonly List<ImpactMapItem> _affectedRequirements = [];
    private readonly List<ImpactMapItem> _affectedTasks = [];
    private readonly List<ImpactMapItem> _affectedProjectDecisions = [];
    private readonly List<ImpactMapItem> _affectedApiInterfacesDocumentsTests = [];
    private readonly List<ImpactMapItem> _affectedArchitecturalConstraints = [];
    private readonly List<ImpactMapItem> _affectedOrganizationalContextItems = [];
    private readonly List<ImpactMapItem> _contradictions = [];
    private readonly List<ImpactMapItem> _missingInformation = [];
    private readonly List<ImpactMapItem> _clarificationQuestions = [];
    private readonly List<ImpactMapItem> _risks = [];
    private readonly List<ImpactMapItem> _optionsForExpertReview = [];

    public ImpactMapItem ChangeSummary { get; set; } =
        ImpactMapItem.CreateSingleton(ImpactMapItemType.ChangeSummary);

    public IReadOnlyList<ImpactMapItem> AffectedRequirements => _affectedRequirements;

    public IReadOnlyList<ImpactMapItem> AffectedTasks => _affectedTasks;

    public IReadOnlyList<ImpactMapItem> AffectedProjectDecisions => _affectedProjectDecisions;

    public IReadOnlyList<ImpactMapItem> AffectedApiInterfacesDocumentsTests => _affectedApiInterfacesDocumentsTests;

    public IReadOnlyList<ImpactMapItem> AffectedArchitecturalConstraints => _affectedArchitecturalConstraints;

    public IReadOnlyList<ImpactMapItem> AffectedOrganizationalContextItems => _affectedOrganizationalContextItems;

    public IReadOnlyList<ImpactMapItem> Contradictions => _contradictions;

    public IReadOnlyList<ImpactMapItem> MissingInformation => _missingInformation;

    public IReadOnlyList<ImpactMapItem> ClarificationQuestions => _clarificationQuestions;

    public IReadOnlyList<ImpactMapItem> Risks => _risks;

    public IReadOnlyList<ImpactMapItem> OptionsForExpertReview => _optionsForExpertReview;

    public ImpactMapItem PreliminaryAssessment { get; set; } =
        ImpactMapItem.CreateSingleton(ImpactMapItemType.PreliminaryAssessment);

    public ImpactMapItem AddAffectedRequirement() =>
        AddItem(_affectedRequirements, ImpactMapItemType.AffectedRequirement);

    public ImpactMapItem AddAffectedTask() =>
        AddItem(_affectedTasks, ImpactMapItemType.AffectedTask);

    public ImpactMapItem AddAffectedProjectDecision() =>
        AddItem(_affectedProjectDecisions, ImpactMapItemType.AffectedProjectDecision);

    public ImpactMapItem AddAffectedApiInterfaceDocumentTest() =>
        AddItem(_affectedApiInterfacesDocumentsTests, ImpactMapItemType.AffectedApiInterfaceDocumentTest);

    public ImpactMapItem AddAffectedArchitecturalConstraint() =>
        AddItem(_affectedArchitecturalConstraints, ImpactMapItemType.AffectedArchitecturalConstraint);

    public ImpactMapItem AddAffectedOrganizationalContextItem() =>
        AddItem(_affectedOrganizationalContextItems, ImpactMapItemType.AffectedOrganizationalContextItem);

    public ImpactMapItem AddContradiction() =>
        AddItem(_contradictions, ImpactMapItemType.Contradiction);

    public ImpactMapItem AddMissingInformation() =>
        AddItem(_missingInformation, ImpactMapItemType.MissingInformation);

    public ImpactMapItem AddClarificationQuestion() =>
        AddItem(_clarificationQuestions, ImpactMapItemType.ClarificationQuestion);

    public ImpactMapItem AddRisk() =>
        AddItem(_risks, ImpactMapItemType.Risk);

    public ImpactMapItem AddOptionForExpertReview() =>
        AddItem(_optionsForExpertReview, ImpactMapItemType.OptionForExpertReview);

    private static ImpactMapItem AddItem(List<ImpactMapItem> items, ImpactMapItemType itemType)
    {
        var item = ImpactMapItem.CreateListItem(itemType, items.Count + 1);
        items.Add(item);
        return item;
    }
}
