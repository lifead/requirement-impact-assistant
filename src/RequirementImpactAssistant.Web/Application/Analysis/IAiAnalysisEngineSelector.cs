using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public interface IAiAnalysisEngineSelector
{
    IAiAnalysisEngine Select(AnalysisMode analysisMode);
}
