using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class AiAnalysisResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnalysisId { get; set; }

    public AiAnalysisResultStatus Status { get; set; } = AiAnalysisResultStatus.NotStarted;

    public DateTimeOffset? GeneratedAt { get; set; }

    public string EngineName { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public string InputSnapshot { get; set; } = string.Empty;

    public string RawResponse { get; set; } = string.Empty;

    public ImpactMap? ImpactMap { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;
}
