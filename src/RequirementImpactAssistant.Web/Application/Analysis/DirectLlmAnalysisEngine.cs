using System.Text;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed class DirectLlmAnalysisEngine : IAiAnalysisEngine
{
    private readonly ILlmProvider _llmProvider;
    private readonly AiAnalysisOptions _options;

    public DirectLlmAnalysisEngine(
        ILlmProvider llmProvider,
        IOptions<AiAnalysisOptions> options)
    {
        _llmProvider = llmProvider;
        _options = options.Value;
    }

    public async Task<AiAnalysisResponse> AnalyzeAsync(
        AiAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerRequest = new LlmProviderRequest(
            Provider: _options.Provider,
            Prompt: BuildPrompt(request),
            AnalysisRequest: request);

        try
        {
            var providerResponse = await _llmProvider
                .CompleteAsync(providerRequest, cancellationToken)
                .ConfigureAwait(false);

            return AiAnalysisResponseValidator.Validate(providerResponse, request.BoundaryNotice);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new AiAnalysisResponse(
                Status: AiAnalysisResponseStatus.Failed,
                ImpactMap: null,
                RawResponse: string.Empty,
                Errors:
                [
                    "LLM provider call failed before an analytical result was returned.",
                    exception.Message
                ],
                BoundaryNotice: request.BoundaryNotice);
        }
    }

    private static string BuildPrompt(AiAnalysisRequest request)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are preparing preliminary analytical material for requirement impact analysis.");
        prompt.AppendLine("Do not make management decisions, do not approve changes, and do not replace human expert review.");
        prompt.AppendLine();
        prompt.AppendLine("Boundary notice:");
        prompt.AppendLine($"- Is preliminary analytical material: {request.BoundaryNotice.IsPreliminaryAnalyticalMaterial}");
        prompt.AppendLine($"- AI does not make management decision: {request.BoundaryNotice.AiDoesNotMakeManagementDecision}");
        prompt.AppendLine($"- Human decision authority: {request.BoundaryNotice.HumanDecisionAuthority}");
        prompt.AppendLine($"- Result use statement: {request.BoundaryNotice.ResultUseStatement}");
        prompt.AppendLine();
        prompt.AppendLine("Input snapshot JSON:");
        prompt.AppendLine(request.InputSnapshotJson);
        prompt.AppendLine();
        prompt.AppendLine("Expected result sections:");

        foreach (var section in request.ExpectedResult.Sections)
        {
            prompt.AppendLine(
                $"- {section.Key}: itemType={section.ItemType}, isCollection={section.IsCollection}, allowsRelatedContextFragmentIds={section.AllowsRelatedContextFragmentIds}");
        }

        prompt.AppendLine();
        prompt.AppendLine("Return only preliminary analytical material compatible with the expected impact map sections.");

        return prompt.ToString();
    }

}
