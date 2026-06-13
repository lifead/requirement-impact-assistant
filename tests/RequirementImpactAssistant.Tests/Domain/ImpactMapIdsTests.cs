using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Domain;

public sealed class ImpactMapIdsTests
{
    [Theory]
    [InlineData(ImpactMapItemType.ChangeSummary, "change-summary")]
    [InlineData(ImpactMapItemType.AffectedRequirement, "affected-requirements")]
    [InlineData(ImpactMapItemType.AffectedTask, "affected-tasks")]
    [InlineData(ImpactMapItemType.AffectedProjectDecision, "affected-project-decisions")]
    [InlineData(ImpactMapItemType.AffectedApiInterfaceDocumentTest, "affected-api-interfaces-documents-tests")]
    [InlineData(ImpactMapItemType.AffectedArchitecturalConstraint, "affected-architectural-constraints")]
    [InlineData(ImpactMapItemType.AffectedOrganizationalContextItem, "affected-organizational-context-items")]
    [InlineData(ImpactMapItemType.Contradiction, "contradictions")]
    [InlineData(ImpactMapItemType.MissingInformation, "missing-information")]
    [InlineData(ImpactMapItemType.ClarificationQuestion, "clarification-questions")]
    [InlineData(ImpactMapItemType.Risk, "risks")]
    [InlineData(ImpactMapItemType.OptionForExpertReview, "options-for-expert-review")]
    [InlineData(ImpactMapItemType.PreliminaryAssessment, "preliminary-assessment")]
    public void GetSectionId_ReturnsExpectedStableSectionId(
        ImpactMapItemType itemType,
        string expectedSectionId)
    {
        var sectionId = ImpactMapIds.GetSectionId(itemType);

        Assert.Equal(expectedSectionId, sectionId);
    }

    [Theory]
    [InlineData(ImpactMapItemType.ChangeSummary, 1, "change-summary")]
    [InlineData(ImpactMapItemType.PreliminaryAssessment, 1, "preliminary-assessment")]
    [InlineData(ImpactMapItemType.AffectedRequirement, 1, "affected-requirement-001")]
    [InlineData(ImpactMapItemType.AffectedRequirement, 12, "affected-requirement-012")]
    [InlineData(ImpactMapItemType.AffectedTask, 3, "affected-task-003")]
    [InlineData(ImpactMapItemType.Risk, 7, "risk-007")]
    [InlineData(ImpactMapItemType.OptionForExpertReview, 2, "option-for-expert-review-002")]
    public void CreateItemId_ReturnsDeterministicStableIds(
        ImpactMapItemType itemType,
        int ordinal,
        string expectedId)
    {
        var firstId = ImpactMapIds.CreateItemId(itemType, ordinal);
        var secondId = ImpactMapIds.CreateItemId(itemType, ordinal);

        Assert.Equal(expectedId, firstId);
        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public void CreateListItem_UsesStableGeneratedId()
    {
        var item = ImpactMapItem.CreateListItem(ImpactMapItemType.Contradiction, 4);

        Assert.Equal("contradiction-004", item.Id);
        Assert.Equal(ImpactMapItemType.Contradiction, item.ItemType);
    }

    [Fact]
    public void AddAffectedRequirement_AssignsStableSequentialIds()
    {
        var firstMap = new ImpactMap();
        var firstItem = firstMap.AddAffectedRequirement();
        var secondItem = firstMap.AddAffectedRequirement();

        var secondMap = new ImpactMap();
        var repeatedFirstItem = secondMap.AddAffectedRequirement();

        Assert.Equal("affected-requirement-001", firstItem.Id);
        Assert.Equal(ImpactMapItemType.AffectedRequirement, firstItem.ItemType);
        Assert.Equal("affected-requirement-002", secondItem.Id);
        Assert.Equal("affected-requirement-001", repeatedFirstItem.Id);
    }
}
