namespace RequirementImpactAssistant.Web.Application.Analysis.Llm;

public sealed record LlmProviderRequest(
    string Provider,
    string Prompt,
    AiAnalysisRequest AnalysisRequest);
