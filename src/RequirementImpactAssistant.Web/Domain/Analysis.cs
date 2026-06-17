using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class Analysis
{
    public Analysis()
    {
        var now = DateTimeOffset.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public AnalysisStatus Status { get; set; } = AnalysisStatus.Draft;

    public ProjectRequestType ProjectRequestType { get; set; } = ProjectRequestType.Other;

    public string OriginalDescription { get; set; } = string.Empty;

    public string ProjectRequest { get; set; } = string.Empty;

    public string SituationDescription { get; set; } = string.Empty;

    public string ChangeSource { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? FixedAt { get; set; }

    public List<ContextFragment> ContextFragments { get; } = [];

    public AiAnalysisResult? AiAnalysisResult { get; set; }

    public ExpertEvaluation? ExpertEvaluation { get; set; }

    public ExpertConclusion? ExpertConclusion { get; set; }
}
