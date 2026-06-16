using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed class AiAnalysisEngineSelector : IAiAnalysisEngineSelector
{
    private readonly DirectLlmAnalysisEngine directLlmAnalysisEngine;
    private readonly ExternalRagAnalysisEngine externalRagAnalysisEngine;

    public AiAnalysisEngineSelector(
        DirectLlmAnalysisEngine directLlmAnalysisEngine,
        ExternalRagAnalysisEngine externalRagAnalysisEngine)
    {
        this.directLlmAnalysisEngine = directLlmAnalysisEngine;
        this.externalRagAnalysisEngine = externalRagAnalysisEngine;
    }

    public IAiAnalysisEngine Select(AnalysisMode analysisMode) =>
        analysisMode switch
        {
            AnalysisMode.DirectLlm => directLlmAnalysisEngine,
            AnalysisMode.ExternalRag => externalRagAnalysisEngine,
            _ => throw new ArgumentOutOfRangeException(nameof(analysisMode), analysisMode, "Unsupported analysis mode.")
        };
}
