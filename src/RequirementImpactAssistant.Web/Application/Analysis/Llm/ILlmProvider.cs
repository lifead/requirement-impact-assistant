namespace RequirementImpactAssistant.Web.Application.Analysis.Llm;

public interface ILlmProvider
{
    Task<LlmProviderResponse> CompleteAsync(
        LlmProviderRequest request,
        CancellationToken cancellationToken = default);
}
