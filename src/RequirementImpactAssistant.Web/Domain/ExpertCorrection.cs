using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class ExpertCorrection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ExpertEvaluationTargetType TargetType { get; set; } = ExpertEvaluationTargetType.ImpactItem;

    public string TargetId { get; set; } = string.Empty;

    public ImpactMapItemType ItemType { get; set; } = ImpactMapItemType.Other;

    public string Text { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;
}
