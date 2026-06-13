using DomainAnalysis = RequirementImpactAssistant.Web.Domain.Analysis;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public interface IAnalysisInputAssembler
{
    AiAnalysisRequest Assemble(DomainAnalysis analysis);
}
