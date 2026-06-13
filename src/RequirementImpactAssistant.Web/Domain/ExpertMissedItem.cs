using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class ExpertMissedItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ImpactMapItemType ItemType { get; set; } = ImpactMapItemType.Other;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ImpactSeverity Severity { get; set; } = ImpactSeverity.NotSpecified;

    public string Comment { get; set; } = string.Empty;
}
