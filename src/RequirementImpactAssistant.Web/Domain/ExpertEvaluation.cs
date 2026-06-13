using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class ExpertEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnalysisId { get; set; }

    public ContextSufficiencyRating ContextSufficiency { get; set; } = ContextSufficiencyRating.NotAssessed;

    public ResultUsefulnessRating ResultUsefulness { get; set; } = ResultUsefulnessRating.NotAssessed;

    public string GeneralComment { get; set; } = string.Empty;

    public List<ExpertEvaluatedItem> EvaluatedItems { get; } = [];

    public List<ExpertMissedItem> MissedItems { get; } = [];

    public List<ExpertCorrection> Corrections { get; } = [];
}
