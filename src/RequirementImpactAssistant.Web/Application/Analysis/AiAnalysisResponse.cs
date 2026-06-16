using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public enum AiAnalysisResponseStatus
{
    Succeeded = 1,
    Partial = 2,
    Failed = 3
}

public sealed record AiAnalysisResponse(
    AiAnalysisResponseStatus Status,
    ImpactMap? ImpactMap,
    string RawResponse,
    IReadOnlyList<string> Errors,
    AnalysisBoundaryNotice BoundaryNotice,
    AiAnalysisResultMetadata? ResultMetadata = null);
