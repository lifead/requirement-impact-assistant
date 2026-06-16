using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class AiAnalysisResult
{
    private AiAnalysisResultMetadata? metadata;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnalysisId { get; set; }

    public AiAnalysisResultStatus Status { get; set; } = AiAnalysisResultStatus.NotStarted;

    public DateTimeOffset? GeneratedAt { get; set; }

    public string EngineName { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public AiAnalysisResultMetadata Metadata
    {
        get => EnsureMetadata();
        set => metadata = value ?? CreateDefaultMetadata();
    }

    public string PromptVersion { get; set; } = string.Empty;

    public string InputSnapshot { get; set; } = string.Empty;

    public string RawResponse { get; set; } = string.Empty;

    public ImpactMap? ImpactMap { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    private AiAnalysisResultMetadata CreateDefaultMetadata() =>
        AiAnalysisResultMetadata.CreateLegacyMvp0Default(
            EngineName,
            ProviderName,
            ModelName);

    private AiAnalysisResultMetadata EnsureMetadata()
    {
        metadata ??= CreateDefaultMetadata();

        if (metadata.AnalysisMode == AnalysisMode.DirectLlm)
        {
            if (string.IsNullOrWhiteSpace(metadata.EngineName) && !string.IsNullOrWhiteSpace(EngineName))
            {
                metadata.EngineName = EngineName;
            }

            if (string.IsNullOrWhiteSpace(metadata.ProviderName) && !string.IsNullOrWhiteSpace(ProviderName))
            {
                metadata.ProviderName = ProviderName;
            }

            if (string.IsNullOrWhiteSpace(metadata.ModelWorkflowProfileName) && !string.IsNullOrWhiteSpace(ModelName))
            {
                metadata.ModelWorkflowProfileName = ModelName;
            }
        }

        return metadata;
    }
}
