namespace RequirementImpactAssistant.Web.Application.Analysis.Llm;

public sealed class AiAnalysisOptions
{
    public const string SectionName = "AiAnalysis";

    public string Provider { get; set; } = LlmProviderNames.DeepSeek;

    public DeepSeekLlmProviderOptions DeepSeek { get; set; } = new();
}
