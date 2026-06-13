using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class ContextFragment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnalysisId { get; set; }

    public ContextFragmentType Type { get; set; } = ContextFragmentType.Other;

    public string Source { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string? FileName { get; set; }

    public string? FilePath { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
