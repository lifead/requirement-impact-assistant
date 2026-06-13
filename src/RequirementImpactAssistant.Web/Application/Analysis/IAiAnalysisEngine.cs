namespace RequirementImpactAssistant.Web.Application.Analysis;

public interface IAiAnalysisEngine
{
    Task<AiAnalysisResponse> AnalyzeAsync(
        AiAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
