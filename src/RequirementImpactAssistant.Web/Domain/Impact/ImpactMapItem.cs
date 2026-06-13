using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain.Impact;

public sealed class ImpactMapItem
{
    public string Id { get; private set; } = string.Empty;

    public ImpactMapItemType ItemType { get; private set; } = ImpactMapItemType.Other;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ImpactSeverity Severity { get; set; } = ImpactSeverity.NotSpecified;

    public List<Guid> RelatedContextFragmentIds { get; } = [];

    public string Notes { get; set; } = string.Empty;

    private ImpactMapItem()
    {
    }

    public static ImpactMapItem CreateSingleton(ImpactMapItemType itemType) =>
        new()
        {
            Id = ImpactMapIds.CreateItemId(itemType, 1),
            ItemType = itemType
        };

    public static ImpactMapItem CreateListItem(ImpactMapItemType itemType, int ordinal) =>
        new()
        {
            Id = ImpactMapIds.CreateItemId(itemType, ordinal),
            ItemType = itemType
        };
}
