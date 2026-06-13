using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class ExpertEvaluatedItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ExpertEvaluationTargetType TargetType { get; set; } = ExpertEvaluationTargetType.ImpactItem;

    public string TargetId { get; set; } = string.Empty;

    public ExpertMark Mark { get; set; } = ExpertMark.NotSet;

    public string Comment { get; set; } = string.Empty;

    public string CorrectionText { get; set; } = string.Empty;
}
