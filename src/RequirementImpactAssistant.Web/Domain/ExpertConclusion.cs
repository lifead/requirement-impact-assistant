using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class ExpertConclusion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnalysisId { get; set; }

    public ExpertConclusionType ConclusionType { get; set; } = ExpertConclusionType.NotSet;

    public string Comment { get; set; } = string.Empty;

    public string Rationale { get; set; } = string.Empty;

    public DateTimeOffset? FixedAt { get; set; }
}
