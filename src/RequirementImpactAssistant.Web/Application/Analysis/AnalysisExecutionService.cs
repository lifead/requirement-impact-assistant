using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using DomainAnalysis = RequirementImpactAssistant.Web.Domain.Analysis;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed class AnalysisExecutionService : IAnalysisExecutionService
{
    private const string PromptVersion = "direct-llm-analysis-v1";

    private readonly ApplicationDbContext _dbContext;
    private readonly IAnalysisInputAssembler _inputAssembler;
    private readonly IAiAnalysisEngine _analysisEngine;
    private readonly AiAnalysisOptions _options;

    public AnalysisExecutionService(
        ApplicationDbContext dbContext,
        IAnalysisInputAssembler inputAssembler,
        IAiAnalysisEngine analysisEngine,
        IOptions<AiAnalysisOptions> options)
    {
        _dbContext = dbContext;
        _inputAssembler = inputAssembler;
        _analysisEngine = analysisEngine;
        _options = options.Value;
    }

    public async Task<AnalysisExecutionOutcome> RunAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        var analysis = await _dbContext.Analyses
            .Include(candidate => candidate.ContextFragments)
            .Include(candidate => candidate.AiAnalysisResult)
            .SingleOrDefaultAsync(candidate => candidate.Id == analysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            return new AnalysisExecutionOutcome(
                AnalysisExecutionOutcomeKind.NotFound,
                analysisId,
                ResultStatus: null,
                Message: "Analysis was not found.");
        }

        if (!HasMinimumInput(analysis))
        {
            return new AnalysisExecutionOutcome(
                AnalysisExecutionOutcomeKind.InvalidInput,
                analysis.Id,
                ResultStatus: null,
                Message: "Minimum analysis fields are not fully filled. Complete the source fields before running analysis.");
        }

        var request = _inputAssembler.Assemble(analysis);
        var response = await _analysisEngine
            .AnalyzeAsync(request, cancellationToken)
            .ConfigureAwait(false);

        var resultStatus = MapResultStatus(response);
        var result = analysis.AiAnalysisResult ?? new AiAnalysisResult
        {
            AnalysisId = analysis.Id
        };

        result.Status = resultStatus;
        result.GeneratedAt = DateTimeOffset.UtcNow;
        result.EngineName = _analysisEngine.GetType().Name;
        result.ProviderName = _options.Provider;
        result.ModelName = ResolveModelName(_options);
        result.PromptVersion = PromptVersion;
        result.InputSnapshot = request.InputSnapshotJson;
        result.RawResponse = response.RawResponse ?? string.Empty;
        result.ErrorMessage = string.Join(Environment.NewLine, response.Errors);
        result.ImpactMap = response.ImpactMap;

        if (analysis.AiAnalysisResult is null)
        {
            analysis.AiAnalysisResult = result;
            _dbContext.AiAnalysisResults.Add(result);
        }

        analysis.Status = resultStatus is AiAnalysisResultStatus.Completed or AiAnalysisResultStatus.CompletedWithWarnings
            ? AnalysisStatus.NeedsExpertEvaluation
            : AnalysisStatus.LlmAnalysisFailed;
        analysis.UpdatedAt = result.GeneratedAt.Value;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AnalysisExecutionOutcome(
            AnalysisExecutionOutcomeKind.Completed,
            analysis.Id,
            resultStatus,
            CreateCompletionMessage(resultStatus));
    }

    private static bool HasMinimumInput(DomainAnalysis analysis) =>
        !string.IsNullOrWhiteSpace(analysis.Title) &&
        !string.IsNullOrWhiteSpace(analysis.OriginalDescription) &&
        !string.IsNullOrWhiteSpace(analysis.ProjectRequest) &&
        !string.IsNullOrWhiteSpace(analysis.SituationDescription) &&
        !string.IsNullOrWhiteSpace(analysis.ChangeSource);

    private static AiAnalysisResultStatus MapResultStatus(AiAnalysisResponse response) =>
        response.Status switch
        {
            AiAnalysisResponseStatus.Succeeded => AiAnalysisResultStatus.Completed,
            AiAnalysisResponseStatus.Partial => AiAnalysisResultStatus.CompletedWithWarnings,
            AiAnalysisResponseStatus.Failed when IsInvalidResponse(response) => AiAnalysisResultStatus.InvalidResponse,
            AiAnalysisResponseStatus.Failed => AiAnalysisResultStatus.Failed,
            _ => AiAnalysisResultStatus.Failed
        };

    private static bool IsInvalidResponse(AiAnalysisResponse response) =>
        response.Errors.Any(error =>
            error.Contains("LLM response is invalid", StringComparison.OrdinalIgnoreCase));

    private static string ResolveModelName(AiAnalysisOptions options)
    {
        if (string.Equals(options.Provider, LlmProviderNames.Demo, StringComparison.OrdinalIgnoreCase))
        {
            return "demo-deterministic";
        }

        if (string.Equals(options.Provider, LlmProviderNames.DeepSeek, StringComparison.OrdinalIgnoreCase))
        {
            return options.DeepSeek.Model;
        }

        return string.Empty;
    }

    private static string CreateCompletionMessage(AiAnalysisResultStatus status) =>
        status switch
        {
            AiAnalysisResultStatus.Completed => "Preliminary AI analysis completed.",
            AiAnalysisResultStatus.CompletedWithWarnings => "Preliminary AI analysis completed with diagnostics that require attention.",
            AiAnalysisResultStatus.InvalidResponse => "AI analysis returned an invalid response. Raw response and diagnostics were saved.",
            AiAnalysisResultStatus.Failed => "AI analysis failed. Diagnostics were saved.",
            _ => "AI analysis finished."
        };
}
