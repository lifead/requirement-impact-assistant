using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Application.Analysis.Llm;

public enum LlmProviderResponseStatus
{
    Succeeded = 1,
    Partial = 2,
    Failed = 3
}

public sealed record LlmProviderResponse(
    LlmProviderResponseStatus Status,
    ImpactMap? ImpactMap,
    string RawResponse,
    IReadOnlyList<string> Errors);
